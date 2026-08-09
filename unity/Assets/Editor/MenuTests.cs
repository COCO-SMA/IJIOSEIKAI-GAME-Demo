using System.Collections.Generic;
using UnityEngine;
using KunchengRPG.UI;

namespace KunchengRPG.EditorTools
{
    /// <summary>
    /// Batchmode checks for the Tab menu: body-diagram navigation, backpack grid
    /// navigation, the slot-category mapping, and the grant-all test affordance.
    ///
    /// Navigation is the part worth testing. The body diagram is a sparse grid —
    /// row 2 has legs but no torso — so "press up from 左腿" is a real query, not
    /// an array increment, and getting it wrong is invisible until someone plays.
    /// </summary>
    public static class MenuTests
    {
        const string TAG = "[MenuTest]";
        static int pass, fail;

        /// <summary>Failures from the last run, so FullVerify can chain suites.</summary>
        public static int FailCount => fail;

        static void Check(bool ok, string label)
        {
            if (ok) { pass++; Debug.Log($"{TAG} PASS {label}"); }
            else { fail++; Debug.LogError($"{TAG} FAIL {label}"); }
        }

        static void CheckEq(string label, object actual, object expected) =>
            Check(Equals(actual, expected), $"{label} (got {actual}, want {expected})");

        public static void RunAll()
        {
            pass = fail = 0;

            TestBodyLayout();
            TestBodyNavigation();
            TestListNavigation();
            TestBagNavigation();
            TestSlotMapping();
            TestLabels();
            TestGrantAll();

            Debug.Log($"{TAG} RESULT pass={pass} fail={fail}");
        }

        // --- layout ---------------------------------------------------------

        static void TestBodyLayout()
        {
            var cells = MenuNav.BodyLayout();
            CheckEq("nine slots", cells.Length, 9);

            // Six components plus three carry positions, no duplicates, no gaps.
            var seen = new HashSet<string>();
            foreach (var c in cells) seen.Add(c.slot);
            CheckEq("all slots distinct", seen.Count, 9);

            foreach (var s in new[] { "brain", "torso", "left_hand", "right_hand",
                                      "left_leg", "right_leg", "carry_1", "carry_2", "carry_3" })
                Check(seen.Contains(s), $"layout contains {s}");

            CheckEq("brain index resolves", cells[MenuNav.IndexOfSlot(cells, "brain")].slot, "brain");
            // Unknown slot must not throw or wander off the end.
            CheckEq("unknown slot falls back to 0", MenuNav.IndexOfSlot(cells, "tail"), 0);
        }

        static string StepTo(SlotCell[] cells, string from, int dx, int dy)
        {
            int i = MenuNav.IndexOfSlot(cells, from);
            return cells[MenuNav.Step(cells, i, dx, dy)].slot;
        }

        static void TestBodyNavigation()
        {
            var cells = MenuNav.BodyLayout();

            // Across the middle row.
            CheckEq("left_hand right to torso", StepTo(cells, "left_hand", 1, 0), "torso");
            CheckEq("torso right to right_hand", StepTo(cells, "torso", 1, 0), "right_hand");
            CheckEq("right_hand left to torso", StepTo(cells, "right_hand", -1, 0), "torso");

            // The ragged part: legs sit in row 2 with nothing between them, so up
            // from a leg must find the hand in its own column.
            CheckEq("left_leg up to left_hand", StepTo(cells, "left_leg", 0, -1), "left_hand");
            CheckEq("right_leg up to right_hand", StepTo(cells, "right_leg", 0, -1), "right_hand");

            // Down from a hand reaches the leg below it.
            CheckEq("left_hand down to left_leg", StepTo(cells, "left_hand", 0, 1), "left_leg");

            // Torso has no cell directly below it, so down goes to the nearest row
            // rather than skipping to the aligned carry slot two rows on. The legs
            // are equidistant and the tie falls to the earlier one; either is fine
            // to the hand, but pin it so a layout reorder cannot change it silently.
            CheckEq("torso down to nearest row", StepTo(cells, "torso", 0, 1), "left_leg");

            CheckEq("brain down to torso", StepTo(cells, "brain", 0, 1), "torso");
            CheckEq("torso up to brain", StepTo(cells, "torso", 0, -1), "brain");

            // Edges hold rather than wrap: the body diagram is a shape, not a loop.
            CheckEq("brain up holds", StepTo(cells, "brain", 0, -1), "brain");
            CheckEq("left_hand left holds", StepTo(cells, "left_hand", -1, 0), "left_hand");
            CheckEq("carry_3 down holds", StepTo(cells, "carry_3", 0, 1), "carry_3");
            CheckEq("carry_3 right holds", StepTo(cells, "carry_3", 1, 0), "carry_3");

            // Degenerate inputs must not throw.
            CheckEq("zero direction holds", MenuNav.Step(cells, 3, 0, 0), 3);
            Check(MenuNav.Step(null, 0, 1, 0) == 0, "null cells survives");
            Check(MenuNav.Step(new SlotCell[0], 0, 1, 0) == 0, "empty cells survives");
            // Out-of-range index gets clamped rather than indexing past the array.
            Check(MenuNav.Step(cells, 99, 0, 1) >= 0, "out-of-range index clamped");
        }

