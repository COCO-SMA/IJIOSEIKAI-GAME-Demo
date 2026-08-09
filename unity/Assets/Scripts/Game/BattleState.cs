using System.Collections.Generic;
using UnityEngine;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Everything about one battle in progress: the grid, the rule being played
    /// to, the weather, and the action history. Pure model, so batchmode tests
    /// can play a whole fight without a scene.
    /// </summary>
    public class BattleState
    {
        public readonly BattleGrid grid;
        public readonly VictoryCondition condition;
        public readonly BattleActionLog log = new BattleActionLog();

        public WeatherBand weather;

        /// <summary>1-based; incremented by <see cref="EndTurn"/>.</summary>
        public int turn = 1;

        /// <summary>Score rule: damage dealt by each side becomes points.</summary>
        public int playerScore;
        public int enemyScore;

        /// <summary>Escape / Control rules: consecutive turns the condition has held.</summary>
        public int streak;

        /// <summary>Resource rule: running total of whatever is being collected.</summary>
        public int resourceTotal;

        public BattleUnit player;

        public BattleState(BattleGrid grid, VictoryCondition condition, WeatherBand weather)
        {
            this.grid = grid;
            this.condition = condition;
            this.weather = weather;
        }

        public bool IsNemesisScale => grid.width >= BattleGrid.NemesisSize;

        /// <summary>Living units on the enemy side.</summary>
        public int LivingEnemies => CountSide(true);

        /// <summary>Living units on the player's side, allies included.</summary>
        public int LivingFriendlies => CountSide(false);

        private int CountSide(bool enemies)
        {
            int n = 0;
            foreach (var u in grid.LivingUnits)
                if ((u.side == BattleSide.Enemy) == enemies) n++;
            return n;
        }

        /// <summary>
        /// Modifiers acting on a unit from weather and the cell it stands on.
        /// Equipment modifiers stay with GameManager; this covers the battlefield
        /// only, so both feed the same StatModifierSet without either owning it.
        /// </summary>
        public void CollectBattlefieldModifiers(BattleUnit unit, StatModifierSet into)
        {
            BattleWeather.ApplyTo(weather, into);
            grid.CollectTerrainModifiers(unit, into);
        }

        public void CreditDamage(BattleSide dealer, int damage)
        {
            if (damage <= 0) return;
            if (dealer == BattleSide.Enemy) enemyScore += damage;
            else playerScore += damage;
        }

        /// <summary>
        /// Advance the clock and update whatever the active rule accumulates.
        /// Call once per full round, after every unit has acted.
        /// </summary>
        public void EndTurn()
        {
            UpdateStreak();
            turn++;
            grid.BeginTurn();
        }

        private void UpdateStreak()
        {
            switch (condition.rule)
            {
                case VictoryRule.Escape:
                    int d = player == null ? -1 : grid.DistanceToNearestOpponent(player);
                    // No opponents left counts as distance kept, not as a broken streak.
                    if (d < 0 || d >= condition.requiredDistance) streak++;
                    else streak = 0;
                    break;

                case VictoryRule.Control:
                    streak = HoldsAllControlCells() ? streak + 1 : 0;
                    break;
            }
        }

        private bool HoldsAllControlCells()
        {
            if (condition.controlCells == null || condition.controlCells.Count == 0)
                return false;
            foreach (var cell in condition.controlCells)
            {
                var u = grid.UnitAt(cell);
                if (u == null || u.side == BattleSide.Enemy) return false;
            }
            return true;
        }

        /// <summary>
        /// Resolve the current rule. Death and total enemy wipe are checked first
        /// for every rule: a fight cannot continue with nobody standing, whatever
        /// the rule says the goal is.
        /// </summary>
        public BattleOutcome Evaluate()
        {
            if (LivingFriendlies == 0) return BattleOutcome.Defeat;

            bool clockDone = condition.HasTurnLimit && turn > condition.turnLimit;

            switch (condition.rule)
            {
                case VictoryRule.Escape:
                    if (streak >= condition.turnLimit) return BattleOutcome.Victory;
                    return BattleOutcome.Ongoing;

                case VictoryRule.Survival:
                    if (clockDone) return BattleOutcome.Victory;
                    if (LivingEnemies == 0) return BattleOutcome.Victory;
                    return BattleOutcome.Ongoing;

                case VictoryRule.Score:
                    if (LivingEnemies == 0) return BattleOutcome.Victory;
                    if (!clockDone) return BattleOutcome.Ongoing;
                    // A tie goes to the defender; the attacker had the clock to win it.
                    return playerScore > enemyScore
                        ? BattleOutcome.Victory : BattleOutcome.Defeat;

                case VictoryRule.Control:
                    if (streak >= condition.turnLimit) return BattleOutcome.Victory;
                    if (clockDone) return BattleOutcome.Defeat;
                    return BattleOutcome.Ongoing;

                case VictoryRule.Protect:
                    var guarded = FindUnit(condition.protectUnitId);
                    if (guarded != null && !guarded.IsAlive) return BattleOutcome.Defeat;
                    if (LivingEnemies == 0) return BattleOutcome.Victory;
                    if (clockDone) return BattleOutcome.Victory;
                    return BattleOutcome.Ongoing;

                case VictoryRule.Attrition:
                case VictoryRule.Resource:
                    if (condition.threshold > 0 && resourceTotal >= condition.threshold)
                        return BattleOutcome.Victory;
                    if (LivingEnemies == 0) return BattleOutcome.Victory;
                    if (clockDone) return BattleOutcome.Defeat;
                    return BattleOutcome.Ongoing;

                default:
                    if (LivingEnemies == 0) return BattleOutcome.Victory;
                    if (clockDone) return BattleOutcome.Defeat;
                    return BattleOutcome.Ongoing;
            }
        }

        public BattleUnit FindUnit(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var u in grid.Units)
                if (u.id == id) return u;
            return null;
        }

        /// <summary>Turn order for the round: fastest first, per GDD 29.2.</summary>
        public List<BattleUnit> InitiativeOrder()
        {
            var order = new List<BattleUnit>();
            foreach (var u in grid.LivingUnits) order.Add(u);
            order.Sort((a, b) => b.speed.CompareTo(a.speed));
            return order;
        }
    }
}
