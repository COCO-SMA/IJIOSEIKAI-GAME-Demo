using System.Collections.Generic;
using UnityEngine;
using KunchengRPG.Game;

namespace KunchengRPG.EditorTools
{
    /// <summary>
    /// Batchmode checks for the grid combat model: grid maths, movement derived
    /// from action power, victory rules, weather, the action log, and squad setup.
    /// One entry point so a whole run costs a single Unity launch.
    /// </summary>
    public static class GridCombatTests
    {
        private const string Tag = "[GridTest]";
        private static int passed, failed;

        public static void RunAll()
        {
            passed = failed = 0;

            TestGridPos();
            TestMovement();
            TestDisplace();
            TestTerrain();
            TestWeather();
            TestVictoryParsing();
            TestScoreRule();
            TestEscapeRule();
            TestSurvivalRule();
            TestActionLog();
            TestSquad();
            TestEnemyTuning();
            TestRandomizedRoster();
            TestCombatLoop();
            TestEncounterData();

            Debug.Log($"{Tag} RESULT passed={passed} failed={failed}");
        }

        private static void Check(string name, bool ok, string detail = "")
        {
            if (ok) { passed++; Debug.Log($"{Tag} PASS {name}"); }
            else { failed++; Debug.LogError($"{Tag} FAIL {name} {detail}"); }
        }

        private static void CheckEq(string name, object actual, object expected) =>
            Check(name, Equals(actual, expected), $"got {actual}, want {expected}");

        // --- grid ---------------------------------------------------------

        private static void TestGridPos()
        {
            var a = new GridPos(1, 1);
            CheckEq("chebyshev diagonal", a.DistanceTo(new GridPos(3, 3)), 2);
            CheckEq("chebyshev orthogonal", a.DistanceTo(new GridPos(1, 5)), 4);
            Check("adjacency diagonal", a.IsAdjacentTo(new GridPos(2, 2)));
            Check("equality", a == new GridPos(1, 1));

            var g = new BattleGrid(BattleGrid.TrashSize);
            CheckEq("trash grid size", g.width, 6);
            Check("in bounds", g.InBounds(new GridPos(5, 5)));
            Check("out of bounds", !g.InBounds(new GridPos(6, 0)));
        }

        private static BattleUnit Unit(string id, BattleSide side, int speed = 8, int hp = 30) =>
            new BattleUnit
            {
                id = id, displayName = id, side = side,
                hp = hp, maxHp = hp, speed = speed, attack = 5, defense = 1
            };

        private static void TestMovement()
        {
            // Move range is speed / 4, floored, minimum one cell.
            CheckEq("move range speed 8", Unit("a", BattleSide.Player, 8).MoveRange, 2);
            CheckEq("move range speed 3", Unit("b", BattleSide.Player, 3).MoveRange, 1);
            CheckEq("move range speed 0", Unit("c", BattleSide.Player, 0).MoveRange, 1);
            CheckEq("move range speed 12", Unit("d", BattleSide.Player, 12).MoveRange, 3);

            var g = new BattleGrid(8);
            var u = Unit("mover", BattleSide.Player, 8);
            g.Place(u, new GridPos(0, 0));

            CheckEq("move within range", g.TryMove(u, new GridPos(2, 0)), 2);
            CheckEq("position after move", u.pos, new GridPos(2, 0));
            CheckEq("range exhausted", g.MoveRemainingOf(u), 0);
            CheckEq("second move refused", g.TryMove(u, new GridPos(3, 0)), 0);

            g.BeginTurn();
            CheckEq("range restored", g.MoveRemainingOf(u), 2);
            CheckEq("over-range refused", g.TryMove(u, new GridPos(6, 0)), 0);

            var blocker = Unit("blocker", BattleSide.Enemy);
            g.Place(blocker, new GridPos(3, 0));
            CheckEq("occupied refused", g.TryMove(u, new GridPos(3, 0)), 0);
            Check("occupancy lookup", g.UnitAt(new GridPos(3, 0)) == blocker);
        }

