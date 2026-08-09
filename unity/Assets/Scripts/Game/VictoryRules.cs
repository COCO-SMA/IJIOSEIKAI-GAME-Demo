using System.Collections.Generic;
using UnityEngine;

namespace KunchengRPG.Game
{
    /// <summary>
    /// How a battle is won. Closed set on purpose: rule-rewriting anomalies mean
    /// every item has to be sane under every rule, so the balance surface is
    /// (items x rules) and the second factor must stay small.
    /// The first three names match values already used in enemy JSON.
    /// </summary>
    public enum VictoryRule
    {
        /// <summary>Kill everything. The default when data says nothing.</summary>
        Annihilation,

        /// <summary>Existing data value: grind the target down past a stat threshold.</summary>
        Attrition,

        /// <summary>Existing data value: stay alive for a set number of turns.</summary>
        Survival,

        /// <summary>Existing data value: win on a resource total rather than on damage.</summary>
        Resource,

        /// <summary>Damage becomes points; highest total when the clock runs out wins.</summary>
        Score,

        /// <summary>Keep your distance for the required number of turns.</summary>
        Escape,

        /// <summary>A designated unit must still be standing at the end.</summary>
        Protect,

        /// <summary>Hold designated cells for the required number of turns.</summary>
        Control
    }

    // BattleOutcome lives in BattleGrid.cs alongside BattleSide.

    /// <summary>
    /// Rule parameters for one battle. Turn limits are opt-in: most fights have
    /// none, but Score/Escape/Survival/Control are meaningless without one, so
    /// those supply their own default when data omits it.
    /// </summary>
    public class VictoryCondition
    {
        public VictoryRule rule = VictoryRule.Annihilation;

        /// <summary>
        /// Anomalies at absurd and above may rewrite the rule; below that, no.
        /// Rewriting inverts which items matter at all, so it stays a late-game power.
        /// </summary>
        public const string MinRewriteRarity = "absurd";

        /// <summary>0 = unlimited. Rules that need a clock fill this in themselves.</summary>
        public int turnLimit;

        /// <summary>Escape: cells that must be kept between you and the nearest enemy.</summary>
        public int requiredDistance = 4;

        /// <summary>Attrition / Resource / Score: the number to reach.</summary>
        public int threshold;

        /// <summary>Attrition / Resource: which stat or resource is being watched.</summary>
        public string thresholdStat;

        /// <summary>Protect: unit that must survive.</summary>
        public string protectUnitId;

        /// <summary>Control: cells that must be held.</summary>
        public List<GridPos> controlCells = new List<GridPos>();

        public bool HasTurnLimit => turnLimit > 0;

        // Factory names match the rule names in NamingBible 4.6 one for one, so a
        // content writer and a programmer are always saying the same word.

        public static VictoryCondition Annihilation() =>
            new VictoryCondition { rule = VictoryRule.Annihilation };

        /// <summary>Score needs a clock or nothing ever decides it; 10 is the design default.</summary>
        public static VictoryCondition Score(int turns = 10) =>
            new VictoryCondition { rule = VictoryRule.Score, turnLimit = turns };

        public static VictoryCondition Escape(int turns = 5, int distance = 4) =>
            new VictoryCondition
            {
                rule = VictoryRule.Escape, turnLimit = turns, requiredDistance = distance
            };

        public static VictoryCondition Survival(int turns) =>
            new VictoryCondition { rule = VictoryRule.Survival, turnLimit = turns };

        public static VictoryCondition Protect(string unitId) =>
            new VictoryCondition { rule = VictoryRule.Protect, protectUnitId = unitId };

        public static VictoryCondition Control(List<GridPos> cells, int turns) =>
            new VictoryCondition
            {
                rule = VictoryRule.Control, controlCells = cells, turnLimit = turns
            };

        /// <summary>
        /// Build from enemy JSON. Unknown or missing rule names fall back to
        /// annihilation rather than throwing: a typo in content should not brick a fight.
        /// </summary>
        public static VictoryCondition FromData(string winCondition,
                                                Dictionary<string, object> winThreshold,
                                                bool isNemesis)
        {
            var c = new VictoryCondition { rule = ParseRule(winCondition) };

            if (winThreshold != null)
            {
                if (winThreshold.TryGetValue("stat", out var stat))
                    c.thresholdStat = stat?.ToString();
                if (winThreshold.TryGetValue("value", out var val))
                    c.threshold = ParseInt(val);
                if (winThreshold.TryGetValue("turns", out var turns))
                    c.turnLimit = ParseInt(turns);
                if (winThreshold.TryGetValue("distance", out var dist))
                    c.requiredDistance = ParseInt(dist);
                if (winThreshold.TryGetValue("protect", out var prot))
                    c.protectUnitId = prot?.ToString();
            }

            // Rules that cannot resolve without a clock get the design defaults.
            if (!c.HasTurnLimit)
            {
                switch (c.rule)
                {
                    case VictoryRule.Score:    c.turnLimit = 10; break;
                    case VictoryRule.Escape:   c.turnLimit = 5; break;
                    case VictoryRule.Survival: c.turnLimit = isNemesis ? 15 : 6; break;
                    case VictoryRule.Control:  c.turnLimit = 8; break;
                }
            }

            return c;
        }

        private static int ParseInt(object o)
        {
            if (o == null) return 0;
            if (o is int i) return i;
            if (o is long l) return (int)l;
            if (o is float f) return Mathf.RoundToInt(f);
            if (o is double d) return Mathf.RoundToInt((float)d);
            return int.TryParse(o.ToString(), out var parsed) ? parsed : 0;
        }

        public static VictoryRule ParseRule(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return VictoryRule.Annihilation;
            switch (raw.Trim().ToLowerInvariant())
            {
                case "attrition":   return VictoryRule.Attrition;
                case "survival":    return VictoryRule.Survival;
                case "resource":    return VictoryRule.Resource;
                case "score":       return VictoryRule.Score;
                case "escape":      return VictoryRule.Escape;
                case "protect":     return VictoryRule.Protect;
                case "control":     return VictoryRule.Control;
                // Both spellings accepted: "annihilation" is the bible term, but
                // "elimination" is the word content authors reach for first.
                case "annihilation":
                case "elimination": return VictoryRule.Annihilation;
                default:            return VictoryRule.Annihilation;
            }
        }

        /// <summary>Short label for the combat HUD, e.g. "积分胜利 (10回合)".</summary>
        public string DisplayName
        {
            get
            {
                string name;
                switch (rule)
                {
                    case VictoryRule.Attrition:   name = "消耗胜利"; break;
                    case VictoryRule.Survival:    name = "存活胜利"; break;
                    case VictoryRule.Resource:    name = "资源胜利"; break;
                    case VictoryRule.Score:       name = "积分胜利"; break;
                    case VictoryRule.Escape:      name = "逃脱胜利"; break;
                    case VictoryRule.Protect:     name = "护卫胜利"; break;
                    case VictoryRule.Control:     name = "占位胜利"; break;
                    default:                      name = "歼灭胜利"; break;
                }
                return HasTurnLimit ? $"{name}（{turnLimit}回合）" : name;
            }
        }
    }
}
