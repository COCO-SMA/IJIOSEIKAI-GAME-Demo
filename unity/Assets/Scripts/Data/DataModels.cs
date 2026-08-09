using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace KunchengRPG.Data
{
    // === District / Map Data ===
    [Serializable]
    public class DistrictData
    {
        public string id;
        public string name;
        public string anomalyType;
        public string anomalyDescription;
        public int width;
        public int height;
        public int[][] tiles;
        public List<SubDistrict> subDistricts;
        public List<NPCData> npcs;
        public List<ExitData> exits;
        public List<POIData> points;
        public string music;
        public string atmosphere;
    }

    [Serializable]
    public class SubDistrict
    {
        public string id;
        public string name;
        public string type;
    }

    [Serializable]
    public class NPCData
    {
        public string id;
        public string name;
        public int x;
        public int y;
        public string dialogueId;
    }

    [Serializable]
    public class ExitData
    {
        public string type;
        public string target;
        public int x;
        public int y;
    }

    [Serializable]
    public class POIData
    {
        public string id;
        public string name;
        public int x;
        public int y;
        public string type;

        /// <summary>
        /// Set on a POI with type "enemy": which enemy to fight when the player
        /// interacts. Keeps the encounter in district data rather than in code,
        /// so placing a fight on a map is a content edit.
        /// </summary>
        public string enemyId;
    }

    // === Origin Data ===
    [Serializable]
    public class OriginData
    {
        public string id;
        public string district;
        public string name;
        public string familyBackground;
        public SkillData initialSkill;
        public string growthPath;
        public string drama;
        public Dictionary<string, float> componentAffinity;
        public int startingMoney;
        public List<string> startingItems;
        public Dictionary<string, int> statModifiers;
        public string birthLottery;
    }

    [Serializable]
    public class SkillData
    {
        public string id;
        public string name;
        public string description;
    }

    // === Event Data ===
    [Serializable]
    public class EventData
    {
        public string id;
        public string title;
        public string description;
        public List<EventChoice> choices;
    }

    [Serializable]
    public class EventChoice
    {
        public string id;
        public string text;
        public Dictionary<string, float> consequence;
    }

    // === Enemy / Nemesis Data ===
    [Serializable]
    public class EnemyData
    {
        public string id;
        public string name;
        public string title;
        public string eventName;
        public int stars;
        public string lifeStage;
        public string winCondition;
        public Dictionary<string, object> winThreshold;
        public string description;
        public string appearance;
        public string thatMoment;
        public EnemyStats stats;
        public EnemyDrops drops;
        public ForgedEquipmentData forgedEquipment;

        /// <summary>
        /// Extra units fighting alongside this one on the grid. Null or empty means
        /// a solo fight; combat pads it out to the minimum squad size either way.
        /// </summary>
        public List<EnemySquadEntry> squad;

        /// <summary>Grid edge length. 0 = pick by star rating.</summary>
        public int gridSize;

        /// <summary>
        /// Roll hp / defense / speed per spawned unit instead of copying the
        /// authored values. Attack is left exactly as authored so a fight can pin
        /// its damage while everything else varies. Only test dummies set this —
        /// a randomized enemy is not authored content and does not sit on the
        /// <see cref="Game.EnemyTuning"/> star ladder.
        /// </summary>
        public bool randomizeStats;
    }

    /// <summary>One repeated minion in an enemy squad.</summary>
    [Serializable]
    public class EnemySquadEntry
    {
        public string id;
        public int count = 1;

        /// <summary>Fraction of the leader's stats, for minions with no own entry.</summary>
        public float statScale = 1f;
    }

    [Serializable]
    public class EnemyStats
    {
        public int hp;
        public int attack;
        public int defense;
        public int speed;
    }

    [Serializable]
    public class EnemyDrops
    {
        public string equipment;
        public string anomaly;
        public int resonanceShards;
    }

    [Serializable]
    public class ForgedEquipmentData
    {
        public string id;
        public string name;
        public string desc;
    }

    // === Item Data ===
    [Serializable]
    public class ItemData
    {
        public string id;
        public string name;
        public string type;
        public string category;
        public string desc;
        public Dictionary<string, float> effects;
        public int price;
        public bool stackable;
        public int maxStack;
    }

    // === Dialogue Data ===
    [Serializable]
    public class DialogueTree
    {
        public string id;
        public string speaker;
        public DialogueNode start;
        public Dictionary<string, DialogueNode> nodes;
    }

    [Serializable]
    public class DialogueNode
    {
        public string id;
        public string text;
        public List<DialogueChoice> choices;
    }

    [Serializable]
    public class DialogueChoice
    {
        public string id;
        public string text;
        public string next;

        /// <summary>
        /// Optional side effect fired when the choice is taken. Recognised verbs
        /// live in DialogueSystem.ApplyEffect; unknown ones are logged and ignored
        /// so a typo in content cannot break a conversation.
        /// Examples: "grant_all_anomalies", "grant:roast_goose_cleaver",
        /// "affinity:jiuxu:5", "flag:chose_celibacy:1", "money:-50".
        /// </summary>
        public string effect;
    }

    // === Tileset Data ===
    [Serializable]
    public class TilesetData
    {
        public string name;
        public string image;
        public int tileWidth;
        public int tileHeight;
        public int columns;
        public int rows;
        public List<TileInfo> tiles;
    }

    [Serializable]
    public class TileInfo
    {
        public int id;
        public string name;
        public bool walkable;
    }

    // === Ending Data ===

    /// <summary>
    /// One ending's presentation copy. Conditions live in EndingSystem, not here,
    /// so the data file stays pure text and can be rewritten without touching code.
    /// </summary>
    [Serializable]
    public class EndingData
    {
        public string id;
        public string title;
        public string titleEn;
        /// <summary>victory / neutral / failure / unfinished. Drives result-screen framing.</summary>
        public string nature;
        /// <summary>Version that first makes this ending reachable, for the unlock list.</summary>
        public string sinceVersion;
        public List<string> body = new List<string>();
        /// <summary>Meta reward line shown under the copy. Optional.</summary>
        public string metaReward;
    }
}
