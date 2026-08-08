using UnityEngine;
using System.Collections.Generic;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Event system: triggers random events at POIs, processes choices, applies consequences.
    /// No random encounters while walking — events only trigger via POI interaction.
    /// </summary>
    public class EventSystem : MonoBehaviour
    {
        public static EventSystem Instance { get; private set; }

        // Current event state
        public Data.EventData activeEvent { get; private set; }
        public int selectedChoiceIndex { get; private set; }
        public string eventResultText { get; private set; }
        public bool isActive { get; private set; }

        // Events
        public System.Action<Data.EventData> OnEventStart;
        public System.Action<string> OnEventResolved; // result text

        private List<Data.EventData> eventPool;
        private HashSet<string> usedEventIds = new HashSet<string>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            eventPool = Core.GameManager.Instance.events;
        }

        /// <summary>
        /// Trigger a random event from the pool.
        /// </summary>
        public void TriggerRandomEvent()
        {
            if (eventPool == null || eventPool.Count == 0)
            {
                eventPool = Core.GameManager.Instance.events;
                if (eventPool == null || eventPool.Count == 0)
                {
                    Debug.LogWarning("[EventSystem] No events available");
                    return;
                }
            }

            // Pick a random event, prefer unused ones
            Data.EventData selected = null;
            var available = eventPool.FindAll(e => !usedEventIds.Contains(e.id));

            if (available.Count > 0)
            {
                selected = available[Random.Range(0, available.Count)];
            }
            else
            {
                // All used, reset and pick any
                usedEventIds.Clear();
                selected = eventPool[Random.Range(0, eventPool.Count)];
            }

            usedEventIds.Add(selected.id);
            activeEvent = selected;
            selectedChoiceIndex = 0;
            eventResultText = null;
            isActive = true;

            OnEventStart?.Invoke(activeEvent);
            Debug.Log($"[EventSystem] Event triggered: {activeEvent.id} - {activeEvent.title}");
        }

        /// <summary>
        /// Select a choice by index.
        /// </summary>
        public void SelectChoice(int index)
        {
            if (activeEvent == null || index < 0 || index >= activeEvent.choices.Count)
                return;

            selectedChoiceIndex = index;
        }

        /// <summary>
        /// Confirm the selected choice and apply consequences.
        /// </summary>
        public void ConfirmChoice()
        {
            if (activeEvent == null || selectedChoiceIndex < 0)
                return;

            var choice = activeEvent.choices[selectedChoiceIndex];
            var player = Core.GameManager.Instance.Player;

            string resultText = "";

            // Apply consequences
            if (choice.consequence != null)
            {
                foreach (var kvp in choice.consequence)
                {
                    ApplyConsequence(player, kvp.Key, kvp.Value, ref resultText);
                }
            }

            if (string.IsNullOrEmpty(resultText))
                resultText = "nothing happened. or did it?";

            eventResultText = resultText;
            OnEventResolved?.Invoke(resultText);

            Debug.Log($"[EventSystem] Event resolved: {activeEvent.id} -> choice {choice.id}: {resultText}");
        }

        private void ApplyConsequence(Player player, string key, float value, ref string resultText)
        {
            switch (key)
            {
                case "money":
                    player.AddMoney((int)value);
                    resultText += value > 0 ? $"gained {value} yuan. " : $"lost {Mathf.Abs((int)value)} yuan. ";
                    break;

                case "hp":
                    if (value > 0) player.Heal((int)value);
                    else player.TakeDamage((int)(-value));
                    resultText += value > 0 ? $"restored {value} HP. " : $"lost {Mathf.Abs((int)value)} HP. ";
                    break;

                case "stamina":
                    if (value > 0) player.RestoreStamina((int)value);
                    else player.SpendStamina((int)(-value));
                    resultText += value > 0 ? $"gained {value} stamina. " : $"lost {Mathf.Abs((int)value)} stamina. ";
                    break;

                case "weight":
                    player.weight += (int)value;
                    resultText += $"weight {value:+#;-#;0}. ";
                    break;

                case "perception":
                    player.stats.perception += (int)value;
                    resultText += $"perception {value:+#;-#;0}. ";
                    break;

                case "fortune":
                    player.stats.fortune += (int)value;
                    resultText += $"fortune {value:+#;-#;0}. ";
                    break;

                case "resilience":
                    player.stats.resilience += (int)value;
                    resultText += $"resilience {value:+#;-#;0}. ";
                    break;

                case "affinity":
                    string districtId = player.districtId;
                    if (!player.affinity.ContainsKey(districtId))
                        player.affinity[districtId] = 0;
                    player.affinity[districtId] += (int)value;
                    resultText += $"district affinity {value:+#;-#;0}. ";
                    break;

                default:
                    // Unknown consequence key — store in flags
                    if (!player.flags.ContainsKey(key))
                        player.flags[key] = 0;
                    player.flags[key] += (int)value;
                    break;
            }
        }

        /// <summary>
        /// Close the current event and return to exploration.
        /// </summary>
        public void CloseEvent()
        {
            activeEvent = null;
            eventResultText = null;
            isActive = false;
        }
    }
}
