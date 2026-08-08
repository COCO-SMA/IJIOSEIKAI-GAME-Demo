using System;
using System.Collections.Generic;
using UnityEngine;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Builds the package a dying character hands to their successor, and applies it
    /// to the next one. Death is not a game over here — it is the unit of progression.
    /// </summary>
    public class InheritanceSystem
    {
        public int maxSkillRecipes = 4;
        public int maxEquipment = 6;
        public float equipmentDurabilityMultiplier = 0.7f;
        public float moneyEstateTax = 0.10f;

        private const float BirthLotteryCurseChance = 0.06f;

        private static readonly string[] DeathCauses =
        {
            "died peacefully in sleep, unfortunately",
            "lost a chess game to something very old in Yeping. won the next one. then died.",
            "ate something from a Yanao container that was probably not food. it was fine. mostly.",
            "argued with a parking ticket until natural death. the ticket outlived them.",
            "fell asleep on the last train. the one not on the schedule. did not wake up at a station.",
            "the commute card expired. so did they. the timing was suspicious.",
            "tried to cross the road during rush hour. the road won."
        };

        private static readonly string[] Epilogues =
        {
            "they won. we are not sure what they won.",
            "the commute card still had money on it. and grievances.",
            "the tea restaurant boss still remembers the unpaid bill.",
            "they left behind a receipt from a convenience store. it was 30 years old.",
            "nobody knows what was in the left pocket. probably nothing useful.",
            "the resonance shards hummed for three days after. then stopped. then started again."
        };

        private struct PropertyOutcome
        {
            public string type;
            public float chance;
            public string desc;

            public PropertyOutcome(string type, float chance, string desc)
            {
                this.type = type; this.chance = chance; this.desc = desc;
            }
        }

        private static readonly PropertyOutcome[] PropertyOutcomes =
        {
            new PropertyOutcome("normal",     0.50f, "inherited smoothly"),
            new PropertyOutcome("demolition", 0.15f, "demolished! sudden windfall"),
            new PropertyOutcome("unfinished", 0.10f, "an unfinished building. a cross-generation easter egg."),
            new PropertyOutcome("dispute",    0.10f, "inheritance dispute. lawyers involved."),
            new PropertyOutcome("mortgage",   0.10f, "inherited along with its mortgage."),
            new PropertyOutcome("fire",       0.05f, "a fire. the insurance had expired. of course.")
        };

        /// <summary>
        /// Snapshot everything the deceased passes on. Appends the death to the family log.
        /// </summary>
        public Data.Inheritance CreateInheritance(
            Player player, List<Data.FamilyLogEntry> familyLog, CitySystem citySystem)
        {
            if (player == null) return null;

            var logEntry = CreateLogEntry(player);
            var result = new Data.Inheritance
            {
                skillRecipes = Trim(ExtractSkillRecipes(player), maxSkillRecipes),
                equipment = Trim(SelectEquipment(player), maxEquipment),
                money = CalculateMoney(player),
                property = RollProperty(player),
                familyRep = CalculateRep(player),
                districtKnowledge = ExtractDistrictKnowledge(player),
                resonanceShards = GetFlag(player, "resonanceShards"),
                birthLotteryStatus = DetermineBirthLottery(player, citySystem),
                logEntry = logEntry
            };

            familyLog?.Add(logEntry);
            return result;
        }

        private static List<T> Trim<T>(List<T> source, int max)
        {
            if (source.Count > max) source.RemoveRange(max, source.Count - max);
            return source;
        }

        private static int GetFlag(Player player, string key)
        {
            if (player.flags == null) return 0;
            return player.flags.TryGetValue(key, out int v) ? v : 0;
        }

        private List<Data.SkillRecipe> ExtractSkillRecipes(Player player)
        {
            var recipes = new List<Data.SkillRecipe>();
            if (player.knownRecipes == null) return recipes;

            foreach (var r in player.knownRecipes)
            {
                if (r == null) continue;
                recipes.Add(new Data.SkillRecipe
                {
                    id = r.id,
                    name = r.name,
                    difficultyMultiplier = 0.5f,
                    inherited = true
                });
            }
            return recipes;
        }

        /// <summary>
        /// Equipment worn on the six body components carries over at reduced durability.
        /// </summary>
        private List<Data.InheritedEquipment> SelectEquipment(Player player)
        {
            var equipped = new List<Data.InheritedEquipment>();
            if (player.bodyComponents == null) return equipped;

            foreach (var comp in player.bodyComponents)
            {
                if (comp == null || !comp.HasEquipment) continue;

                int baseDurability = comp.equipmentDurability > 0 ? comp.equipmentDurability : 100;
                equipped.Add(new Data.InheritedEquipment
                {
                    id = comp.equipmentId,
                    name = comp.equipmentId,
                    durability = Mathf.FloorToInt(baseDurability * equipmentDurabilityMultiplier)
                });
            }
            return equipped;
        }

        private int CalculateMoney(Player player)
        {
            return Mathf.FloorToInt(Mathf.Max(0, player.money) * (1f - moneyEstateTax));
        }

        /// <summary>
        /// Weighted roll over the property outcome table. Returns null when the
        /// deceased owned nothing.
        /// </summary>
        private Data.PropertyInheritance RollProperty(Player player)
        {
            if (GetFlag(player, "hasProperty") <= 0) return null;

            string district = !string.IsNullOrEmpty(player.propertyDistrict)
                ? player.propertyDistrict
                : player.districtId;

            float roll = UnityEngine.Random.value;
            float cumulative = 0f;

            foreach (var outcome in PropertyOutcomes)
            {
                cumulative += outcome.chance;
                if (roll < cumulative)
                    return new Data.PropertyInheritance
                    {
                        type = outcome.type, desc = outcome.desc, district = district
                    };
            }

            var fallback = PropertyOutcomes[0];
            return new Data.PropertyInheritance
            {
                type = fallback.type, desc = fallback.desc, district = district
            };
        }

        private Dictionary<string, int> CalculateRep(Player player)
        {
            var rep = new Dictionary<string, int>();
            if (!string.IsNullOrEmpty(player.districtId))
                rep[player.districtId] = 10;
            return rep;
        }

        private Dictionary<string, List<string>> ExtractDistrictKnowledge(Player player)
        {
            var knowledge = new Dictionary<string, List<string>>();
            if (player.discoveredPOIs == null) return knowledge;

            foreach (var poiId in player.discoveredPOIs)
            {
                string dist = player.districtId ?? "unknown";
                if (!knowledge.TryGetValue(dist, out var list))
                {
                    list = new List<string>();
                    knowledge[dist] = list;
                }
                if (!list.Contains(poiId)) list.Add(poiId);
            }
            return knowledge;
        }

        /// <summary>
        /// Native status is inherited but not guaranteed: a small chance the family
        /// leaves Kuncheng and the heir restarts as a drifter. A drifter lineage can
        /// convert to native by putting down roots first.
        /// </summary>
        private Data.BirthLotteryStatus DetermineBirthLottery(Player player, CitySystem citySystem)
        {
            bool wasNative = player.birthLottery == "native";

            if (wasNative)
            {
                if (UnityEngine.Random.value < BirthLotteryCurseChance)
                    return new Data.BirthLotteryStatus
                    {
                        status = "drifter",
                        cursed = true,
                        curseDesc = "your parents went back to their hometown. you arrive in Kuncheng from zero."
                    };

                return new Data.BirthLotteryStatus { status = "native" };
            }

            if (citySystem != null && citySystem.IsRooted())
                return new Data.BirthLotteryStatus { status = "native", newlyNative = true };

            return new Data.BirthLotteryStatus { status = "drifter" };
        }

        private Data.FamilyLogEntry CreateLogEntry(Player player)
        {
            return new Data.FamilyLogEntry
            {
                generation = player.generation,
                name = player.name,
                age = player.age,
                district = player.districtId,
                origin = player.originId,
                causeOfDeath = Pick(DeathCauses),
                title = GenerateTitle(player),
                timestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        private static string Pick(string[] pool)
        {
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        private string GenerateTitle(Player player)
        {
            string epilogue = Pick(Epilogues);
            switch (UnityEngine.Random.Range(0, 3))
            {
                case 0: return $"Generation {player.generation}, {player.name}. {epilogue}";
                case 1: return $"Generation {player.generation}. {player.name} from {player.districtId}. {epilogue}";
                default: return $"{player.generation}th generation. {player.name}. {epilogue}";
            }
        }

        /// <summary>
        /// Stamp an inheritance onto a freshly created heir. Called after the new
        /// character's base stats are rolled, so inherited money and items stack on top.
        /// </summary>
        public void ApplyInheritance(Player heir, Data.Inheritance inheritance)
        {
            if (heir == null || inheritance == null) return;

            if (heir.flags == null) heir.flags = new Dictionary<string, int>();

            if (inheritance.skillRecipes != null)
            {
                heir.knownRecipes = new List<Data.SkillRecipe>();
                foreach (var r in inheritance.skillRecipes)
                {
                    heir.knownRecipes.Add(new Data.SkillRecipe
                    {
                        id = r.id,
                        name = r.name,
                        // Halved again relative to what the parent already halved:
                        // a recipe practised for generations gets progressively cheaper.
                        difficultyMultiplier = r.difficultyMultiplier * 0.5f,
                        inherited = true
                    });
                }
            }

            if (inheritance.equipment != null)
                foreach (var item in inheritance.equipment)
                    if (item != null && !string.IsNullOrEmpty(item.id))
                        heir.inventory.Add(item.id);

            heir.money += inheritance.money;
            heir.resonanceShards += inheritance.resonanceShards;

            if (inheritance.familyRep != null)
                heir.familyRep = new Dictionary<string, int>(inheritance.familyRep);

            if (inheritance.birthLotteryStatus != null)
            {
                heir.birthLottery = inheritance.birthLotteryStatus.status;
                if (inheritance.birthLotteryStatus.cursed) heir.flags["returnCurse"] = 1;
                if (inheritance.birthLotteryStatus.newlyNative) heir.flags["newlyNative"] = 1;
            }

            ApplyProperty(heir, inheritance.property);

            Debug.Log($"[InheritanceSystem] Applied to gen {heir.generation}: " +
                      $"+${inheritance.money}, {inheritance.equipment?.Count ?? 0} items, " +
                      $"{inheritance.skillRecipes?.Count ?? 0} recipes, " +
                      $"lottery={heir.birthLottery}, property={inheritance.property?.type ?? "none"}");
        }

        /// <summary>
        /// Property outcomes diverge: some hand over a deed, one pays out cash,
        /// and dispute/fire hand over nothing but the story.
        /// </summary>
        private void ApplyProperty(Player heir, Data.PropertyInheritance property)
        {
            if (property == null) return;

            switch (property.type)
            {
                case "normal":
                case "mortgage":
                    heir.flags["hasProperty"] = 1;
                    heir.propertyDistrict = property.district;
                    if (property.type == "mortgage") heir.flags["mortgaged"] = 1;
                    break;

                case "demolition":
                    heir.money += 5000;
                    break;

                case "unfinished":
                    heir.flags["unfinishedProperty"] = 1;
                    heir.propertyDistrict = property.district;
                    break;

                // dispute / fire: nothing carries over.
            }
        }

        /// <summary>
        /// Age every remembered NPC by one generation gap and nudge attitudes by how
        /// well they knew the family. Run once per inheritance, before the heir plays.
        /// </summary>
        public void ApplyCrossGenNpcMemory(CitySystem citySystem, int generation)
        {
            if (citySystem == null) return;

            var ids = new List<string>(citySystem.npcMemories.Keys);
            foreach (var npcId in ids)
            {
                citySystem.AgeNpc(npcId, generation);

                var mem = citySystem.GetNpcMemory(npcId);
                if (!mem.deceased)
                    mem.attitudeShift += Mathf.FloorToInt(mem.familiarity * 0.1f);
            }
        }
    }
}
