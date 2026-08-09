using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Loads district map data into Unity Tilemap.
    /// Handles tile rendering, collision, NPC/POI/exit placement.
    /// </summary>
    public class MapController : MonoBehaviour
    {
        [Header("Tilemap References")]
        public Tilemap groundTilemap;   // Walkable tiles
        public Tilemap buildingTilemap;  // Solid tiles (buildings, walls, etc.)
        public Tilemap decorationTilemap; // Decorations (trees, lamps, etc.)

        [Header("Tile Assets")]
        public TileBase[] tiles; // Array indexed by tile ID (0-23)

        /// <summary>
        /// NPC and enemy markers. Nothing ever drew actors, which is why NPCs were
        /// invisible while their data loaded fine: proximity checks read the JSON
        /// happily, but no GameObject was ever created to look at.
        /// </summary>
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

        // Callbacks
        public System.Action<int, int> OnPlayerStep;
        public System.Action<Data.ExitData> OnExitReached;
        public System.Action<Data.NPCData> OnNPCTalk;
        public System.Action<Data.POIData> OnPOIInteract;

        // Current nearby targets
        public Data.NPCData nearbyNpc { get; private set; }
        public Data.ExitData nearbyExit { get; private set; }
        public Data.POIData nearbyPoi { get; private set; }

        /// <summary>
        /// Load a district's map data into the tilemaps.
        /// </summary>
        public void LoadDistrict(Data.DistrictData data)
        {
            districtData = data;
            ClearTilemaps();

            for (int y = 0; y < data.height; y++)
            {
                for (int x = 0; x < data.width; x++)
                {
                    int tileId = data.tiles[y][x];
                    PlaceTile(x, y, tileId);
                }
            }

            SpawnActors(data);

            // Center camera on map
            CenterCamera(data.width, data.height);
            Debug.Log($"[MapController] Loaded district: {data.id} ({data.width}x{data.height})");
        }

        private void PlaceTile(int x, int y, int tileId)
        {
            if (tileId < 0 || tileId >= tiles.Length || tiles[tileId] == null)
            {
                // Default to grass (tile 0) if missing
                tileId = 0;
            }

            Vector3Int pos = new Vector3Int(x, -y, 0); // Flip Y for Unity coords

            if (solidTileIds.Contains(tileId))
            {
                // Solid tiles go to building layer
                buildingTilemap.SetTile(pos, tiles[tileId]);
            }
            else
            {
                // Walkable tiles go to ground layer
                groundTilemap.SetTile(pos, tiles[tileId]);
            }
        }

        /// <summary>
        /// Rebuild the district's actor markers. They hang off one parent so switching
        /// district is a single Destroy, and they sort below the player (10) so walking
        /// past an NPC never hides you behind it.
        /// </summary>
        private void SpawnActors(Data.DistrictData data)
        {
            if (actorRoot != null) Destroy(actorRoot.gameObject);
            actorRoot = new GameObject("Actors").transform;
            actorRoot.SetParent(transform, false);

            if (data.npcs != null)
                foreach (var npc in data.npcs)
                    SpawnActor(npc.name, npc.x, npc.y, npcSprite, npcColor);

            // Enemy POIs get a marker of their own: an encounter you can see coming is
            // a 明雷, and that visibility is decision information, not decoration.
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

        private void ClearTilemaps()
        {
            groundTilemap.ClearAllTiles();
            buildingTilemap.ClearAllTiles();
            if (decorationTilemap != null)
                decorationTilemap.ClearAllTiles();
        }

        /// <summary>
        /// Frame the map's midpoint. Works in world units via the grid itself, so it
        /// stays correct regardless of the Grid's cell size.
        /// </summary>
        private void CenterCamera(int width, int height)
        {
            var cam = Camera.main;
            if (cam == null || groundTilemap == null) return;

            Vector3 center = groundTilemap.GetCellCenterWorld(new Vector3Int(width / 2, -(height / 2), 0));
            cam.transform.position = new Vector3(center.x, center.y, -10f);
        }

        /// <summary>
        /// Check if a tile position is walkable.
        /// </summary>
        public bool IsWalkable(int x, int y)
        {
            if (districtData == null) return false;
            if (x < 0 || y < 0 || x >= districtData.width || y >= districtData.height)
                return false;

            int tileId = districtData.tiles[y][x];
            return !solidTileIds.Contains(tileId);
        }

        /// <summary>
        /// Convert grid coordinates to world position.
        /// </summary>
        public Vector3 GridToWorld(int x, int y)
        {
            // Map rows grow downward, tilemap rows grow upward, hence the negated y.
            // GetCellCenterWorld already applies the half-cell offset in world units.
            return groundTilemap.GetCellCenterWorld(new Vector3Int(x, -y, 0));
        }

        /// <summary>
        /// Convert world position to grid coordinates.
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            Vector3Int cell = groundTilemap.WorldToCell(worldPos);
            return new Vector2Int(cell.x, -cell.y);
        }

        /// <summary>
        /// Check proximity to NPCs, exits, and POIs.
        /// Called after player moves.
        /// </summary>
        public void CheckProximity(int playerX, int playerY)
        {
            nearbyNpc = null;
            nearbyExit = null;
            nearbyPoi = null;

            if (districtData == null) return;

            // Check NPCs (adjacent tiles)
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

            // Check exits (same tile)
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

            // Check POIs (adjacent tiles)
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

        /// <summary>
        /// Handle interaction input when near NPC or POI.
        /// </summary>
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

        /// <summary>
        /// Find a safe spawn position for the player in a district.
        /// Uses center of map, or looks for first walkable tile.
        /// </summary>
        public Vector2Int GetSpawnPosition(string fromDistrict = null)
        {
            if (districtData == null) return new Vector2Int(15, 10);

            // If coming from another district, find the matching exit
            if (!string.IsNullOrEmpty(fromDistrict))
            {
                foreach (var exit in districtData.exits)
                {
                    if (exit.target == fromDistrict)
                    {
                        // Spawn one tile away from the exit (inside the map)
                        int sx = exit.x;
                        int sy = exit.y;
                        // Try adjacent tiles
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

            // Default: center of map
            int cx = districtData.width / 2;
            int cy = districtData.height / 2;
            if (IsWalkable(cx, cy))
                return new Vector2Int(cx, cy);

            // Search for first walkable tile
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
    }
}
