using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using KunchengRPG.Core;
using KunchengRPG.Game;
using KunchengRPG.Scenes;
using KunchengRPG.UI;

namespace KunchengRPG.EditorTools
{
    /// <summary>
    /// Generates TitleScene and ExploreScene with every inspector reference wired.
    ///
    /// Doing this in code instead of by hand keeps the wiring diffable and lets a
    /// batchmode run rebuild both scenes from scratch, so a broken scene is never
    /// something you have to reconstruct by memory.
    /// </summary>
    public static class SceneBuilder
    {
        public const string SceneDir = "Assets/Scenes";
        public const string PrefabDir = "Assets/Prefabs";
        public const string TitleScenePath = SceneDir + "/TitleScene.unity";
        public const string ExploreScenePath = SceneDir + "/ExploreScene.unity";
        public const string ChoiceItemPath = PrefabDir + "/ChoiceItem.prefab";

        [MenuItem("Kuncheng/2. Build Scenes", false, 11)]
        public static void BuildAll()
        {
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory(PrefabDir);

            GameObject choiceItem = BuildChoiceItemPrefab();
            BuildTitleScene(choiceItem);
            BuildExploreScene(choiceItem);
            RegisterBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneBuilder] TitleScene + ExploreScene built and registered.");
        }

        [MenuItem("Kuncheng/3. Build Everything", false, 12)]
        public static void BuildEverything()
        {
            TilesetBuilder.Build();
            BuildAll();
        }

        /// <summary>
        /// A single selectable row. Controllers instantiate this per choice and read
        /// its Text child, so the Text must be the only one beneath it.
        /// </summary>
        private static GameObject BuildChoiceItemPrefab()
        {
            var root = new GameObject("ChoiceItem", typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(560, 38);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.05f);

            var label = UIBuilder.CreateText("Label", root.transform, "选项", 20, TextAnchor.MiddleLeft);
            var lrt = label.GetComponent<RectTransform>();
            lrt.offsetMin = new Vector2(14, 0);
            lrt.offsetMax = new Vector2(-14, 0);

            // Runtime-instantiated rows are outside any canvas at Awake, so they need
            // their own font pass.
            root.AddComponent<ApplyCJKFont>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ChoiceItemPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// TitleScene: bootstraps GameManager and runs the four-phase character setup
        /// (title → district → origin → name).
        /// </summary>
        private static void BuildTitleScene(GameObject choiceItem)
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // GameManager persists across the load into ExploreScene, so it lives here.
            var boot = new GameObject("GameManager", typeof(GameManager));

            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            UIBuilder.CreateEventSystem();
            UIBuilder.CreateCanvas("UICanvas", out var canvasRoot);

            var controller = canvasRoot.AddComponent<TitleSceneController>();
            controller.choiceItemPrefab = choiceItem;

            // --- Title panel ---
            var titlePanel = UIBuilder.CreatePanel("TitlePanel", canvasRoot.transform);
            controller.titlePanel = titlePanel;

            var title = UIBuilder.CreateText(
                "Title", titlePanel.transform, "坤城", 88, TextAnchor.MiddleCenter, UIBuilder.Accent);
            UIBuilder.Place(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 150), new Vector2(800, 120));
            controller.titleText = title;

