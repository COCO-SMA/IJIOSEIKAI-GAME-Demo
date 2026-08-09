using System.Collections.Generic;
using UnityEngine;

namespace KunchengRPG.Game
{
    /// <summary>
    /// A companion fighting alongside the player. GDD 29.3 allows the player plus
    /// at most two, under AI control with a settable leaning.
    /// </summary>
    public class PartyMember
    {
        public string id;
        public string displayName;
        public int hp = 30;
        public int maxHp = 30;
        public int attack = 5;
        public int defense = 2;
        public int speed = 6;
        public int attackRange = 1;
        public AllyStance stance = AllyStance.Balanced;
    }

    /// <summary>How an AI ally picks its action. GDD 29.3.</summary>
    public enum AllyStance { Aggressive, Defensive, Support, Balanced }

    /// <summary>
    /// Reference numbers for authoring nemeses, one row per star rating.
    /// Anchored on anomaly output at each rarity cap so a fight lasts roughly
    /// five to eight hits: normal caps near 8 attack, uneasy 60, glitch 65,
    /// absurd 168, lethal 336, void 1050. Damage is attack - defense/2, and a
    /// squad multiplies total HP by about 2.2, so these are per-leader values.
    /// Content should track this table; combat never reads it.
    /// </summary>
    public static class EnemyTuning
    {
        public struct Row { public int hp, attack, defense; }

        private static readonly Row[] byStar =
        {
            new Row { hp = 40,   attack = 6,  defense = 4  },   // 1
            new Row { hp = 120,  attack = 14, defense = 14 },   // 2
            new Row { hp = 220,  attack = 22, defense = 22 },   // 3
            new Row { hp = 600,  attack = 45, defense = 45 },   // 4
            new Row { hp = 1600, attack = 90, defense = 80 }    // 5
        };

        public static Row For(int stars) =>
            byStar[Mathf.Clamp(stars, 1, byStar.Length) - 1];
    }

    /// <summary>
    /// Builds a battle from content data: sizes the grid, spawns the enemy squad,
    /// places both sides, and rolls weather. Kept apart from CombatSystem so the
    /// whole setup can be exercised in batchmode without a scene.
    /// </summary>
    public static class BattleSetup
    {
        /// <summary>Squad size floor. A lone enemy on a grid has no tactics in it.</summary>
        public const int MinEnemies = 3;

        /// <summary>Ceiling, so the initiative list stays readable.</summary>
        public const int MaxEnemies = 7;

        /// <summary>Player plus at most two allies, per GDD 29.3.</summary>
        public const int MaxPartyMembers = 2;

        /// <summary>
        /// Grid size from star rating: 3 stars and up is a real nemesis and gets
        /// the larger board; everything else fights on the tighter one so trash
        /// encounters cannot eat the session's time budget.
        /// </summary>
        public static int GridSizeFor(Data.EnemyData enemy)
        {
            if (enemy == null) return BattleGrid.TrashSize;
            if (enemy.gridSize > 0) return enemy.gridSize;
            return enemy.stars >= 3 ? BattleGrid.NemesisSize : BattleGrid.TrashSize;
        }

        public static BattleState Build(Data.EnemyData enemy,
                                        Player player,
                                        EffectiveStats playerStats,
                                        List<PartyMember> party = null,
                                        WeatherBand? forcedWeather = null)
        {
            int size = GridSizeFor(enemy);
            var grid = new BattleGrid(size);

            bool isNemesis = size >= BattleGrid.NemesisSize;
            var condition = VictoryCondition.FromData(
                enemy?.winCondition, enemy?.winThreshold, isNemesis);

            var state = new BattleState(grid, condition,
                forcedWeather ?? BattleWeather.Roll());

            state.player = new BattleUnit
            {
                id = "player",
                displayName = string.IsNullOrEmpty(player?.name) ? "玩家" : player.name,
                side = BattleSide.Player,
                hp = player?.hp ?? 50,
                maxHp = player?.maxHp ?? 50,
                attack = playerStats.Attack,
                defense = playerStats.Defense,
                speed = playerStats.Speed,
                attackRange = 1
            };
            grid.Place(state.player, new GridPos(0, size / 2));

            PlaceAllies(grid, party, size);
            PlaceEnemies(grid, enemy, size);

            return state;
        }

        private static void PlaceAllies(BattleGrid grid, List<PartyMember> party, int size)
        {
            if (party == null) return;
            int placed = 0;
            foreach (var m in party)
            {
                if (placed >= MaxPartyMembers) break;
                var unit = new BattleUnit
                {
                    id = m.id,
                    displayName = m.displayName,
                    side = BattleSide.Ally,
                    hp = m.hp,
                    maxHp = m.maxHp,
                    attack = m.attack,
                    defense = m.defense,
                    speed = m.speed,
                    attackRange = m.attackRange
                };
                // Column 0 next to the player, then column 1 if that edge fills up.
                var slot = FirstFreeInColumn(grid, placed < 2 ? 0 : 1, size);
                if (!slot.HasValue) break;
                grid.Place(unit, slot.Value);
                placed++;
            }
        }

