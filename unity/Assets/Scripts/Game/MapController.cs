using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Loads district map data into Unity Tilemap or layered sprites.
    /// Handles tile rendering, collision, NPC/POI/exit placement.
    ///
    /// Two modes:
    ///  1. Tileset mode (legacy): fills Tilemaps from tile IDs.
    ///  2. Layered background mode: draws ground/collision/decoration sprites
    ///     and uses a fine-resolution walkability grid so actors can walk on
    ///     roads instead of whole 64px tiles.
    /// </summary>
    public class MapController : MonoBehaviour
    {
        [Header("Tilemap References (Tileset Mode)")]
        public Tilemap groundTilemap;   // Walkable tiles
        public Tilemap buildingTilemap;  // Solid tiles (buildings, walls, etc.)
        public Tilemap decorationTilemap; // Decorations (trees, lamps, etc.)

        [Header("Tile Assets")]
        public TileBase[] tiles; // Array indexed by tile ID (0-23)

        [Header("Layered Map Renderers")]
        [Tooltip("Ground layer (roads, grass, plazas). Reuses the existing background renderer slot.")]
        public SpriteRenderer backgroundRenderer; // Ground layer
        public SpriteRenderer collisionRenderer;  // Collision layer (buildings, water)
        public SpriteRenderer decorationRenderer; // Decoration layer (trees, props)

        private SpriteRenderer GroundRenderer => backgroundRenderer;

        [Header("Actor Sprites")]
        public Sprite npcSprite;
        public Sprite enemySprite;
        public Color npcColor = new Color(1f, 0.85f, 0.45f, 1f);
        public Color enemyColor = new Color(0.95f, 0.35f, 0.35f, 1f);

        private Transform actorRoot;

        [Header("Settings")]
        public int tileSize = 32;

        private Data.DistrictData districtData;
        private HashSet<int> solidTileIds = new HashSet<int> { 1, 2, 3, 8, 9, 11, 12, 13, 14, 18, 19, 20, 21 };

        private bool useLayeredMode;
        private bool[,] walkableGrid;     // coarse grid (one cell per tile)
        private bool[,] walkableFineGrid; // fine grid (subTileSize pixels per cell)

        private int fineWidth;
        private int fineHeight;
        private float subTileWorldSize;

        // World bounds of the map (outer edges)
        private float worldMinX;
        private float worldMaxX;
        private float worldMinY;
        private float worldMaxY;

        // Callbacks
        public System.Action<int, int> OnPlayerStep;
        public System.Action<Data.ExitData> OnExitReached;
        public System.Action<Data.NPCData> OnNPCTalk;
        public System.Action<Data.POIData> OnPOIInteract;

        // Current nearby targets
        public Data.NPCData nearbyNpc { get; private set; }
        public Data.ExitData nearbyExit { get; private set; }
        public Data.POIData nearbyPoi { get; private set; }

        public Data.DistrictData DistrictData => districtData;
        public bool UseLayeredMode => useLayeredMode;
        public float SubTileWorldSize => subTileWorldSize;

        void Awake()
        {
            EnsureLayerRenderers();
        }

        /// <summary>
        /// Collision and decoration layers are created at runtime if the scene
        /// does not already provide them. The ground layer reuses the existing
        /// background renderer slot.
        /// </summary>
        private void EnsureLayerRenderers()
        {
            if (collisionRenderer == null)
            {
                var go = new GameObject("CollisionLayer");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, 0f);
                collisionRenderer = go.AddComponent<SpriteRenderer>();
            }
            if (decorationRenderer == null)
            {
                var go = new GameObject("DecorationLayer");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -1f);
                decorationRenderer = go.AddComponent<SpriteRenderer>();
            }
        }

        /// <summary>
        /// Load a district's map data into the tilemaps or layered sprites.
        /// </summary>
        public void LoadDistrict(Data.DistrictData data)
        {
            districtData = data;
            ClearTilemaps();
            ClearLayeredSprites();

            useLayeredMode = !string.IsNullOrEmpty(data.background);
            if (useLayeredMode)
            {
                LoadLayeredMap(data);
            }
            else
            {
                for (int y = 0; y < data.height; y++)
                {
                    for (int x = 0; x < data.width; x++)
                    {
                        int tileId = data.tiles[y][x];
                        PlaceTile(x, y, tileId);
                    }
                }
            }

            BuildWalkableGrid(data);
            ComputeWorldBounds(data);
            SpawnActors(data);

            Debug.Log($"[MapController] Loaded district: {data.id} ({data.width}x{data.height}) layeredMode={useLayeredMode} fine={fineWidth}x{fineHeight}");
        }

        #region Layered Background Mode

        private void LoadLayeredMap(Data.DistrictData data)
        {
            LoadSpriteIntoRenderer(data.background, GroundRenderer, data.tileSize, "Ground");
            LoadSpriteIntoRenderer(data.collisionLayer, collisionRenderer, data.tileSize, "Collision");
            LoadSpriteIntoRenderer(data.decorationLayer, decorationRenderer, data.tileSize, "Decoration");

            // Position sprites so the map origin (top-left) aligns with world (0,0)
            // and one 64px tile equals one world unit.
            Vector3 center = new Vector3(data.width / 2f, -data.height / 2f, 0f);
            float zGround = 1f;
            float zCollision = 0f;
            float zDecoration = -1f;

            if (GroundRenderer != null)
            {
                GroundRenderer.enabled = true;
                GroundRenderer.transform.position = new Vector3(center.x, center.y, zGround);
                GroundRenderer.transform.localScale = Vector3.one;
            }
            if (collisionRenderer != null)
            {
                collisionRenderer.enabled = true;
                collisionRenderer.transform.position = new Vector3(center.x, center.y, zCollision);
                collisionRenderer.transform.localScale = Vector3.one;
            }
            if (decorationRenderer != null)
            {
                decorationRenderer.enabled = true;
                decorationRenderer.transform.position = new Vector3(center.x, center.y, zDecoration);
                decorationRenderer.transform.localScale = Vector3.one;
            }
        }

        private void LoadSpriteIntoRenderer(string path, SpriteRenderer renderer, int ppu, string label)
        {
            if (renderer == null) return;
            if (string.IsNullOrEmpty(path))
            {
                renderer.sprite = null;
                return;
            }

            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[MapController] {label} image not found in Resources: {path}");
                renderer.sprite = null;
                return;
            }

            int pixels = Mathf.Max(1, ppu);
            var sprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                pixels);
            sprite.name = path;
            renderer.sprite = sprite;
        }

        private void ClearLayeredSprites()
        {
            if (GroundRenderer != null) { GroundRenderer.sprite = null; GroundRenderer.enabled = false; }
            if (collisionRenderer != null) { collisionRenderer.sprite = null; collisionRenderer.enabled = false; }
            if (decorationRenderer != null) { decorationRenderer.sprite = null; decorationRenderer.enabled = false; }
        }

        #endregion

        #region Tileset Mode

        private void PlaceTile(int x, int y, int tileId)
        {
            if (tileId < 0 || tileId >= tiles.Length || tiles[tileId] == null)
            {
                tileId = 0;
            }

            Vector3Int pos = new Vector3Int(x, -y, 0);

            if (solidTileIds.Contains(tileId))
            {
                buildingTilemap.SetTile(pos, tiles[tileId]);
            }
            else
            {
                groundTilemap.SetTile(pos, tiles[tileId]);
            }
        }

        private void ClearTilemaps()
        {
            if (groundTilemap != null)
                groundTilemap.ClearAllTiles();
            if (buildingTilemap != null)
                buildingTilemap.ClearAllTiles();
            if (decorationTilemap != null)
                decorationTilemap.ClearAllTiles();
        }

        #endregion

        #region Walkability

        private void BuildWalkableGrid(Data.DistrictData data)
        {
            walkableGrid = new bool[data.height, data.width];
            for (int y = 0; y < data.height; y++)
            {
                for (int x = 0; x < data.width; x++)
                {
                    if (useLayeredMode && data.walkableFine != null)
                    {
                        // Will be filled below from fine grid
                        walkableGrid[y, x] = true;
                    }
                    else if (useLayeredMode && data.walkable != null && y < data.walkable.Length && x < data.walkable[y].Length)
                    {
                        walkableGrid[y, x] = data.walkable[y][x] != 0;
                    }
                    else
                    {
                        int tileId = data.tiles != null && y < data.tiles.Length && x < data.tiles[y].Length
                            ? data.tiles[y][x]
                            : 0;
                        walkableGrid[y, x] = !solidTileIds.Contains(tileId);
                    }
                }
            }

            // Fine grid
            int sub = data.subTileSize > 0 ? data.subTileSize : data.tileSize;
            int divisor = Mathf.Max(1, data.tileSize / sub);
            fineWidth = data.width * divisor;
            fineHeight = data.height * divisor;
            subTileWorldSize = 1f / divisor;
            walkableFineGrid = new bool[fineHeight, fineWidth];

            if (useLayeredMode && data.walkableFine != null && data.walkableFine.Length > 0)
            {
                int srcRows = data.walkableFine.Length;
                int srcCols = data.walkableFine[0]?.Length ?? 0;
                for (int y = 0; y < fineHeight; y++)
                {
                    for (int x = 0; x < fineWidth; x++)
                    {
                        bool walk = true;
                        if (y < srcRows && x < srcCols)
                            walk = data.walkableFine[y][x] != 0;
                        walkableFineGrid[y, x] = walk;
                    }
                }

                // Sync coarse grid from fine grid: a coarse tile is walkable if
                // any fine cell inside it is walkable. This keeps NPC/exit proximity
                // checks reasonable on roads that only occupy part of a tile.
                for (int y = 0; y < data.height; y++)
                {
                    for (int x = 0; x < data.width; x++)
                    {
                        bool anyWalkable = false;
                        for (int fy = y * divisor; fy < (y + 1) * divisor && !anyWalkable; fy++)
                        {
                            for (int fx = x * divisor; fx < (x + 1) * divisor && !anyWalkable; fx++)
                            {
                                if (walkableFineGrid[fy, fx])
                                    anyWalkable = true;
                            }
                        }
                        walkableGrid[y, x] = anyWalkable;
                    }
                }
            }
            else
            {
                // Fall back to coarse grid repeated across fine cells
                for (int y = 0; y < fineHeight; y++)
                {
                    for (int x = 0; x < fineWidth; x++)
                    {
                        int cy = y / divisor;
                        int cx = x / divisor;
                        walkableFineGrid[y, x] = walkableGrid[cy, cx];
                    }
                }
            }
        }

        private void ComputeWorldBounds(Data.DistrictData data)
        {
            worldMinX = 0f;
            worldMaxX = data.width;
            worldMaxY = 0f;
            worldMinY = -data.height;
        }

        /// <summary>
        /// World-space bounds of the map, useful for camera clamping.
        /// </summary>
        public Bounds GetMapBounds()
        {
            Vector3 center = new Vector3((worldMinX + worldMaxX) / 2f, (worldMinY + worldMaxY) / 2f, 0f);
            Vector3 size = new Vector3(worldMaxX - worldMinX, worldMaxY - worldMinY, 1f);
            return new Bounds(center, size);
        }

        /// <summary>
        /// Check if a grid position is walkable (coarse, legacy).
        /// </summary>
        public bool IsWalkable(int x, int y)
        {
            if (districtData == null) return false;
            if (x < 0 || y < 0 || x >= districtData.width || y >= districtData.height)
                return false;

            return walkableGrid[y, x];
        }

        /// <summary>
        /// Check if a world position is walkable using the fine collision grid.
        /// </summary>
        public bool IsWalkable(Vector3 worldPos)
        {
            return IsWalkable(worldPos.x, worldPos.y);
        }

        /// <summary>
        /// Check if a world position is walkable using the fine collision grid.
        /// Tests the center plus four corners of a square of the given radius.
        /// </summary>
        public bool IsWalkable(float worldX, float worldY, float radius = 0.12f)
        {
            if (districtData == null) return false;

            // Center
            if (!IsFineWalkable(worldX, worldY)) return false;

            // Corners
            if (!IsFineWalkable(worldX - radius, worldY - radius)) return false;
            if (!IsFineWalkable(worldX + radius, worldY - radius)) return false;
            if (!IsFineWalkable(worldX - radius, worldY + radius)) return false;
            if (!IsFineWalkable(worldX + radius, worldY + radius)) return false;

            return true;
        }

        private bool IsFineWalkable(float worldX, float worldY)
        {
            if (worldX < worldMinX || worldX > worldMaxX ||
                worldY < worldMinY || worldY > worldMaxY)
                return false;

            int fx = Mathf.FloorToInt((worldX - worldMinX) / subTileWorldSize);
            int fy = Mathf.FloorToInt((worldMaxY - worldY) / subTileWorldSize);

            fx = Mathf.Clamp(fx, 0, fineWidth - 1);
            fy = Mathf.Clamp(fy, 0, fineHeight - 1);

            return walkableFineGrid[fy, fx];
        }

        #endregion

        #region Coordinate Conversion

        /// <summary>
        /// Convert grid coordinates to world position.
        /// </summary>
        public Vector3 GridToWorld(int x, int y)
        {
            if (groundTilemap != null)
            {
                return groundTilemap.GetCellCenterWorld(new Vector3Int(x, -y, 0));
            }
            // Fallback for layered mode without tilemap reference
            return new Vector3(x + 0.5f, -y + 0.5f, 0f);
        }

        /// <summary>
        /// Convert world position to grid coordinates.
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            if (groundTilemap != null)
            {
                Vector3Int cell = groundTilemap.WorldToCell(worldPos);
                return new Vector2Int(cell.x, -cell.y);
            }
            // Fallback
            return new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(-worldPos.y));
        }

        /// <summary>
        /// Snap a world position to the nearest grid cell center.
        /// </summary>
        public Vector3 SnapToGridCenter(Vector3 worldPos)
        {
            Vector2Int grid = WorldToGrid(worldPos);
            return GridToWorld(grid.x, grid.y);
        }

        #endregion

        #region Actors

        private void SpawnActors(Data.DistrictData data)
        {
            if (actorRoot != null) Destroy(actorRoot.gameObject);
            actorRoot = new GameObject("Actors").transform;
            actorRoot.SetParent(transform, false);

            if (data.npcs != null)
                foreach (var npc in data.npcs)
                    SpawnActor(npc.name, npc.x, npc.y, npcSprite, npcColor);

            if (data.points != null)
                foreach (var poi in data.points)
                    if (poi.type == "enemy")
                        SpawnActor(poi.name, poi.x, poi.y, enemySprite, enemyColor);
        }

        private void SpawnActor(string label, int x, int y, Sprite sprite, Color color)
        {
            var go = new GameObject(string.IsNullOrEmpty(label) ? "Actor" : label,
                                    typeof(SpriteRenderer));
            go.transform.SetParent(actorRoot, false);
            go.transform.position = GridToWorld(x, y);

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 5;
        }

        #endregion

        #region Proximity & Interaction

        public void CheckProximity(int playerX, int playerY)
        {
            nearbyNpc = null;
            nearbyExit = null;
            nearbyPoi = null;

            if (districtData == null) return;

            if (districtData.npcs != null)
            {
                foreach (var npc in districtData.npcs)
                {
                    int dist = Mathf.Abs(npc.x - playerX) + Mathf.Abs(npc.y - playerY);
                    if (dist <= 1)
                    {
                        nearbyNpc = npc;
                        break;
                    }
                }
            }

            if (districtData.exits != null)
            {
                foreach (var exit in districtData.exits)
                {
                    if (exit.x == playerX && exit.y == playerY)
                    {
                        nearbyExit = exit;
                        break;
                    }
                }
            }

            if (districtData.points != null)
            {
                foreach (var poi in districtData.points)
                {
                    int dist = Mathf.Abs(poi.x - playerX) + Mathf.Abs(poi.y - playerY);
                    if (dist <= 1)
                    {
                        nearbyPoi = poi;
                        break;
                    }
                }
            }
        }

        public void TryInteract()
        {
            if (nearbyNpc != null)
            {
                OnNPCTalk?.Invoke(nearbyNpc);
            }
            else if (nearbyPoi != null)
            {
                OnPOIInteract?.Invoke(nearbyPoi);
            }
        }

        #endregion

        #region Spawn Position

        public Vector2Int GetSpawnPosition(string fromDistrict = null)
        {
            if (districtData == null) return new Vector2Int(15, 10);

            if (!string.IsNullOrEmpty(fromDistrict))
            {
                foreach (var exit in districtData.exits)
                {
                    if (exit.target == fromDistrict)
                    {
                        int sx = exit.x;
                        int sy = exit.y;
                        int[][] dirs = { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } };
                        foreach (var dir in dirs)
                        {
                            int nx = sx + dir[0];
                            int ny = sy + dir[1];
                            if (IsWalkable(nx, ny))
                                return new Vector2Int(nx, ny);
                        }
                    }
                }
            }

            int cx = districtData.width / 2;
            int cy = districtData.height / 2;
            if (IsWalkable(cx, cy))
                return new Vector2Int(cx, cy);

            for (int y = 0; y < districtData.height; y++)
            {
                for (int x = 0; x < districtData.width; x++)
                {
                    if (IsWalkable(x, y))
                        return new Vector2Int(x, y);
                }
            }

            return new Vector2Int(0, 0);
        }

        #endregion
    }
}