        // --- lists and grids -------------------------------------------------

        static void TestListNavigation()
        {
            // Root menu is short enough that wrapping is the expected feel.
            CheckEq("list forward", MenuNav.StepList(3, 0, 1), 1);
            CheckEq("list wraps at end", MenuNav.StepList(3, 2, 1), 0);
            CheckEq("list wraps at start", MenuNav.StepList(3, 0, -1), 2);
            CheckEq("empty list stays 0", MenuNav.StepList(0, 0, 1), 0);
            CheckEq("single item list", MenuNav.StepList(1, 0, 1), 0);
        }

        static void TestBagNavigation()
        {
            const int cols = 4;

            // Full rows: plain wrapping.
            CheckEq("bag right", MenuNav.StepGrid(8, cols, 0, 1, 0), 1);
            CheckEq("bag right wraps in row", MenuNav.StepGrid(8, cols, 3, 1, 0), 0);
            CheckEq("bag left wraps in row", MenuNav.StepGrid(8, cols, 0, -1, 0), 3);
            CheckEq("bag down a row", MenuNav.StepGrid(8, cols, 0, 0, 1), 4);
            CheckEq("bag down wraps to top", MenuNav.StepGrid(8, cols, 4, 0, 1), 0);

            // Ragged last row: 30 items over 4 columns leaves a row of 2. Moving down
            // column 3 must skip the short row instead of landing on empty space.
            CheckEq("ragged row skipped going down", MenuNav.StepGrid(30, cols, 26, 0, 1), 2);
            CheckEq("ragged row reachable in column 0", MenuNav.StepGrid(30, cols, 24, 0, 1), 28);
            CheckEq("short row wraps within itself", MenuNav.StepGrid(30, cols, 28, 1, 0), 29);
            CheckEq("short row wrap back", MenuNav.StepGrid(30, cols, 29, 1, 0), 28);

            // Never returns an index that would read past the backpack.
            for (int i = 0; i < 30; i++)
                foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0),
                                          new Vector2Int(0, 1), new Vector2Int(0, -1) })
                {
                    int r = MenuNav.StepGrid(30, cols, i, d.x, d.y);
                    if (r < 0 || r >= 30) { Check(false, $"grid step in range from {i} dir {d}"); return; }
                }
            Check(true, "grid step always in range across 30 items");

            CheckEq("empty bag stays 0", MenuNav.StepGrid(0, cols, 0, 1, 0), 0);
            CheckEq("zero columns survives", MenuNav.StepGrid(8, 0, 0, 1, 0), 0);
        }

        // --- slot mapping ----------------------------------------------------

        static void TestSlotMapping()
        {
            // Data declares a category; the player picks the concrete position.
            CheckEq("hand offers two positions", MenuLabels.SlotsFor("hand").Length, 2);
            CheckEq("leg offers two positions", MenuLabels.SlotsFor("leg").Length, 2);
            CheckEq("carry offers three positions", MenuLabels.SlotsFor("carry").Length, 3);
            CheckEq("brain is single", MenuLabels.SlotsFor("brain").Length, 1);
            CheckEq("torso is single", MenuLabels.SlotsFor("torso").Length, 1);
            CheckEq("unknown category offers nothing", MenuLabels.SlotsFor("wing").Length, 0);

            Check(MenuLabels.CanEquipTo("hand", "left_hand"), "cleaver fits left hand");
            Check(MenuLabels.CanEquipTo("hand", "right_hand"), "cleaver fits right hand");
            Check(!MenuLabels.CanEquipTo("hand", "brain"), "cleaver refuses brain");
            Check(!MenuLabels.CanEquipTo("leg", "carry_1"), "shoes refuse carry slot");
            Check(MenuLabels.CanEquipTo("carry", "carry_3"), "carry fits third carry slot");
            Check(!MenuLabels.CanEquipTo(null, "brain"), "null category refuses");

            // Every position the diagram shows must be reachable by some category,
            // or a slot exists that nothing can ever fill.
            var reachable = new HashSet<string>();
            foreach (var cat in new[] { "brain", "torso", "hand", "leg", "carry" })
                foreach (var s in MenuLabels.SlotsFor(cat)) reachable.Add(s);
            foreach (var c in MenuNav.BodyLayout())
                Check(reachable.Contains(c.slot), $"slot {c.slot} is fillable");
        }

        static void TestLabels()
        {
            CheckEq("brain label", MenuLabels.SlotLabel("brain"), "大脑");
            CheckEq("left hand label", MenuLabels.SlotLabel("left_hand"), "左手");
            CheckEq("carry_2 label", MenuLabels.SlotLabel("carry_2"), "携带二");

            // Naming Bible 4.3: the scale gets more casual as it gets worse.
            CheckEq("normal rarity", MenuLabels.RarityLabel("normal"), "普通");
            CheckEq("glitch rarity", MenuLabels.RarityLabel("glitch"), "出问题了");
            CheckEq("void rarity", MenuLabels.RarityLabel("void"), "已经无所谓了");

            // Unknown values echo rather than crash or print English.
            CheckEq("unknown slot echoes", MenuLabels.SlotLabel("tail"), "tail");
            CheckEq("null slot is safe", MenuLabels.SlotLabel(null), "?");
            CheckEq("null rarity is safe", MenuLabels.RarityLabel(null), "?");
        }

        // --- grant-all affordance --------------------------------------------

        static void TestGrantAll()
        {
            var defs = Core.AssetLoader.LoadAllAnomalies();
            CheckEq("definitions available", defs.Count, 30);

            var sys = new Game.AnomalySystem(defs);

            // The test dummy hands over one of everything, including lethal and void
            // which the real game restricts to nemesis drops.
            int granted = 0;
            foreach (var kvp in defs)
                if (sys.Grant(kvp.Key) != null) granted++;
            CheckEq("grants all thirty", granted, 30);
            CheckEq("bag holds thirty", sys.Instances.Count, 30);

            // Dedup: DialogueSystem guards on itemId because Grant always mints a new
            // instance. Without that guard a second conversation doubles the bag.
            var owned = new HashSet<string>();
            foreach (var inst in sys.Instances) owned.Add(inst.itemId);
            CheckEq("thirty distinct item ids", owned.Count, 30);

            int second = 0;
            foreach (var kvp in defs)
                if (!owned.Contains(kvp.Key) && sys.Grant(kvp.Key) != null) second++;
            CheckEq("second visit grants nothing", second, 0);
            CheckEq("bag still thirty", sys.Instances.Count, 30);

            // Every granted item must name a position the diagram can actually show.
            var positions = new HashSet<string>();
            foreach (var c in MenuNav.BodyLayout()) positions.Add(c.slot);
            foreach (var inst in sys.Instances)
            {
                var def = sys.Define(inst.itemId);
                if (def == null) { Check(false, $"definition for {inst.itemId}"); continue; }
                var slots = MenuLabels.SlotsFor(def.slot);
                if (slots.Length == 0) { Check(false, $"{inst.itemId} slot '{def.slot}' maps nowhere"); continue; }
                foreach (var s in slots)
                    if (!positions.Contains(s)) Check(false, $"{inst.itemId} maps to missing position {s}");
            }
            Check(true, "every granted item maps to a real body position");

            // Equipping displaces the previous occupant rather than stacking two
            // items on one component.
            Data.AnomalyInstance a = null, b = null;
            foreach (var inst in sys.Instances)
            {
                var def = sys.Define(inst.itemId);
                if (def == null || def.slot != "hand") continue;
                if (a == null) a = inst; else { b = inst; break; }
            }
            if (a != null && b != null)
            {
                sys.Equip(a, "right_hand");
                CheckEq("first item equipped", a.equippedOn, "right_hand");
                sys.Equip(b, "right_hand");
                CheckEq("second item takes the slot", b.equippedOn, "right_hand");
                Check(a.equippedOn == null, "displaced item returns to bag");

                // Safety valve: unequip clears effects, depth survives.
                b.depth = 120;
                sys.Unequip(b);
                Check(b.equippedOn == null, "unequip clears the slot");
                CheckEq("depth survives unequip", b.depth, 120);
            }
            else Check(false, "found two hand items to test displacement");
        }
    }
}
