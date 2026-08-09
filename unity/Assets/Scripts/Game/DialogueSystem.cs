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

            // Side effects fire before navigation, so a node reached by a granting
            // choice can describe what the player now has.
            if (!string.IsNullOrEmpty(choice.effect))
                ApplyEffect(choice.effect);

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
        /// Run one dialogue side effect. Returns a player-facing summary, or an
        /// empty string when nothing happened. Unknown verbs warn rather than
        /// throw: bad content should be visible in the log, not fatal in play.
        /// </summary>
        public static string ApplyEffect(string effect)
        {
            var gm = Core.GameManager.Instance;
            if (gm == null || string.IsNullOrEmpty(effect)) return "";

            string[] parts = effect.Split(':');
            string verb = parts[0].Trim();

            switch (verb)
            {
                case "grant_all_anomalies":
                    return GrantAllAnomalies(gm);

                case "grant":
                    if (parts.Length < 2) break;
                    return GrantOne(gm, parts[1].Trim());

                case "money":
                    if (parts.Length < 2 || !int.TryParse(parts[1], out int amount)) break;
                    gm.Player.money += amount;
                    return amount >= 0 ? $"拿到 ¥{amount}。" : $"付了 ¥{-amount}。";

                case "affinity":
                    if (parts.Length < 3 || !int.TryParse(parts[2], out int delta)) break;
                    gm.City?.AddDistrictAffinity(parts[1].Trim(), delta);
                    return "";

                case "flag":
                    if (parts.Length < 3 || !int.TryParse(parts[2], out int value)) break;
                    if (gm.Player.flags == null)
                        gm.Player.flags = new Dictionary<string, int>();
                    gm.Player.flags[parts[1].Trim()] = value;
                    return "";
            }

            Debug.LogWarning($"[Dialogue] Unknown or malformed effect: {effect}");
            return "";
        }

        /// <summary>
        /// Test affordance: hand over one instance of every anomaly in the data set,
        /// including the nemesis-only lethal and void tiers. Deliberately ignores
        /// the "acquisition only through events" rule — that rule governs the real
        /// game, and this exists so all 30 items can be exercised without farming
        /// nemeses that are not implemented yet.
        /// </summary>
        private static string GrantAllAnomalies(Core.GameManager gm)
        {
            if (gm.Anomalies == null || gm.anomalies == null) return "";

            // Grant always mints a new instance, so without this guard a second
            // conversation would leave 60 items in a bag meant to hold 30.
            var owned = new HashSet<string>();
            foreach (var inst in gm.Anomalies.Instances) owned.Add(inst.itemId);

            int granted = 0;
            foreach (var kvp in gm.anomalies)
            {
                if (owned.Contains(kvp.Key)) continue;
                if (gm.Anomalies.Grant(kvp.Key) != null) granted++;
            }

            gm.RebuildModifiers();
            if (granted == 0) return "你已经拿过了。都在背包里。";
            return $"塞给你 {granted} 件东西。按 Tab 看背包。";
        }

        private static string GrantOne(Core.GameManager gm, string itemId)
        {
            if (gm.Anomalies == null) return "";
            var inst = gm.Anomalies.Grant(itemId);
            if (inst == null)
            {
                Debug.LogWarning($"[Dialogue] grant: unknown anomaly id {itemId}");
                return "";
            }

            gm.RebuildModifiers();
            var def = gm.Anomalies.Define(itemId);
            return $"拿到了{def?.name ?? itemId}。";
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
