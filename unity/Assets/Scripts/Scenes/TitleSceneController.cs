using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

namespace KunchengRPG.Scenes
{
    /// <summary>
    /// Title scene controller.
    /// Handles the flow: Title → District Select → Origin Select → Name Input → Explore.
    /// </summary>
    public class TitleSceneController : MonoBehaviour
    {
        public enum Phase
        {
            Title,
            DistrictSelect,
            OriginSelect,
            NameInput,
            YearEndSummary
        }

        [Header("Phase Panels")]
        public GameObject titlePanel;
        public GameObject districtPanel;
        public GameObject originPanel;
        public GameObject nameInputPanel;

        [Header("Title Panel")]
        public Text titleText;
        public Text subtitleText;
        public Text pressStartText;

        [Header("District Panel")]
        public Text districtPromptText;
        public Transform districtListContainer;
        public GameObject choiceItemPrefab;

        [Header("Origin Panel")]
        public Text originPromptText;
        public Text originDetailText;
        public Transform originListContainer;

        [Header("Name Input Panel")]
        public InputField nameInputField;
        public Text namePromptText;
        public Button confirmButton;

        [Header("Colors")]
        public Color selectedColor = new Color(0.36f, 0.79f, 0.65f, 1f);
        public Color normalColor = Color.white;

        private Phase currentPhase = Phase.Title;
        private int selectedIndex = 0;
        private List<string> optionIds = new List<string>();
        private List<string> optionNames = new List<string>();
        private string selectedDistrictId;
        private string selectedOriginId;
        private float bobTimer = 0f;

        void Start()
        {
            SetPhase(Phase.Title);
        }

        void Update()
        {
            bobTimer += Time.deltaTime;

            var input = Core.InputManager.Instance;
            if (input == null) return;

            switch (currentPhase)
            {
                case Phase.Title:
                    HandleTitle(input);
                    break;
                case Phase.DistrictSelect:
                    HandleListNavigation(input, GetDistrictOptions());
                    break;
                case Phase.OriginSelect:
                    HandleListNavigation(input, GetOriginOptions());
                    break;
                case Phase.NameInput:
                    HandleNameInput(input);
                    break;
            }
        }

        // === Phase Management ===

        private void SetPhase(Phase phase)
        {
            currentPhase = phase;
            selectedIndex = 0;

            // Hide all panels
            if (titlePanel) titlePanel.SetActive(false);
            if (districtPanel) districtPanel.SetActive(false);
            if (originPanel) originPanel.SetActive(false);
            if (nameInputPanel) nameInputPanel.SetActive(false);

            switch (phase)
            {
                case Phase.Title:
                    if (titlePanel) titlePanel.SetActive(true);
                    break;
                case Phase.DistrictSelect:
                    if (districtPanel) districtPanel.SetActive(true);
                    BuildDistrictList();
                    break;
                case Phase.OriginSelect:
                    if (originPanel) originPanel.SetActive(true);
                    BuildOriginList();
                    break;
                case Phase.NameInput:
                    if (nameInputPanel) nameInputPanel.SetActive(true);
                    if (nameInputField) nameInputField.Select();
                    break;
            }
        }

        // === Title Phase ===

        private void HandleTitle(Core.InputManager input)
        {
            // Blink the start prompt. Confirm takes Enter or Space; the scene text
            // says Enter because that is what players try first.
            if (pressStartText)
                pressStartText.gameObject.SetActive(Mathf.FloorToInt(bobTimer * 2) % 2 == 0);

            if (input.ConfirmPressed)
            {
                input.ConsumeConfirm();
                SetPhase(Phase.DistrictSelect);
            }
        }

        // === District Select ===

        private List<(string id, string name)> GetDistrictOptions()
        {
            var result = new List<(string, string)>();
            var districts = Core.GameManager.Instance.districts;
            if (districts == null) return result;

            // Data-driven: every shipped district file shows up on its own. The old
            // version hardcoded jinyong/jiuxu, so the walk from 2 districts to 11
            // would have needed a code edit per district.
            var ids = new List<string>(districts.Keys);
            ids.Sort();
            foreach (var id in ids)
            {
                var d = districts[id];
                if (d == null) continue;
                string label = string.IsNullOrEmpty(d.name) ? id : d.name;
                string type = AnomalyTypeName(d.anomalyType);
                result.Add((id, string.IsNullOrEmpty(type) ? label : $"{label} · {type}"));
            }
            return result;
        }

        private static string DistrictDisplayName(string id)
        {
            var districts = Core.GameManager.Instance?.districts;
            if (districts != null && districts.TryGetValue(id, out var d)
                && d != null && !string.IsNullOrEmpty(d.name))
                return d.name;
            return id;
        }

        /// <summary>
        /// Anomaly type ids to their NamingBible names. All eleven are listed so
        /// dropping in a new district file needs no code change.
        /// </summary>
        private static string AnomalyTypeName(string id)
        {
            switch (id)
            {
                case "authority_anomaly":   return "权力异常";
                case "information_anomaly": return "信息异常";
                case "legacy_anomaly":      return "旧物异常";
                case "manufacture_anomaly": return "制造异常";
                case "path_anomaly":        return "路径异常";
                case "origin_anomaly":      return "来路异常";
                case "time_anomaly":        return "时间异常";
                case "forest_anomaly":      return "山林异常";
                case "growth_anomaly":      return "生长异常";
                case "tide_anomaly":        return "潮汐异常";
                case "border_anomaly":      return "边境异常";
                default:                    return null;
            }
        }

