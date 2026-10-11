using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  A resource node on the map: a gold mine or a stand of trees.
    // -----------------------------------------------------------------------
    public class ResourceNode
    {
        public int nodeIndex;
        public ResourceKind kind;
        public int amount;
        public Vector2Int tile;
        public Vector3 position;
        public GameObject go;
        public List<UnitController> harvesters = new List<UnitController>();

        public bool Depleted { get { return amount <= 0; } }

        public bool IsFull { get { return harvesters.Count >= (kind == ResourceKind.Gold ? 5 : 1); } }
    }

    // -----------------------------------------------------------------------
    //  The map: terrain heights, walkability, occupancy, resource nodes and
    //  A* pathfinding.  Everything is grid based (TileSize world units).
    // -----------------------------------------------------------------------
    public class GameMap
    {
        public const int Size = GameData.MapSize;
        public const float Tile = GameData.TileSize;

        readonly float[,] heights;
        readonly byte[] blocked;       // 1 = permanently unwalkable (water / rock)
        readonly byte[] occupied;      // 1 = a building footprint sits here
        readonly int[] nodeAt;         // resource node index at tile, -1 for none

        public readonly List<ResourceNode> Nodes = new List<ResourceNode>();

        // pathfinding scratch buffers (allocated once, reused)
        readonly int[] gScore;
        readonly int[] cameFrom;
        readonly byte[] state;         // 0 unvisited, 1 open, 2 closed
        readonly PriorityQueue open = new PriorityQueue();
        readonly List<Vector3> pathScratch = new List<Vector3>();

        public GameMap(int seed)
        {
            heights = new float[Size, Size];
            blocked = new byte[Size * Size];
            occupied = new byte[Size * Size];
            nodeAt = new int[Size * Size];
            gScore = new int[Size * Size];
            cameFrom = new int[Size * Size];
            state = new byte[Size * Size];

            for (int i = 0; i < Size * Size; i++) nodeAt[i] = -1;

            Generate(seed);
        }

        // ===================================================================
        //  Terrain generation
        // ===================================================================

        void Generate(int seed)
        {
            // Two layered value-noise octaves + a border of impassable rock.
            float[,] noise = new float[Size, Size];
            SampleNoise(noise, seed, 24f, 1.0f);
            float[,] detail = new float[Size, Size];
            SampleNoise(detail, seed * 7 + 13, 8f, 1.0f);

            for (int z = 0; z < Size; z++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float h = noise[x, z] * 3.2f + detail[x, z] * 0.6f;
                    heights[x, z] = h;
                    bool border = x < 3 || z < 3 || x >= Size - 3 || z >= Size - 3;
                    if (border || h > 3.35f) blocked[z * Size + x] = 1;
                }
            }

            // Flatten the two starting bases so buildings always fit.
            FlattenArea(new Vector2Int(16, 16), 9);      // Alliance (blue) start
            FlattenArea(new Vector2Int(Size - 17, Size - 17), 9); // Horde (red) start

            PlaceResources();
        }

        void SampleNoise(float[,] dst, int seed, float scale, float amplitude)
        {
            int gridStep = Mathf.Max(2, Mathf.RoundToInt(scale));
            int gw = Size / gridStep + 3;
            float[] lattice = new float[gw * gw];
            System.Random rnd = new System.Random(seed);
            for (int i = 0; i < lattice.Length; i++) lattice[i] = (float)rnd.NextDouble();

            for (int z = 0; z < Size; z++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float fx = x / (float)gridStep;
                    float fz = z / (float)gridStep;
                    int x0 = Mathf.FloorToInt(fx), z0 = Mathf.FloorToInt(fz);
                    float tx = Smooth(fx - x0), tz = Smooth(fz - z0);
                    float a = Lattice(lattice, gw, x0, z0);
                    float b = Lattice(lattice, gw, x0 + 1, z0);
                    float c = Lattice(lattice, gw, x0, z0 + 1);
                    float d = Lattice(lattice, gw, x0 + 1, z0 + 1);
                    dst[x, z] = (Lerp(Lerp(a, b, tx), Lerp(c, d, tx), tz) - 0.5f) * 2f * amplitude;
                }
            }
        }

        static float Lattice(float[] lat, int gw, int x, int z)
        {
            x = Mathf.Clamp(x, 0, gw - 1);
            z = Mathf.Clamp(z, 0, gw - 1);
            return lat[z * gw + x];
        }

        static float Smooth(float t) { return t * t * (3f - 2f * t); }
        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        void FlattenArea(Vector2Int center, int radius)
        {
            // Force the starting yard well above the water line.
            float h = Mathf.Max(HeightAt(center.x, center.y), 1.2f);
            for (int z = center.y - radius; z <= center.y + radius; z++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    if (!InBounds(x, z)) continue;
                    float d = Mathf.Sqrt((x - center.x) * (x - center.x) + (z - center.y) * (z - center.y));
                    if (d > radius) continue;
                    float w = Mathf.Clamp01(1f - (d - radius + 3f) / 3f);
                    heights[x, z] = Mathf.Lerp(heights[x, z], h, w);
                    if (d <= radius - 2) blocked[z * Size + x] = 0;
                }
            }
        }

        void PlaceResources()
        {
            System.Random rnd = new System.Random(9176);
            Vector2Int[] bases =
            {
                new Vector2Int(16, 16),
                new Vector2Int(Size - 17, Size - 17)
            };

            // A gold mine + tree ring next to every start location.
            for (int b = 0; b < bases.Length; b++)
            {
                AddGoldMine(bases[b].x + (b == 0 ? 6 : -6), bases[b].y + (b == 0 ? -3 : 3), 1500 + rnd.Next(0, 400));
                AddTreeCluster(bases[b], 9, 16, rnd);
            }

            // Expansion mines in the middle of the map.
            AddGoldMine(Size / 2, Size / 2, 2500);
            AddGoldMine(Size / 2 + 14, Size / 2 - 12, 1400);
            AddGoldMine(Size / 2 - 14, Size / 2 + 12, 1400);

            // Scatter trees / neutral clusters everywhere else.
            for (int i = 0; i < 130; i++)
            {
                int x = 6 + rnd.Next(Size - 12);
                int y = 6 + rnd.Next(Size - 12);
                if (!IsWalkableTile(x, y)) continue;
                AddTreeCluster(new Vector2Int(x, y), 2, 4, rnd);
            }
        }

        void AddGoldMine(int x, int y, int amount)
        {
            x = Mathf.Clamp(x, 4, Size - 5);
            y = Mathf.Clamp(y, 4, Size - 5);
            ResourceNode n = new ResourceNode
            {
                nodeIndex = Nodes.Count,
                kind = ResourceKind.Gold,
                amount = amount,
                tile = new Vector2Int(x, y),
                position = TileToWorld(x, y)
            };
            Nodes.Add(n);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    if (InBounds(x + dx, y + dz)) nodeAt[(y + dz) * Size + (x + dx)] = n.nodeIndex;
            // The mine itself is a 3x3 obstacle.
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    if (InBounds(x + dx, y + dz)) blocked[(y + dz) * Size + (x + dx)] = 1;
        }

        void AddTreeCluster(Vector2Int center, int min, int max, System.Random rnd)
        {
            int count = rnd.Next(min, max + 1);
            for (int i = 0; i < count; i++)
            {
                int x = center.x + rnd.Next(-3, 4);
                int y = center.y + rnd.Next(-3, 4);
                if (!InBounds(x, y)) continue;
                if (blocked[y * Size + x] != 0) continue;
                if (occupied[y * Size + x] != 0) continue;
                if (nodeAt[y * Size + x] >= 0) continue;
                if (Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y) > 4) continue;

                ResourceNode n = new ResourceNode
                {
                    nodeIndex = Nodes.Count,
                    kind = ResourceKind.Lumber,
                    amount = 80,
                    tile = new Vector2Int(x, y),
                    position = TileToWorld(x, y)
                };
                Nodes.Add(n);
                nodeAt[y * Size + x] = n.nodeIndex;
                blocked[y * Size + x] = 1;
            }
        }

        // ===================================================================
        //  Queries
        // ===================================================================

        public static bool InBounds(int x, int z)
        {
            return x >= 0 && z >= 0 && x < Size && z < Size;
        }

        public static Vector3 TileToWorld(int x, int z)
        {
            return new Vector3((x - Size * 0.5f + 0.5f) * Tile, 0f, (z - Size * 0.5f + 0.5f) * Tile);
        }

        public static Vector2Int WorldToTile(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x / Tile + Size * 0.5f);
            int z = Mathf.FloorToInt(p.z / Tile + Size * 0.5f);
            return new Vector2Int(x, z);
        }

        public float HeightAt(int x, int z)
        {
            if (!InBounds(x, z)) return 0f;
            return heights[x, z];
        }

        /// <summary>Bilinear interpolated terrain height at a world position.</summary>
        public float HeightAtWorld(Vector3 p)
        {
            float fx = p.x / Tile + Size * 0.5f - 0.5f;
            float fz = p.z / Tile + Size * 0.5f - 0.5f;
            int x0 = Mathf.FloorToInt(fx), z0 = Mathf.FloorToInt(fz);
            float tx = fx - x0, tz = fz - z0;
            float a = HeightAt(x0, z0), b = HeightAt(x0 + 1, z0);
            float c = HeightAt(x0, z0 + 1), d = HeightAt(x0 + 1, z0 + 1);
            return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), tz);
        }

        public bool IsWalkableTile(int x, int z)
        {
            if (!InBounds(x, z)) return false;
            return blocked[z * Size + x] == 0 && occupied[z * Size + x] == 0;
        }

        /// <summary>Walkable for pathfinding: buildings block, resource nodes do not
        ///  (so workers can path right up to a mine).</summary>
        bool IsPathable(int x, int z)
        {
            if (!InBounds(x, z)) return false;
            return blocked[z * Size + x] == 0 && occupied[z * Size + x] == 0;
        }

        public bool IsWalkableWorld(Vector3 p)
        {
            Vector2Int t = WorldToTile(p);
            return IsWalkableTile(t.x, t.y);
        }

        /// <summary>Nearest tile to (x,z) that a unit can actually stand on.</summary>
        public Vector2Int NearestWalkable(int x, int z, int maxRadius = 8)
        {
            if (IsWalkableTile(x, z)) return new Vector2Int(x, z);
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        if (IsWalkableTile(x + dx, z + dz)) return new Vector2Int(x + dx, z + dz);
                    }
                }
            }
            return new Vector2Int(Mathf.Clamp(x, 4, Size - 5), Mathf.Clamp(z, 4, Size - 5));
        }

        public ResourceNode NodeAtTile(int x, int z)
        {
            if (!InBounds(x, z)) return null;
            int idx = nodeAt[z * Size + x];
            if (idx < 0 || idx >= Nodes.Count) return null;
            ResourceNode n = Nodes[idx];
            return n.Depleted ? null : n;
        }

        public ResourceNode NodeAtWorld(Vector3 p)
        {
            Vector2Int t = WorldToTile(p);
            return NodeAtTile(t.x, t.y);
        }

        /// <summary>Destroys tree nodes inside a radius (keeps gold mines).</summary>
        public void ClearNodesNear(Vector3 worldCenter, float radius)
        {
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                ResourceNode n = Nodes[i];
                if (n.kind != ResourceKind.Lumber || n.Depleted) continue;
                float dx = n.position.x - worldCenter.x;
                float dz = n.position.z - worldCenter.z;
                if (dx * dx + dz * dz > radius * radius) continue;
                if (n.go != null) Object.Destroy(n.go);
                n.amount = 0;
                RemoveNode(n);
            }
        }

        /// <summary>Removes a depleted node and frees its tiles.</summary>
        public void RemoveNode(ResourceNode n)
        {
            if (n == null) return;
            int r = n.kind == ResourceKind.Gold ? 1 : 0;
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = n.tile.x + dx, z = n.tile.y + dz;
                    if (!InBounds(x, z)) continue;
                    nodeAt[z * Size + x] = -1;
                    blocked[z * Size + x] = 0;
                }
        }

        // ===================================================================
        //  Building occupancy
        // ===================================================================

        public bool CanPlaceBuilding(Vector2Int origin, int sizeX, int sizeZ, bool ignoreResources = false)
        {
            for (int z = origin.y; z < origin.y + sizeZ; z++)
            {
                for (int x = origin.x; x < origin.x + sizeX; x++)
                {
                    if (!InBounds(x, z)) return false;
                    if (occupied[z * Size + x] != 0) return false;
                    if (blocked[z * Size + x] != 0) return false;
                    if (!ignoreResources && nodeAt[z * Size + x] >= 0) return false;
                }
            }
            return true;
        }

        public void SetOccupied(Vector2Int origin, int sizeX, int sizeZ, byte value)
        {
            for (int z = origin.y; z < origin.y + sizeZ; z++)
                for (int x = origin.x; x < origin.x + sizeX; x++)
                    if (InBounds(x, z)) occupied[z * Size + x] = value;
        }

        // ===================================================================
        //  A* pathfinding
        // ===================================================================

        static readonly Vector2Int[] Dirs4 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        static readonly Vector2Int[] DirsDiag =
        {
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        /// <summary>
        /// Returns a world-space path from <paramref name="from"/> to
        /// <paramref name="to"/>.  Falls back to the closest reachable tile.
        /// </summary>
        public List<Vector3> FindPath(Vector3 from, Vector3 to, int maxIterations = 20000)
        {
            Vector2Int start = WorldToTile(from);
            Vector2Int goal = WorldToTile(to);
            start = NearestWalkable(start.x, start.y, 4);

            if (!IsPathable(goal.x, goal.y))
                goal = NearestWalkable(goal.x, goal.y, 6);

            pathScratch.Clear();
            if (start == goal)
            {
                pathScratch.Add(to);
                return new List<Vector3>(pathScratch);
            }

            System.Array.Clear(state, 0, state.Length);
            open.Clear();

            int startIdx = start.y * Size + start.x;
            gScore[startIdx] = 0;
            cameFrom[startIdx] = -1;
            state[startIdx] = 1;
            open.Push(startIdx, Heuristic(start, goal));

            int bestIdx = startIdx;
            int bestH = Heuristic(start, goal);
            int iterations = 0;
            bool found = false;

            while (open.Count > 0 && iterations++ < maxIterations)
            {
                int cur = open.Pop();
                int curX = cur % Size, curZ = cur / Size;

                if (curX == goal.x && curZ == goal.y) { found = true; break; }

                state[cur] = 2;
                int hCur = Heuristic(new Vector2Int(curX, curZ), goal);
                if (hCur < bestH) { bestH = hCur; bestIdx = cur; }

                int gCur = gScore[cur];

                for (int i = 0; i < 4; i++)
                {
                    int nx = curX + Dirs4[i].x, nz = curZ + Dirs4[i].y;
                    if (!IsPathable(nx, nz)) continue;
                    Relax(nx, nz, gCur + 10, cur, goal);
                }

                for (int i = 0; i < 4; i++)
                {
                    int nx = curX + DirsDiag[i].x, nz = curZ + DirsDiag[i].y;
                    if (!IsPathable(nx, nz)) continue;
                    // no corner cutting
                    if (!IsPathable(curX + DirsDiag[i].x, curZ) || !IsPathable(curX, curZ + DirsDiag[i].y)) continue;
                    Relax(nx, nz, gCur + 14, cur, goal);
                }
            }

            int endIdx = found ? (goal.y * Size + goal.x) : bestIdx;
            Reconstruct(endIdx);
            return new List<Vector3>(pathScratch);
        }

        void Relax(int nx, int nz, int tentative, int from, Vector2Int goal)
        {
            int idx = nz * Size + nx;
            if (state[idx] == 2) return;
            if (state[idx] == 1 && tentative >= gScore[idx]) return;

            gScore[idx] = tentative;
            cameFrom[idx] = from;
            state[idx] = 1;
            open.Push(idx, tentative + Heuristic(new Vector2Int(nx, nz), goal));
        }

        static int Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dz = Mathf.Abs(a.y - b.y);
            int min = Mathf.Min(dx, dz);
            return 10 * (dx + dz) + (14 - 20) * min; // octile, integer scaled
        }

        void Reconstruct(int endIdx)
        {
            List<Vector2Int> tiles = new List<Vector2Int>();
            int cur = endIdx;
            int guard = 0;
            while (cur >= 0 && guard++ < Size * Size)
            {
                tiles.Add(new Vector2Int(cur % Size, cur / Size));
                cur = cameFrom[cur];
            }
            tiles.Reverse();

            if (tiles.Count > 0) tiles.RemoveAt(0); // drop the tile we stand on

            // Simple line-of-sight string pulling so units do not walk in stairs.
            int anchor = 0;
            for (int i = 2; i < tiles.Count; i++)
            {
                if (!LineWalkable(tiles[anchor], tiles[i]))
                {
                    Vector2Int t = tiles[i - 1];
                    pathScratch.Add(new Vector3(TileToWorld(t.x, t.y).x, 0f, TileToWorld(t.x, t.y).z));
                    anchor = i - 1;
                }
            }
            for (int i = anchor + 1; i < tiles.Count; i++)
            {
                Vector3 w = TileToWorld(tiles[i].x, tiles[i].y);
                pathScratch.Add(new Vector3(w.x, 0f, w.z));
            }
        }

        public bool LineWalkable(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(b.x - a.x), dz = Mathf.Abs(b.y - a.y);
            int sx = a.x < b.x ? 1 : -1, sz = a.y < b.y ? 1 : -1;
            int err = dx - dz;
            int x = a.x, z = a.y;
            int guard = 0;
            while (guard++ < 4096)
            {
                if (!IsPathable(x, z)) return false;
                if (x == b.x && z == b.y) return true;
                int e2 = 2 * err;
                if (e2 > -dz) { err -= dz; x += sx; }
                if (e2 < dx) { err += dx; z += sz; }
            }
            return false;
        }

        // ===================================================================
        //  Minimal binary heap for A*
        // ===================================================================
        class PriorityQueue
        {
            readonly List<int> keys = new List<int>(4096);
            readonly List<int> priorities = new List<int>(4096);

            public int Count { get { return keys.Count; } }

            public void Clear() { keys.Clear(); priorities.Clear(); }

            public void Push(int key, int priority)
            {
                keys.Add(key);
                priorities.Add(priority);
                int i = keys.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (priorities[parent] <= priorities[i]) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public int Pop()
            {
                int top = keys[0];
                int last = keys.Count - 1;
                keys[0] = keys[last];
                priorities[0] = priorities[last];
                keys.RemoveAt(last);
                priorities.RemoveAt(last);

                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, best = i;
                    if (l < keys.Count && priorities[l] < priorities[best]) best = l;
                    if (r < keys.Count && priorities[r] < priorities[best]) best = r;
                    if (best == i) break;
                    Swap(i, best);
                    i = best;
                }
                return top;
            }

            void Swap(int a, int b)
            {
                int tk = keys[a]; keys[a] = keys[b]; keys[b] = tk;
                int tp = priorities[a]; priorities[a] = priorities[b]; priorities[b] = tp;
            }
        }
    }
}
