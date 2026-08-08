using UnityEngine;

namespace KunchengRPG.Core
{
    /// <summary>
    /// Central game state manager. Singleton persisted across scenes.
    /// Holds player state, current district, and all loaded game data.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        public GameState state = GameState.Title;
        public string currentDistrictId;
        public int generation = 1;

        /// <summary>
        /// District the player just came from. Lives here rather than on
        /// ExploreSceneController because that controller is destroyed by the
        /// scene reload that district transitions use.
        /// </summary>
        public string previousDistrictId;

        // Cross-generation systems. Plain classes, owned by this singleton.
        public SaveManager Save { get; private set; }
        public Game.CitySystem City { get; private set; }
        public Game.InheritanceSystem Inheritance { get; private set; }
        public Game.EndingSystem Endings { get; private set; }

        /// <summary>The ending the most recent life earned. Read by the result screen.</summary>
        public Game.EndingResult LastEnding { get; private set; }

        [Header("Loaded Data")]
        public System.Collections.Generic.Dictionary<string, Data.DistrictData> districts;
        public System.Collections.Generic.Dictionary<string, Data.OriginData> origins;
        public System.Collections.Generic.Dictionary<string, Data.EnemyData> enemies;
        public System.Collections.Generic.Dictionary<string, Data.ItemData> items;
        public System.Collections.Generic.List<Data.EventData> events;

        // Player reference (created at character creation)
        public Game.Player Player { get; set; }

        // Events
        public System.Action<GameState> OnStateChanged;

        /// <summary>Fired after a death produced an inheritance for the next generation.</summary>
        public System.Action<Data.Inheritance> OnGenerationEnded;

        /// <summary>Fired with the resolved ending once a life is over.</summary>
        public System.Action<Game.EndingResult> OnEndingReached;

        public enum GameState
        {
            Title,
            DistrictSelect,
            OriginSelect,
            NameInput,
            Exploring,
            InEvent,
            InDialogue,
            InCombat,
            YearEnd,
            Inheritance,
            GameOver
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Save = new SaveManager();
            City = new Game.CitySystem();
            Inheritance = new Game.InheritanceSystem();
            Endings = new Game.EndingSystem();

            Debug.Log("[GameManager] Initialized");
        }

        void Start()
        {
            LoadAllData();

            // Resume the lineage if this machine already has a save; otherwise start
            // a fresh one. Either way City reflects the save before play begins.
            var save = Save.Load() ?? Save.CreateNewSave();
            generation = Mathf.Max(1, save.generation);
            City.InitFromSave(save);
        }

        void LoadAllData()
        {
            districts = AssetLoader.LoadAllDistricts();
            origins = AssetLoader.LoadAllOrigins();
            enemies = AssetLoader.LoadAllEnemies();
            items = AssetLoader.LoadAllItems();
            events = AssetLoader.LoadEvents();
            Endings.LoadData();

            Debug.Log($"[GameManager] Data loaded: {districts.Count} districts, " +
                      $"{origins.Count} origins, {enemies.Count} enemies, " +
                      $"{items.Count} items, {events.Count} events");
        }

        public void SetState(GameState newState)
        {
            if (state == newState) return;
            var oldState = state;
            state = newState;
            Debug.Log($"[GameManager] State: {oldState} -> {newState}");
            OnStateChanged?.Invoke(newState);
        }

        public Data.DistrictData GetCurrentDistrict()
        {
            if (districts == null || !districts.ContainsKey(currentDistrictId))
                return null;
            return districts[currentDistrictId];
        }

        /// <summary>
        /// Start a new game with the given parameters.
        /// </summary>
        public void StartNewGame(string districtId, string originId, string playerName)
        {
            CreateCharacter(districtId, originId, playerName);

            // An heir inherits before their first action; a founder has nothing pending.
            var pending = Save.Current?.pendingInheritance;
            if (pending != null)
            {
                Inheritance.ApplyInheritance(Player, pending);
                Save.Current.pendingInheritance = null;
            }

            PersistProgress();
            SetState(GameState.Exploring);
            Debug.Log($"[GameManager] Gen {generation} started: {playerName}, age {Player.age}, " +
                      $"district {districtId}, origin {originId}");
        }

