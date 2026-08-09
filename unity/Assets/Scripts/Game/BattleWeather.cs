using UnityEngine;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Battle weather, three bands. Numeric modifiers only: making weather
    /// interact with statuses (rain douses burning, damp conducts) would be
    /// elemental countering, which the design rules out.
    /// </summary>
    public enum WeatherBand { Clear, Overcast, Harsh }

    /// <summary>
    /// Weather exists inside battle and nowhere else in v1.0. The numbers are
    /// anchored on the one place weather was already written into content: the
    /// Real Umbrella L2, "attack +30% in harsh weather, -20% in clear".
    /// </summary>
    public static class BattleWeather
    {
        /// <summary>
        /// Roughly even, with overcast the most common so the neutral band is the
        /// baseline players read the other two against.
        /// </summary>
        public static WeatherBand Roll()
        {
            float r = Random.value;
            if (r < 0.30f) return WeatherBand.Clear;
            if (r < 0.75f) return WeatherBand.Overcast;
            return WeatherBand.Harsh;
        }

        /// <summary>Straight from the Real Umbrella L2 wording.</summary>
        public static float AttackMultiplierFor(WeatherBand band)
        {
            switch (band)
            {
                case WeatherBand.Harsh: return 1.3f;
                case WeatherBand.Clear: return 0.8f;
                default: return 1.0f;
            }
        }

        /// <summary>
        /// Secondary modifiers. Harsh weather trades accuracy for the damage it
        /// grants; clear weather is easy to fight in and gives a little back.
        /// Overcast contributes nothing, which is what makes it the baseline.
        /// </summary>
        public static void ApplyTo(WeatherBand band, StatModifierSet into)
        {
            switch (band)
            {
                case WeatherBand.Harsh:
                    into.Add(StatKeys.HitRate, -0.10f);
                    into.Add(StatKeys.Dodge, 0.05f);
                    break;
                case WeatherBand.Clear:
                    into.Add(StatKeys.HitRate, 0.05f);
                    break;
            }
        }

        public static string DisplayName(WeatherBand band)
        {
            switch (band)
            {
                case WeatherBand.Clear: return "晴";
                case WeatherBand.Harsh: return "恶劣";
                default: return "阴";
            }
        }

        /// <summary>
        /// English half of the bilingual UI label. The naming bible has not
        /// registered these yet; they go in with the rest of this revision.
        /// </summary>
        public static string EnglishName(WeatherBand band)
        {
            switch (band)
            {
                case WeatherBand.Clear: return "CLEAR";
                case WeatherBand.Harsh: return "HARSH";
                default: return "OVERCAST";
            }
        }
    }
}
