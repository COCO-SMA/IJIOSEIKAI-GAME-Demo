using System;
using System.Collections.Generic;
using UnityEngine;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Six-component body system: left_leg, right_leg, left_hand, right_hand, torso, brain.
    /// Each component has efficiency/stability/growth, can hold equipment, and can have anomalies.
    /// </summary>
    [Serializable]
    public class BodyComponent
    {
        public string partName;
        public int efficiency;      // How strong (feeds attributes)
        public int stability;       // How stable (controls anomaly trigger rate)
        public float growth;        // Current growth progress
        public float growthMultiplier; // Per-component growth speed modifier
        public bool injured;
        public string anomaly;      // Active anomaly ID (null if clean)
        public string equipmentId;  // Equipped item ID (null if empty)
        public int equipmentDurability; // Remaining durability of equipmentId

        public bool HasAnomaly => !string.IsNullOrEmpty(anomaly);
        public bool HasEquipment => !string.IsNullOrEmpty(equipmentId);

        // Soft cap 50, hard cap 80
        public const int SOFT_CAP = 50;
        public const int HARD_CAP = 80;

        public void AddEfficiency(int amount)
        {
            efficiency = Math.Min(HARD_CAP, efficiency + amount);
        }

        public void AddStability(int amount)
        {
            stability = Math.Min(HARD_CAP, stability + amount);
        }

        public float AnomalyTriggerChance()
        {
            // stability 10 = 20%, stability 50 = 0%
            if (stability >= 50) return 0f;
            return (50 - stability) * 0.004f; // 0.4% per point below 50
        }
    }

    /// <summary>
    /// Player stats — Tier 2 (base attributes) and Tier 3 (derived attributes).
    /// </summary>
    [Serializable]
    public class PlayerStats
    {
        // Tier 2: Base attributes
        public int perception;   // 感知
        public int fortune;      // 机缘
        public int resilience;   // 韧性
        public int strength;     // 力量 (bridge value = avg of both hands)
        public int actionPower;  // 行动力
        public int vitality;     // 生命 (feeds HP)

        // Tier 3: Derived attributes (calculated)
        // Formulas live in StatFormulas so unmodified and modifier-adjusted stats
        // derive identically. These are the values before equipment/district modifiers.
        public int Attack => StatFormulas.Attack(strength);
        public float CritRate => StatFormulas.CritRate(fortune);
        public float CritDamage => StatFormulas.CritDamage(fortune);
        public float DodgeRate => StatFormulas.DodgeRate(actionPower);
        public float HitRate => StatFormulas.HitRate(perception);
        public float DamageReduction => StatFormulas.DamageReduction(resilience);
        public float AnomalyTriggerRate => StatFormulas.AnomalyTriggerRate(fortune);
    }

    /// <summary>
    /// Main player state. Persists across scenes, passed between generations via inheritance.
    /// </summary>
    [Serializable]
    public class Player
    {
        // Identity
        public string name;
        public int generation;
        public int age;
        public string districtId;
        public string birthLottery; // "native" or "drifter"

        // Vitals
        public int hp;
        public int maxHp;
        public int stamina;
        public int maxStamina;
        public int actionPoints;     // Remaining AP this year
        public int maxActionPoints;  // Total AP per year
        public int money;
        public int weight;           // Body weight (affects gameplay)

        // Systems
        public PlayerStats stats;
        public BodyComponent[] bodyComponents; // 6 components
        public List<string> inventory;         // Item IDs
        public Dictionary<string, int> flags;   // Custom flags (weightGain, etc.)
        public Dictionary<string, int> affinity; // Per-district affinity

        // Inheritance
        public string originId;
        public string propertyDistrict;
        public int resonanceShards;
        public List<Data.SkillRecipe> knownRecipes = new List<Data.SkillRecipe>();
        public List<string> discoveredPOIs = new List<string>();
        public Dictionary<string, int> familyRep = new Dictionary<string, int>();

        // Position
        public int tileX;
        public int tileY;
        public int facing; // 0=down, 1=left, 2=right, 3=up

        // Life stage
        public string LifeStage
        {
            get
            {
                if (age <= 5) return "infant";
                if (age <= 12) return "childhood";
                if (age <= 18) return "teen";
                if (age <= 35) return "young_adult";
                if (age <= 65) return "prime";
                if (age <= 80) return "middle_age";
                return "elderly";
            }
        }

        public bool IsAdult => age >= 19;

        // Actions per year based on life stage
        public int ActionsPerYear
        {
            get
            {
                switch (LifeStage)
                {
                    case "infant": return 0;
                    case "childhood": return 4;
                    case "teen": return 8;
                    default: return 14;
                }
            }
        }

        public bool HasChildren()
        {
            return flags != null && flags.ContainsKey("has_children") && flags["has_children"] > 0;
        }

        /// <summary>
        /// True when the player actively declined the marriage/family route rather than
        /// failing to reach it. This is what separates the "Free Bird" ending from
        /// "The Last Native" - both die childless, but only one of them chose to.
        /// </summary>
        public bool ChoseCelibacy()
        {
            return flags != null && flags.ContainsKey("chose_celibacy") && flags["chose_celibacy"] > 0;
        }

        /// <summary>
        /// Record the player's answer to the marriage beat. Declining is sticky: once you
        /// have said no on purpose, a later childless death still reads as a choice.
        /// Accepting clears the flag so someone who changes their mind and then fails to
        /// have a child gets "The Last Native" instead.
        /// </summary>
        public void SetCelibacyChoice(bool declined)
        {
            if (flags == null) flags = new Dictionary<string, int>();
            flags["chose_celibacy"] = declined ? 1 : 0;
        }

        public bool HasAnomaly()
        {
            if (bodyComponents == null) return false;
            foreach (var comp in bodyComponents)
            {
                if (comp != null && comp.HasAnomaly) return true;
            }
            return false;
        }

        public int GetTotalEfficiency()
        {
            int total = 0;
            foreach (var comp in bodyComponents)
                if (comp != null) total += comp.efficiency;
            return total;
        }

        public int GetTotalStability()
        {
            int total = 0;
            foreach (var comp in bodyComponents)
                if (comp != null) total += comp.stability;
            return total;
        }

        public void Heal(int amount)
        {
            hp = Math.Min(maxHp, hp + amount);
        }

        public void TakeDamage(int amount)
        {
            hp = Math.Max(0, hp - amount);
        }

        public void RestoreStamina(int amount)
        {
            stamina = Math.Min(maxStamina, stamina + amount);
        }

        public void SpendStamina(int amount)
        {
            stamina = Math.Max(0, stamina - amount);
        }

        public void AddMoney(int amount)
        {
            money = Math.Max(0, money + amount);
        }

        public bool SpendMoney(int amount)
        {
            if (money < amount) return false;
            money -= amount;
            return true;
        }

        public void ResetActionPoints()
        {
            maxActionPoints = ActionsPerYear;
            actionPoints = maxActionPoints;
        }

        public void ConsumeActionPoint()
        {
            actionPoints = Math.Max(0, actionPoints - 1);
        }

        public bool HasActionsLeft()
        {
            return actionPoints > 0;
        }
    }
}