        /// <summary>
        /// Roll a character from an origin. Does not touch inheritance or game state.
        /// </summary>
        private void CreateCharacter(string districtId, string originId, string playerName)
        {
            currentDistrictId = districtId;
            var origin = origins[originId];

            Player = new Game.Player
            {
                name = playerName,
                generation = generation,
                originId = originId,
                age = 6,
                districtId = districtId,
                birthLottery = origin.birthLottery,
                money = origin.startingMoney,
                hp = 50,
                maxHp = 50,
                stamina = 30,
                maxStamina = 30,
                actionPoints = 4,
                maxActionPoints = 4,
                weight = 50,
                stats = new Game.PlayerStats
                {
                    perception = 10 + GetStatMod(origin, "perception"),
                    fortune = 10 + GetStatMod(origin, "fortune"),
                    resilience = 10 + GetStatMod(origin, "resilience"),
                    strength = 10,
                    actionPower = 10,
                    vitality = 10
                },
                bodyComponents = new Game.BodyComponent[6],
                inventory = new System.Collections.Generic.List<string>(origin.startingItems ?? new System.Collections.Generic.List<string>()),
                flags = new System.Collections.Generic.Dictionary<string, int>(),
                affinity = new System.Collections.Generic.Dictionary<string, int>()
            };

            // Initialize body components
            string[] partNames = { "left_leg", "right_leg", "left_hand", "right_hand", "torso", "brain" };
            for (int i = 0; i < 6; i++)
            {
                float affinity = 1.0f;
                if (origin.componentAffinity != null && origin.componentAffinity.ContainsKey(partNames[i]))
                    affinity = origin.componentAffinity[partNames[i]];

                Player.bodyComponents[i] = new Game.BodyComponent
                {
                    partName = partNames[i],
                    efficiency = 10,
                    stability = 10,
                    growth = 1.0f,
                    growthMultiplier = affinity,
                    injured = false,
                    anomaly = null
                };
            }
        }

        private int GetStatMod(Data.OriginData origin, string stat)
        {
            if (origin.statModifiers == null || !origin.statModifiers.ContainsKey(stat))
                return 0;
            return origin.statModifiers[stat];
        }

        /// <summary>
        /// Triggered when player dies. Handles inheritance or game over.
        /// </summary>
        public void OnPlayerDeath(Game.DeathCause cause = Game.DeathCause.OldAge)
        {
            if (Player == null) return;

            Debug.Log($"[GameManager] Gen {generation} died at age {Player.age} ({cause})");

            var save = Save.Current ?? Save.CreateNewSave(currentDistrictId, Player.originId);

            // Resolved before decay runs, so the rooted gate is judged on the affinity
            // this life actually finished with.
            LastEnding = Endings.Resolve(Player, City, generation, cause,
                                         districts != null ? districts.Keys : null);
            Debug.Log($"[GameManager] Ending: {LastEnding.endingId} ({LastEnding.reason})");

            // Built and banked even without an heir, so the family log records this life
            // either way.
            var inheritance = Inheritance.CreateInheritance(Player, save.familyLog, City);

            City.ApplyCrossGenDecay();
            Inheritance.ApplyCrossGenNpcMemory(City, generation);

            // A victory ends the run even when an heir exists - there is nothing left to
            // inherit toward once the city has claimed you.
            if (LastEnding.IsVictory || !Player.HasChildren())
            {
                save.pendingInheritance = null;
                save.lastEndingId = LastEnding.endingId;
                if (!save.unlockedEndings.Contains(LastEnding.endingId))
                    save.unlockedEndings.Add(LastEnding.endingId);
                City.WriteToSave(save);
                Save.Save(save);

                OnGenerationEnded?.Invoke(inheritance);
                OnEndingReached?.Invoke(LastEnding);
                SetState(GameState.GameOver);
                Debug.Log($"[GameManager] Run ended: {LastEnding.endingId}");
                return;
            }

            generation++;
            save.generation = generation;
            save.pendingInheritance = inheritance;
            save.lastEndingId = LastEnding.endingId;
            if (!save.unlockedEndings.Contains(LastEnding.endingId))
                save.unlockedEndings.Add(LastEnding.endingId);
            City.WriteToSave(save);
            Save.Save(save);

            OnGenerationEnded?.Invoke(inheritance);
            OnEndingReached?.Invoke(LastEnding);

            // The heir picks their own district and origin, so hand control back to
            // selection instead of auto-rolling a character here.
            SetState(GameState.Inheritance);
            Debug.Log($"[GameManager] Inheritance ready for gen {generation}: " +
                      $"${inheritance.money}, lottery={inheritance.birthLotteryStatus?.status}");
        }

        /// <summary>
        /// Write the current run's progress into the save without ending a generation.
        /// </summary>
        public void PersistProgress()
        {
            var save = Save.Current ?? Save.CreateNewSave();
            save.generation = generation;
            save.currentDistrictId = currentDistrictId;
            save.currentOriginId = Player?.originId;
            City.WriteToSave(save);
            Save.Save(save);
        }
    }
}
