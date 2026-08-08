using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace KunchengRPG.UI
{
    /// <summary>
    /// Event panel: shows event title/description, choice list, and result.
    /// Player navigates choices with up/down, confirms with Space.
    /// </summary>
    public class EventPanel : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject panel;
        public Text titleText;
        public Text descriptionText;
        public Transform choiceListContainer;
        public GameObject choiceItemPrefab;
        public Text resultText;

        [Header("Colors")]
        public Color selectedColor = new Color(0.36f, 0.79f, 0.65f, 1f);
        public Color normalColor = Color.white;

        private List<Text> choiceTexts = new List<Text>();

        void Start()
        {
            if (Game.EventSystem.Instance != null)
            {
                Game.EventSystem.Instance.OnEventStart += OnEventStart;
                Game.EventSystem.Instance.OnEventResolved += OnEventResolved;
            }
            if (panel) panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (Game.EventSystem.Instance != null)
            {
                Game.EventSystem.Instance.OnEventStart -= OnEventStart;
                Game.EventSystem.Instance.OnEventResolved -= OnEventResolved;
            }
        }

        void Update()
        {
            if (Game.EventSystem.Instance == null || !Game.EventSystem.Instance.isActive)
            {
                if (panel) panel.SetActive(false);
                return;
            }

            if (!panel.activeSelf)
            {
                panel.SetActive(true);
                RefreshUI();
            }

            var input = Core.InputManager.Instance;
            if (input == null) return;

            // Result phase
            if (Game.EventSystem.Instance.eventResultText != null)
            {
                if (input.ConfirmPressed)
                {
                    input.ConsumeConfirm();
                    Game.EventSystem.Instance.CloseEvent();
                    // Consume AP after event closes
                    Game.LifecycleManager.Instance?.ConsumeAction();
                }
                return;
            }

            // Choice navigation
            var evt = Game.EventSystem.Instance.activeEvent;
            if (evt == null) return;

            int choiceCount = evt.choices.Count;

            if (input.Direction.y > 0) // Up
            {
                int idx = Game.EventSystem.Instance.selectedChoiceIndex;
                Game.EventSystem.Instance.SelectChoice((idx - 1 + choiceCount) % choiceCount);
                RefreshChoices();
            }
            else if (input.Direction.y < 0) // Down
            {
                int idx = Game.EventSystem.Instance.selectedChoiceIndex;
                Game.EventSystem.Instance.SelectChoice((idx + 1) % choiceCount);
                RefreshChoices();
            }

            if (input.ConfirmPressed)
            {
                input.ConsumeConfirm();
                Game.EventSystem.Instance.ConfirmChoice();
                ShowResult();
            }
        }

        private void OnEventStart(Data.EventData evt)
        {
            if (panel) panel.SetActive(true);
            if (resultText) resultText.gameObject.SetActive(false);
            RefreshUI();
        }

        private void OnEventResolved(string result)
        {
            ShowResult();
        }

        private void RefreshUI()
        {
            var evt = Game.EventSystem.Instance.activeEvent;
            if (evt == null) return;

            if (titleText) titleText.text = evt.title;
            if (descriptionText) descriptionText.text = evt.description;
            if (resultText) resultText.gameObject.SetActive(false);

            // Clear old choices
            foreach (Transform child in choiceListContainer)
                Destroy(child.gameObject);
            choiceTexts.Clear();

            // Create choice items
            foreach (var choice in evt.choices)
            {
                var go = Instantiate(choiceItemPrefab, choiceListContainer);
                var text = go.GetComponent<Text>();
                if (text == null)
                    text = go.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = choice.text;
                    text.color = normalColor;
                    choiceTexts.Add(text);
                }
            }

            RefreshChoices();
        }

        private void RefreshChoices()
        {
            int selected = Game.EventSystem.Instance.selectedChoiceIndex;
            for (int i = 0; i < choiceTexts.Count; i++)
            {
                if (choiceTexts[i] != null)
                {
                    choiceTexts[i].color = (i == selected) ? selectedColor : normalColor;
                    choiceTexts[i].text = (i == selected ? "> " : "  ") +
                        Game.EventSystem.Instance.activeEvent.choices[i].text;
                }
            }
        }

        private void ShowResult()
        {
            if (resultText == null) return;
            resultText.gameObject.SetActive(true);
            resultText.text = Game.EventSystem.Instance.eventResultText ?? "...";
        }
    }
}
