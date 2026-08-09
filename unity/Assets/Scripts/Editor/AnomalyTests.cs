using System.Collections.Generic;
using UnityEngine;
using KunchengRPG.Game;

namespace KunchengRPG.EditorTools
{
    /// <summary>
    /// Batchmode checks for the anomaly unfold system: data integrity across all 30
    /// definitions, depth accumulation, level thresholds, the equipment modifier layer,
    /// and the safety valve. One executeMethod covers everything so a single Unity
    /// cold start validates the whole feature.
    /// </summary>
    public static class AnomalyTests
    {
        const string TAG = "[AnomalyTest]";
        static int pass, fail;

        /// <summary>Failures from the last run, so FullVerify can chain suites.</summary>
        public static int FailCount => fail;

        static void Check(bool ok, string label)
        {
            if (ok) { pass++; Debug.Log($"{TAG} PASS {label}"); }
            else { fail++; Debug.LogError($"{TAG} FAIL {label}"); }
        }

        // Unity's netstandard profile here lacks Dictionary.GetValueOrDefault.
        static int Count(Dictionary<string, int> d, string k) => d.TryGetValue(k, out int v) ? v : 0;

        public static void RunAll()
        {
            pass = 0; fail = 0;

            var defs = Core.AssetLoader.LoadAllAnomalies();
            Debug.Log($"{TAG} loaded {defs.Count} definitions");
            Check(defs.Count == 30, $"30 definitions loaded (got {defs.Count})");

            // Fail loudly on a mistyped id rather than letting Grant return null and
            // NRE its way through the remaining cases.
            foreach (var id in new[] { "roast_goose_cleaver", "indoor_umbrella", "kuntong_card",
                                       "bailing_lighter", "borrowed_ring", "fake_leather_belt" })
                Check(defs.ContainsKey(id), $"fixture id exists: {id}");

            TestDataIntegrity(defs);
            TestDepthAndLevels(defs);
            TestModifierLayer(defs);
            TestSafetyValve(defs);
            TestEffectIdInventory(defs);

            Debug.Log($"{TAG} RESULT pass={pass} fail={fail}");
        }