        private static void TestDisplace()
        {
            var g = new BattleGrid(8);
            var u = Unit("u", BattleSide.Player, 4);       // 1 cell of normal movement
            g.Place(u, new GridPos(0, 0));

            Check("displace ignores range", g.Displace(u, new GridPos(7, 7)));
            CheckEq("displaced position", u.pos, new GridPos(7, 7));

            var other = Unit("other", BattleSide.Enemy);
            g.Place(other, new GridPos(3, 3));
            Check("displace onto occupied falls back", g.Displace(u, new GridPos(3, 3)));
            Check("fallback is adjacent", u.pos.IsAdjacentTo(new GridPos(3, 3)));
            Check("blocker not moved", other.pos == new GridPos(3, 3));
        }

        private static void TestTerrain()
        {
            var g = new BattleGrid(8);
            var u = Unit("u", BattleSide.Player);
            g.Place(u, new GridPos(4, 4));

            g.SetTerrainArea(new GridPos(4, 4), 1, TerrainCell.Rain());
            var mods = new StatModifierSet();
            g.CollectTerrainModifiers(u, mods);
            // Indoor umbrella L2: rain grants evasion. No elemental weakness.
            CheckEq("rain dodge bonus", mods.Get(StatKeys.Dodge), 0.15f);

            g.Place(u, new GridPos(0, 0));
            var dry = new StatModifierSet();
            g.CollectTerrainModifiers(u, dry);
            Check("outside patch is dry", dry.IsEmpty);
        }

        private static void TestWeather()
        {
            // Anchored on the real umbrella L2: harsh +30%, clear -20%.
            CheckEq("harsh attack mult", BattleWeather.AttackMultiplierFor(WeatherBand.Harsh), 1.3f);
            CheckEq("clear attack mult", BattleWeather.AttackMultiplierFor(WeatherBand.Clear), 0.8f);
            CheckEq("overcast neutral", BattleWeather.AttackMultiplierFor(WeatherBand.Overcast), 1.0f);

            var harsh = new StatModifierSet();
            BattleWeather.ApplyTo(WeatherBand.Harsh, harsh);
            CheckEq("harsh hit penalty", harsh.Get(StatKeys.HitRate), -0.10f);

            var overcast = new StatModifierSet();
            BattleWeather.ApplyTo(WeatherBand.Overcast, overcast);
            Check("overcast has no modifiers", overcast.IsEmpty);
        }

        // --- victory rules ------------------------------------------------

        private static BattleState MakeState(VictoryCondition cond, int size = 8)
        {
            var g = new BattleGrid(size);
            var s = new BattleState(g, cond, WeatherBand.Overcast);
            s.player = Unit("player", BattleSide.Player);
            g.Place(s.player, new GridPos(0, 0));
            return s;
        }

        private static void TestVictoryParsing()
        {
            // The one rule already living in enemy JSON must survive being absorbed.
            var thr = new Dictionary<string, object> { { "stat", "resilience" }, { "value", 8.0 } };
            var c = VictoryCondition.FromData("attrition", thr, false);
            CheckEq("attrition rule parsed", c.rule, VictoryRule.Attrition);
            CheckEq("attrition threshold", c.threshold, 8);
            CheckEq("attrition stat", c.thresholdStat, "resilience");

            var unknown = VictoryCondition.FromData("nonsense", null, false);
            CheckEq("unknown falls back", unknown.rule, VictoryRule.Annihilation);
            Check("annihilation is unlimited", !unknown.HasTurnLimit);

            var missing = VictoryCondition.FromData(null, null, false);
            CheckEq("null falls back", missing.rule, VictoryRule.Annihilation);

            CheckEq("survival nemesis clock",
                VictoryCondition.FromData("survival", null, true).turnLimit, 15);
            CheckEq("survival trash clock",
                VictoryCondition.FromData("survival", null, false).turnLimit, 6);
        }

