using System.Collections.Generic;
using UnityEngine;

namespace KunchengRPG.Game
{
    /// <summary>Integer cell coordinate on the battle grid.</summary>
    public struct GridPos
    {
        public int x, y;
        public GridPos(int x, int y) { this.x = x; this.y = y; }

        /// <summary>
        /// Chebyshev distance: diagonals cost the same as orthogonals, so move
        /// range reads as a square. Cheaper to reason about than true pathing,
        /// and the boards are small enough that the difference never shows.
        /// </summary>
        public int DistanceTo(GridPos o) =>
            Mathf.Max(Mathf.Abs(x - o.x), Mathf.Abs(y - o.y));

        public bool IsAdjacentTo(GridPos o) => DistanceTo(o) == 1;

        public static bool operator ==(GridPos a, GridPos b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(GridPos a, GridPos b) => !(a == b);
        public override bool Equals(object o) => o is GridPos p && p == this;
        public override int GetHashCode() => x * 397 ^ y;
        public override string ToString() => $"({x},{y})";
    }

    public enum BattleSide { Player, Ally, Enemy }

    public enum BattleOutcome { Ongoing, Victory, Defeat }

    /// <summary>
    /// One combatant's position and battle-local numbers. Deliberately a plain
    /// class holding resolved values: the player's own PlayerStats stays
    /// untouched, which is the attribute-chain invariant.
    /// </summary>
    public class BattleUnit
    {
        public string id;
        public string displayName;
        public BattleSide side;

        public int hp, maxHp;
        public int attack, defense, speed;

        /// <summary>Cells this unit can strike from. 1 is melee.</summary>
        public int attackRange = 1;

        public GridPos pos;

        public bool IsAlive => hp > 0;

        /// <summary>
        /// Cells per turn, derived from speed rather than being its own stat.
        /// Dividing by <see cref="MoveDivisor"/> keeps a 0-100 speed range inside
        /// a sane 1-6 cells, so raw numbers do not translate into raw mobility.
        /// </summary>
        public int MoveRange => Mathf.Max(1, speed / MoveDivisor);

        public const int MoveDivisor = 4;

        public bool IsHostileTo(BattleUnit other) =>
            other != null && (side == BattleSide.Enemy) != (other.side == BattleSide.Enemy);
    }

    /// <summary>
    /// A weather or effect patch sitting on one cell. Modifiers only, never
    /// elemental relationships, since there is no elemental countering.
    /// </summary>
    public class TerrainCell
    {
        public string id;
        public Dictionary<string, float> modifiers = new Dictionary<string, float>();

        /// <summary>Turns left; -1 lasts the whole battle.</summary>
        public int turnsRemaining = -1;

        /// <summary>
        /// Indoor Umbrella L2. Its original wording gave enemies a water
        /// weakness, which would have been elemental countering through the back
        /// door; standing in the rain grants evasion instead.
        /// </summary>
        public static TerrainCell Rain() => new TerrainCell
        {
            id = "rain",
            modifiers = { { StatKeys.Dodge, 0.15f } }
        };
    }

    /// <summary>
    /// The battlefield: occupancy, movement budgets, and terrain patches.
    /// No Unity dependency beyond Mathf, so batchmode drives it directly.
    /// </summary>
    public class BattleGrid
    {
        /// <summary>Trash encounters. Small on purpose: contact by turn one or two.</summary>
        public const int TrashSize = 6;

        /// <summary>Nemesis fights, where positioning is worth the extra turns.</summary>
        public const int NemesisSize = 8;

        public readonly int width, height;

        private readonly List<BattleUnit> units = new List<BattleUnit>();
        private readonly Dictionary<string, int> moveRemaining = new Dictionary<string, int>();
        private readonly Dictionary<GridPos, TerrainCell> terrain =
            new Dictionary<GridPos, TerrainCell>();

        public BattleGrid(int size) : this(size, size) { }