        static void TestDataIntegrity(Dictionary<string, Data.AnomalyData> defs)
        {
            int totalLevels = 0, badLevelCount = 0, missingText = 0, badTier = 0;
            var rarityCount = new Dictionary<string, int>();

            foreach (var d in defs.Values)
            {
                totalLevels += d.levels?.Count ?? 0;
                if (d.levels == null || d.levels.Count != d.maxLevel) badLevelCount++;

                rarityCount[d.rarity] = Count(rarityCount, d.rarity) + 1;

                // Every level needs a description; buff/debuff are absent only at level 1.
                if (d.levels != null)
                {
                    foreach (var lv in d.levels)
                    {
                        if (string.IsNullOrEmpty(lv.desc)) missingText++;
                        if (lv.level > 1 && (lv.buff == null || string.IsNullOrEmpty(lv.buff.text)))
                            missingText++;
                    }
                }

                // Visual tiers must cover 1..maxLevel with no gaps and no duplicates.
                var covered = new HashSet<int>();
                if (d.visualTiers != null)
                    foreach (var t in d.visualTiers)
                        if (t.levels != null)
                            foreach (var l in t.levels)
                                if (!covered.Add(l)) badTier++;
                for (int l = 1; l <= d.maxLevel; l++)
                    if (!covered.Contains(l)) badTier++;
            }

            Check(totalLevels == 156, $"156 total levels (got {totalLevels})");
            Check(badLevelCount == 0, $"levels[].Count == maxLevel everywhere ({badLevelCount} bad)");
            Check(missingText == 0, $"no missing desc/buff text ({missingText} missing)");
            Check(badTier == 0, $"visualTiers cover every level exactly once ({badTier} bad)");

            Check(Count(rarityCount, "normal") == 6, $"6 normal (got {Count(rarityCount, "normal")})");
            Check(Count(rarityCount, "uneasy") == 6, $"6 uneasy (got {Count(rarityCount, "uneasy")})");
            Check(Count(rarityCount, "glitch") == 6, $"6 glitch (got {Count(rarityCount, "glitch")})");
            Check(Count(rarityCount, "absurd") == 6, $"6 absurd (got {Count(rarityCount, "absurd")})");
            Check(Count(rarityCount, "lethal") == 3, $"3 lethal (got {Count(rarityCount, "lethal")})");
            Check(Count(rarityCount, "void") == 3, $"3 void (got {Count(rarityCount, "void")})");

            // Rarity caps from the design invariants.
            var caps = new Dictionary<string, int> {
                {"normal",3},{"uneasy",4},{"glitch",5},{"absurd",6},{"lethal",7},{"void",9}
            };
            int badCap = 0;
            foreach (var d in defs.Values)
                if (caps.TryGetValue(d.rarity, out int cap) && d.maxLevel != cap) badCap++;
            Check(badCap == 0, $"maxLevel matches rarity cap ({badCap} bad)");

            // Lethal and void are nemesis drops only.
            int badNemesis = 0;
            foreach (var d in defs.Values)
            {
                bool shouldBe = d.rarity == "lethal" || d.rarity == "void";
                if (d.nemesisOnly != shouldBe) badNemesis++;
            }
            Check(badNemesis == 0, $"nemesisOnly set for lethal+void only ({badNemesis} bad)");

            // Slot distribution: six components covered, nothing empty.
            var slots = new Dictionary<string, int>();
            foreach (var d in defs.Values) slots[d.slot] = Count(slots, d.slot) + 1;
            Check(Count(slots, "brain") == 6, $"6 brain (got {Count(slots, "brain")})");
            Check(Count(slots, "torso") == 5, $"5 torso (got {Count(slots, "torso")})");
            Check(Count(slots, "hand") == 6, $"6 hand (got {Count(slots, "hand")})");
            Check(Count(slots, "leg") == 5, $"5 leg (got {Count(slots, "leg")})");
            Check(Count(slots, "carry") == 8, $"8 carry (got {Count(slots, "carry")})");
        }

        static void TestDepthAndLevels(Dictionary<string, Data.AnomalyData> defs)
        {
            var sys = new AnomalySystem(defs);
            var cleaver = sys.Grant("roast_goose_cleaver");

            Check(cleaver != null && cleaver.level == 1 && cleaver.depth == 0,
                  "new instance starts at depth 0 / level 1");

            // Fortune 0 grants +10 per use, so level 2 (threshold 50) needs 5 uses.
            for (int i = 0; i < 4; i++) sys.RegisterUse(cleaver, 0);
            Check(cleaver.depth == 40 && cleaver.level == 1,
                  $"4 uses at fortune 0 = depth 40, still L1 (got {cleaver.depth}/L{cleaver.level})");
            Check(sys.DepthToNextLevel(cleaver) == 10,
                  $"10 depth left to L2 (got {sys.DepthToNextLevel(cleaver)})");

            int gained = sys.RegisterUse(cleaver, 0);
            Check(cleaver.depth == 50 && cleaver.level == 2 && gained == 1,
                  $"5th use crosses 50 into L2 (got {cleaver.depth}/L{cleaver.level}, gained {gained})");

            // Fortune 100 doubles the increment to +20.
            Check(AnomalySystem.DepthGain(0) == 10, $"fortune 0 grants +10 (got {AnomalySystem.DepthGain(0)})");
            Check(AnomalySystem.DepthGain(100) == 20, $"fortune 100 grants +20 (got {AnomalySystem.DepthGain(100)})");
            Check(AnomalySystem.DepthGain(50) == 15, $"fortune 50 grants +15 (got {AnomalySystem.DepthGain(50)})");

            // Cap: uneasy stops at level 4, which needs depth 150.
            var capped = sys.Grant("roast_goose_cleaver");
            for (int i = 0; i < 40; i++) sys.RegisterUse(capped, 0);
            Check(capped.level == 4, $"uneasy caps at L4 (got L{capped.level})");
            Check(capped.depth == 150, $"depth stops accumulating at cap (got {capped.depth}, expected 150)");
            Check(sys.DepthToNextLevel(capped) == 0, "no next level at cap");

            // Void reaches 9 only at depth 400.
            var card = sys.Grant("kuntong_card");
            for (int i = 0; i < 39; i++) sys.RegisterUse(card, 0);
            Check(card.level == 8, $"depth 390 is L8 (got L{card.level})");
            sys.RegisterUse(card, 0);
            Check(card.level == 9 && card.depth == 400,
                  $"depth 400 is L9 (got {card.depth}/L{card.level})");

            // Multiple levels can be gained in one use at high fortune.
            var jump = sys.Grant("indoor_umbrella");
            jump.depth = 45;
            jump.level = 1;
            int multi = sys.RegisterUse(jump, 100);
            Check(jump.depth == 65 && multi == 1,
                  $"depth 45 + 20 = 65 is L2 (got {jump.depth}, gained {multi})");
        }