        private static void PlaceEnemies(BattleGrid grid, Data.EnemyData enemy, int size)
        {
            var roster = BuildRoster(enemy);
            int column = size - 1;
            int placedInColumn = 0;

            for (int i = 0; i < roster.Count; i++)
            {
                var slot = FirstFreeInColumn(grid, column, size);
                if (!slot.HasValue)
                {
                    column--;
                    placedInColumn = 0;
                    if (column < size / 2) break;   // never spawn on the player's half
                    slot = FirstFreeInColumn(grid, column, size);
                    if (!slot.HasValue) break;
                }
                grid.Place(roster[i], slot.Value);
                placedInColumn++;
            }
        }

        /// <summary>
        /// The leader plus its squad, padded to <see cref="MinEnemies"/> with
        /// scaled-down copies of the leader and trimmed to <see cref="MaxEnemies"/>.
        /// Padding matters because no existing enemy JSON declares a squad yet.
        /// </summary>
        public static List<BattleUnit> BuildRoster(Data.EnemyData enemy)
        {
            var list = new List<BattleUnit>();
            var s = enemy?.stats;

            list.Add(new BattleUnit
            {
                id = enemy?.id ?? "enemy",
                displayName = enemy?.name ?? "敌人",
                side = BattleSide.Enemy,
                hp = s?.hp ?? 30,
                maxHp = s?.hp ?? 30,
                attack = s?.attack ?? 5,
                defense = s?.defense ?? 2,
                speed = s?.speed ?? 5,
                attackRange = 1
            });

            if (enemy?.squad != null)
            {
                foreach (var entry in enemy.squad)
                {
                    for (int i = 0; i < Mathf.Max(1, entry.count); i++)
                    {
                        if (list.Count >= MaxEnemies) break;
                        list.Add(ScaledCopy(list[0], entry.id, entry.statScale, list.Count));
                    }
                }
            }

            // Escorts run at 60% of the leader so a padded fight is not three bosses.
            while (list.Count < MinEnemies)
                list.Add(ScaledCopy(list[0], null, 0.6f, list.Count));

            if (list.Count > MaxEnemies) list.RemoveRange(MaxEnemies, list.Count - MaxEnemies);

            if (enemy != null && enemy.randomizeStats) Randomize(list, enemy.stats?.attack ?? 1);
            return list;
        }

        /// <summary>Bounds for <see cref="Data.EnemyData.randomizeStats"/>.</summary>
        public const int RandomHpMin = 20, RandomHpMax = 50;
        public const int RandomDefenseMin = 0, RandomDefenseMax = 5;
        public const int RandomSpeedMin = 3, RandomSpeedMax = 12;

        /// <summary>
        /// Reroll everything except attack. Attack is the one value a test fight
        /// wants nailed down, because it is what makes the damage numbers legible;
        /// the rest varying is the point of a dummy.
        /// </summary>
        private static void Randomize(List<BattleUnit> roster, int attack)
        {
            foreach (var u in roster)
            {
                u.maxHp = Random.Range(RandomHpMin, RandomHpMax + 1);
                u.hp = u.maxHp;
                u.defense = Random.Range(RandomDefenseMin, RandomDefenseMax + 1);
                u.speed = Random.Range(RandomSpeedMin, RandomSpeedMax + 1);
                u.attack = Mathf.Max(1, attack);
            }
        }

        private static BattleUnit ScaledCopy(BattleUnit leader, string id, float scale, int index)
        {
            if (scale <= 0f) scale = 0.6f;
            int hp = Mathf.Max(1, Mathf.RoundToInt(leader.maxHp * scale));
            return new BattleUnit
            {
                id = $"{id ?? leader.id}_{index}",
                displayName = $"{leader.displayName} 随从{index}",
                side = BattleSide.Enemy,
                hp = hp,
                maxHp = hp,
                attack = Mathf.Max(1, Mathf.RoundToInt(leader.attack * scale)),
                defense = Mathf.Max(0, Mathf.RoundToInt(leader.defense * scale)),
                speed = Mathf.Max(1, Mathf.RoundToInt(leader.speed * scale)),
                attackRange = leader.attackRange
            };
        }

        private static GridPos? FirstFreeInColumn(BattleGrid grid, int column, int size)
        {
            int mid = size / 2;
            for (int offset = 0; offset < size; offset++)
            {
                int y = mid + (offset % 2 == 0 ? offset / 2 : -(offset / 2 + 1));
                var p = new GridPos(column, y);
                if (grid.IsFree(p)) return p;
            }
            return null;
        }
    }
}
