using System;
using System.Collections.Generic;

namespace KunchengRPG.Data
{
    /// <summary>
    /// One entry in the family chronicle — written when a character dies.
    /// </summary>
    [Serializable]
    public class FamilyLogEntry
    {
        public int generation;
        public string name;
        public int age;
        public string district;
        public string origin;
        public string causeOfDeath;
        public string title;
        public long timestampUnix;
    }

    /// <summary>
    /// Rolled-up stand-in for family log entries trimmed during compression,
    /// so an ancient lineage still reads as long rather than starting at gen N.
    /// </summary>
    [Serializable]
    public class FamilyLogSummary
    {
        public int count;
        public List<int> generations = new List<int>();
        public List<string> preview = new List<string>();
    }

    /// <summary>
    /// Property carried across a death, with the outcome roll already applied.
    /// </summary>
    [Serializable]
    public class PropertyInheritance
    {
        public string type;
        public string desc;
        public string district;
    }

    [Serializable]
    public class BirthLotteryStatus
    {
        public string status;       // "native" | "drifter"
        public bool cursed;
        public bool newlyNative;
        public string curseDesc;
    }

    [Serializable]
    public class SkillRecipe
    {
        public string id;
        public string name;
        public float difficultyMultiplier = 1f;
        public bool inherited;
    }

    [Serializable]
    public class InheritedEquipment
    {
        public string id;
        public string name;
        public int durability;
    }

    /// <summary>
    /// The full package handed from one generation to the next.
    /// </summary>
    [Serializable]
    public class Inheritance
    {
        public List<SkillRecipe> skillRecipes = new List<SkillRecipe>();
        public List<InheritedEquipment> equipment = new List<InheritedEquipment>();
        public int money;
        public PropertyInheritance property;
        public Dictionary<string, int> familyRep = new Dictionary<string, int>();
        public Dictionary<string, List<string>> districtKnowledge = new Dictionary<string, List<string>>();
        public int resonanceShards;
        public BirthLotteryStatus birthLotteryStatus;
        public FamilyLogEntry logEntry;
    }

    /// <summary>
    /// Root save document. Version 2 matches the JS save format field-for-field so
    /// an existing browser save can be hand-migrated if needed.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 2;
        public long createdAtUnix;
        public int seed;
        public int generation = 1;

        public List<FamilyLogEntry> familyLog = new List<FamilyLogEntry>();
        public FamilyLogSummary familyLogSummary;

        /// <summary>Ending id the most recent life earned.</summary>
        public string lastEndingId;
        /// <summary>Every ending this lineage has seen, for a gallery.</summary>
        public List<string> unlockedEndings = new List<string>();

        public Dictionary<string, int> familyRep = new Dictionary<string, int>();
        public Dictionary<string, int> cityAffinity = new Dictionary<string, int>();
        public bool rooted;
        public Dictionary<string, Game.NpcMemory> npcMemories = new Dictionary<string, Game.NpcMemory>();

        public List<SkillRecipe> inheritedSkillRecipes = new List<SkillRecipe>();
        public List<InheritedEquipment> inheritedItems = new List<InheritedEquipment>();
        public List<string> inheritedKnowledge = new List<string>();
        public int resonanceShards;
        public List<ForgedEquipmentData> forgedEquipment = new List<ForgedEquipmentData>();

        /// <summary>
        /// Owned anomaly copies with their accumulated depth. Depth is permanent and
        /// per-instance, so it persists across combats, years and generations.
        /// </summary>
        public List<AnomalyInstance> anomalyInstances = new List<AnomalyInstance>();

        // Pending package applied to the next character created after a death.
        public Inheritance pendingInheritance;

        public string currentDistrictId;
        public string currentOriginId;
    }
}