        private static void TestScoreRule()
        {
            var s = MakeState(VictoryCondition.Score(10));
            var foe = Unit("foe", BattleSide.Enemy, 5, 500);
            s.grid.Place(foe, new GridPos(7, 7));

            CheckEq("score starts ongoing", s.Evaluate(), BattleOutcome.Ongoing);
            s.CreditDamage(BattleSide.Player, 30);
            s.CreditDamage(BattleSide.Enemy, 10);
            CheckEq("player score", s.playerScore, 30);
            CheckEq("enemy score", s.enemyScore, 10);
            s.CreditDamage(BattleSide.Player, -5);
            CheckEq("negative damage ignored", s.playerScore, 30);

            for (int i = 0; i < 10; i++) s.EndTurn();
            CheckEq("score win on clock", s.Evaluate(), BattleOutcome.Victory);

            var tie = MakeState(VictoryCondition.Score(1));
            tie.grid.Place(Unit("foe2", BattleSide.Enemy, 5, 500), new GridPos(7, 7));
            tie.CreditDamage(BattleSide.Player, 10);
            tie.CreditDamage(BattleSide.Enemy, 10);
            tie.EndTurn();
            CheckEq("tie goes to defender", tie.Evaluate(), BattleOutcome.Defeat);
        }

        private static void TestEscapeRule()
        {
            var s = MakeState(VictoryCondition.Escape(5, 4));
            var foe = Unit("foe", BattleSide.Enemy);
            s.grid.Place(foe, new GridPos(1, 0));       // 1 cell away: too close

            for (int i = 0; i < 5; i++) s.EndTurn();
            CheckEq("streak broken while adjacent", s.streak, 0);
            CheckEq("escape still ongoing", s.Evaluate(), BattleOutcome.Ongoing);

            s.grid.Displace(foe, new GridPos(6, 6));    // now far enough
            for (int i = 0; i < 5; i++) s.EndTurn();
            CheckEq("streak accumulated", s.streak, 5);
            CheckEq("escape win", s.Evaluate(), BattleOutcome.Victory);

            // Escape has a turn limit, but reaching it is the win, not a loss.
            var reset = MakeState(VictoryCondition.Escape(5, 4));
            var near = Unit("near", BattleSide.Enemy);
            reset.grid.Place(near, new GridPos(6, 6));
            reset.EndTurn();
            reset.EndTurn();
            CheckEq("partial streak", reset.streak, 2);
            reset.grid.Displace(near, new GridPos(1, 0));
            reset.EndTurn();
            CheckEq("streak resets on approach", reset.streak, 0);
        }

        private static void TestSurvivalRule()
        {
            var s = MakeState(VictoryCondition.Survival(6));
            var foe = Unit("foe", BattleSide.Enemy, 5, 500);
            s.grid.Place(foe, new GridPos(7, 7));

            CheckEq("survival ongoing", s.Evaluate(), BattleOutcome.Ongoing);
            for (int i = 0; i < 6; i++) s.EndTurn();
            CheckEq("survival win on clock", s.Evaluate(), BattleOutcome.Victory);

            // Death outranks every rule.
            var dead = MakeState(VictoryCondition.Survival(6));
            dead.grid.Place(Unit("foe2", BattleSide.Enemy), new GridPos(7, 7));
            dead.player.hp = 0;
            CheckEq("death beats the clock", dead.Evaluate(), BattleOutcome.Defeat);

            // Clearing the field wins under any rule.
            var cleared = MakeState(VictoryCondition.Annihilation());
            var victim = Unit("victim", BattleSide.Enemy);
            cleared.grid.Place(victim, new GridPos(7, 7));
            CheckEq("annihilation ongoing", cleared.Evaluate(), BattleOutcome.Ongoing);
            victim.hp = 0;
            CheckEq("annihilation win", cleared.Evaluate(), BattleOutcome.Victory);
            CheckEq("living enemies counted", cleared.LivingEnemies, 0);
        }

        // --- action log / retcon -------------------------------------------

