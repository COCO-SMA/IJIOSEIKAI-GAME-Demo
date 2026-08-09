using System;
using System.Collections.Generic;

namespace KunchengRPG.Data
{
    /// <summary>
    /// Static definition of one anomaly item, loaded from Resources/Data/anomalies/*.json.
    /// One file per item. 30 items in v1.0.
    /// </summary>
    [Serializable]
    public class AnomalyData
    {
        public string id;
        public string name;
        public string rarity;      // normal | uneasy | glitch | absurd | lethal | void
        public string category;    // equipment | carriable
        public string slot;        // brain | torso | hand | leg | carry
        public int maxLevel;       // 3/4/5/6/7/9, bound to rarity
        public bool inheritable;
        public string hook;
        public string statKey;     // null for effect-driven items (kuntong_card)
        public Dictionary<string, float> baseStats;
        public string canonSource; // non-null when text is quoted from GDD
        public bool nemesisOnly;   // lethal/void: nemesis drop only
        public List<AnomalyVisualTier> visualTiers;
        public List<AnomalyLevelData> levels;
        public AnomalySource source;

        public AnomalyLevelData LevelAt(int level)
        {
            if (levels == null) return null;
            foreach (var l in levels)
                if (l.level == level) return l;
            return null;
        }

        public AnomalyVisualTier TierForLevel(int level)
        {
            if (visualTiers == null) return null;
            foreach (var t in visualTiers)
                if (t.levels != null && t.levels.Contains(level)) return t;
            return null;
        }
    }

    [Serializable]
    public class AnomalyLevelData
    {
        public int level;
        public float multiplier;
        public Dictionary<string, float> statOverride; // null = use multiplier
        public string effectText;                      // effect-driven items only
        public AnomalyEffect buff;
        public AnomalyEffect debuff;
        public string desc;
    }

    [Serializable]
    public class AnomalyEffect
    {
        public string effectId; // null until the status-effect system defines it
        public float value;
        public string text;     // authored copy, always present
    }

    [Serializable]
    public class AnomalyVisualTier
    {
        public int tier;
        public List<int> levels;
        public string sprite;
        public ProceduralVisual procedural; // non-null = generated from tier 0 sprite
    }

    [Serializable]
    public class ProceduralVisual
    {
        public string tint;
        public string outline;
        public float? shake;
    }

    [Serializable]
    public class AnomalySource
    {
        public List<string> events;
        public List<string> nemeses;
        public List<string> quests;
    }

    /// <summary>
    /// One owned copy of an anomaly item. Depth belongs to the instance, not the type —
    /// two roast goose cleavers carry separate depth. Persisted in SaveData.
    /// </summary>
    [Serializable]
    public class AnomalyInstance
    {
        public string instanceId;
        public string itemId;
        public int depth;          // permanent, never reset between combats
        public int level;          // derived from depth, cached
        public string equippedOn;  // BodyComponent.partName, null = stored/carried

        public bool IsEquipped => !string.IsNullOrEmpty(equippedOn);
    }
}