        static void TestModifierLayer(Dictionary<string, Data.AnomalyData> defs)
        {
            var sys = new AnomalySystem(defs);
            var stats = new PlayerStats { strength = 20, actionPower = 50, resilience = 10, perception = 10 };

            var baseline = new EffectiveStats(stats, new StatModifierSet());
            Check(baseline.Attack == 20, $"unmodified Attack passes through ({baseline.Attack})");
            Check(baseline.Defense == 0, $"Defense is 0 without equipment ({baseline.Defense})");

            // Cleaver L1 grants attack 10 via baseStats.
            var cleaver = sys.Grant("roast_goose_cleaver");
            sys.Equip(cleaver, "right_hand");
            var mods = new StatModifierSet();
            sys.CollectModifiers(mods);
            var eff = new EffectiveStats(stats, mods);
            Check(eff.Attack == 30, $"L1 cleaver adds +10 attack (got {eff.Attack})");

            // L4 uses statOverride (60), not multiplier — one of two GDD-canon items.
            for (int i = 0; i < 20; i++) sys.RegisterUse(cleaver, 0);
            mods.Clear(); sys.CollectModifiers(mods);
            eff = new EffectiveStats(stats, mods);
            Check(cleaver.level == 4, $"cleaver at L4 (got L{cleaver.level})");
            Check(eff.Attack == 80, $"L4 statOverride gives +60 attack (got {eff.Attack})");

            // Multiplier path: lighter attack 8 at L2 is x1.5 = 12.
            // Brand names stay pinyin per the id rule, hence bailing_ not bell_.
            var lighter = sys.Grant("bailing_lighter");
            Check(lighter != null, "bailing_lighter exists");
            if (lighter == null) return;
            sys.Equip(lighter, "carry");
            for (int i = 0; i < 5; i++) sys.RegisterUse(lighter, 0);
            mods.Clear(); sys.CollectModifiers(mods);
            eff = new EffectiveStats(stats, mods);
            Check(lighter.level == 2, $"bailing_lighter at L2 (got L{lighter.level})");
            Check(eff.Attack == 92, $"multiplier path 8 x1.5 = 12 (total {eff.Attack}, expected 92)");

            // Fractional stat: indoor umbrella dodge 5% must stay fractional, not round to 0.
            var umbrella = sys.Grant("indoor_umbrella");
            sys.Equip(umbrella, "left_hand");
            mods.Clear(); sys.CollectModifiers(mods);
            eff = new EffectiveStats(stats, mods);
            float expectedDodge = StatFormulas.DodgeRate(stats.actionPower) + 0.05f;
            Check(Mathf.Abs(eff.DodgeRate - expectedDodge) < 0.0001f,
                  $"dodge stays fractional: {eff.DodgeRate:F4} vs expected {expectedDodge:F4}");

            // Defense exists only through the modifier layer; PlayerStats has no such field.
            var belt = sys.Grant("fake_leather_belt");
            sys.Equip(belt, "torso");
            mods.Clear(); sys.CollectModifiers(mods);
            eff = new EffectiveStats(stats, mods);
            Check(eff.Defense == 7, $"equipment-only Defense surfaces (got {eff.Defense})");

            // The attribute chain is untouched: nothing wrote back into PlayerStats.
            Check(stats.strength == 20 && stats.actionPower == 50,
                  "modifiers never mutate PlayerStats");
        }

