using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;
using KunchengRPG.Game;

namespace KunchengRPG.UI
{
    /// <summary>
    /// Grid combat screen. Builds its own canvas at runtime rather than being
    /// wired into the scene, so a fight can be dropped into any scene by adding
    /// the component — no prefab surgery, which is what the other panels need.
    /// The board is drawn as full-width CJK glyphs: they are fixed-advance in the
    /// CJK font, so the grid lines up without a monospace asset.
    /// </summary>
    public class CombatPanel : MonoBehaviour
    {
        private enum Mode { Menu, MoveTarget, AttackTarget, Dismiss }

        private static readonly string[] MenuItems =
        {
            "移动 / Move", "攻击 / Attack", "装作没事 / Act Normal",
            "等待 / Wait", "跑 / Flee"
        };

        private const int LogLines = 9;

        private Canvas canvas;
        private Text headerText, gridText, rosterText, logText, menuText, hintText;
        private Font font;

        private Mode mode = Mode.Menu;
        private int menuIndex;
        private GridPos cursor;
        private int targetIndex;
        private readonly List<string> log = new List<string>();

        // Held-key repeat, so one tap moves the cursor one cell.
        private Vector2Int lastDir;
        private float repeatAt;
        private const float FirstRepeat = 0.28f, NextRepeat = 0.07f;

        public static CombatPanel Instance { get; private set; }

        /// <summary>
        /// True while the panel owns the keyboard. Stays true after the fight ends
        /// so the result screen gets read before explore input resumes — otherwise
        /// the same Space press that dismisses this also triggers a map action.
        /// </summary>
        public bool IsShowing => canvas != null && canvas.gameObject.activeSelf;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            font = CJKFont.Get();
            Build();
            var cs = CombatSystem.Instance;
            if (cs != null)
            {
                cs.OnLogMessage += OnLogMessage;
                cs.OnCombatStart += OnCombatStart;
                cs.OnCombatEnd += OnCombatEnd;
            }
            canvas.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            var cs = CombatSystem.Instance;
            if (cs == null) return;
            cs.OnLogMessage -= OnLogMessage;
            cs.OnCombatStart -= OnCombatStart;
            cs.OnCombatEnd -= OnCombatEnd;
        }

        private void OnLogMessage(string line) => log.Add(line);

        private void OnCombatStart(Data.EnemyData enemy)
        {
            log.Clear();
            var cs = CombatSystem.Instance;
            if (cs?.combatLog != null) log.AddRange(cs.combatLog);
            mode = Mode.Menu;
            menuIndex = 0;
            cursor = cs?.state?.player?.pos ?? new GridPos(0, 0);
            canvas.gameObject.SetActive(true);
            Refresh();
        }

        private void OnCombatEnd()
        {
            mode = Mode.Dismiss;
            Refresh();
        }

        // --- construction ---------------------------------------------------

        private void Build()
        {
            var go = new GameObject("CombatCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            MakeImage(go.transform, "Backdrop", new Color(0.04f, 0.04f, 0.06f, 0.94f),
                      Vector2.zero, Vector2.one);

            headerText = MakeText(go.transform, "Header", 22, TextAnchor.UpperLeft,
                new Vector2(0.03f, 0.90f), new Vector2(0.97f, 0.99f));
            gridText = MakeText(go.transform, "Grid", 30, TextAnchor.UpperLeft,
                new Vector2(0.03f, 0.30f), new Vector2(0.46f, 0.89f));
            rosterText = MakeText(go.transform, "Roster", 19, TextAnchor.UpperLeft,
                new Vector2(0.48f, 0.55f), new Vector2(0.97f, 0.89f));
            logText = MakeText(go.transform, "Log", 18, TextAnchor.LowerLeft,
                new Vector2(0.48f, 0.13f), new Vector2(0.97f, 0.54f));
            menuText = MakeText(go.transform, "Menu", 21, TextAnchor.UpperLeft,
                new Vector2(0.03f, 0.10f), new Vector2(0.46f, 0.29f));
            hintText = MakeText(go.transform, "Hint", 17, TextAnchor.LowerLeft,
                new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.09f));
            hintText.color = new Color(0.62f, 0.62f, 0.66f);