        public BattleGrid(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        public IReadOnlyList<BattleUnit> Units => units;

        public IEnumerable<BattleUnit> LivingUnits
        {
            get { foreach (var u in units) if (u.IsAlive) yield return u; }
        }

        public bool InBounds(GridPos p) =>
            p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;

        public BattleUnit UnitAt(GridPos p)
        {
            foreach (var u in units)
                if (u.IsAlive && u.pos == p) return u;
            return null;
        }

        public bool IsFree(GridPos p) => InBounds(p) && UnitAt(p) == null;

        public void Place(BattleUnit unit, GridPos p)
        {
            unit.pos = p;
            if (!units.Contains(unit)) units.Add(unit);
            moveRemaining[unit.id] = unit.MoveRange;
        }

        /// <summary>Refresh movement budgets and expire timed terrain.</summary>
        public void BeginTurn()
        {
            foreach (var u in units) moveRemaining[u.id] = u.MoveRange;

            var expired = new List<GridPos>();
            foreach (var kvp in terrain)
            {
                var cell = kvp.Value;
                if (cell.turnsRemaining < 0) continue;
                cell.turnsRemaining--;
                if (cell.turnsRemaining <= 0) expired.Add(kvp.Key);
            }
            foreach (var p in expired) terrain.Remove(p);
        }

        public int MoveRemainingOf(BattleUnit unit) =>
            moveRemaining.TryGetValue(unit.id, out var v) ? v : 0;

        /// <summary>
        /// Move if the target is in bounds, unoccupied, and within the remaining
        /// budget. Returns cells spent, 0 when refused, so callers can tell a
        /// rejected move from a free one.
        /// </summary>
        public int TryMove(BattleUnit unit, GridPos target)
        {
            if (!unit.IsAlive || !IsFree(target)) return 0;
            int cost = unit.pos.DistanceTo(target);
            if (cost <= 0 || cost > MoveRemainingOf(unit)) return 0;

            unit.pos = target;
            moveRemaining[unit.id] -= cost;
            return cost;
        }

        /// <summary>
        /// Forced relocation, ignoring the movement budget. Backs the 21 displace
        /// effects (teleports, knockbacks, swaps) which are not the unit's own
        /// movement. Lands adjacent when the target cell is taken.
        /// </summary>
        public bool Displace(BattleUnit unit, GridPos target)
        {
            if (!unit.IsAlive) return false;
            if (IsFree(target)) { unit.pos = target; return true; }

            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var p = new GridPos(target.x + dx, target.y + dy);
                    if (IsFree(p)) { unit.pos = p; return true; }
                }
            return false;
        }

        public void SetTerrain(GridPos p, TerrainCell cell)
        {
            if (!InBounds(p)) return;
            terrain[p] = cell;
        }

        /// <summary>Stamp a square patch of radius <paramref name="radius"/>.</summary>
        public void SetTerrainArea(GridPos center, int radius, TerrainCell cell)
        {
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                    SetTerrain(new GridPos(center.x + dx, center.y + dy), cell);
        }

        public TerrainCell TerrainAt(GridPos p) =>
            terrain.TryGetValue(p, out var c) ? c : null;

        public void CollectTerrainModifiers(BattleUnit unit, StatModifierSet into)
        {
            var cell = TerrainAt(unit.pos);
            if (cell == null) return;
            foreach (var kvp in cell.modifiers) into.Add(kvp.Key, kvp.Value);
        }

        public bool InAttackRange(BattleUnit attacker, BattleUnit target) =>
            attacker.pos.DistanceTo(target.pos) <= attacker.attackRange;

        /// <summary>Distance to the closest living hostile, or -1 if none remain.</summary>
        public int DistanceToNearestOpponent(BattleUnit unit)
        {
            int best = -1;
            foreach (var other in LivingUnits)
            {
                if (!unit.IsHostileTo(other)) continue;
                int d = unit.pos.DistanceTo(other.pos);
                if (best < 0 || d < best) best = d;
            }
            return best;
        }
    }
}
