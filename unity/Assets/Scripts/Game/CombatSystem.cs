using UnityEngine;
using System.Collections.Generic;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Drives one grid battle: initiative, the player's turn, the AI turns, and
    /// the victory check. The rules live in BattleState / BattleGrid /
    /// VictoryRules; this class is only the loop that turns input into moves and
    /// hands the rest of the round over, so a whole fight can be played in
    /// batchmode with no scene attached.
    /// </summary>
    public class CombatSystem : MonoBehaviour
    {
        public static CombatSystem Instance { get; private set; }

        public Data.EnemyData currentEnemy { get; private set; }
        public BattleState state { get; private set; }
        public bool isActive { get; private set; }
        public BattleOutcome outcome { get; private set; }
        public List<string> combatLog { get; private set; } = new List<string>();

        /// <summary>Unit currently taking its turn. Null between battles.</summary>
        public BattleUnit activeUnit { get; private set; }

        /// <summary>True while the human is the one being waited on.</summary>
        public bool IsPlayerTurn =>
            isActive && state != null && activeUnit != null && activeUnit == state.player;

        /// <summary>
        /// Moving does not end a turn; attacking, waiting or a failed escape does.
        /// </summary>
        public bool hasActed { get; private set; }

        public System.Action<Data.EnemyData> OnCombatStart;
        public System.Action OnCombatEnd;
        public System.Action<string> OnLogMessage;
        public System.Action OnStateChanged;

        private readonly List<BattleUnit> order = new List<BattleUnit>();
        private int cursor;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Build the battlefield from enemy data and hand the first turn to
        /// whoever is fastest. Safe to call without a GameManager so tests can
        /// drive it directly; combat then runs on the stats it is given.
        /// </summary>
        public void StartCombat(Data.EnemyData enemy, Player player = null,
                                EffectiveStats? playerStats = null,
                                List<PartyMember> party = null)
        {
            var gm = Core.GameManager.Instance;
            player = player ?? gm?.Player;
            var stats = playerStats ?? gm?.EffectivePlayerStats
                        ?? new EffectiveStats(new PlayerStats(), new StatModifierSet());

            currentEnemy = enemy;
            state = BattleSetup.Build(enemy, player, stats, party);
            outcome = BattleOutcome.Ongoing;
            isActive = true;
            combatLog.Clear();

            Log($"━━ NEMESIS ENCOUNTER ━━");
            if (!string.IsNullOrEmpty(enemy?.eventName)) Log($"「{enemy.eventName}」");
            if (!string.IsNullOrEmpty(enemy?.appearance)) Log(enemy.appearance);
            Log($"Battlefield: {state.grid.width}×{state.grid.height}　" +
                $"Weather: {BattleWeather.EnglishName(state.weather)} {BattleWeather.DisplayName(state.weather)}");
            Log($"Victory Rule: {state.condition.DisplayName}");
            Log($"敌方 {state.LivingEnemies} 个单位。你方 {state.LivingFriendlies} 个。");
            Log("");

            OnCombatStart?.Invoke(enemy);
            BeginRound();
        }

        // --- initiative ----------------------------------------------------

        private void BeginRound()
        {
            order.Clear();
            order.AddRange(state.InitiativeOrder());
            cursor = -1;
            AdvanceTurn();
        }

        /// <summary>
        /// Hand the turn to the next living unit, running AI turns inline until
        /// the player is up or the fight is decided. Dead units are skipped
        /// rather than removed, so the initiative list stays stable for a round.
        /// </summary>
        private void AdvanceTurn()
        {
            if (!isActive) return;

            while (true)
            {
                if (CheckOutcome()) return;

                cursor++;
                if (cursor >= order.Count)
                {
                    state.EndTurn();
                    if (CheckOutcome()) return;
                    Log($"— 第 {state.turn} 回合 —");
                    order.Clear();
                    order.AddRange(state.InitiativeOrder());
                    cursor = 0;
                    if (order.Count == 0) return;
                }

                activeUnit = order[cursor];
                if (activeUnit == null || !activeUnit.IsAlive) continue;

                hasActed = false;
                OnStateChanged?.Invoke();

                if (activeUnit == state.player) return;   // wait for input
                AiTurn(activeUnit);
            }
        }

        /// <summary>Resolve the rule; ends the battle when it is no longer ongoing.</summary>
        private bool CheckOutcome()
        {
            var result = state.Evaluate();
            if (result == BattleOutcome.Ongoing) return false;
            Finish(result);
            return true;
        }
        // --- the player's turn ---------------------------------------------

        /// <summary>Cells left in the active unit's movement budget this round.</summary>
        public int MoveRemaining =>
            activeUnit == null ? 0 : state.grid.MoveRemainingOf(activeUnit);

        public bool CanMoveTo(GridPos p) =>
            IsPlayerTurn && !hasActed && state.grid.IsFree(p) &&
            activeUnit.pos.DistanceTo(p) <= MoveRemaining &&
            activeUnit.pos != p;

        /// <summary>
        /// Step the player. Movement is free-form inside the budget and does not
        /// consume the turn, so repositioning before committing is the decision
        /// the grid is there to create.
        /// </summary>
        public bool PlayerMove(GridPos p)
        {
            if (!CanMoveTo(p)) return false;

            var from = activeUnit.pos;
            int spent = state.grid.TryMove(activeUnit, p);
            if (spent <= 0) return false;

            state.log.Record(new BattleActionEntry
            {
                turn = state.turn, actorId = activeUnit.id,
                kind = BattleActionKind.Move, fromPos = from, toPos = p
            });

            // Legs carried you, so anything worn there earns its depth.
            AccumulateAnomalyDepth("leg");
            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>Living hostiles the active unit can reach without moving.</summary>
        public List<BattleUnit> AttackableTargets()
        {
            var list = new List<BattleUnit>();
            if (activeUnit == null || hasActed) return list;
            foreach (var u in state.grid.LivingUnits)
                if (activeUnit.IsHostileTo(u) && state.grid.InAttackRange(activeUnit, u))
                    list.Add(u);
            return list;
        }

        public bool PlayerAttack(BattleUnit target)
        {
            if (!IsPlayerTurn || hasActed || target == null || !target.IsAlive) return false;
            if (!state.grid.InAttackRange(activeUnit, target)) return false;

            ResolveAttack(activeUnit, target);
            AccumulateAnomalyDepth("hand");
            EndActorTurn();
            return true;
        }

        /// <summary>
        /// 装作没事 — stand still. Costs the turn, but stability can convince a
        /// trash encounter to lose interest, which is the cheapest way out of one.
        /// </summary>
        public bool PlayerActNormal()
        {
            if (!IsPlayerTurn || hasActed) return false;

            var player = Core.GameManager.Instance?.Player;
            float chance = 0.10f + (player?.GetTotalStability() ?? 0) * 0.003f;
            if (!state.IsNemesisScale && Random.value < chance)
            {
                Log("你装作什么都没发生。他们看了你一眼，然后觉得算了。");
                Finish(BattleOutcome.Victory, false);
                return true;
            }

            Log("你装作没事。没人信。");
            EndActorTurn();
            return true;
        }

        public bool PlayerWait()
        {
            if (!IsPlayerTurn || hasActed) return false;
            Log($"{activeUnit.displayName} 站着不动。");
            EndActorTurn();
            return true;
        }

        /// <summary>
        /// 跑 — only from the edge you came in on, so fleeing is a position you
        /// have to earn rather than a button that is always available.
        /// </summary>
        public bool PlayerFlee()
        {
            if (!IsPlayerTurn || hasActed) return false;

            if (activeUnit.pos.x != 0)
            {
                Log("你想跑，但你离出口还有距离。往左边退到底才跑得掉。");
                return false;
            }

            var eff = Core.GameManager.Instance?.EffectivePlayerStats;
            float chance = Mathf.Clamp(0.3f + (eff?.Speed ?? 8) * 0.01f
                                       - NearestEnemySpeed() * 0.01f, 0.1f, 0.8f);
            if (Random.value < chance)
            {
                Log("你跑了。你没回头。");
                Finish(BattleOutcome.Defeat, false);
                return true;
            }

            Log("你想跑。跑不掉。");
            EndActorTurn();
            return true;
        }

        private int NearestEnemySpeed()
        {
            int best = 0;
            foreach (var u in state.grid.LivingUnits)
                if (u.side == BattleSide.Enemy) best = Mathf.Max(best, u.speed);
            return best;
        }

        private void EndActorTurn()
        {
            hasActed = true;
            OnStateChanged?.Invoke();
            AdvanceTurn();
        }
        // --- AI ------------------------------------------------------------

        /// <summary>
        /// One AI turn: hit something if it is already in range, otherwise close
        /// the distance and hit if the step brought a target into range. Called
        /// from inside the initiative loop, so it never advances the turn itself.
        /// </summary>
        private void AiTurn(BattleUnit unit)
        {
            var target = NearestHostile(unit);
            if (target == null) { hasActed = true; return; }

            if (!state.grid.InAttackRange(unit, target))
            {
                StepToward(unit, target);
                target = NearestHostile(unit);
            }

            if (target != null && state.grid.InAttackRange(unit, target))
                ResolveAttack(unit, target);
            else
                Log($"{unit.displayName} 挪了两步，没够着。");

            hasActed = true;
            OnStateChanged?.Invoke();
        }

        private BattleUnit NearestHostile(BattleUnit unit)
        {
            BattleUnit best = null;
            int bestDist = int.MaxValue;
            foreach (var u in state.grid.LivingUnits)
            {
                if (!unit.IsHostileTo(u)) continue;
                int d = unit.pos.DistanceTo(u.pos);
                if (d < bestDist) { bestDist = d; best = u; }
            }
            return best;
        }

        /// <summary>
        /// Greedy approach: of every free cell inside the movement budget, take
        /// the one that ends closest to the target. TryMove is a budgeted jump
        /// rather than a path, so this is the whole of the AI's positioning.
        /// </summary>
        private void StepToward(BattleUnit unit, BattleUnit target)
        {
            int budget = state.grid.MoveRemainingOf(unit);
            if (budget <= 0) return;

            var bestCell = unit.pos;
            int bestDist = unit.pos.DistanceTo(target.pos);

            for (int dx = -budget; dx <= budget; dx++)
            {
                for (int dy = -budget; dy <= budget; dy++)
                {
                    var p = new GridPos(unit.pos.x + dx, unit.pos.y + dy);
                    if (p == unit.pos || !state.grid.IsFree(p)) continue;
                    if (unit.pos.DistanceTo(p) > budget) continue;

                    int d = p.DistanceTo(target.pos);
                    if (d < bestDist) { bestDist = d; bestCell = p; }
                }
            }

            if (bestCell == unit.pos) return;
            var from = unit.pos;
            if (state.grid.TryMove(unit, bestCell) <= 0) return;

            state.log.Record(new BattleActionEntry
            {
                turn = state.turn, actorId = unit.id,
                kind = BattleActionKind.Move, fromPos = from, toPos = bestCell
            });
        }

        // --- damage --------------------------------------------------------

        /// <summary>Floor for a hit before perception and terrain get a say.</summary>
        public const float BaseHitRate = 0.90f;

        /// <summary>
        /// One exchange. Weather scales the attack, the defender's cell can add
        /// evasion, and the entry goes into the action log with the HP the target
        /// had beforehand — that recorded value is what makes Retcon possible.
        /// </summary>
        private void ResolveAttack(BattleUnit attacker, BattleUnit target)
        {
            var atkMods = new StatModifierSet();
            var defMods = new StatModifierSet();
            state.CollectBattlefieldModifiers(attacker, atkMods);
            state.CollectBattlefieldModifiers(target, defMods);

            float chance = Mathf.Clamp(
                BaseHitRate + atkMods.Get(StatKeys.HitRate) - defMods.Get(StatKeys.Dodge),
                0.05f, 0.99f);

            int before = target.hp;
            bool landed = Random.value < chance;
            int damage = 0;

            if (landed)
            {
                float mult = BattleWeather.AttackMultiplierFor(state.weather);
                damage = Mathf.Max(1,
                    Mathf.RoundToInt(attacker.attack * mult) - target.defense / 2);
                target.hp = Mathf.Max(0, target.hp - damage);
                state.CreditDamage(attacker.side, damage);

                Log($"{attacker.displayName} → {target.displayName}　-{damage}　" +
                    $"({target.hp}/{target.maxHp})");
            }
            else
            {
                Log($"{attacker.displayName} → {target.displayName}　MISS");
            }

            state.log.Record(new BattleActionEntry
            {
                turn = state.turn, actorId = attacker.id, kind = BattleActionKind.Attack,
                targetId = target.id, hit = landed, damage = damage,
                targetHpBefore = before
            });

            if (target == state.player && damage > 0)
            {
                Core.GameManager.Instance?.Player?.TakeDamage(damage);
                AccumulateAnomalyDepth("torso");
            }

            if (!target.IsAlive) Log($"{target.displayName} 倒了。");
        }
        // --- resolution ----------------------------------------------------

        /// <summary>
        /// Close the battle. Walking away and running away both end it without
        /// rewards or penalties: nothing was settled, so nothing changes hands.
        /// </summary>
        private void Finish(BattleOutcome result, bool withStakes = true)
        {
            if (!isActive) return;

            outcome = result;
            isActive = false;
            activeUnit = null;
            Log("");

            if (result == BattleOutcome.Victory)
            {
                Log($"你还站着。{state.condition.DisplayName} 达成。");
                if (withStakes) GrantDrops();
            }
            else
            {
                Log("你倒了。天黑了一下，然后你又醒了。");
                if (withStakes) ApplyDefeatPenalty();
            }

            OnStateChanged?.Invoke();
            OnCombatEnd?.Invoke();
        }

        private void GrantDrops()
        {
            var player = Core.GameManager.Instance?.Player;
            var drops = currentEnemy?.drops;
            if (player == null || drops == null) return;

            if (drops.resonanceShards > 0)
            {
                for (int i = 0; i < drops.resonanceShards; i++)
                    player.inventory.Add("resonance_shard");
                Log($"Resonance Shard +{drops.resonanceShards}");
            }

            if (!string.IsNullOrEmpty(drops.anomaly))
            {
                Log($"Anomaly Acquired: {drops.anomaly}");
                AssignAnomaly(drops.anomaly);
            }
        }

        private void ApplyDefeatPenalty()
        {
            var player = Core.GameManager.Instance?.Player;
            if (player == null) return;

            int lost = player.money / 5;
            player.SpendMoney(lost);
            Log($"少了 ¥{lost}。装备也磕坏了点。");
        }

        private void AssignAnomaly(string anomalyId)
        {
            var player = Core.GameManager.Instance?.Player;
            if (player?.bodyComponents == null) return;

            for (int i = 0; i < player.bodyComponents.Length; i++)
            {
                if (!player.bodyComponents[i].HasAnomaly)
                {
                    player.bodyComponents[i].anomaly = anomalyId;
                    return;
                }
            }
            player.bodyComponents[0].anomaly = anomalyId;
        }

        /// <summary>
        /// Grant depth to anomalies equipped on the component family that just
        /// acted. slotFamily is the anomaly slot ("hand", "leg", "brain",
        /// "torso", "carry") and matches both sides of a pair, so left_hand and
        /// right_hand both count as "hand". Wearing something idle earns nothing.
        /// </summary>
        private void AccumulateAnomalyDepth(string slotFamily)
        {
            var gm = Core.GameManager.Instance;
            if (gm?.Anomalies == null || string.IsNullOrEmpty(slotFamily)) return;

            int fortune = gm.EffectivePlayerStats.Fortune;
            bool leveled = false;

            foreach (var inst in gm.Anomalies.Instances)
            {
                if (!inst.IsEquipped || !inst.equippedOn.EndsWith(slotFamily)) continue;
                if (gm.Anomalies.RegisterUse(inst, fortune) <= 0) continue;

                leveled = true;
                var def = gm.Anomalies.Define(inst.itemId);
                var lv = def?.LevelAt(inst.level);
                Log($"Anomaly Unfold! {def?.name} → LV.{inst.level}");
                if (lv?.buff != null && !string.IsNullOrEmpty(lv.buff.text))
                    Log($"  + {lv.buff.text}");
                if (lv?.debuff != null && !string.IsNullOrEmpty(lv.debuff.text))
                    Log($"  - {lv.debuff.text}");
            }

            if (leveled) gm.RebuildModifiers();
        }

        private void Log(string message)
        {
            combatLog.Add(message);
            OnLogMessage?.Invoke(message);
        }
    }
}
