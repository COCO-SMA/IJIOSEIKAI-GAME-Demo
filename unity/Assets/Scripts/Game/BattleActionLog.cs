using System.Collections.Generic;
using UnityEngine;

namespace KunchengRPG.Game
{
    public enum BattleActionKind
    {
        Wait, Move, Attack, UseItem, Skill, Withdraw, Displace, StatusApplied
    }

    /// <summary>
    /// One resolved action, recorded with enough state to be undone. Two anomalies
    /// (the Amended Notebook and the Correct Calendar, 12 levels between them) are
    /// built on rewriting an action that already resolved, so the log stores
    /// before-values rather than a description of what happened.
    /// </summary>
    public class BattleActionEntry
    {
        public int turn;
        public string actorId;
        public BattleActionKind kind;
        public string targetId;

        public GridPos fromPos, toPos;

        /// <summary>Did the attack connect. Retcons flip this.</summary>
        public bool hit;

        public int damage;

        /// <summary>Target HP before resolution, so an undo restores exactly.</summary>
        public int targetHpBefore;

        /// <summary>Set once this entry has been rewritten; a retcon cannot stack.</summary>
        public bool retconned;

        public string note;

        public override string ToString() =>
            $"T{turn} {actorId} {kind}" +
            (string.IsNullOrEmpty(targetId) ? "" : $" -> {targetId}") +
            (kind == BattleActionKind.Attack ? $" hit={hit} dmg={damage}" : "") +
            (retconned ? " [retconned]" : "");
    }

    /// <summary>
    /// Append-only history of a battle, with targeted rewriting on top. Kept
    /// separate from CombatSystem so the rewrite rules can be tested without
    /// running a fight, and so combat never has to know why history is retained.
    /// </summary>
    public class BattleActionLog
    {
        private readonly List<BattleActionEntry> entries = new List<BattleActionEntry>();

        /// <summary>
        /// How far back a rewrite may reach. Bounded because an unbounded rewrite
        /// window turns every past turn into a pending decision.
        /// </summary>
        public const int RetconWindow = 3;

        public IReadOnlyList<BattleActionEntry> Entries => entries;
        public int Count => entries.Count;

        public BattleActionEntry Record(BattleActionEntry entry)
        {
            entries.Add(entry);
            return entry;
        }

        public BattleActionEntry Last => entries.Count == 0 ? null : entries[entries.Count - 1];

        /// <summary>Most recent entries first, newest to oldest.</summary>
        public IEnumerable<BattleActionEntry> Recent(int count)
        {
            for (int i = entries.Count - 1; i >= 0 && entries.Count - i <= count; i--)
                yield return entries[i];
        }

        /// <summary>
        /// The most recent attack that can still be rewritten: inside the window
        /// and not already retconned.
        /// </summary>
        public BattleActionEntry LastRetconnableAttack(string actorId = null)
        {
            int examined = 0;
            for (int i = entries.Count - 1; i >= 0 && examined < RetconWindow; i--, examined++)
            {
                var e = entries[i];
                if (e.kind != BattleActionKind.Attack || e.retconned) continue;
                if (actorId != null && e.actorId != actorId) continue;
                return e;
            }
            return null;
        }

        /// <summary>
        /// Rewrite a resolved attack into a hit for <paramref name="newDamage"/>.
        /// Restores the target from <see cref="BattleActionEntry.targetHpBefore"/>
        /// first, so a miss-turned-hit and a hit-turned-harder both end up exact
        /// instead of compounding whatever already landed.
        /// </summary>
        public bool RetconToHit(BattleActionEntry entry, BattleUnit target, int newDamage)
        {
            if (entry == null || entry.retconned) return false;
            if (entry.kind != BattleActionKind.Attack) return false;
            if (target == null || target.id != entry.targetId) return false;

            target.hp = Mathf.Clamp(entry.targetHpBefore - Mathf.Max(0, newDamage),
                                    0, target.maxHp);
            entry.hit = true;
            entry.damage = Mathf.Max(0, newDamage);
            entry.retconned = true;
            entry.note = "retcon: miss rewritten as hit";
            return true;
        }

        /// <summary>
        /// Rewrite a resolved attack into a miss, undoing its damage entirely.
        /// </summary>
        public bool RetconToMiss(BattleActionEntry entry, BattleUnit target)
        {
            if (entry == null || entry.retconned) return false;
            if (entry.kind != BattleActionKind.Attack) return false;
            if (target == null || target.id != entry.targetId) return false;

            target.hp = Mathf.Clamp(entry.targetHpBefore, 0, target.maxHp);
            entry.hit = false;
            entry.damage = 0;
            entry.retconned = true;
            entry.note = "retcon: hit rewritten as miss";
            return true;
        }

        public void Clear() => entries.Clear();
    }
}