        private static void TestActionLog()
        {
            var log = new BattleActionLog();
            var target = Unit("target", BattleSide.Enemy, 5, 100);

            // A miss: recorded with the HP it had, target untouched.
            var miss = log.Record(new BattleActionEntry
            {
                turn = 1, actorId = "player", kind = BattleActionKind.Attack,
                targetId = "target", hit = false, damage = 0, targetHpBefore = 100
            });
            CheckEq("log records", log.Count, 1);
            Check("last entry", log.Last == miss);

            Check("retcon miss to hit", log.RetconToHit(miss, target, 40));
            CheckEq("retcon applied damage", target.hp, 60);
            Check("entry marked hit", miss.hit);
            Check("entry marked retconned", miss.retconned);
            Check("retcon cannot stack", !log.RetconToHit(miss, target, 40));
            CheckEq("hp unchanged by refused retcon", target.hp, 60);

            // A landed hit rewritten into a miss restores exactly.
            var hit = log.Record(new BattleActionEntry
            {
                turn = 2, actorId = "player", kind = BattleActionKind.Attack,
                targetId = "target", hit = true, damage = 25, targetHpBefore = 60
            });
            target.hp = 35;
            Check("retcon hit to miss", log.RetconToMiss(hit, target));
            CheckEq("damage undone exactly", target.hp, 60);

            // Non-attacks are not rewritable.
            var move = log.Record(new BattleActionEntry
            {
                turn = 2, actorId = "player", kind = BattleActionKind.Move
            });
            Check("moves are not retconnable", !log.RetconToHit(move, target, 10));

            // The window bounds how far back a rewrite reaches.
            var fresh = new BattleActionLog();
            var old = fresh.Record(new BattleActionEntry
            {
                turn = 1, actorId = "player", kind = BattleActionKind.Attack,
                targetId = "target", targetHpBefore = 100
            });
            for (int i = 0; i < BattleActionLog.RetconWindow; i++)
                fresh.Record(new BattleActionEntry
                {
                    turn = 2 + i, actorId = "player", kind = BattleActionKind.Move
                });
            Check("out-of-window attack unreachable", fresh.LastRetconnableAttack() == null);
            Check("old entry was the attack", old.kind == BattleActionKind.Attack);

            var reachable = new BattleActionLog();
            reachable.Record(new BattleActionEntry
            {
                turn = 1, actorId = "player", kind = BattleActionKind.Attack,
                targetId = "target", targetHpBefore = 100
            });
            reachable.Record(new BattleActionEntry
            {
                turn = 1, actorId = "player", kind = BattleActionKind.Move
            });
            Check("in-window attack found", reachable.LastRetconnableAttack() != null);
            Check("actor filter excludes others",
                reachable.LastRetconnableAttack("someone_else") == null);
        }

        // --- squad / setup --------------------------------------------------

        private static Data.EnemyData FakeEnemy(int stars, int hp = 100) => new Data.EnemyData
        {
            id = "test_foe", name = "测试天敌", stars = stars,
            stats = new Data.EnemyStats { hp = hp, attack = 10, defense = 5, speed = 8 }
        };

