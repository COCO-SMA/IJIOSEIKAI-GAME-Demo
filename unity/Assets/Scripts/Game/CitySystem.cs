using System;
using System.Collections.Generic;
using UnityEngine;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Per-NPC cross-generation memory. Survives death and is carried in the save file.
    /// </summary>
    [Serializable]
    public class NpcMemory
    {
        public int familiarity;
        public List<int> generationMet = new List<int>();
        public List<NpcMemoryEvent> events = new List<NpcMemoryEvent>();
        public int? lastInteraction;
        public bool deceased;
        public string successor;
        public int attitudeShift;
    }

    [Serializable]
    public class NpcMemoryEvent
    {
        public int generation;
        public string kind;
        public string detail;
    }

    /// <summary>
    /// Named affinity band. Drives NPC greeting tone.
    /// </summary>
    public class AffinityLevel
    {
        public int min;
        public string name;
        public string greeting;

        public AffinityLevel(int min, string name, string greeting)
        {
            this.min = min;
            this.name = name;
            this.greeting = greeting;
        }
    }

    /// <summary>
    /// How the city remembers the family: per-district affinity plus per-NPC memory.
    /// Both decay between generations rather than resetting, so a lineage accumulates
    /// standing in Kuncheng across playthroughs of a single save.
    /// </summary>
    public class CitySystem
    {
        public static readonly string[] DistrictIds =
        {
            "jinyong", "yundun", "jiuxu", "tiewei", "chagang",
            "yanao", "zhongling", "yeping", "hetang", "chaowan", "bianpu"
        };

        // Ordered low to high; lookups walk the list and keep the last match.
        private static readonly AffinityLevel[] Levels =
        {
            new AffinityLevel(0,  "stranger",     "who are you"),
            new AffinityLevel(10, "acquaintance", "oh, you again"),
            new AffinityLevel(30, "regular",      "you are the X family kid, right?"),
            new AffinityLevel(50, "insider",      "back again? your dad still owes me money"),
            new AffinityLevel(70, "local_boss",   "sit down, you know where")
        };

        public const int DecayNotLiving = -2;
        public const int DecayNegativeEvent = -10;
        public const int DecaySevereNegative = -20;
        public const float CrossGenMultiplier = 0.7f;

        public const int GainLiving = 3;
        public const int GainQuest = 10;
        public const int GainNemesis = 18;
        public const int GainEvent = 3;
        public const int GainNpc = 6;
        public const int GainConsume = 1;

        public Dictionary<string, int> affinity = new Dictionary<string, int>();
        public Dictionary<string, NpcMemory> npcMemories = new Dictionary<string, NpcMemory>();
        public bool rooted;

        public void InitFromSave(Data.SaveData save)
        {
            if (save == null) return;
            affinity = save.cityAffinity ?? new Dictionary<string, int>();
            npcMemories = save.npcMemories ?? new Dictionary<string, NpcMemory>();
            rooted = save.rooted;
        }

        public void WriteToSave(Data.SaveData save)
        {
            if (save == null) return;
            save.cityAffinity = affinity;
            save.npcMemories = npcMemories;
            save.rooted = rooted;
        }

        // === District affinity ===

        public int GetDistrictAffinity(string districtId)
        {
            if (string.IsNullOrEmpty(districtId)) return 0;
            return affinity.TryGetValue(districtId, out int v) ? v : 0;
        }

        /// <summary>
        /// Clamped to [0, 100]. The JS original clamped only the upper bound, which let
        /// repeated negative events drive affinity arbitrarily negative and made the
        /// "stranger" band unreachable again; the floor keeps bands meaningful.
        /// </summary>
        public void AddDistrictAffinity(string districtId, int amount)
        {
            if (string.IsNullOrEmpty(districtId)) return;
            int next = Mathf.Clamp(GetDistrictAffinity(districtId) + amount, 0, 100);
            affinity[districtId] = next;
        }

        public int GetTotalAffinity()
        {
            int total = 0;
            foreach (var d in DistrictIds)
                total += GetDistrictAffinity(d);
            return Mathf.RoundToInt(total / (float)DistrictIds.Length);
        }

        public string GetAffinityLevel(string districtId)
        {
            return GetAffinityLevelInfo(districtId).name;
        }

        public AffinityLevel GetAffinityLevelInfo(string districtId)
        {
            return BandFor(GetDistrictAffinity(districtId));
        }

        private static AffinityLevel BandFor(int value)
        {
            AffinityLevel level = Levels[0];
            foreach (var lv in Levels)
                if (value >= lv.min) level = lv;
            return level;
        }

        /// <summary>
        /// Local-boss ending gate: rooted, decent average standing, and real depth
        /// in at least five districts.
        /// </summary>
        public bool CheckLocalBossTrigger()
        {
            return CheckLocalBossTrigger(DistrictIds);
        }

        /// <summary>
        /// Same gate, scoped to the districts a build actually ships. Averaging over all
        /// eleven ids caps a two-district build at 18 average affinity, which puts the
        /// 70 threshold out of reach and makes the victory ending unreachable. Builds
        /// with fewer than five districts instead require depth in every district
        /// present, which is the v1.0 rule (both districts >= 70 plus rooted).
        /// </summary>
        public bool CheckLocalBossTrigger(IEnumerable<string> availableDistricts)
        {
            if (!rooted) return false;

            var districts = new List<string>();
            if (availableDistricts != null)
                foreach (var d in availableDistricts)
                    if (!string.IsNullOrEmpty(d)) districts.Add(d);
            if (districts.Count == 0) return false;

            if (districts.Count < 5)
            {
                foreach (var d in districts)
                    if (GetDistrictAffinity(d) < 70) return false;
                return true;
            }

            int total = 0;
            foreach (var d in districts)
                total += GetDistrictAffinity(d);
            if (Mathf.RoundToInt(total / (float)districts.Count) < 70) return false;

            int deep = 0;
            foreach (var d in districts)
                if (GetDistrictAffinity(d) >= 60) deep++;
            return deep >= 5;
        }

        public void ApplyYearlyDecay(string livingDistrict)
        {
            foreach (var d in DistrictIds)
                if (d != livingDistrict)
                    AddDistrictAffinity(d, DecayNotLiving);
        }

        public void ApplyCrossGenDecay()
        {
            foreach (var d in DistrictIds)
                affinity[d] = Mathf.RoundToInt(GetDistrictAffinity(d) * CrossGenMultiplier);
        }

        public void ApplyNegativeEvent(string districtId, bool severe = false)
        {
            AddDistrictAffinity(districtId, severe ? DecaySevereNegative : DecayNegativeEvent);
        }

        public void SetRooted(bool value) { rooted = value; }
        public bool IsRooted() { return rooted; }

        // === NPC memory ===

        public NpcMemory GetNpcMemory(string npcId)
        {
            if (!npcMemories.TryGetValue(npcId, out var mem))
            {
                mem = new NpcMemory();
                npcMemories[npcId] = mem;
            }
            return mem;
        }

        /// <summary>
        /// Record an interaction. Returns the resulting familiarity band.
        /// Event history is capped at 20 per NPC to keep saves bounded.
        /// </summary>
        public AffinityLevel InteractWithNpc(string npcId, int generation, NpcMemoryEvent evt = null)
        {
            var mem = GetNpcMemory(npcId);
            mem.familiarity += GainNpc;
            mem.lastInteraction = generation;

            if (!mem.generationMet.Contains(generation))
                mem.generationMet.Add(generation);

            if (evt != null)
            {
                evt.generation = generation;
                mem.events.Add(evt);
                if (mem.events.Count > 20)
                    mem.events.RemoveRange(0, mem.events.Count - 20);
            }

            return GetNpcFamiliarityLevel(npcId);
        }

        public AffinityLevel GetNpcFamiliarityLevel(string npcId)
        {
            return BandFor(GetNpcMemory(npcId).familiarity);
        }

        public NpcDialogueModifier GetNpcDialogueModifier(string npcId, int generation)
        {
            var mem = GetNpcMemory(npcId);
            var level = GetNpcFamiliarityLevel(npcId);
            int firstGen = mem.generationMet.Count > 0 ? mem.generationMet[0] : generation;
            int generationsKnown = generation - firstGen;

            return new NpcDialogueModifier
            {
                level = level.name,
                greeting = level.greeting,
                generationsKnown = generationsKnown,
                isFamily = generationsKnown > 0,
                attitudeShift = mem.attitudeShift,
                tone = ToneFor(level.name)
            };
        }

        private static string ToneFor(string levelName)
        {
            switch (levelName)
            {
                case "acquaintance": return "neutral";
                case "regular": return "warm";
                case "insider": return "casual";
                case "local_boss": return "rude";
                default: return "cold";
            }
        }

        /// <summary>
        /// An NPC unseen for three or more generations is presumed dead.
        /// </summary>
        public void AgeNpc(string npcId, int generation)
        {
            var mem = GetNpcMemory(npcId);
            if (mem.generationMet.Count == 0) return;

            int lastGen = mem.generationMet[mem.generationMet.Count - 1];
            if (generation - lastGen >= 3)
                mem.deceased = true;
        }
    }

    /// <summary>
    /// Tone/greeting hints handed to the dialogue layer for a specific NPC.
    /// </summary>
    public class NpcDialogueModifier
    {
        public string level;
        public string greeting;
        public int generationsKnown;
        public bool isFamily;
        public int attitudeShift;
        public string tone;
    }
}
