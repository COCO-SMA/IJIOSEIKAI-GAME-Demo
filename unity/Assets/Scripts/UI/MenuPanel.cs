using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace KunchengRPG.UI
{
    /// <summary>
    /// Tab menu: equipment and backpack.
    ///
    /// Every page renders as one multi-line Text with a ▶ marker on the selection
    /// rather than as a tree of widgets. That is the cheapest thing that actually
    /// works — no prefab wiring, no layout groups to fight, and the body diagram
    /// stays legible while the real equipment screen is still unbuilt.
    /// </summary>
    public class MenuPanel : MonoBehaviour
    {
        public static MenuPanel Instance { get; private set; }

        public enum Page { Root, Equip, SlotPick, Bag }

        [Header("Wired by SceneBuilder")]
        public GameObject root;
        public Text titleText;
        public Text contentText;
        public Text footerText;

        public bool IsShowing => root != null && root.activeSelf;
        public Page CurrentPage { get; private set; } = Page.Root;

        private static readonly string[] RootItems = { "装备", "背包", "关闭" };

        private int rootIndex;
        private int slotIndex;          // index into the body layout
        private int pickIndex;          // index into the SlotPick candidate list
        private int bagIndex;           // index into the backpack grid
        private const int BagColumns = 4;

        private SlotCell[] layout;
        private readonly List<Data.AnomalyInstance> candidates = new List<Data.AnomalyInstance>();
        private readonly StringBuilder sb = new StringBuilder(1024);
        private string flash = "";

        void Awake()
        {
            Instance = this;
            layout = MenuNav.BodyLayout();
            if (root != null) root.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (root == null) return;
            CurrentPage = Page.Root;
            rootIndex = 0;
            flash = "";
            root.SetActive(true);
            Render();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        void Update()
        {
            if (!IsShowing) return;
            var input = Core.InputManager.Instance;
            if (input == null) return;

            // Tab closes from anywhere; Esc steps back one page.
            if (input.MenuPressed)
            {
                input.ConsumeMenu();
                Close();
                return;
            }

            if (input.CancelPressed)
            {
                input.ConsumeCancel();
                Back();
                Render();
                return;
            }

            var d = input.DirectionPressed;
            bool confirm = input.ConfirmPressed;
            if (confirm) input.ConsumeConfirm();

            switch (CurrentPage)
            {
                case Page.Root:     UpdateRoot(d, confirm); break;
                case Page.Equip:    UpdateEquip(d, confirm); break;
                case Page.SlotPick: UpdatePick(d, confirm); break;
                case Page.Bag:      UpdateBag(d, confirm); break;
            }

            Render();
        }

        private void Back()
        {
            flash = "";
            switch (CurrentPage)
            {
                case Page.Root:     Close(); break;
                case Page.SlotPick: CurrentPage = Page.Equip; break;
                default:            CurrentPage = Page.Root; break;
            }
        }

        private void UpdateRoot(Vector2Int d, bool confirm)
        {
            if (d.y != 0)
                rootIndex = MenuNav.StepList(RootItems.Length, rootIndex, d.y > 0 ? -1 : 1);
            if (!confirm) return;

            flash = "";
            switch (rootIndex)
            {
                case 0: CurrentPage = Page.Equip; slotIndex = 0; break;
                case 1: CurrentPage = Page.Bag; bagIndex = 0; break;
                default: Close(); break;
            }
        }

        private void UpdateEquip(Vector2Int d, bool confirm)
        {
            if (d.x != 0 || d.y != 0)
                slotIndex = MenuNav.Step(layout, slotIndex, d.x, d.y);
            if (!confirm) return;

            BuildCandidates(layout[slotIndex].slot);
            pickIndex = 0;
            CurrentPage = Page.SlotPick;
            flash = "";
        }

        private void UpdatePick(Vector2Int d, bool confirm)
        {
            int count = candidates.Count + 1;   // + 卸下/返回 row
            if (d.y != 0)
                pickIndex = MenuNav.StepList(count, pickIndex, d.y > 0 ? -1 : 1);
            if (!confirm) return;

            string slot = layout[slotIndex].slot;
            var anomalies = Core.GameManager.Instance?.Anomalies;
            if (anomalies == null) return;

            if (pickIndex >= candidates.Count)
            {
                var worn = EquippedAt(slot);
                if (worn != null)
                {
                    anomalies.Unequip(worn);
                    Core.GameManager.Instance.RebuildModifiers();
                    flash = $"卸下了{NameOf(worn)}。深度留着，效果没了。";
                }
                CurrentPage = Page.Equip;
                return;
            }

            var inst = candidates[pickIndex];
            anomalies.Equip(inst, slot);
            Core.GameManager.Instance.RebuildModifiers();
            flash = $"{MenuLabels.SlotLabel(slot)} 装上了{NameOf(inst)}。";
            CurrentPage = Page.Equip;
        }

        private void UpdateBag(Vector2Int d, bool confirm)
        {
            var all = AllInstances();
            if (all.Count == 0) return;

            if (d.x != 0 || d.y != 0)
                bagIndex = MenuNav.StepGrid(all.Count, BagColumns, bagIndex, d.x, d.y > 0 ? -1 : d.y < 0 ? 1 : 0);
            if (!confirm) return;

            bagIndex = Mathf.Clamp(bagIndex, 0, all.Count - 1);
            var inst = all[bagIndex];
            var def = Core.GameManager.Instance?.Anomalies?.Define(inst.itemId);
            if (def == null) return;

            // Jump to the body diagram with the first legal slot pre-selected, so
            // the backpack is a way in rather than a dead end.
            var legal = MenuLabels.SlotsFor(def.slot);
            if (legal.Length == 0)
            {
                flash = $"{def.name} 没有能放的位置。";
                return;
            }

            slotIndex = MenuNav.IndexOfSlot(layout, legal[0]);
            BuildCandidates(legal[0]);
            pickIndex = Mathf.Max(0, candidates.IndexOf(inst));
            CurrentPage = Page.SlotPick;
            flash = "";
        }

        // --- data helpers ---------------------------------------------------

        private static List<Data.AnomalyInstance> AllInstances()
        {
            var result = new List<Data.AnomalyInstance>();
            var anomalies = Core.GameManager.Instance?.Anomalies;
            if (anomalies == null) return result;
            foreach (var inst in anomalies.Instances) result.Add(inst);
            return result;
        }

        private static Data.AnomalyInstance EquippedAt(string slot)
        {
            var anomalies = Core.GameManager.Instance?.Anomalies;
            if (anomalies == null) return null;
            foreach (var inst in anomalies.Instances)
                if (inst.equippedOn == slot) return inst;
            return null;
        }

        private static string NameOf(Data.AnomalyInstance inst)
        {
            var def = Core.GameManager.Instance?.Anomalies?.Define(inst?.itemId);
            return def?.name ?? inst?.itemId ?? "?";
        }

        /// <summary>
        /// Everything that could legally go in this slot. Excludes what is already
        /// worn there — re-equipping the same item in the same place is a no-op the
        /// player should not have to read past.
        /// </summary>
        private void BuildCandidates(string slot)
        {
            candidates.Clear();
            var anomalies = Core.GameManager.Instance?.Anomalies;
            if (anomalies == null) return;

            foreach (var inst in anomalies.Instances)
            {
                if (inst.equippedOn == slot) continue;
                var def = anomalies.Define(inst.itemId);
                if (def == null) continue;
                if (MenuLabels.CanEquipTo(def.slot, slot)) candidates.Add(inst);
            }
        }

        private string Describe(Data.AnomalyInstance inst)
        {
            var anomalies = Core.GameManager.Instance?.Anomalies;
            var def = anomalies?.Define(inst.itemId);
            if (def == null) return inst.itemId;

            string where = inst.IsEquipped
                ? $"（在{MenuLabels.SlotLabel(inst.equippedOn)}）" : "";
            return $"{def.name} L{inst.level}/{def.maxLevel} " +
                   $"[{MenuLabels.RarityLabel(def.rarity)}]{where}";
        }

        // --- rendering ------------------------------------------------------

        private void Render()
        {
            if (contentText == null) return;
            sb.Length = 0;

            switch (CurrentPage)
            {
                case Page.Root:     RenderRoot(); break;
                case Page.Equip:    RenderEquip(); break;
                case Page.SlotPick: RenderPick(); break;
                case Page.Bag:      RenderBag(); break;
            }

            if (!string.IsNullOrEmpty(flash))
                sb.Append('\n').Append("— ").Append(flash);

            contentText.text = sb.ToString();
        }

        private void RenderRoot()
        {
            if (titleText != null) titleText.text = "菜单";
            if (footerText != null) footerText.text = "↑↓ 选择　空格 确定　Tab 关闭";

            for (int i = 0; i < RootItems.Length; i++)
                sb.Append(i == rootIndex ? "▶ " : "　 ").Append(RootItems[i]).Append('\n');
        }

        private void RenderEquip()
        {
            if (titleText != null) titleText.text = "装备";
            if (footerText != null)
                footerText.text = "方向键 选格　空格 放入/卸下　Esc 返回　Tab 关闭";

            AppendBodyRow(1, "brain");
            sb.Append('\n');
            AppendBodyRow(3, "left_hand", "torso", "right_hand");
            sb.Append('\n');
            AppendBodyRow(3, "left_leg", null, "right_leg");
            sb.Append('\n');
            sb.Append("携带\n");
            AppendBodyRow(3, "carry_1", "carry_2", "carry_3");
        }

        /// <summary>
        /// One row of the body diagram: slot names on one line, their contents on
        /// the next. Two lines per row keeps item names from shoving the columns
        /// out of alignment in a proportional font.
        /// </summary>
        private void AppendBodyRow(int width, params string[] slots)
        {
            string pad = width == 1 ? "　　　　" : "";

            sb.Append(pad);
            foreach (var slot in slots)
            {
                if (slot == null) { sb.Append("　　　　　　　"); continue; }
                bool sel = layout[slotIndex].slot == slot;
                sb.Append(sel ? "▶[" : "　[").Append(MenuLabels.SlotLabel(slot))
                  .Append(sel ? "]◀" : "] ").Append("　");
            }
            sb.Append('\n').Append(pad);

            foreach (var slot in slots)
            {
                if (slot == null) { sb.Append("　　　　　　　"); continue; }
                var worn = EquippedAt(slot);
                sb.Append("　").Append(worn == null ? "（空）　　" : $"{NameOf(worn)} L{worn.level}")
                  .Append("　");
            }
            sb.Append('\n');
        }

        private void RenderPick()
        {
            string slot = layout[slotIndex].slot;
            if (titleText != null) titleText.text = $"装备 · {MenuLabels.SlotLabel(slot)}";
            if (footerText != null) footerText.text = "↑↓ 选择　空格 确定　Esc 返回";

            var worn = EquippedAt(slot);
            sb.Append("当前：").Append(worn == null ? "（空）" : Describe(worn)).Append("\n\n");

            for (int i = 0; i < candidates.Count; i++)
                sb.Append(i == pickIndex ? "▶ " : "　 ").Append(Describe(candidates[i])).Append('\n');

            sb.Append(pickIndex >= candidates.Count ? "▶ " : "　 ")
              .Append(worn == null ? "返回" : "卸下").Append('\n');

            if (candidates.Count == 0)
                sb.Append("\n背包里没有能放这里的东西。\n");
        }

        private void RenderBag()
        {
            if (titleText != null) titleText.text = "背包";
            if (footerText != null) footerText.text = "方向键 选择　空格 装备　Esc 返回";

            var all = AllInstances();
            if (all.Count == 0)
            {
                sb.Append("空的。\n\n什么都还没捡到。\n");
                return;
            }

            bagIndex = Mathf.Clamp(bagIndex, 0, all.Count - 1);
            var anomalies = Core.GameManager.Instance.Anomalies;

            for (int i = 0; i < all.Count; i++)
            {
                var def = anomalies.Define(all[i].itemId);
                string cell = def?.name ?? all[i].itemId;
                if (all[i].IsEquipped) cell = "*" + cell;

                sb.Append(i == bagIndex ? "▶[" : " [").Append(cell)
                  .Append($" L{all[i].level}").Append(i == bagIndex ? "]◀" : "] ");
                if (i % BagColumns == BagColumns - 1) sb.Append('\n');
            }
            if (all.Count % BagColumns != 0) sb.Append('\n');

            sb.Append('\n').Append(Describe(all[bagIndex])).Append('\n');
            var sel = anomalies.Define(all[bagIndex].itemId);
            if (sel != null)
            {
                int toNext = anomalies.DepthToNextLevel(all[bagIndex]);
                sb.Append($"深度 {all[bagIndex].depth}")
                  .Append(toNext > 0 ? $"，还差 {toNext} 升层" : "，已满层").Append('\n');
                if (!string.IsNullOrEmpty(sel.hook)) sb.Append(sel.hook).Append('\n');
            }
            sb.Append($"\n共 {all.Count} 件（* = 已装备）\n");
        }
    }
}