        private void BuildDistrictList()
        {
            var options = GetDistrictOptions();
            optionIds.Clear();
            optionNames.Clear();

            if (districtPromptText)
                districtPromptText.text = "选择你的出身之地";

            // Clear existing items
            if (districtListContainer)
            {
                foreach (Transform child in districtListContainer)
                    Destroy(child.gameObject);
            }

            foreach (var (id, name) in options)
            {
                optionIds.Add(id);
                optionNames.Add(name);
                CreateChoiceItem(name);
            }

            RefreshSelection();
        }

        // === Origin Select ===

        private List<(string id, string name)> GetOriginOptions()
        {
            var result = new List<(string, string)>();
            var origins = Core.AssetLoader.LoadOriginsForDistrict(selectedDistrictId);
            foreach (var origin in origins)
            {
                result.Add((origin.id, $"{origin.name} - {origin.familyBackground}"));
            }
            return result;
        }

        private void BuildOriginList()
        {
            var options = GetOriginOptions();
            optionIds.Clear();
            optionNames.Clear();

            // Was printing the raw id ("jinyong"). Show the district's own name.
            if (originPromptText)
                originPromptText.text = $"你在{DistrictDisplayName(selectedDistrictId)}的出身";

            if (originListContainer)
            {
                foreach (Transform child in originListContainer)
                    Destroy(child.gameObject);
            }

            foreach (var (id, name) in options)
            {
                optionIds.Add(id);
                optionNames.Add(name);
                CreateChoiceItem(name);
            }

            RefreshSelection();
        }

        // === Name Input ===

        private void HandleNameInput(Core.InputManager input)
        {
            // Enter only. ConfirmPressed also covers Space, and the name field has
            // focus here, so a space in the middle of a name would submit the form.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                input.ConsumeConfirm();
                ConfirmName();
            }
        }

        public void ConfirmName()
        {
            string name = "Player";
            if (nameInputField != null && !string.IsNullOrEmpty(nameInputField.text))
                name = nameInputField.text;

            // Start the game!
            Core.GameManager.Instance.StartNewGame(selectedDistrictId, selectedOriginId, name);

            // Load explore scene
            SceneManager.LoadScene("ExploreScene");
        }

        // === Shared List Navigation ===

        private void HandleListNavigation(Core.InputManager input, List<(string id, string name)> options)
        {
            if (options.Count == 0) return;

            // Edge-triggered: Direction is held-state, which scrolled a two-item
            // list at frame rate and made it impossible to land on a choice.
            if (input.DirectionPressed.y > 0) // Up
            {
                selectedIndex = (selectedIndex - 1 + options.Count) % options.Count;
                RefreshSelection();
            }
            else if (input.DirectionPressed.y < 0) // Down
            {
                selectedIndex = (selectedIndex + 1) % options.Count;
                RefreshSelection();
            }

            if (input.ConfirmPressed)
            {
                input.ConsumeConfirm();
                ConfirmSelection();
            }

            if (input.CancelPressed)
            {
                input.ConsumeCancel();
                if (currentPhase == Phase.OriginSelect)
                    SetPhase(Phase.DistrictSelect);
            }
        }

        private void ConfirmSelection()
        {
            if (selectedIndex >= optionIds.Count) return;

            switch (currentPhase)
            {
                case Phase.DistrictSelect:
                    selectedDistrictId = optionIds[selectedIndex];
                    SetPhase(Phase.OriginSelect);
                    break;
                case Phase.OriginSelect:
                    selectedOriginId = optionIds[selectedIndex];
                    SetPhase(Phase.NameInput);
                    break;
            }
        }

        // === UI Helpers ===

        private void CreateChoiceItem(string text)
        {
            if (choiceItemPrefab == null) return;

            Transform container = null;
            if (currentPhase == Phase.DistrictSelect) container = districtListContainer;
            else if (currentPhase == Phase.OriginSelect) container = originListContainer;

            if (container == null) return;

            var go = Instantiate(choiceItemPrefab, container);
            // The prefab's own ApplyCJKFont lost its script reference, so rows swap the
            // font here the way CombatPanel already does rather than trusting the prefab.
            UI.CJKFont.ApplyTo(go);
            var uiText = go.GetComponent<Text>();
            if (uiText == null) uiText = go.GetComponentInChildren<Text>();
            if (uiText != null) uiText.text = text;
        }

        private void RefreshSelection()
        {
            Transform container = null;
            if (currentPhase == Phase.DistrictSelect) container = districtListContainer;
            else if (currentPhase == Phase.OriginSelect) container = originListContainer;

            if (container == null) return;

            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                var text = child.GetComponent<Text>();
                if (text == null) text = child.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.color = (i == selectedIndex) ? selectedColor : normalColor;
                    text.text = (i == selectedIndex ? "> " : "  ") + optionNames[i];
                }
            }
        }
    }
}
