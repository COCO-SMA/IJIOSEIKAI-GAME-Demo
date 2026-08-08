using System.Collections.Generic;
using UnityEngine;

namespace KunchengRPG.Game
{
    /// <summary>
    /// How the life ended. Passed in from the death check so the ending can tell a
    /// violent end apart from a life that simply ran out of years.
    /// </summary>
    public enum DeathCause
    {
        OldAge,
        Hp,
        MaxAge,
        Devoured
    }

    /// <summary>
    /// The resolved ending for one life, plus the copy to show for it.
    /// </summary>
    public class EndingResult
    {
        public string endingId;
        public Data.EndingData data;
        public int generation;
        public int ageAtDeath;
        public DeathCause cause;
        /// <summary>Why this ending won, for the log and for debugging the gate.</summary>
        public string reason;

        public string Title => data != null ? data.title : endingId;
        public bool IsVictory => data != null && data.nature == "victory";
    }

    /// <summary>
    /// Decides which ending a finished life earns.
    ///
    /// Priority order, first match wins:
    ///   1. devoured          - a violent end overrides every other state
    ///   2. becoming_local    - the win condition outranks bloodline state, so the
    ///                          "stay single and chase rooted status" route can still win
    ///   3. free_bird         - childless on purpose
    ///   4. last_native       - childless without choosing it
    ///   5. legacy_continues  - an heir exists but the gate was not met
    ///
    /// free_bird sits below becoming_local because the two describe opposite outcomes:
    /// one leaves the city, the other is claimed by it. A player who declines marriage
    /// and still reaches rooted status has clearly not "left without leaving much".
    /// </summary>
    public class EndingSystem
    {
        private Dictionary<string, Data.EndingData> endings = new Dictionary<string, Data.EndingData>();

        public void LoadData()
        {
            endings = Core.AssetLoader.LoadAllEndings();
            Debug.Log($"[EndingSystem] Loaded {endings.Count} endings");
        }

        public Data.EndingData Get(string endingId)
        {
            return endings.TryGetValue(endingId, out var data) ? data : null;
        }

        public IEnumerable<Data.EndingData> All => endings.Values;

        /// <summary>
        /// Resolve the ending for a life that has just finished.
        /// </summary>
        /// <param name="availableDistricts">District ids this build ships, for the rooted gate.</param>
        public EndingResult Resolve(Player player, CitySystem city, int generation,
                                    DeathCause cause, IEnumerable<string> availableDistricts)
        {
            string id;
            string reason;

            if (cause == DeathCause.Devoured)
            {
                id = "devoured";
                reason = "violent end";
            }
            else if (city != null && city.CheckLocalBossTrigger(availableDistricts))
            {
                id = "becoming_local";
                reason = "rooted and affinity gate met";
            }
            else if (player != null && !player.HasChildren() && player.ChoseCelibacy())
            {
                id = "free_bird";
                reason = "childless by choice";
            }
            else if (player == null || !player.HasChildren())
            {
                id = "last_native";
                reason = "childless, not chosen";
            }
            else
            {
                id = "legacy_continues";
                reason = "heir exists, gate not met";
            }

            var result = new EndingResult
            {
                endingId = id,
                data = Get(id),
                generation = generation,
                ageAtDeath = player != null ? player.age : 0,
                cause = cause,
                reason = reason
            };

            if (result.data == null)
                Debug.LogWarning($"[EndingSystem] No copy found for ending '{id}'");

            return result;
        }
    }
}
