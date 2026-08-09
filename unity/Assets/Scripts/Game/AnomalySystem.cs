using System.Collections.Generic;
using UnityEngine;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Anomaly unfolding: depth accumulation, level derivation and stat contribution.
    ///
    /// Depth is permanent and belongs to the instance, not the item type. It is never
    /// reset between combats (this supersedes GDD 20.3 "reset to 1 after combat").
    /// Levels come from accumulated depth, not per-turn probability rolls.
    /// </summary>
    public class AnomalySystem
    {
        public const int DepthPerLevel = 50;   // level N needs 50 * (N-1)
        public const int BaseDepthGain = 10;   // before fortune scaling

        private readonly Dictionary<string, Data.AnomalyData> defs;
        private readonly List<Data.AnomalyInstance> instances = new List<Data.AnomalyInstance>();

        /// <summary>Fired when an instance crosses a level threshold: (instance, def, newLevel).</summary>
        public event System.Action<Data.AnomalyInstance, Data.AnomalyData, int> OnUnfold;

        public AnomalySystem(Dictionary<string, Data.AnomalyData> definitions)
        {
            defs = definitions ?? new Dictionary<string, Data.AnomalyData>();
        }

        public IReadOnlyList<Data.AnomalyInstance> Instances => instances;

        public Data.AnomalyData Define(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            return defs.TryGetValue(itemId, out var d) ? d : null;
        }

        // --- Instance lifecycle ---

        public Data.AnomalyInstance Grant(string itemId, string instanceId = null)
        {
            var def = Define(itemId);
            if (def == null)
            {
                Debug.LogWarning($"[AnomalySystem] unknown anomaly id: {itemId}");
                return null;
            }
            var inst = new Data.AnomalyInstance
            {
                instanceId = string.IsNullOrEmpty(instanceId)
                    ? $"{itemId}#{instances.Count + 1}"
                    : instanceId,
                itemId = itemId,
                depth = 0,
                level = 1,
                equippedOn = null
            };
            instances.Add(inst);
            return inst;
        }

        public Data.AnomalyInstance Find(string instanceId)
        {
            foreach (var i in instances)
                if (i.instanceId == instanceId) return i;
            return null;
        }

        public void Load(List<Data.AnomalyInstance> saved)
        {
            instances.Clear();
            if (saved == null) return;
            foreach (var s in saved)
            {
                s.level = LevelForDepth(s.depth, Define(s.itemId));
                instances.Add(s);
            }
        }

        public List<Data.AnomalyInstance> Export() => new List<Data.AnomalyInstance>(instances);

        /// <summary>
        /// Equip onto a body component (or "carry" for carriables). One instance per slot.
        /// </summary>
        public bool Equip(Data.AnomalyInstance inst, string partName)
        {
            if (inst == null || string.IsNullOrEmpty(partName)) return false;
            foreach (var other in instances)
                if (other != inst && other.equippedOn == partName) other.equippedOn = null;
            inst.equippedOn = partName;
            return true;
        }

        /// <summary>
        /// Safety valve: unequipping stops every effect, buff and debuff alike,
        /// while depth is retained. Without this a level-7 debuff stack is unescapable.
        /// </summary>
        public void Unequip(Data.AnomalyInstance inst)
        {
            if (inst != null) inst.equippedOn = null;
        }

        // --- Depth ---

        public static int LevelForDepth(int depth, Data.AnomalyData def)
        {
            int cap = def?.maxLevel ?? 1;
            int lv = depth / DepthPerLevel + 1;
            return Mathf.Clamp(lv, 1, Mathf.Max(1, cap));
        }

        /// <summary>Depth gained per use. Fortune raises the increment, not a probability.</summary>
        public static int DepthGain(int fortune) =>
            Mathf.Max(1, RoundHalfUp(BaseDepthGain * (1f + fortune / 100f)));

        /// <summary>
        /// Register one use of an instance. "Use" means the equipped component acted this
        /// turn, or a carriable was actively used — wearing something idle earns nothing.
        /// Returns the number of levels gained.
        /// </summary>
        public int RegisterUse(Data.AnomalyInstance inst, int fortune)
        {
            var def = Define(inst?.itemId);
            if (inst == null || def == null) return 0;

            int before = inst.level;
            if (before >= def.maxLevel) return 0; // capped by rarity, stop accumulating

            inst.depth += DepthGain(fortune);
            int after = LevelForDepth(inst.depth, def);
            if (after == before) return 0;

            inst.level = after;
            for (int lv = before + 1; lv <= after; lv++)
                OnUnfold?.Invoke(inst, def, lv);
            return after - before;
        }

        public int DepthToNextLevel(Data.AnomalyInstance inst)
        {
            var def = Define(inst?.itemId);
            if (inst == null || def == null || inst.level >= def.maxLevel) return 0;
            return Mathf.Max(0, inst.level * DepthPerLevel - inst.depth);
        }

        /// <summary>
        /// Second-stage safety valve: the paid suppression service. Removes depth and
        /// drops the level accordingly, never below level 1. Callers charge money and
        /// reputation for this — free reduction turns it into a save-scumming outlet.
        /// Returns the number of levels lost.
        /// </summary>
        public int ReduceDepth(Data.AnomalyInstance inst, int amount)
        {
            var def = Define(inst?.itemId);
            if (inst == null || def == null || amount <= 0) return 0;

            int before = inst.level;
            inst.depth = Mathf.Max(0, inst.depth - amount);
            inst.level = LevelForDepth(inst.depth, def);
            return Mathf.Max(0, before - inst.level);
        }

        // --- Stat contribution ---

        /// <summary>
        /// Collect stat modifiers from every equipped instance at its current level.
        /// Values come from statOverride when present, otherwise baseStats * multiplier.
        /// </summary>
        public void CollectModifiers(StatModifierSet into)
        {
            if (into == null) return;
            foreach (var inst in instances)
            {
                if (!inst.IsEquipped) continue;
                var def = Define(inst.itemId);
                if (def == null) continue;
                var lv = def.LevelAt(inst.level);
                if (lv == null) continue;

                if (lv.statOverride != null && lv.statOverride.Count > 0)
                {
                    into.AddAll(lv.statOverride);
                    continue;
                }
                if (def.baseStats == null) continue;
                foreach (var kvp in def.baseStats)
                    into.Add(kvp.Key, ScaleStat(kvp.Key, kvp.Value, lv.multiplier));
            }
        }

        /// <summary>Active effect copy for UI and combat log, equipped instances only.</summary>
        public List<string> ActiveEffectTexts(bool debuffs)
        {
            var result = new List<string>();
            foreach (var inst in instances)
            {
                if (!inst.IsEquipped) continue;
                var def = Define(inst.itemId);
                var lv = def?.LevelAt(inst.level);
                var fx = debuffs ? lv?.debuff : lv?.buff;
                if (fx != null && !string.IsNullOrEmpty(fx.text))
                    result.Add($"{def.name} L{inst.level}: {fx.text}");
            }
            return result;
        }

        /// <summary>
        /// Fractional stats keep full precision; integer stats round half up to match
        /// the values written in the anomaly setting document.
        /// </summary>
        private static float ScaleStat(string statKey, float baseValue, float multiplier)
        {
            float raw = baseValue * (multiplier <= 0f ? 1f : multiplier);
            bool fractional = statKey == StatKeys.Dodge || statKey == StatKeys.HitRate;
            return fractional ? raw : RoundHalfUp(raw);
        }

        private static int RoundHalfUp(float v) => Mathf.FloorToInt(v + 0.5f);
    }
}