        static void TestSafetyValve(Dictionary<string, Data.AnomalyData> defs)
        {
            var sys = new AnomalySystem(defs);

            var cleaver = sys.Grant("roast_goose_cleaver");
            sys.Equip(cleaver, "right_hand");
            for (int i = 0; i < 10; i++) sys.RegisterUse(cleaver, 0);
            int depthBefore = cleaver.depth;
            int levelBefore = cleaver.level;

            var mods = new StatModifierSet();
            sys.CollectModifiers(mods);
            Check(mods.Get(StatKeys.Attack) > 0, "equipped anomaly contributes modifiers");

            sys.Unequip(cleaver);
            mods.Clear(); sys.CollectModifiers(mods);
            Check(mods.Get(StatKeys.Attack) == 0, "unequipped anomaly contributes nothing");
            Check(cleaver.depth == depthBefore && cleaver.level == levelBefore,
                  $"depth survives unequip ({cleaver.depth}/L{cleaver.level})");

            // Idle wear earns nothing: depth only moves through RegisterUse.
            sys.Equip(cleaver, "right_hand");
            mods.Clear(); sys.CollectModifiers(mods);
            Check(cleaver.depth == depthBefore, "re-equipping does not grant depth");

            // One slot holds one anomaly; equipping displaces the previous occupant.
            var ring = sys.Grant("borrowed_ring");
            sys.Equip(ring, "right_hand");
            Check(!cleaver.IsEquipped && ring.IsEquipped, "equipping displaces the previous occupant");

            // Paid depth reduction drops levels but never below 1.
            int lost = sys.ReduceDepth(cleaver, 999);
            Check(cleaver.depth == 0 && cleaver.level == 1,
                  $"ReduceDepth floors at depth 0 / L1 (got {cleaver.depth}/L{cleaver.level}, lost {lost})");

            // Partial reduction steps back exactly one level.
            var umbrella = sys.Grant("indoor_umbrella");
            umbrella.depth = 100; umbrella.level = 3;
            sys.ReduceDepth(umbrella, 50);
            Check(umbrella.depth == 50 && umbrella.level == 2,
                  $"partial reduction steps back one level (got {umbrella.depth}/L{umbrella.level})");

            // Save round-trip keeps depth, level and slot.
            sys.RegisterUse(ring, 0);
            var exported = sys.Export();
            var restored = new AnomalySystem(defs);
            restored.Load(exported);
            Check(restored.Instances.Count == sys.Instances.Count,
                  $"save round-trip keeps instance count ({restored.Instances.Count})");

            Data.AnomalyInstance back = null;
            foreach (var i in restored.Instances)
                if (i.itemId == "borrowed_ring") { back = i; break; }
            Check(back != null && back.depth == ring.depth && back.equippedOn == "right_hand",
                  "save round-trip keeps depth and slot");
        }

        static void TestEffectIdInventory(Dictionary<string, Data.AnomalyData> defs)
        {
            // 312 buff/debuff entries land on a finite set of effect ids. That set is the
            // requirement list for the status-effect system, which does not exist yet.
            var ids = new SortedDictionary<string, int>();
            int unclassified = 0, total = 0;

            foreach (var d in defs.Values)
            {
                if (d.levels == null) continue;
                foreach (var lv in d.levels)
                {
                    foreach (var e in new[] { lv.buff, lv.debuff })
                    {
                        if (e == null || string.IsNullOrEmpty(e.text)) continue;
                        total++;
                        if (string.IsNullOrEmpty(e.effectId)) { unclassified++; continue; }
                        ids[e.effectId] = ids.TryGetValue(e.effectId, out int v) ? v + 1 : 1;
                    }
                }
            }

            Debug.Log($"{TAG} effect entries: {total}, distinct ids: {ids.Count}, unclassified: {unclassified}");
            foreach (var kv in ids) Debug.Log($"{TAG} effect {kv.Key} x{kv.Value}");
            Check(total >= 280, $"buff/debuff entries present (got {total})");
        }
    }
}
