using UnityEngine;
using System.Collections.Generic;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Manages the lifecycle system: action points, aging, year-end settlement,
    /// idle action, and death checks.
    ///
    /// AP is only consumed by:
    /// - POI interaction (1 AP)
    /// - NPC dialogue (1 AP)
    /// - Idle/slack off (1 AP, age 19+)
    /// Walking is FREE.
    /// </summary>
    public class LifecycleManager : MonoBehaviour
    {
        public static LifecycleManager Instance { get; private set; }

        [Header("Year End")]
        public bool yearEndPending = false;

        // Events
        public System.Action OnYearEnd;
        public System.Action<DeathCause> OnPlayerDeath;
        public System.Action<int> OnAgeChanged; // newAge
        public System.Action<int, int> OnAPChanged; // current, max

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
        /// Consume one action point. Triggers year-end if AP runs out.
        /// </summary>
        public bool ConsumeAction()
        {
            var player = Core.GameManager.Instance.Player;
            if (player == null) return false;

            if (!player.HasActionsLeft())
            {
                // No AP left, trigger year end
                TriggerYearEnd();
                return false;
            }

            player.ConsumeActionPoint();
            OnAPChanged?.Invoke(player.actionPoints, player.maxActionPoints);

            if (!player.HasActionsLeft())
            {
                TriggerYearEnd();
            }

            return true;
        }

        /// <summary>
        /// Idle action: lose money, gain weight, lose AP.
        /// Only available for adults (age 19+).
        /// </summary>
        public bool TryIdle()
        {
            var player = Core.GameManager.Instance.Player;
            if (player == null || !player.IsAdult) return false;

            if (player.money < 50)
            {
                Debug.Log("[LifecycleManager] Too broke to slack off");
                return false;
            }

            player.SpendMoney(50);
            player.flags["weightGain"] = (player.flags.ContainsKey("weightGain") ? player.flags["weightGain"] : 0) + 1;
            player.weight += 1;

            ConsumeAction();
            return true;
        }

        /// <summary>
        /// End the current year early (player presses E).
        /// </summary>
        public void EndYearEarly()
        {
            TriggerYearEnd();
        }

        /// <summary>
        /// Trigger year-end settlement: age up, reset AP, check death.
        /// </summary>
        public void TriggerYearEnd()
        {
            if (yearEndPending) return;
            yearEndPending = true;

            var player = Core.GameManager.Instance.Player;
            if (player == null) return;

            // Year-end processing
            player.age++;
            OnAgeChanged?.Invoke(player.age);

            // Reset AP for new year
            player.ResetActionPoints();
            OnAPChanged?.Invoke(player.actionPoints, player.maxActionPoints);

            // Natural HP regeneration
            int regen = 5 + player.stats.resilience / 5;
            player.Heal(regen);

            // Death checks
            if (CheckDeath(player, out DeathCause cause))
            {
                OnPlayerDeath?.Invoke(cause);
                yearEndPending = false;
                return;
            }

            yearEndPending = false;
            OnYearEnd?.Invoke();

            Debug.Log($"[LifecycleManager] Year end. Age: {player.age}, Stage: {player.LifeStage}, AP: {player.actionPoints}/{player.maxActionPoints}");
        }

        /// <summary>
        /// Check if player should die based on age and HP.
        /// Death curve: 90 normal, 120 with items, 150 forced.
        /// </summary>
        private bool CheckDeath(Player player, out DeathCause cause)
        {
            cause = DeathCause.OldAge;

            // HP death
            if (player.hp <= 0)
            {
                cause = DeathCause.Hp;
                Debug.Log("[LifecycleManager] Player died: HP reached 0");
                return true;
            }

            // Age death
            if (player.age >= 150)
            {
                cause = DeathCause.MaxAge;
                Debug.Log("[LifecycleManager] Player died: maximum age reached");
                return true;
            }

            if (player.age >= 90)
            {
                // Increasing death chance after 90
                float chance = (player.age - 90) * 0.02f; // 2% per year after 90
                if (Random.value < chance)
                {
                    cause = DeathCause.OldAge;
                    Debug.Log($"[LifecycleManager] Player died: old age (age {player.age}, chance {chance:P})");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// End the life immediately with an explicit cause. Combat and stability-collapse
        /// paths use this so the ending can tell being devoured apart from dying old.
        /// </summary>
        public void KillPlayer(DeathCause cause)
        {
            if (yearEndPending) return;
            var player = Core.GameManager.Instance.Player;
            if (player == null) return;

            yearEndPending = true;
            Debug.Log($"[LifecycleManager] Player died: {cause} (age {player.age})");
            OnPlayerDeath?.Invoke(cause);
            yearEndPending = false;
        }
    }
}
