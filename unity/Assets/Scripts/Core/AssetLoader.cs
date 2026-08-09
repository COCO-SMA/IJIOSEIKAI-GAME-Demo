using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace KunchengRPG.Core
{
    /// <summary>
    /// Handles loading JSON data files from Assets/Data/.
    /// All game data is JSON-driven, loaded at runtime.
    /// </summary>
    public static class AssetLoader
    {
        private const string DATA_ROOT = "Data";

        // --- District maps ---
        public static Dictionary<string, Data.DistrictData> LoadAllDistricts()
        {
            var result = new Dictionary<string, Data.DistrictData>();
            TextAsset[] files = Resources.LoadAll<TextAsset>($"{DATA_ROOT}/districts");
            foreach (var file in files)
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<Data.DistrictData>(file.text);
                    result[data.id] = data;
                    Debug.Log($"[AssetLoader] Loaded district: {data.id} ({data.width}x{data.height})");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AssetLoader] Failed to parse district {file.name}: {e.Message}");
                }
            }
            return result;
        }

        public static Data.DistrictData LoadDistrict(string districtId)
        {
            TextAsset file = Resources.Load<TextAsset>($"{DATA_ROOT}/districts/{districtId}");
            if (file == null)
            {
                Debug.LogError($"[AssetLoader] District not found: {districtId}");
                return null;
            }
            return JsonConvert.DeserializeObject<Data.DistrictData>(file.text);
        }

        // --- Origins ---
        public static Dictionary<string, Data.OriginData> LoadAllOrigins()
        {
            var result = new Dictionary<string, Data.OriginData>();
            TextAsset[] files = Resources.LoadAll<TextAsset>($"{DATA_ROOT}/origins");
            foreach (var file in files)
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<Data.OriginData>(file.text);
                    result[data.id] = data;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AssetLoader] Failed to parse origin {file.name}: {e.Message}");
                }
            }
            return result;
        }

        public static List<Data.OriginData> LoadOriginsForDistrict(string districtId)
        {
            var all = LoadAllOrigins();
            var result = new List<Data.OriginData>();
            foreach (var kvp in all)
            {
                if (kvp.Value.district == districtId)
                    result.Add(kvp.Value);
            }
            // Also add common origins
            foreach (var kvp in all)
            {
                if (kvp.Value.district == "common")
                    result.Add(kvp.Value);
            }
            return result;
        }

        // --- Events ---
        public static List<Data.EventData> LoadEvents()
        {
            TextAsset file = Resources.Load<TextAsset>($"{DATA_ROOT}/events/events_demo");
            if (file == null)
            {
                Debug.LogWarning("[AssetLoader] Events file not found");
                return new List<Data.EventData>();
            }
            return JsonConvert.DeserializeObject<List<Data.EventData>>(file.text);
        }

        // --- Enemies ---
        public static Dictionary<string, Data.EnemyData> LoadAllEnemies()
        {
            var result = new Dictionary<string, Data.EnemyData>();
            TextAsset[] files = Resources.LoadAll<TextAsset>($"{DATA_ROOT}/enemies");
            foreach (var file in files)
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<Data.EnemyData>(file.text);
                    result[data.id] = data;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AssetLoader] Failed to parse enemy {file.name}: {e.Message}");
                }
            }
            return result;
        }

        // --- Items ---
        public static Dictionary<string, Data.ItemData> LoadAllItems()
        {
            var result = new Dictionary<string, Data.ItemData>();
            TextAsset[] files = Resources.LoadAll<TextAsset>($"{DATA_ROOT}/items");
            foreach (var file in files)
            {
                try
                {
                    var items = JsonConvert.DeserializeObject<List<Data.ItemData>>(file.text);
                    foreach (var item in items)
                        result[item.id] = item;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AssetLoader] Failed to parse items {file.name}: {e.Message}");
                }
            }
            return result;
        }

        // --- Endings ---
        public static Dictionary<string, Data.EndingData> LoadAllEndings()
        {
            var result = new Dictionary<string, Data.EndingData>();
            TextAsset[] files = Resources.LoadAll<TextAsset>($"{DATA_ROOT}/endings");
            foreach (var file in files)
            {
                try
                {
                    var endings = JsonConvert.DeserializeObject<List<Data.EndingData>>(file.text);
                    foreach (var ending in endings)
                        result[ending.id] = ending;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AssetLoader] Failed to parse endings {file.name}: {e.Message}");
                }
            }
            return result;
        }

        // --- Dialogue ---
        public static Data.DialogueTree LoadDialogue(string dialogueId)
        {
            TextAsset file = Resources.Load<TextAsset>($"{DATA_ROOT}/dialogue/{dialogueId}");
            if (file == null)
            {
                Debug.LogWarning($"[AssetLoader] Dialogue not found: {dialogueId}");
                return null;
            }
            return JsonConvert.DeserializeObject<Data.DialogueTree>(file.text);
        }

        // --- Tileset ---
        public static Data.TilesetData LoadTileset()
        {
            TextAsset file = Resources.Load<TextAsset>($"{DATA_ROOT}/tilesets/city_tileset");
            if (file == null)
            {
                Debug.LogWarning("[AssetLoader] Tileset metadata not found");
                return null;
            }
            return JsonConvert.DeserializeObject<Data.TilesetData>(file.text);
        }

        // --- Anomalies ---
        /// <summary>
        /// One file per anomaly item, keyed by id. 30 items in v1.0.
        /// </summary>
        public static Dictionary<string, Data.AnomalyData> LoadAllAnomalies()
        {
            var result = new Dictionary<string, Data.AnomalyData>();
            TextAsset[] files = Resources.LoadAll<TextAsset>($"{DATA_ROOT}/anomalies");
            foreach (var file in files)
            {
                var data = JsonConvert.DeserializeObject<Data.AnomalyData>(file.text);
                if (data != null && !string.IsNullOrEmpty(data.id))
                    result[data.id] = data;
            }
            Debug.Log($"[AssetLoader] Loaded {result.Count} anomalies");
            return result;
        }
    }
}
