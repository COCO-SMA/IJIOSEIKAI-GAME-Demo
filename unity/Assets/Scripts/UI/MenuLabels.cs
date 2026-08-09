namespace KunchengRPG.UI
{
    /// <summary>
    /// Chinese labels and the slot-category mapping the menu needs.
    ///
    /// Anomaly data declares a slot *category* ("hand", "leg", "carry") while the
    /// body diagram has concrete positions ("left_hand", "carry_2"). Keeping the
    /// translation here means neither the data nor the six-component model has to
    /// bend: 烧鹅刀 is a hand item, and the player decides which hand.
    /// </summary>
    public static class MenuLabels
    {
        public static string SlotLabel(string slot)
        {
            switch (slot)
            {
                case "brain":      return "大脑";
                case "torso":      return "躯干";
                case "left_hand":  return "左手";
                case "right_hand": return "右手";
                case "left_leg":   return "左腿";
                case "right_leg":  return "右腿";
                case "carry_1":    return "携带一";
                case "carry_2":    return "携带二";
                case "carry_3":    return "携带三";
                case "hand":       return "手";
                case "leg":        return "腿";
                case "carry":      return "携带";
                default:           return slot ?? "?";
            }
        }

        /// <summary>
        /// Naming Bible 4.3. The scale gets more casual as it gets more dangerous;
        /// that reversed slope is the joke. Do not "fix" it into 传说/史诗.
        /// </summary>
        public static string RarityLabel(string rarity)
        {
            switch (rarity)
            {
                case "normal": return "普通";
                case "uneasy": return "不对劲";
                case "glitch": return "出问题了";
                case "absurd": return "离谱了";
                case "lethal": return "要命了";
                case "void":   return "已经无所谓了";
                default:       return rarity ?? "?";
            }
        }

        /// <summary>Concrete body positions a data-declared category can occupy.</summary>
        public static string[] SlotsFor(string category)
        {
            switch (category)
            {
                case "brain": return new[] { "brain" };
                case "torso": return new[] { "torso" };
                case "hand":  return new[] { "right_hand", "left_hand" };
                case "leg":   return new[] { "right_leg", "left_leg" };
                case "carry": return new[] { "carry_1", "carry_2", "carry_3" };
                default:      return new string[0];
            }
        }

        public static bool CanEquipTo(string category, string slot)
        {
            foreach (var s in SlotsFor(category))
                if (s == slot) return true;
            return false;
        }
    }
}