        private static void TestSquad()
        {
            CheckEq("trash board for 1 star", BattleSetup.GridSizeFor(FakeEnemy(1)), 6);
            CheckEq("trash board for 2 stars", BattleSetup.GridSizeFor(FakeEnemy(2)), 6);
            CheckEq("nemesis board for 3 stars", BattleSetup.GridSizeFor(FakeEnemy(3)), 8);
            CheckEq("nemesis board for 5 stars", BattleSetup.GridSizeFor(FakeEnemy(5)), 8);

            // No enemy JSON declares a squad yet, so padding is the common path.
            var solo = BattleSetup.BuildRoster(FakeEnemy(1));
            CheckEq("padded to minimum", solo.Count, BattleSetup.MinEnemies);
            CheckEq("leader keeps full hp", solo[0].maxHp, 100);
            CheckEq("escort scaled to 60%", solo[1].maxHp, 60);
            Check("all on enemy side", solo.TrueForAll(u => u.side == BattleSide.Enemy));

            var withSquad = FakeEnemy(4);
            withSquad.squad = new List<Data.EnemySquadEntry>
            {
                new Data.EnemySquadEntry { id = "minion", count = 4, statScale = 0.5f }
            };
            var roster = BattleSetup.BuildRoster(withSquad);
            CheckEq("declared squad size", roster.Count, 5);
            CheckEq("declared scale applied", roster[1].maxHp, 50);

            var overflow = FakeEnemy(5);
            overflow.squad = new List<Data.EnemySquadEntry>
            {
                new Data.EnemySquadEntry { id = "horde", count = 20, statScale = 0.4f }
            };
            CheckEq("trimmed to maximum",
                BattleSetup.BuildRoster(overflow).Count, BattleSetup.MaxEnemies);

            // Full setup: everyone placed, nobody sharing a cell, sides separated.
            var party = new List<PartyMember>
            {
                new PartyMember { id = "ally1", displayName = "队友甲" },
                new PartyMember { id = "ally2", displayName = "队友乙" },
                new PartyMember { id = "ally3", displayName = "多余的" }
            };
            var state = BattleSetup.Build(
                FakeEnemy(3), new Player { name = "阿晖", hp = 50, maxHp = 50 },
                new EffectiveStats(new PlayerStats { strength = 20, actionPower = 12 },
                                   new StatModifierSet()),
                party, WeatherBand.Overcast);

            CheckEq("party capped at two", state.LivingFriendlies, 3);   // player + 2
            CheckEq("enemies spawned", state.LivingEnemies, BattleSetup.MinEnemies);
            CheckEq("nemesis grid", state.grid.width, 8);
            Check("nemesis scale flag", state.IsNemesisScale);

            var seen = new HashSet<GridPos>();
            bool unique = true;
            foreach (var u in state.grid.LivingUnits)
                if (!seen.Add(u.pos)) unique = false;
            Check("no two units share a cell", unique);

            // Asserted once outside the loop: rosters are 3-7 units, so a per-unit Check
            // made the total test count drift between runs and hid whether a case ran.
            string strayed = null;
            foreach (var u in state.grid.LivingUnits)
                if (u.side == BattleSide.Enemy && u.pos.x < state.grid.width / 2)
                    strayed = $"{u.id} at {u.pos}";
            Check("enemies stay on their half", strayed == null, strayed ?? "");

            CheckEq("player speed from action power", state.player.speed, 12);
            CheckEq("player move range", state.player.MoveRange, 3);
            CheckEq("player attack from strength", state.player.attack, 20);

            // Initiative is fastest first.
            var order = state.InitiativeOrder();
            bool sorted = true;
            for (int i = 1; i < order.Count; i++)
                if (order[i - 1].speed < order[i].speed) sorted = false;
            Check("initiative sorted by speed", sorted);
            CheckEq("initiative covers the field", order.Count, state.grid.Units.Count);
        }

        private static void TestEnemyTuning()
        {
            // The ladder must rise; content is authored against it.
            bool rising = true;
            for (int s = 2; s <= 5; s++)
                if (EnemyTuning.For(s).hp <= EnemyTuning.For(s - 1).hp) rising = false;
            Check("tuning ladder rises", rising);
            Check("tuning clamps low", EnemyTuning.For(0).hp == EnemyTuning.For(1).hp);
            Check("tuning clamps high", EnemyTuning.For(99).hp == EnemyTuning.For(5).hp);

            // Shipped enemies should sit on their star row.
            var gm = Core.GameManager.Instance;
            var enemies = gm != null && gm.enemies != null
                ? gm.enemies : Core.AssetLoader.LoadAllEnemies();
            foreach (var kvp in enemies)
            {
                var e = kvp.Value;
                // Test dummies roll their own numbers and are not authored content,
                // so the ladder does not apply to them.
                if (e.randomizeStats) continue;
                var row = EnemyTuning.For(e.stars);
                Check($"tuned hp {e.id}", e.stats.hp == row.hp,
                      $"hp {e.stats.hp} vs row {row.hp} for {e.stars} stars");
                Check($"tuned defense {e.id}", e.stats.defense == row.defense,
                      $"def {e.stats.defense} vs row {row.defense}");
            }
        }