            CJKFont.ApplyTo(go);
        }

        private Text MakeText(Transform parent, string name, int size,
                              TextAnchor anchor, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.1f;
            var rt = t.rectTransform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return t;
        }

        private Image MakeImage(Transform parent, string name, Color c,
                                Vector2 min, Vector2 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            var rt = img.rectTransform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return img;
        }
        // --- input ----------------------------------------------------------

        void Update()
        {
            var cs = CombatSystem.Instance;
            if (cs == null || canvas == null) return;

            if (!cs.isActive && mode != Mode.Dismiss)
            {
                if (canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
                return;
            }
            if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);

            var input = Core.InputManager.Instance;
            if (input == null) return;

            if (mode == Mode.Dismiss)
            {
                if (input.ConfirmPressed)
                {
                    input.ConsumeConfirm();
                    canvas.gameObject.SetActive(false);
                    mode = Mode.Menu;
                }
                Refresh();
                return;
            }

            // Allies and enemies resolve inside CombatSystem, so there is nothing
            // to read here until the human is up again.
            if (!cs.IsPlayerTurn) { Refresh(); return; }

            var dir = NextDirection(input);

            if (input.CancelPressed && mode != Mode.Menu)
            {
                input.ConsumeCancel();
                mode = Mode.Menu;
                Refresh();
                return;
            }

            switch (mode)
            {
                case Mode.Menu:        HandleMenu(cs, input, dir); break;
                case Mode.MoveTarget:  HandleMoveTarget(cs, input, dir); break;
                case Mode.AttackTarget: HandleAttackTarget(cs, input, dir); break;
            }

            Refresh();
        }

        /// <summary>
        /// One cell per tap, then an accelerating repeat while held. InputManager
        /// reports the direction as held state, so the edge has to be found here.
        /// </summary>
        private Vector2Int NextDirection(Core.InputManager input)
        {
            var dir = input.Direction;
            if (dir == Vector2Int.zero)
            {
                lastDir = dir;
                return Vector2Int.zero;
            }
            if (dir != lastDir)
            {
                lastDir = dir;
                repeatAt = Time.unscaledTime + FirstRepeat;
                return dir;
            }
            if (Time.unscaledTime < repeatAt) return Vector2Int.zero;
            repeatAt = Time.unscaledTime + NextRepeat;
            return dir;
        }

        private void HandleMenu(CombatSystem cs, Core.InputManager input, Vector2Int dir)
        {
            if (dir.y != 0)
                menuIndex = (menuIndex - dir.y + MenuItems.Length) % MenuItems.Length;

            if (!input.ConfirmPressed) return;
            input.ConsumeConfirm();

            switch (menuIndex)
            {
                case 0:
                    if (cs.MoveRemaining <= 0) { log.Add("这回合已经走不动了。"); break; }
                    cursor = cs.activeUnit.pos;
                    mode = Mode.MoveTarget;
                    break;

                case 1:
                    if (cs.AttackableTargets().Count == 0)
                    {
                        log.Add("够不着。先走两步。");
                        break;
                    }
                    targetIndex = 0;
                    mode = Mode.AttackTarget;
                    break;

                case 2: cs.PlayerActNormal(); break;
                case 3: cs.PlayerWait(); break;
                case 4: cs.PlayerFlee(); break;
            }
        }

        private void HandleMoveTarget(CombatSystem cs, Core.InputManager input, Vector2Int dir)
        {
            // Row 0 is drawn at the top, so up on the pad is -y on the grid.
            if (dir != Vector2Int.zero)
            {
                var next = new GridPos(cursor.x + dir.x, cursor.y - dir.y);
                if (cs.state.grid.InBounds(next)) cursor = next;
            }

            if (!input.ConfirmPressed) return;
            input.ConsumeConfirm();

            if (cs.PlayerMove(cursor)) mode = Mode.Menu;
            else log.Add("去不了那格。");
        }

        private void HandleAttackTarget(CombatSystem cs, Core.InputManager input, Vector2Int dir)
        {
            var targets = cs.AttackableTargets();
            if (targets.Count == 0) { mode = Mode.Menu; return; }
            targetIndex = Mathf.Clamp(targetIndex, 0, targets.Count - 1);

            if (dir.x != 0 || dir.y != 0)
            {
                int step = dir.x != 0 ? dir.x : -dir.y;
                targetIndex = (targetIndex + step + targets.Count) % targets.Count;
            }
            cursor = targets[targetIndex].pos;

            if (!input.ConfirmPressed) return;
            input.ConsumeConfirm();

            cs.PlayerAttack(targets[targetIndex]);
            mode = Mode.Menu;
        }
        // --- rendering ------------------------------------------------------

        private const string ColPlayer = "#7CE0B0", ColAlly = "#7CC0E0",
                             ColEnemy  = "#E08A80", ColDim  = "#3C3C46",
                             ColCursor = "#F0D060", ColRange = "#4E8A70";

        private void Refresh()
        {
            var cs = CombatSystem.Instance;
            if (cs?.state == null) return;

            DrawHeader(cs);
            DrawGrid(cs);
            DrawRoster(cs);
            DrawLog();
            DrawMenu(cs);
        }

        private void DrawHeader(CombatSystem cs)
        {
            var e = cs.currentEnemy;
            string stars = e == null ? "" : new string('★', Mathf.Clamp(e.stars, 0, 5))
                                          + new string('☆', 5 - Mathf.Clamp(e.stars, 0, 5));
            headerText.text =
                $"NEMESIS ENCOUNTER　「{e?.eventName ?? e?.name ?? "遭遇"}」　{stars}\n" +
                $"TURN {cs.state.turn}　│　WEATHER: {BattleWeather.EnglishName(cs.state.weather)} " +
                $"{BattleWeather.DisplayName(cs.state.weather)}　│　{cs.state.condition.DisplayName}";
        }

        private void DrawGrid(CombatSystem cs)
        {
            var grid = cs.state.grid;
            var actor = cs.activeUnit;
            int budget = cs.IsPlayerTurn ? cs.MoveRemaining : 0;
            bool picking = mode == Mode.MoveTarget;

            var sb = new StringBuilder();
            for (int y = 0; y < grid.height; y++)
            {
                for (int x = 0; x < grid.width; x++)
                {
                    var p = new GridPos(x, y);
                    var u = grid.UnitAt(p);
                    bool isCursor = (mode == Mode.MoveTarget || mode == Mode.AttackTarget)
                                    && p == cursor;

                    string glyph, col;
                    if (u != null && u.IsAlive)
                    {
                        glyph = u.side == BattleSide.Enemy ? "敌"
                              : u == cs.state.player ? "你" : "友";
                        col = u.side == BattleSide.Enemy ? ColEnemy
                            : u == cs.state.player ? ColPlayer : ColAlly;
                    }
                    else if (picking && actor != null &&
                             actor.pos.DistanceTo(p) <= budget && grid.IsFree(p))
                    {
                        glyph = "＋"; col = ColRange;
                    }
                    else
                    {
                        glyph = "・"; col = ColDim;
                    }

                    if (isCursor)
                    {
                        if (u == null || !u.IsAlive) glyph = "＠";
                        col = ColCursor;
                    }
                    sb.Append($"<color={col}>{glyph}</color>");
                }
                sb.Append('\n');
            }
            gridText.text = sb.ToString();
        }

        private void DrawRoster(CombatSystem cs)
        {
            var sb = new StringBuilder();
            sb.Append("<color=#8890A0>── SQUAD ──</color>\n");
            foreach (var u in cs.state.grid.Units)
            {
                if (u.side == BattleSide.Enemy) continue;
                sb.Append(UnitLine(u, cs));
            }
            sb.Append("\n<color=#8890A0>── ENEMY ──</color>\n");
            foreach (var u in cs.state.grid.Units)
            {
                if (u.side != BattleSide.Enemy) continue;
                sb.Append(UnitLine(u, cs));
            }
            rosterText.text = sb.ToString();
        }

        private string UnitLine(BattleUnit u, CombatSystem cs)
        {
            string col = u.side == BattleSide.Enemy ? ColEnemy
                       : u == cs.state.player ? ColPlayer : ColAlly;
            if (!u.IsAlive)
                return $"<color={ColDim}>　{u.displayName}　倒地</color>\n";

            int filled = Mathf.CeilToInt(8f * u.hp / Mathf.Max(1, u.maxHp));
            string bar = new string('▮', filled) + new string('▯', 8 - filled);
            string mark = u == cs.activeUnit ? "▶" : "　";
            return $"<color={col}>{mark}{u.displayName}</color>  {bar} {u.hp}/{u.maxHp}" +
                   $"  <color=#8890A0>ATK {u.attack} DEF {u.defense} MOV {u.MoveRange} {u.pos}</color>\n";
        }

        private void DrawLog()
        {
            int from = Mathf.Max(0, log.Count - LogLines);
            var sb = new StringBuilder();
            for (int i = from; i < log.Count; i++) sb.Append(log[i]).Append('\n');
            logText.text = sb.ToString();
        }

        private void DrawMenu(CombatSystem cs)
        {
            if (mode == Mode.Dismiss)
            {
                string verdict = cs.outcome == BattleOutcome.Victory
                    ? $"<color={ColPlayer}>VICTORY</color>" : $"<color={ColEnemy}>DEFEAT</color>";
                menuText.text = $"{verdict}\n\n战斗结束。";
                hintText.text = "[SPACE] 关掉这块屏";
                return;
            }

            if (!cs.IsPlayerTurn)
            {
                menuText.text = $"<color=#8890A0>{cs.activeUnit?.displayName} 在动……</color>";
                hintText.text = "";
                return;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < MenuItems.Length; i++)
            {
                bool sel = i == menuIndex && mode == Mode.Menu;
                sb.Append(sel ? $"<color={ColCursor}>▶ {MenuItems[i]}</color>\n"
                              : $"　 {MenuItems[i]}\n");
            }
            menuText.text = sb.ToString();

            switch (mode)
            {
                case Mode.MoveTarget:
                    hintText.text = $"移动：方向键选格　[SPACE] 确定　[ESC] 返回　" +
                                    $"剩余 {cs.MoveRemaining} 格　光标 {cursor}";
                    break;
                case Mode.AttackTarget:
                    hintText.text = "攻击：方向键切目标　[SPACE] 打　[ESC] 返回";
                    break;
                default:
                    hintText.text = "方向键选项　[SPACE] 确定。移动不结束回合，攻击才结束。";
                    break;
            }
        }
    }
}
