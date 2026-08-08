using UnityEngine;
using System.Collections.Generic;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Turn-based combat system for Nemesis encounters.
    /// Damage formula: attack * actionModifier * crit * (1 - damageReduction) * anomalyMultiplier
    /// </summary>
    public class CombatSystem : MonoBehaviour
    {
        public static CombatSystem Instance { get; private set; }

        // Combat state
        public Data.EnemyData currentEnemy { get; private set; }
        public int enemyHp { get; private set; }
        public int playerHp { get; private set; }
        public bool isPlayerTurn { get; private set; }
        public bool isActive { get; private set; }
        public List<string> combatLog { get; private set; } = new List<string>();

        // Actions: 0=Attack, 1=Act Normal, 2=Use Item, 3=Talk, 4=Run
        public int selectedAction { get; private set; }
        public const int ACTION_COUNT = 5;

        // Events
        public System.Action<Data.EnemyData> OnCombatStart;
        public System.Action OnCombatEnd;
        public System.Action<string> OnLogMessage;
        public System.Action<int, int, int, int> OnHpChanged; // playerHp, playerMaxHp, enemyHp, enemyMaxHp

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
        /// Start combat with a specific enemy.
        /// </summary>
        public void StartCombat(Data.EnemyData enemy)
        {
            currentEnemy = enemy;
            var player = Core.GameManager.Instance.Player;

            enemyHp = enemy.stats.hp;
            playerHp = player.hp;
            isPlayerTurn = true; // TODO: calculate based on combat type
            isActive = true;
            selectedAction = 0;
            combatLog.Clear();

            Log($"--- {enemy.eventName} ---");
            Log(enemy.appearance);
            Log("");

            OnCombatStart?.Invoke(enemy);
            OnHpChanged?.Invoke(playerHp, player.maxHp, enemyHp, enemy.stats.hp);

            Debug.Log($"[CombatSystem] Combat started: {enemy.name}");
        }

        /// <summary>
        /// Player selects an action.
        /// </summary>
        public void SelectAction(int index)
        {
            selectedAction = Mathf.Clamp(index, 0, ACTION_COUNT - 1);
        }

        /// <summary>
        /// Execute the player's selected action.
        /// </summary>
        public void ExecutePlayerAction()
        {
            if (!isActive || !isPlayerTurn) return;

            var player = Core.GameManager.Instance.Player;

            switch (selectedAction)
            {
                case 0: // Attack
                    PlayerAttack();
                    break;

                case 1: // Act Normal (装没事)
                    TryActNormal();
                    break;

                case 2: // Use Item
                    Log("you fumble through your pockets. nothing useful.");
                    EndPlayerTurn();
                    break;

                case 3: // Talk (说点什么)
                    Log($"you say something. the air shifts. {currentEnemy.name} doesn't care.");
                    EndPlayerTurn();
                    break;

                case 4: // Run
                    TryRun();
                    break;
            }
        }

        private void PlayerAttack()
        {
            var player = Core.GameManager.Instance.Player;
            float baseAttack = player.stats.Attack;

            // Crit roll
            bool isCrit = Random.value < player.stats.CritRate;
            float critMult = isCrit ? player.stats.CritDamage : 1.0f;

            // Damage
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseAttack * critMult) - currentEnemy.stats.defense / 2);

            enemyHp = Mathf.Max(0, enemyHp - damage);

            if (isCrit)
                Log($"CRITICAL! you hit {currentEnemy.name} for {damage}!");
            else
                Log($"you hit {currentEnemy.name} for {damage}.");

            OnHpChanged?.Invoke(playerHp, player.maxHp, enemyHp, currentEnemy.stats.hp);

            if (enemyHp <= 0)
            {
                WinCombat();
                return;
            }

            EndPlayerTurn();
        }

        private void TryActNormal()
        {
            var player = Core.GameManager.Instance.Player;
            // Probability: 10% + stability * 0.3%
            float chance = 0.10f + player.GetTotalStability() * 0.003f;

            if (Random.value < chance)
            {
                Log($"you act like nothing is happening. {currentEnemy.name} ... buys it. it walks away.");
                EndCombat(false); // No drops
            }
            else
            {
                Log($"you try to act normal. {currentEnemy.name} is not convinced.");
                EndPlayerTurn();
            }
        }

        private void TryRun()
        {
            var player = Core.GameManager.Instance.Player;
            // Run chance based on action power vs enemy speed
            float chance = 0.3f + player.stats.actionPower * 0.01f - currentEnemy.stats.speed * 0.01f;
            chance = Mathf.Clamp(chance, 0.1f, 0.8f);

            if (Random.value < chance)
            {
                Log("you run. you don't look back.");
                EndCombat(false);
            }
            else
            {
                Log("you try to run. you can't.");
                EndPlayerTurn();
            }
        }

        private void EndPlayerTurn()
        {
            isPlayerTurn = false;

            // Enemy turn (with small delay for readability)
            // In Unity, this would be handled by a coroutine or timer
            EnemyTurn();
        }

        private void EnemyTurn()
        {
            if (!isActive) return;

            int damage = Mathf.Max(1, currentEnemy.stats.attack - Core.GameManager.Instance.Player.stats.DamageReduction > 0
                ? Mathf.RoundToInt(currentEnemy.stats.attack * (1 - Core.GameManager.Instance.Player.stats.DamageReduction))
                : 1);

            playerHp = Mathf.Max(0, playerHp - damage);
            Log($"{currentEnemy.name} hits you for {damage}.");

            var player = Core.GameManager.Instance.Player;
            player.TakeDamage(damage);
            OnHpChanged?.Invoke(playerHp, player.maxHp, enemyHp, currentEnemy.stats.hp);

            if (playerHp <= 0)
            {
                LoseCombat();
                return;
            }

            isPlayerTurn = true;
        }

        private void WinCombat()
        {
            Log("");
            Log($"you survived. {currentEnemy.name} dissolves.");
            Log($"resonance shards: +{currentEnemy.drops.resonanceShards}");

            // Apply drops
            var player = Core.GameManager.Instance.Player;
            // Add resonance shards as items
            for (int i = 0; i < currentEnemy.drops.resonanceShards; i++)
            {
                player.inventory.Add("resonance_shard");
            }

            // Add anomaly if any
            if (!string.IsNullOrEmpty(currentEnemy.drops.anomaly))
            {
                Log($"anomaly gained: {currentEnemy.drops.anomaly}");
                // Assign to random component without anomaly
                AssignAnomaly(currentEnemy.drops.anomaly);
            }

            EndCombat(true);
        }

        private void LoseCombat()
        {
            Log("");
            Log("you collapsed. the world goes dark.");
            Log("...but you wake up. something is different.");

            // Non-lethal combat: lose money, equipment damage
            var player = Core.GameManager.Instance.Player;
            int lostMoney = player.money / 5;
            player.SpendMoney(lostMoney);
            Log($"lost {lostMoney} yuan. equipment damaged.");

            EndCombat(false);
        }

        private void AssignAnomaly(string anomalyId)
        {
            var player = Core.GameManager.Instance.Player;
            // Find first component without anomaly
            for (int i = 0; i < player.bodyComponents.Length; i++)
            {
                if (!player.bodyComponents[i].HasAnomaly)
                {
                    player.bodyComponents[i].anomaly = anomalyId;
                    return;
                }
            }
            // All slots full — replace the first one
            player.bodyComponents[0].anomaly = anomalyId;
        }

        private void EndCombat(bool victory)
        {
            isActive = false;
            currentEnemy = null;
            combatLog.Clear();
            OnCombatEnd?.Invoke();
        }

        private void Log(string message)
        {
            combatLog.Add(message);
            OnLogMessage?.Invoke(message);
        }
    }
}