            var subtitle = UIBuilder.CreateText(
                "Subtitle", titlePanel.transform, "一个关于世代的故事", 26, TextAnchor.MiddleCenter);
            UIBuilder.Place(subtitle.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 70), new Vector2(800, 40));
            controller.subtitleText = subtitle;

            var press = UIBuilder.CreateText(
                "PressStart", titlePanel.transform, "按 Enter 开始", 24, TextAnchor.MiddleCenter);
            UIBuilder.Place(press.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -120), new Vector2(800, 40));
            controller.pressStartText = press;

            // --- District panel ---
            var districtPanel = UIBuilder.CreatePanel("DistrictPanel", canvasRoot.transform);
            controller.districtPanel = districtPanel;

            var districtPrompt = UIBuilder.CreateText(
                "Prompt", districtPanel.transform, "选择你的出身之地", 34, TextAnchor.MiddleCenter, UIBuilder.Accent);
            UIBuilder.Place(districtPrompt.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -70), new Vector2(900, 50));
            controller.districtPromptText = districtPrompt;
            controller.districtListContainer = UIBuilder.CreateVerticalList(
                "List", districtPanel.transform, new Vector2(0, -160), new Vector2(600, 400));

            // --- Origin panel ---
            var originPanel = UIBuilder.CreatePanel("OriginPanel", canvasRoot.transform);
            controller.originPanel = originPanel;

            var originPrompt = UIBuilder.CreateText(
                "Prompt", originPanel.transform, "选择你的出身", 34, TextAnchor.MiddleCenter, UIBuilder.Accent);
            UIBuilder.Place(originPrompt.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -70), new Vector2(900, 50));
            controller.originPromptText = originPrompt;
            controller.originListContainer = UIBuilder.CreateVerticalList(
                "List", originPanel.transform, new Vector2(-210, -160), new Vector2(460, 400));

            var originDetail = UIBuilder.CreateText(
                "Detail", originPanel.transform, "", 19, TextAnchor.UpperLeft);
            UIBuilder.Place(originDetail.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(270, -160), new Vector2(440, 420));
            controller.originDetailText = originDetail;

            // --- Name input panel ---
            var namePanel = UIBuilder.CreatePanel("NameInputPanel", canvasRoot.transform);
            controller.nameInputPanel = namePanel;

            var namePrompt = UIBuilder.CreateText(
                "Prompt", namePanel.transform, "为你的角色取名", 34, TextAnchor.MiddleCenter, UIBuilder.Accent);
            UIBuilder.Place(namePrompt.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 110), new Vector2(900, 50));
            controller.namePromptText = namePrompt;

            controller.nameInputField = CreateInputField(namePanel.transform);
            controller.confirmButton = CreateConfirmButton(namePanel.transform, controller);

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        private static InputField CreateInputField(Transform parent)
        {
            var go = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            UIBuilder.Place(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 30), new Vector2(420, 48));
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);

            var text = UIBuilder.CreateText("Text", go.transform, "", 22, TextAnchor.MiddleLeft);
            var trt = text.GetComponent<RectTransform>();
            trt.offsetMin = new Vector2(12, 0);
            trt.offsetMax = new Vector2(-12, 0);

            var placeholder = UIBuilder.CreateText(
                "Placeholder", go.transform, "输入姓名…", 22, TextAnchor.MiddleLeft,
                new Color(0.92f, 0.94f, 0.93f, 0.4f));
            var prt = placeholder.GetComponent<RectTransform>();
            prt.offsetMin = new Vector2(12, 0);
            prt.offsetMax = new Vector2(-12, 0);

            var field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = 12;
            return field;
        }

        private static Button CreateConfirmButton(Transform parent, TitleSceneController controller)
        {
            var go = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            UIBuilder.Place(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -50), new Vector2(200, 46));
            go.GetComponent<Image>().color = UIBuilder.Accent;

            UIBuilder.CreateText("Label", go.transform, "确定", 22, TextAnchor.MiddleCenter, Color.black);

            var button = go.GetComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                button.onClick, controller.ConfirmName);
            return button;
        }

        /// <summary>
        /// ExploreScene: tilemap grid, player, camera follow, HUD, and the event and
        /// dialogue panels.
        /// </summary>
        private static void BuildExploreScene(GameObject choiceItem)
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 7.5f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            // --- Tilemaps ---
            var gridGo = new GameObject("Grid", typeof(Grid));
            var grid = gridGo.GetComponent<Grid>();
            grid.cellSize = Vector3.one; // 1 unit per tile, matching spritePixelsPerUnit

            var ground = CreateTilemapLayer("Ground", gridGo.transform, 0);
            var building = CreateTilemapLayer("Buildings", gridGo.transform, 1);
            var decoration = CreateTilemapLayer("Decorations", gridGo.transform, 2);

            var mapGo = new GameObject("MapController", typeof(MapController));
            var map = mapGo.GetComponent<MapController>();
            map.groundTilemap = ground;
            map.buildingTilemap = building;
            map.decorationTilemap = decoration;
            map.tiles = TilesetBuilder.LoadTilesInOrder();

            int missing = 0;
            foreach (var t in map.tiles) if (t == null) missing++;
            if (missing > 0)
                Debug.LogWarning($"[SceneBuilder] {missing}/{TilesetBuilder.TileCount} tile assets missing — run 'Build Tileset Assets' first.");

            // --- Player ---
            var playerGo = new GameObject("Player", typeof(SpriteRenderer), typeof(PlayerController));
            var sr = playerGo.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            sr.color = UIBuilder.Accent;
            sr.sprite = MakePlayerSprite();

            var player = playerGo.GetComponent<PlayerController>();
            player.spriteRenderer = sr;
            player.mapController = map;

            // --- HUD + panels ---
            UIBuilder.CreateEventSystem();
            UIBuilder.CreateCanvas("UICanvas", out var canvasRoot);

            var hud = BuildHUD(canvasRoot.transform);
            var eventPanel = BuildEventPanel(canvasRoot.transform, choiceItem);
            var dialoguePanel = BuildDialoguePanel(canvasRoot.transform, choiceItem);

            var sceneGo = new GameObject("ExploreSceneController", typeof(ExploreSceneController));
            var controller = sceneGo.GetComponent<ExploreSceneController>();
            controller.mapController = map;
            controller.playerController = player;
            controller.hud = hud;
            controller.eventPanel = eventPanel;
            controller.dialoguePanel = dialoguePanel;
            controller.mainCamera = cam;

            EditorSceneManager.SaveScene(scene, ExploreScenePath);
        }

        private static Tilemap CreateTilemapLayer(string name, Transform parent, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = order;
            return go.GetComponent<Tilemap>();
        }

        /// <summary>
        /// A 1x1 white sprite for the player placeholder. Generated rather than
        /// committed so there is no stray art asset to keep in sync.
        /// </summary>
        private static Sprite MakePlayerSprite()
        {
            const string path = "Assets/Art/Sprites/player_placeholder.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory("Assets/Art/Sprites");

            var tex = new Texture2D(TilesetBuilder.TileSize, TilesetBuilder.TileSize);
            var px = new Color[tex.width * tex.height];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = TilesetBuilder.TileSize;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static HUDController BuildHUD(Transform parent)
        {
            var root = UIBuilder.CreateRect("HUD", parent);
            var hud = root.AddComponent<HUDController>();

            // Status strip, top-left. One Text per stat so the controller can update
            // each independently.
            var strip = new GameObject("Status", typeof(RectTransform));
            strip.transform.SetParent(root.transform, false);
            UIBuilder.Place(strip, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20, -20), new Vector2(420, 210));
            strip.AddComponent<Image>().color = UIBuilder.PanelBg;

            var col = strip.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(14, 14, 12, 12);
            col.spacing = 3f;
            col.childControlHeight = false;
            col.childForceExpandHeight = false;

            hud.nameText = Row(strip.transform, "Name", "姓名");
            hud.ageText = Row(strip.transform, "Age", "年龄");
            hud.stageText = Row(strip.transform, "Stage", "阶段");
            hud.apText = Row(strip.transform, "AP", "行动点");
            hud.moneyText = Row(strip.transform, "Money", "钱");
            hud.hpText = Row(strip.transform, "HP", "体力");
            hud.staminaText = Row(strip.transform, "Stamina", "精力");
            hud.districtText = Row(strip.transform, "District", "地区");

            // Interaction prompt, bottom-center.
            var prompt = UIBuilder.CreateRect("PromptPanel", root.transform);
            UIBuilder.Place(prompt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 40), new Vector2(680, 46));
            prompt.AddComponent<Image>().color = UIBuilder.PanelBg;
            hud.promptPanel = prompt;
            hud.promptText = UIBuilder.CreateText(
                "Text", prompt.transform, "", 20, TextAnchor.MiddleCenter);

            // Transient message, upper-center.
            var message = UIBuilder.CreateRect("MessagePanel", root.transform);
            UIBuilder.Place(message, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -30), new Vector2(680, 46));
            message.AddComponent<Image>().color = UIBuilder.PanelBg;
            hud.messagePanel = message;
            hud.messageText = UIBuilder.CreateText(
                "Text", message.transform, "", 20, TextAnchor.MiddleCenter, UIBuilder.Accent);

            return hud;
        }

        private static Text Row(Transform parent, string name, string label)
        {
            var text = UIBuilder.CreateText(name, parent, label, 18, TextAnchor.MiddleLeft);
            var le = text.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 22;
            return text;
        }

        private static EventPanel BuildEventPanel(Transform parent, GameObject choiceItem)
        {
            var root = UIBuilder.CreateRect("EventPanel", parent);
            var panel = root.AddComponent<EventPanel>();

            var box = UIBuilder.CreatePanel("Box", root.transform);
            UIBuilder.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760, 520));

            panel.panel = box;
            panel.choiceItemPrefab = choiceItem;

            var titleText = UIBuilder.CreateText(
                "Title", box.transform, "", 30, TextAnchor.MiddleCenter, UIBuilder.Accent);
            UIBuilder.Place(titleText.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -30), new Vector2(700, 44));
            panel.titleText = titleText;

            var desc = UIBuilder.CreateText("Description", box.transform, "", 20);
            UIBuilder.Place(desc.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -90), new Vector2(680, 150));
            panel.descriptionText = desc;

            panel.choiceListContainer = UIBuilder.CreateVerticalList(
                "Choices", box.transform, new Vector2(0, -255), new Vector2(680, 200));

            var result = UIBuilder.CreateText(
                "Result", box.transform, "", 20, TextAnchor.UpperCenter);
            UIBuilder.Place(result.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 30), new Vector2(680, 110));
            panel.resultText = result;

            return panel;
        }

        private static DialoguePanel BuildDialoguePanel(Transform parent, GameObject choiceItem)
        {
            var root = UIBuilder.CreateRect("DialoguePanel", parent);
            var panel = root.AddComponent<DialoguePanel>();

            var box = UIBuilder.CreatePanel("Box", root.transform);
            UIBuilder.Place(box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 30), new Vector2(920, 300));

            panel.panel = box;
            panel.choiceItemPrefab = choiceItem;

            var speaker = UIBuilder.CreateText(
                "Speaker", box.transform, "", 24, TextAnchor.MiddleLeft, UIBuilder.Accent);
            UIBuilder.Place(speaker.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28, -22), new Vector2(500, 34));
            panel.speakerText = speaker;

            var body = UIBuilder.CreateText("Text", box.transform, "", 21);
            UIBuilder.Place(body.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -70), new Vector2(860, 100));
            panel.dialogueText = body;

            panel.choiceListContainer = UIBuilder.CreateVerticalList(
                "Choices", box.transform, new Vector2(0, -180), new Vector2(860, 110));

            return panel;
        }

        /// <summary>
        /// TitleScene must be index 0 so a fresh Play starts at character creation.
        /// </summary>
        private static void RegisterBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(ExploreScenePath, true)
            };
        }
    }
}
