using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace KunchengRPG.EditorTools
{
    /// <summary>
    /// Slices city_tileset.png into 24 sprites and generates one Tile asset per sprite.
    ///
    /// This exists as code rather than as manual Sprite Editor work so the result is
    /// reproducible and reviewable: the tile grid, filter mode, and pivot are all
    /// stated here instead of living in an importer blob nobody can diff.
    /// </summary>
    public static class TilesetBuilder
    {
        public const string TexturePath = "Assets/Art/Tilesets/city_tileset.png";
        public const string TileOutputDir = "Assets/Art/Tiles";

        public const int TileSize = 32;
        public const int Columns = 8;
        public const int Rows = 3;
        public const int TileCount = Columns * Rows; // 24, matches MapController.tiles

        [MenuItem("Kuncheng/1. Build Tileset Assets", false, 10)]
        public static void Build()
        {
            if (SliceTexture() == false) return;
            GenerateTiles();
            AssetDatabase.SaveAssets();
            Debug.Log($"[TilesetBuilder] Done: {TileCount} tiles in {TileOutputDir}");
        }

        /// <summary>
        /// Configure the importer for pixel art and emit one sprite rect per cell.
        /// </summary>
        public static bool SliceTexture()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[TilesetBuilder] Texture not found: {TexturePath}");
                return false;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = TileSize;   // 1 tile == 1 world unit
            importer.filterMode = FilterMode.Point;    // no bilinear smear on pixel art
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            var rects = new List<SpriteMetaData>(TileCount);
            for (int i = 0; i < TileCount; i++)
            {
                int col = i % Columns;
                int row = i / Columns;

                // Texture space is bottom-up; tile IDs read left-to-right, top-down.
                int y = (Rows - 1 - row) * TileSize;

                rects.Add(new SpriteMetaData
                {
                    name = $"tile_{i:00}",
                    rect = new Rect(col * TileSize, y, TileSize, TileSize),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }

            importer.spritesheet = rects.ToArray();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            Debug.Log($"[TilesetBuilder] Sliced {rects.Count} sprites from {TexturePath}");
            return true;
        }

        /// <summary>
        /// Create or update a Tile asset per sliced sprite. Reuses existing assets so
        /// re-running does not break references already wired into scenes.
        /// </summary>
        public static void GenerateTiles()
        {
            Directory.CreateDirectory(TileOutputDir);

            var sprites = LoadSprites();
            if (sprites.Count == 0)
            {
                Debug.LogError("[TilesetBuilder] No sprites found after slicing.");
                return;
            }

            for (int i = 0; i < TileCount; i++)
            {
                if (!sprites.TryGetValue($"tile_{i:00}", out var sprite)) continue;

                string path = $"{TileOutputDir}/Tile_{i:00}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);

                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.colliderType = Tile.ColliderType.None;
                    AssetDatabase.CreateAsset(tile, path);
                }
                else
                {
                    tile.sprite = sprite;
                    EditorUtility.SetDirty(tile);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Dictionary<string, Sprite> LoadSprites()
        {
            var result = new Dictionary<string, Sprite>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(TexturePath))
                if (obj is Sprite s) result[s.name] = s;
            return result;
        }

        /// <summary>
        /// Tile assets in ID order, for scene wiring. Missing entries stay null so the
        /// caller can report which IDs failed rather than silently shifting indices.
        /// </summary>
        public static TileBase[] LoadTilesInOrder()
        {
            var tiles = new TileBase[TileCount];
            for (int i = 0; i < TileCount; i++)
                tiles[i] = AssetDatabase.LoadAssetAtPath<Tile>($"{TileOutputDir}/Tile_{i:00}.asset");
            return tiles;
        }
    }
}
