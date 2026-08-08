using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace KunchengRPG.Core
{
    /// <summary>
    /// Persists the save document to disk under Application.persistentDataPath.
    ///
    /// The JS original used localStorage with a quota; a file has no practical size
    /// limit, but the compression pass is kept so save size stays bounded across
    /// hundreds of generations and load stays fast.
    /// </summary>
    public class SaveManager
    {
        private const string FileName = "kuncheng_rpg_save.json";
        private const string LegacyFileName = "shenzhen_rpg_save.json";
        private const int FamilyLogMax = 200;
        private const int NpcMemoryMax = 500;
        private const int NpcEventMax = 20;

        public Data.SaveData Current { get; private set; }

        private static string SaveDir => Application.persistentDataPath;
        private static string SavePath => Path.Combine(SaveDir, FileName);
        private static string LegacyPath => Path.Combine(SaveDir, LegacyFileName);

        private bool migrated;

        /// <summary>
        /// Rename a pre-rebrand save into place, once per session.
        /// </summary>
        private void EnsureMigrated()
        {
            if (migrated) return;
            migrated = true;

            try
            {
                if (File.Exists(LegacyPath) && !File.Exists(SavePath))
                {
                    File.Move(LegacyPath, SavePath);
                    Debug.Log("[SaveManager] Migrated legacy save file");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Legacy save migration skipped: {e.Message}");
            }
        }

        public bool HasSave()
        {
            EnsureMigrated();
            return File.Exists(SavePath);
        }

        public bool Save(Data.SaveData state)
        {
            if (state == null) return false;
            EnsureMigrated();

            try
            {
                Compress(state);
                Directory.CreateDirectory(SaveDir);

                // Write to a temp file then swap, so a crash mid-write cannot
                // leave a truncated save where a valid one used to be.
                string tmp = SavePath + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(state, Formatting.Indented));
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tmp, SavePath);

                Current = state;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
                return false;
            }
        }

        public Data.SaveData Load()
        {
            EnsureMigrated();
            if (!File.Exists(SavePath)) return null;

            try
            {
                var data = JsonConvert.DeserializeObject<Data.SaveData>(File.ReadAllText(SavePath));
                Current = data;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load failed: {e.Message}");
                return null;
            }
        }

        public void DeleteSave()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                Current = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Delete failed: {e.Message}");
            }
        }

        /// <summary>
        /// Absolute path to the save file, for "reveal in explorer" style UI.
        /// </summary>
        public string GetSavePath() => SavePath;

        public Data.SaveData CreateNewSave(string districtId = null, string originId = null)
        {
            var save = new Data.SaveData
            {
                version = 2,
                createdAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                seed = UnityEngine.Random.Range(0, 999999),
                generation = 1,
                currentDistrictId = districtId,
                currentOriginId = originId
            };
            Current = save;
            return save;
        }

        /// <summary>
        /// Trim unbounded history in place: keep the most recent family log entries
        /// (summarising the rest), cap per-NPC event lists, and drop the least
        /// familiar NPCs once the roster gets large.
        /// </summary>
        private void Compress(Data.SaveData state)
        {
            if (state.familyLog != null && state.familyLog.Count > FamilyLogMax)
            {
                int dropCount = state.familyLog.Count - FamilyLogMax;
                var dropped = state.familyLog.GetRange(0, dropCount);

                state.familyLogSummary = Summarize(dropped, state.familyLogSummary);
                state.familyLog.RemoveRange(0, dropCount);
            }

            if (state.npcMemories == null || state.npcMemories.Count == 0) return;

            foreach (var mem in state.npcMemories.Values)
            {
                if (mem?.events != null && mem.events.Count > NpcEventMax)
                    mem.events.RemoveRange(0, mem.events.Count - NpcEventMax);
            }

            if (state.npcMemories.Count > NpcMemoryMax)
            {
                var ids = new List<string>(state.npcMemories.Keys);
                ids.Sort((a, b) => state.npcMemories[b].familiarity.CompareTo(state.npcMemories[a].familiarity));

                var kept = new Dictionary<string, Game.NpcMemory>(NpcMemoryMax);
                for (int i = 0; i < NpcMemoryMax; i++)
                    kept[ids[i]] = state.npcMemories[ids[i]];

                state.npcMemories = kept;
            }
        }

        /// <summary>
        /// Folds newly dropped entries into any existing summary rather than
        /// replacing it, so the running total survives repeated compression.
        /// </summary>
        private Data.FamilyLogSummary Summarize(
            List<Data.FamilyLogEntry> dropped, Data.FamilyLogSummary existing)
        {
            var summary = existing ?? new Data.FamilyLogSummary();
            summary.count += dropped.Count;

            foreach (var e in dropped)
                if (!summary.generations.Contains(e.generation))
                    summary.generations.Add(e.generation);

            summary.preview.Clear();
            for (int i = Math.Max(0, dropped.Count - 3); i < dropped.Count; i++)
                summary.preview.Add(dropped[i].title);

            return summary;
        }
    }
}