        // --- test dummies / encounter wiring ---------------------------------

        private static void TestRandomizedRoster()
        {
            var dummy = FakeEnemy(1, 999);
            dummy.randomizeStats = true;
            dummy.stats.attack = 1;

            bool hpInRange = true, defInRange = true, spdInRange = true, attackPinned = true;
            var hpSeen = new HashSet<int>();

            // Many rolls, because a single one cannot show that a bound is respected.
            for (int i = 0; i < 40; i++)
            {
                foreach (var u in BattleSetup.BuildRoster(dummy))
                {
                    hpSeen.Add(u.maxHp);
                    if (u.maxHp < BattleSetup.RandomHpMin ||
                        u.maxHp > BattleSetup.RandomHpMax) hpInRange = false;
                    if (u.hp != u.maxHp) hpInRange = false;
                    if (u.defense < BattleSetup.RandomDefenseMin ||
                        u.defense > BattleSetup.RandomDefenseMax) defInRange = false;
                    if (u.speed < BattleSetup.RandomSpeedMin ||
                        u.speed > BattleSetup.RandomSpeedMax) spdInRange = false;
                    if (u.attack != 1) attackPinned = false;
                }
            }

            Check("random hp within bounds", hpInRange);
            Check("random defense within bounds", defInRange);
            Check("random speed within bounds", spdInRange);
            Check("attack stays pinned at 1", attackPinned);
            Check("hp actually varies", hpSeen.Count > 1, $"only saw {hpSeen.Count} value(s)");

            // Authored enemies must be untouched by the dummy path.
            var authored = FakeEnemy(2, 120);
            foreach (var u in BattleSetup.BuildRoster(authored))
                if (u.id == authored.id)
                    CheckEq("authored leader keeps hp", u.maxHp, 120);
        }

