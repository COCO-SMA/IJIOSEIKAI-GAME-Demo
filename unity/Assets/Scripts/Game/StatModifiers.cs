using System.Collections.Generic;
using UnityEngine;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Stat vocabulary shared by anomaly JSON, district effects and enemy data.
    /// Tier 2 keys feed the derivation formulas; Tier 3 keys are applied flat on top.
    /// </summary>
    public static class StatKeys
    {
        // Tier 2 — base attributes (modifiers here flow through the formulas)
        public const string Perception = "perception";
        public const string Fortune = "fortune";
        public const string Resilience = "resilience";
        public const string Strength = "strength";
        public const string ActionPower = "actionPower";
        public const string Vitality = "vitality";

        // Tier 3 — derived / combat-facing (modifiers here are flat additions)
        public const string Attack = "attack";
        public const string Defense = "defense";
        public const string Speed = "speed";
        public const string Dodge = "dodge";
        public const string HitRate = "hitRate";
    }

    /// <summary>
    /// The Tier 2 -> Tier 3 derivation formulas, in one place.
    /// PlayerStats and EffectiveStats both route through here so the chain
    /// (component efficiency -> base attribute -> derived attribute) has a single definition.
    /// </summary>
    public static class StatFormulas
    {
        public static int Attack(int strength) => strength;
        public static float CritRate(int fortune) => Mathf.Min(0.5f, fortune * 0.005f);
        public static float CritDamage(int fortune) => 1.5f + fortune * 0.01f;
        public static float DodgeRate(int actionPower) => Mathf.Min(0.4f, actionPower * 0.004f);
        public static float HitRate(int perception) => Mathf.Min(0.95f, 0.7f + perception * 0.005f);
        public static float DamageReduction(int resilience) => Mathf.Min(0.5f, resilience * 0.005f);
        public static float AnomalyTriggerRate(int fortune) => Mathf.Min(0.3f, fortune * 0.003f);
    }

    /// <summary>
    /// Accumulated flat modifiers from equipped anomalies, district effects and buffs.
    /// Deliberately additive and order-independent: nothing here mutates PlayerStats,
    /// so the attribute chain stays intact and modifiers only intervene at the end.
    /// </summary>
    public class StatModifierSet
    {
        private readonly Dictionary<string, float> flat = new Dictionary<string, float>();

        public void Clear() => flat.Clear();

        public void Add(string statKey, float value)
        {
            if (string.IsNullOrEmpty(statKey) || Mathf.Approximately(value, 0f)) return;
            flat.TryGetValue(statKey, out float cur);
            flat[statKey] = cur + value;
        }

        public void AddAll(Dictionary<string, float> stats)
        {
            if (stats == null) return;
            foreach (var kvp in stats) Add(kvp.Key, kvp.Value);
        }

        public float Get(string statKey)
        {
            if (string.IsNullOrEmpty(statKey)) return 0f;
            return flat.TryGetValue(statKey, out float v) ? v : 0f;
        }

        public int GetInt(string statKey) => Mathf.RoundToInt(Get(statKey));

        public bool IsEmpty => flat.Count == 0;

        public IEnumerable<KeyValuePair<string, float>> All => flat;
    }

    /// <summary>
    /// Read-only view of a player's stats with modifiers applied.
    /// Tier 2 modifiers are folded in before derivation; Tier 3 modifiers are added after.
    /// Build one per query — it is a snapshot, not persistent state.
    /// </summary>
    public struct EffectiveStats
    {
        private readonly PlayerStats stats;
        private readonly StatModifierSet mods;

        public EffectiveStats(PlayerStats stats, StatModifierSet mods)
        {
            this.stats = stats;
            this.mods = mods ?? new StatModifierSet();
        }

        // --- Tier 2, modifiers folded in ---
        public int Perception => Mathf.Max(0, stats.perception + mods.GetInt(StatKeys.Perception));
        public int Fortune => Mathf.Max(0, stats.fortune + mods.GetInt(StatKeys.Fortune));
        public int Resilience => Mathf.Max(0, stats.resilience + mods.GetInt(StatKeys.Resilience));
        public int Strength => Mathf.Max(0, stats.strength + mods.GetInt(StatKeys.Strength));
        public int ActionPower => Mathf.Max(0, stats.actionPower + mods.GetInt(StatKeys.ActionPower));
        public int Vitality => Mathf.Max(0, stats.vitality + mods.GetInt(StatKeys.Vitality));

        // --- Tier 3, derived from effective Tier 2 then offset flat ---
        public int Attack => Mathf.Max(0, StatFormulas.Attack(Strength) + mods.GetInt(StatKeys.Attack));
        public int Defense => Mathf.Max(0, mods.GetInt(StatKeys.Defense));
        public int Speed => Mathf.Max(0, ActionPower + mods.GetInt(StatKeys.Speed));

        public float CritRate => StatFormulas.CritRate(Fortune);
        public float CritDamage => StatFormulas.CritDamage(Fortune);
        public float DamageReduction => StatFormulas.DamageReduction(Resilience);
        public float AnomalyTriggerRate => StatFormulas.AnomalyTriggerRate(Fortune);

        public float DodgeRate =>
            Mathf.Clamp(StatFormulas.DodgeRate(ActionPower) + mods.Get(StatKeys.Dodge), 0f, 0.75f);

        public float HitRate =>
            Mathf.Clamp(StatFormulas.HitRate(Perception) + mods.Get(StatKeys.HitRate), 0.05f, 0.99f);
    }
}
