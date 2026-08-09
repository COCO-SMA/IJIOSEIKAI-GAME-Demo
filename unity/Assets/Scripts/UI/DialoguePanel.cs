using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace KunchengRPG.UI
{
    /// <summary>
    /// Dialogue panel: shows speaker name, dialogue text, and choices.
    /// </summary>
    public class DialoguePanel : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject panel;
        public Text speakerText;
        public Text dialogueText;
        public Transform choiceListContainer;
        public GameObject choiceItemPrefab;

        [Header("Colors")]
        public Color selectedColor = new Color(0.36f, 0.79f, 0.65f, 1f);
        public Color normalColor = Color.white;

        private List<Text> choiceTexts = new List<Text>();

        void Start()
        {
            if (Game.DialogueSystem.Instance != null)
            {
                Game.DialogueSystem.Instance.OnNodeChanged += OnNodeChanged;
                Game.DialogueSystem.Instance.OnChoicesChanged += OnChoicesChanged;
                Game.DialogueSystem.Instance.OnDialogueEnd += OnDialogueEnd;
            }
            if (panel) panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (Game.DialogueSystem.Instance != null)
            {
                Game.DialogueSystem.Instance.OnNodeChanged -= OnNodeChanged;
                Game.DialogueSystem.Instance.OnChoicesChanged -= OnChoicesChanged;
                Game.DialogueSystem.Instance.OnDialogueEnd -= OnDialogueEnd;
            }
        }

        void Update()
        {
            if (Game.DialogueSystem.Instance == null || !Game.DialogueSystem.Instance.isActive)
            {
                if (panel) panel.SetActive(false);
                return;
            }

            if (!panel.activeSelf)
            {
                panel.SetActive(true);
            }

            var input = Core.InputManager.Instance;
            if (input == null) return;

            // Navigate choices
            var choices = Game.DialogueSystem.Instance.currentNode?.choices;
            if (choices == null || choices.Count == 0)
            {
                // No choices — press confirm to end
                if (input.ConfirmPressed)
                {
                    input.ConsumeConfirm();
                    Game.DialogueSystem.Instance.EndDialogue();
                    // Consume AP after dialogue
                    Game.LifecycleManager.Instance?.ConsumeAction();
                }
                return;
            }

            int count = choices.Count;

            // Edge-triggered, same reason as EventPanel: one keypress, one step.
            if (input.DirectionPressed.y > 0) // Up
            {
                int idx = Game.DialogueSystem.Instance.selectedChoiceIndex;
                Game.DialogueSystem.Instance.SelectChoice((idx - 1 + count) % count);
                RefreshChoices();
            }
            else if (input.DirectionPressed.y < 0) // Down
            {
                int idx = Game.DialogueSystem.Instance.selectedChoiceIndex;
                Game.DialogueSystem.Instance.SelectChoice((idx + 1) % count);
                RefreshChoices();
            }

            if (input.ConfirmPressed)
            {
                input.ConsumeConfirm();
                Game.DialogueSystem.Instance.ConfirmChoice();
            }
        }

        private void OnNodeChanged(string speaker, string text)
        {
            if (speakerText) speakerText.text = speaker;
            if (dialogueText) dialogueText.text = text;
        }

        private void OnChoicesChanged(List<Data.DialogueChoice> choices)
        {
            // Clear old choices
            if (choiceListContainer != null)
            {
                foreach (Transform child in choiceListContainer)
                    Destroy(child.gameObject);
            }
            choiceTexts.Clear();

            if (choices == null || choices.Count == 0)
            {
                // Show "[Space] Continue" prompt
                if (choiceItemPrefab != null && choiceListContainer != null)
                {
                    var go = Instantiate(choiceItemPrefab, choiceListContainer);
                    CJKFont.ApplyTo(go);
                    var text = go.GetComponent<Text>();
                    if (text == null) text = go.GetComponentInChildren<Text>();
                    if (text != null)
                    {
                        text.text = "[空格] 继续";
                        text.color = selectedColor;
                        choiceTexts.Add(text);
                    }
                }
                return;
            }

            foreach (var choice in choices)
            {
                if (choiceItemPrefab == null || choiceListContainer == null) continue;
                var go = Instantiate(choiceItemPrefab, choiceListContainer);
                CJKFont.ApplyTo(go);
                var text = go.GetComponent<Text>();
                if (text == null) text = go.GetComponentInChildren<Text>();
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
            int selected = Game.DialogueSystem.Instance.selectedChoiceIndex;
            var choices = Game.DialogueSystem.Instance.currentNode?.choices;

            for (int i = 0; i < choiceTexts.Count; i++)
            {
                if (choiceTexts[i] == null) continue;

                bool isSelected = (i == selected);
                choiceTexts[i].color = isSelected ? selectedColor : normalColor;

                if (choices != null && i < choices.Count)
                {
                    choiceTexts[i].text = (isSelected ? "> " : "  ") + choices[i].text;
                }
            }
        }

        private void OnDialogueEnd()
        {
            if (panel) panel.SetActive(false);
        }
    }
}
