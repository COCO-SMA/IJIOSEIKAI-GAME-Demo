using UnityEngine;
using UnityEngine.SceneManagement;

namespace KunchengRPG.Scenes
{
    /// <summary>
    /// Main explore scene controller.
    /// Coordinates player movement, map loading, interaction, events, dialogue, combat, and lifecycle.
    /// </summary>
    public class ExploreSceneController : MonoBehaviour
    {
        [Header("Systems")]
        public Game.MapController mapController;
        public Game.PlayerController playerController;
        public UI.HUDController hud;
        public UI.EventPanel eventPanel;
        public UI.DialoguePanel dialoguePanel;

        [Header("Camera")]
        public Camera mainCamera;
        public float cameraLerpSpeed = 5f;

        [Header("Prefabs")]
        public GameObject npcPrefab;
        public GameObject poiMarkerPrefab;

        private string previousDistrictId;
        private bool transitioning = false;

        void Start()
        {
            var gm = Core.GameManager.Instance;
            if (gm == null || gm.Player == null)
            {
                Debug.LogError("[ExploreScene] No GameManager or Player!");
                return;
            }

            // Load district map
            var district = gm.GetCurrentDistrict();
            if (district == null)
            {
                Debug.LogError($"[ExploreScene] District not found: {gm.currentDistrictId}");
                return;
            }

            EnsureCombatRuntime();
            mapController.LoadDistrict(district);

            // Place player at spawn
            var spawn = mapController.GetSpawnPosition(previousDistrictId);
            playerController.SetPosition(spawn.x, spawn.y);
            gm.Player.tileX = spawn.x;
            gm.Player.tileY = spawn.y;

            // Set up callbacks
            playerController.OnStepComplete += OnPlayerStep;
            mapController.OnExitReached += OnExitReached;
            mapController.OnNPCTalk += OnNPCTalk;
            mapController.OnPOIInteract += OnPOIInteract;

            // Show intro message
            if (hud != null)
            {
                hud.ShowMessage($"Gen {gm.Player.generation}. {gm.Player.name}, age {gm.Player.age}.\nKuncheng does not know you yet.\nArrow keys to move. Space to interact.\n[I] Slack off  [E] End Year");
            }

            Debug.Log($"[ExploreScene] Started in {district.id}. Player at ({spawn.x}, {spawn.y})");
        }

        void Update()
        {
            var gm = Core.GameManager.Instance;
            if (gm == null || gm.Player == null) return;

            // Don't process input if in event/dialogue/combat
            if (Game.EventSystem.Instance != null && Game.EventSystem.Instance.isActive) return;
            if (Game.DialogueSystem.Instance != null && Game.DialogueSystem.Instance.isActive) return;
            if (Game.CombatSystem.Instance != null && Game.CombatSystem.Instance.isActive) return;
            // Also blocked while the result screen is still up, so its dismiss key
            // does not double as a map interaction.
            if (UI.CombatPanel.Instance != null && UI.CombatPanel.Instance.IsShowing) return;

            // Handle input
            var input = Core.InputManager.Instance;
            if (input == null) return;

            // Interaction
            if (input.ConfirmPressed)
            {
                input.ConsumeConfirm();
                mapController.TryInteract();
                return;
            }

            // Idle action
            if (input.IdlePressed && gm.Player.IsAdult)
            {
                input.ConsumeIdle();
                if (Game.LifecycleManager.Instance.TryIdle())
                {
                    if (hud != null)
                        hud.ShowMessage("You slacked off. -$50. Gained weight. Lost an action.");
                }
                else
                {
                    if (hud != null)
                        hud.ShowMessage("Too broke to slack off.");
                }
                return;
            }

            // End year
            if (input.EndYearPressed)
            {
                input.ConsumeEndYear();
                Game.LifecycleManager.Instance.EndYearEarly();
                if (hud != null)
                    hud.ShowMessage("Year ended. Moving on...");
                return;
            }

            // Update HUD prompts
            if (hud != null)
            {
                string prompt = hud.GetInteractionPrompt(mapController);
                string idle = hud.GetIdlePrompt();
                hud.ShowPrompt(string.IsNullOrEmpty(prompt) ? idle : prompt);
            }

            // Camera follow
            if (mainCamera != null && playerController != null)
            {
                Vector3 target = playerController.transform.position;
                target.z = -10;
                mainCamera.transform.position = Vector3.Lerp(
                    mainCamera.transform.position, target, cameraLerpSpeed * Time.deltaTime);
            }
        }

        private void OnPlayerStep(int x, int y)
        {
            var gm = Core.GameManager.Instance;
            gm.Player.tileX = x;
            gm.Player.tileY = y;

            // Check proximity
            mapController.CheckProximity(x, y);

            // Check exit
            if (mapController.nearbyExit != null)
            {
                OnExitReached(mapController.nearbyExit);
            }
        }

        private void OnExitReached(Data.ExitData exit)
        {
            if (transitioning) return;
            transitioning = true;

            Debug.Log($"[ExploreScene] Exit reached: {exit.target}");

            // Save current district as previous
            previousDistrictId = Core.GameManager.Instance.currentDistrictId;

            // Load new district
            Core.GameManager.Instance.currentDistrictId = exit.target;

            // Reload scene
            SceneManager.LoadScene("ExploreScene");
        }

        private void OnNPCTalk(Data.NPCData npc)
        {
            Debug.Log($"[ExploreScene] Talking to NPC: {npc.name}");
            Game.DialogueSystem.Instance.StartDialogue(npc);
        }

        private void OnPOIInteract(Data.POIData poi)
        {
            Debug.Log($"[ExploreScene] POI interaction: {poi.name}");

            // A POI can carry an encounter instead of an event, so putting a fight
            // on a map stays a content edit rather than a code change.
            if (poi.type == "enemy" && !string.IsNullOrEmpty(poi.enemyId))
            {
                if (TryStartCombat(poi.enemyId)) return;
            }

            Game.EventSystem.Instance.TriggerRandomEvent();
        }

        private bool TryStartCombat(string enemyId)
        {
            var gm = Core.GameManager.Instance;
            if (gm?.enemies == null || !gm.enemies.TryGetValue(enemyId, out var enemy))
            {
                Debug.LogWarning($"[ExploreScene] Unknown enemy: {enemyId}");
                return false;
            }
            if (Game.CombatSystem.Instance == null)
            {
                Debug.LogWarning("[ExploreScene] No CombatSystem in the scene.");
                return false;
            }

            Game.CombatSystem.Instance.StartCombat(enemy);
            return true;
        }

        /// <summary>
        /// The scene predates grid combat and has no CombatSystem or panel wired
        /// in, so build them here. Doing it in code rather than in the scene file
        /// keeps the encounter runnable in batchmode too.
        /// </summary>
        private void EnsureCombatRuntime()
        {
            if (Game.CombatSystem.Instance != null && UI.CombatPanel.Instance != null) return;

            var host = new GameObject("CombatRuntime");
            if (Game.CombatSystem.Instance == null)
                host.AddComponent<Game.CombatSystem>();
            if (UI.CombatPanel.Instance == null)
                host.AddComponent<UI.CombatPanel>();
        }
    }
}