        private static void TestCombatLoop()
        {
            var dummy = FakeEnemy(1, 30);
            dummy.randomizeStats = true;
            dummy.stats.attack = 1;
            dummy.winCondition = "annihilation";
            dummy.drops = new Data.EnemyDrops { resonanceShards = 1 };

            var host = new GameObject("CombatTestHost");
            var cs = host.AddComponent<Game.CombatSystem>();
            try
            {
                var stats = new EffectiveStats(
                    new PlayerStats { strength = 20, actionPower = 12 }, new StatModifierSet());
                cs.StartCombat(dummy, new Player { name = "阿晖", hp = 500, maxHp = 500 }, stats);

                Check("combat became active", cs.isActive);
                CheckEq("three enemy units", cs.state.LivingEnemies, 3);
                CheckEq("solo party", cs.state.LivingFriendlies, 1);
                CheckEq("trash board", cs.state.grid.width, 6);
                Check("someone holds the turn", cs.activeUnit != null);

                // Play it out. The player closes in and swings; the AI does its own.
                int guard = 0;
                bool movedWithoutEndingTurn = false;
                // Swings are counted, not asserted per swing: roster size and HP are
                // randomised, so a Check in here made the suite total drift run to run.
                int swings = 0, refused = 0;
                while (cs.isActive && guard++ < 400)
                {
                    if (!cs.IsPlayerTurn) break;   // AI turns resolve inside AdvanceTurn

                    var targets = cs.AttackableTargets();
                    if (targets.Count > 0)
                    {
                        if (cs.PlayerAttack(targets[0])) swings++; else refused++;
                        continue;
                    }

                    if (StepPlayerTowardEnemy(cs))
                    {
                        if (cs.IsPlayerTurn && !cs.hasActed) movedWithoutEndingTurn = true;
                        continue;
                    }
                    cs.PlayerWait();
                }

                Check("every attack accepted", refused == 0, $"{refused} of {swings + refused} refused");
                Check("player actually swung", swings > 0);
                Check("movement does not spend the turn", movedWithoutEndingTurn);
                Check("fight terminates", !cs.isActive || guard < 400,
                      $"guard={guard} active={cs.isActive}");
                Check("outcome decided", cs.outcome != BattleOutcome.Ongoing,
                      $"outcome={cs.outcome}");
                Check("log written", cs.combatLog.Count > 0);
                Check("actions recorded", cs.state.log.Count > 0);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>Greedy step toward the closest enemy; false when already close.</summary>
        private static bool StepPlayerTowardEnemy(Game.CombatSystem cs)
        {
            var me = cs.activeUnit;
            BattleUnit foe = null;
            int best = int.MaxValue;
            foreach (var u in cs.state.grid.LivingUnits)
            {
                if (u.side != BattleSide.Enemy) continue;
                int d = me.pos.DistanceTo(u.pos);
                if (d < best) { best = d; foe = u; }
            }
            if (foe == null) return false;

            int budget = cs.MoveRemaining;
            var target = me.pos;
            int targetDist = best;
            for (int dx = -budget; dx <= budget; dx++)
                for (int dy = -budget; dy <= budget; dy++)
                {
                    var p = new GridPos(me.pos.x + dx, me.pos.y + dy);
                    if (!cs.CanMoveTo(p)) continue;
                    int d = p.DistanceTo(foe.pos);
                    if (d < targetDist) { targetDist = d; target = p; }
                }

            return target != me.pos && cs.PlayerMove(target);
        }

        /// <summary>
        /// The shipped encounter: the test dummy must exist and 金涌 must carry a
        /// POI pointing at it, or nothing on the map opens a fight.
        /// </summary>
        private static void TestEncounterData()
        {
            var gm = Core.GameManager.Instance;
            var enemies = gm != null && gm.enemies != null
                ? gm.enemies : Core.AssetLoader.LoadAllEnemies();

            Check("test dummy shipped", enemies.ContainsKey("test_guard_check"));
            if (enemies.TryGetValue("test_guard_check", out var dummy))
            {
                Check("dummy randomizes", dummy.randomizeStats);
                CheckEq("dummy attack is 1", dummy.stats.attack, 1);
                Check("dummy is trash tier", dummy.stars < 3, $"stars={dummy.stars}");
            }

            var districts = gm != null && gm.districts != null
                ? gm.districts : Core.AssetLoader.LoadAllDistricts();
            if (!districts.TryGetValue("jinyong", out var jinyong))
            {
                Check("jinyong loaded", false);
                return;
            }

            Data.POIData gate = null;
            foreach (var p in jinyong.points)
                if (p.type == "enemy") { gate = p; break; }

            Check("jinyong has an enemy POI", gate != null);
            if (gate == null) return;

            CheckEq("POI points at the dummy", gate.enemyId, "test_guard_check");
            Check("POI stands on walkable ground",
                  !IsSolid(jinyong, gate.x, gate.y), $"({gate.x},{gate.y}) is blocked");

            // An NPC on an adjacent tile wins the proximity check and the fight
            // would be unreachable, so the gate needs elbow room.
            bool clear = true;
            if (jinyong.npcs != null)
                foreach (var n in jinyong.npcs)
                    if (Mathf.Abs(n.x - gate.x) + Mathf.Abs(n.y - gate.y) <= 1) clear = false;
            Check("no NPC shadows the gate", clear);
        }

        private static bool IsSolid(Data.DistrictData d, int x, int y)
        {
            if (d.tiles == null || y < 0 || y >= d.height || x < 0 || x >= d.width) return true;
            int id = d.tiles[y][x];
            return id == 1 || id == 2 || id == 3 || id == 8 || id == 9 || id == 11 ||
                   id == 12 || id == 13 || id == 14 || id == 18 || id == 19 ||
                   id == 20 || id == 21;
        }
    }
}
