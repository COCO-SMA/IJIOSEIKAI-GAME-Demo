using UnityEngine;
using System.Collections.Generic;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Dialogue system for NPC conversations.
    /// Loads dialogue trees from JSON, handles node navigation.
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        public static DialogueSystem Instance { get; private set; }

        // Current dialogue state
        public Data.DialogueTree currentTree { get; private set; }
        public Data.DialogueNode currentNode { get; private set; }
        public string currentSpeaker { get; private set; }
        public int selectedChoiceIndex { get; private set; }
        public bool isActive { get; private set; }

        // Events
        public System.Action<string, string> OnNodeChanged; // speaker, text
        public System.Action<List<Data.DialogueChoice>> OnChoicesChanged;
        public System.Action OnDialogueEnd;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Start a dialogue with an NPC.
        /// </summary>
        public void StartDialogue(Data.NPCData npc)
        {
            var tree = Core.AssetLoader.LoadDialogue(npc.dialogueId);
            if (tree == null)
            {
                Debug.LogWarning($"[DialogueSystem] Failed to load dialogue: {npc.dialogueId}");
                return;
            }

            currentTree = tree;
            currentSpeaker = tree.speaker ?? npc.name;
            currentNode = tree.start;
            selectedChoiceIndex = 0;
            isActive = true;

            OnNodeChanged?.Invoke(currentSpeaker, currentNode.text);
            OnChoicesChanged?.Invoke(currentNode.choices ?? new List<Data.DialogueChoice>());

            Debug.Log($"[DialogueSystem] Dialogue started: {npc.name}");
        }

        /// <summary>
        /// Navigate to a choice by index.
        /// </summary>
        public void SelectChoice(int index)
        {
            if (currentNode == null || currentNode.choices == null) return;
            selectedChoiceIndex = Mathf.Clamp(index, 0, Mathf.Max(0, currentNode.choices.Count - 1));
        }

        /// <summary>
        /// Confirm the selected choice and navigate to next node.
        /// </summary>
        public void ConfirmChoice()
        {
            if (currentNode == null || currentNode.choices == null || currentNode.choices.Count == 0)
            {
                EndDialogue();
                return;
            }

            if (selectedChoiceIndex >= currentNode.choices.Count) return;

            var choice = currentNode.choices[selectedChoiceIndex];

            // Check if this choice leads to an end (no next node)
            if (string.IsNullOrEmpty(choice.next))
            {
                EndDialogue();
                return;
            }

            // Navigate to next node
            if (currentTree.nodes != null && currentTree.nodes.ContainsKey(choice.next))
            {
                currentNode = currentTree.nodes[choice.next];
                selectedChoiceIndex = 0;

                OnNodeChanged?.Invoke(currentSpeaker, currentNode.text);
                OnChoicesChanged?.Invoke(currentNode.choices ?? new List<Data.DialogueChoice>());
            }
            else
            {
                // Node not found — end dialogue
                EndDialogue();
            }
        }

        /// <summary>
        /// End the current dialogue.
        /// </summary>
        public void EndDialogue()
        {
            isActive = false;
            currentTree = null;
            currentNode = null;

            OnDialogueEnd?.Invoke();
            Debug.Log("[DialogueSystem] Dialogue ended");
        }
    }
}
