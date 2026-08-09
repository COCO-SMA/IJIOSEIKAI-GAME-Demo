using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KunchengRPG.UI
{
    /// <summary>
    /// HUD overlay for the explore scene.
    /// Shows: player name, age, AP, money, HP, stamina, district name.
    /// Interaction prompts: "[Space] Talk", "[Space] Investigate", "[I] Slack off", "[E] End Year"
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Status Panel")]
        public Text nameText;
        public Text ageText;
        public Text stageText;
        public Text apText;
        public Text moneyText;
        public Text hpText;
        public Text staminaText;
        public Text districtText;

        [Header("Prompt Panel")]
        public GameObject promptPanel;
        public Text promptText;

        [Header("Message")]
        public GameObject messagePanel;
        public Text messageText;

        private float bobTimer = 0f;

        void Update()
        {
            var player = Core.GameManager.Instance?.Player;
            if (player == null) return;

            // Update status
            if (nameText) nameText.text = player.name;
            if (ageText) ageText.text = $"Age {player.age}";
            if (stageText) stageText.text = player.LifeStage;
            if (apText) apText.text = $"AP: {player.actionPoints}/{player.maxActionPoints}";
            if (moneyText) moneyText.text = $"${player.money}";
            if (hpText) hpText.text = $"HP: {player.hp}/{player.maxHp}";
            if (staminaText) staminaText.text = $"STA: {player.stamina}/{player.maxStamina}";

            var district = Core.GameManager.Instance.GetCurrentDistrict();
            if (districtText && district != null)
                districtText.text = district.name;

            // Bob animation for prompts
            bobTimer += Time.deltaTime;
        }

        /// <summary>
        /// Show interaction prompt.
        /// </summary>
        public void ShowPrompt(string text)
        {
            if (promptPanel == null || promptText == null) return;

            if (string.IsNullOrEmpty(text))
            {
                promptPanel.SetActive(false);
                return;
            }

            promptPanel.SetActive(true);
            // Blink effect
            if (Mathf.FloorToInt(bobTimer * 2) % 2 == 0)
                promptText.text = text;
            else
                promptText.text = "";
        }

        /// <summary>
        /// Show a temporary message.
        /// </summary>
        public void ShowMessage(string text)
        {
            if (messagePanel == null || messageText == null) return;
            messagePanel.SetActive(true);
            messageText.text = text;
        }

        /// <summary>
        /// Hide message.
        /// </summary>
        public void HideMessage()
        {
            if (messagePanel != null)
                messagePanel.SetActive(false);
        }

        /// <summary>
        /// Get the prompt text based on nearby interactables.
        /// </summary>
        public string GetInteractionPrompt(Game.MapController mapController)
        {
            if (mapController == null) return "";

            if (mapController.nearbyNpc != null)
                return $"[SPACE] Talk to {mapController.nearbyNpc.name}";

            if (mapController.nearbyPoi != null)
            {
                var poi = mapController.nearbyPoi;
                if (poi.type == "enemy")
                    return $"[SPACE] 打一场：{poi.name}";
                return $"[SPACE] Investigate {poi.name}";
            }

            return "";
        }

        /// <summary>
        /// Get idle prompt for adults.
        /// </summary>
        public string GetIdlePrompt()
        {
            var player = Core.GameManager.Instance?.Player;
            if (player == null || !player.IsAdult) return "";
            return "[I] Slack off (-$50)   [E] End Year";
        }
    }
}
