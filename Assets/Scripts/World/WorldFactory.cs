using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Everything visual is generated procedurally at runtime: no art assets
    //  are required to run this project.  Meshes, materials, terrain, units,
    //  buildings, resource nodes and effects all come from here.
    // -----------------------------------------------------------------------
    public static class WorldFactory
    {
        static GameObject worldRoot;
        static GameObject unitRoot;
        static GameObject buildingRoot;
        static GameObject fxRoot;

        static readonly Dictionary<long, Material> materialCache = new Dictionary<long, Material>();
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        // ===================================================================
        //  Scene roots
        // ===================================================================

        public static GameObject Root(string name)
        {
            if (worldRoot == null)
            {
                worldRoot = new GameObject("[World]");
                unitRoot = new GameObject("Units");
                unitRoot.transform.SetParent(worldRoot.transform, false);
                buildingRoot = new GameObject("Buildings");
                buildingRoot.transform.SetParent(worldRoot.transform, false);
                fxRoot = new GameObject("FX");
                fxRoot.transform.SetParent(worldRoot.transform, false);
            }
            GameObject go = new GameObject(name);
            go.transform.SetParent(worldRoot.transform, false);
            return go;
        }

        public static Transform UnitRoot { get { return unitRoot != null ? unitRoot.transform : null; } }
        public static Transform BuildingRoot { get { return buildingRoot != null ? buildingRoot.transform : null; } }
        public static Transform FxRoot { get { return fxRoot != null ? fxRoot.transform : null; } }

        // ===================================================================
        //  Kenney model loading (CC0): real low-poly meshes with a
        //  procedural fallback when a model is missing.
        // ===================================================================

        static readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();

        public static GameObject TryModel(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            GameObject p;
            if (prefabCache.TryGetValue(name, out p)) return p;
            p = Resources.Load<GameObject>("Models/" + name);
            prefabCache[name] = p;
            return p;
        }

        static readonly Dictionary<string, Texture> texCache = new Dictionary<string, Texture>();

        static Texture LoadTex(string name)
        {
            Texture t;
            if (texCache.TryGetValue(name, out t)) return t;
            t = Resources.Load<Texture>("Models/" + name);
            texCache[name] = t;
            return t;
        }

        static void FixMaterials(GameObject go, Color tint, string texName)
        {
            Texture tex = texName != null ? LoadTex(texName) : null;
            bool wantTint = tint != Color.white;
            if (!wantTint && tex == null) return;
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rs.Length; i++)
            {
                Material[] ms = rs[i].sharedMaterials;
                for (int j = 0; j < ms.Length; j++)
                {
                    Material m = new Material(ms[j]);
                    if (wantTint) m.color = tint;
                    if (m.mainTexture == null && tex != null) m.mainTexture = tex;
                    ms[j] = m;
                }
                rs[i].sharedMaterials = ms;
            }
        }

        static Bounds Measure(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            for (int i = 0; i < rs.Length; i++)
            {
                if (i == 0) b = rs[i].bounds;
                else b.Encapsulate(rs[i].bounds);
            }
            return b;
        }

        /// <summary>Instantiates a model scaled to a target height, feet on
        ///  <paramref name="pos"/>, optionally tinted.</summary>
        public static GameObject PlaceModel(Transform parent, string name, Vector3 pos, Quaternion rot,
                                            float targetHeight, string objName, Color tint, string tex = null)
        {
            GameObject prefab = TryModel(name);
            if (prefab == null) return null;
            GameObject go = (GameObject)Object.Instantiate(prefab, parent);
            go.name = objName;
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;

            Bounds b = Measure(go);
            float s = targetHeight / Mathf.Max(0.01f, b.size.y);
            go.transform.localScale = go.transform.localScale * s;
            b = Measure(go);
            Vector3 wp = parent.TransformPoint(pos);
            go.transform.position += new Vector3(wp.x - b.center.x, wp.y - b.min.y, wp.z - b.center.z);

            FixMaterials(go, tint, tex);
            return go;
        }

        /// <summary>Instantiates a model scaled so its X size matches
        ///  <paramref name="fitWidth"/> world units.</summary>
        public static GameObject PlaceModelFit(Transform parent, string name, float fitWidth, float baseWidth,
                                               string tex = null)
        {
            GameObject prefab = TryModel(name);
            if (prefab == null) return null;
            GameObject go = (GameObject)Object.Instantiate(prefab, parent);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            float s = fitWidth / Mathf.Max(0.01f, baseWidth);
            go.transform.localScale = go.transform.localScale * s;
            Bounds b = Measure(go);
            Vector3 wp = parent.position;
            go.transform.position += new Vector3(wp.x - b.center.x, wp.y - b.min.y, wp.z - b.center.z);
            FixMaterials(go, Color.white, tex);
            return go;
        }

        static string UnitModelName(UnitId id)
        {
            switch (id)
            {
                case UnitId.Peasant: return "man";
                case UnitId.Footman: return "knight";
                case UnitId.Archer: return "man";
                case UnitId.Knight: return "knight";
                case UnitId.Paladin: return "knight";
                case UnitId.Archmage: return "woman";
                case UnitId.Priest: return "man";
                case UnitId.Sorceress: return "woman";
                case UnitId.Peon: return "man";
                case UnitId.Grunt: return "knight";
                case UnitId.TrollAxethrower: return "man";
                case UnitId.Ogre: return "knight";
                case UnitId.Blademaster: return "knight";
                case UnitId.FarSeer: return "woman";
                default: return null;
            }
        }

        /// <summary>Quaternius character FBX materials reference these skin
        /// textures by name; assign them at runtime when the importer left
        /// the slot empty so units are never flat grey.</summary>
        static string UnitTexName(string model)
        {
            if (model == "knight") return "ClothedDarkSkin1";
            if (model == "man") return "ClothedLightSkin1";
            if (model == "woman") return "ClothedLightSkin2";
            return null;
        }

        static string BuildingModelName(BuildingId id)
        {
            switch (id)
            {
                case BuildingId.TownHall:
                case BuildingId.GreatHall:
                case BuildingId.Stronghold: return "town_hall";
                case BuildingId.Farm:
                case BuildingId.Burrow: return "farm";
                case BuildingId.Barracks:
                case BuildingId.TrollHut: return "barracks";
                case BuildingId.LumberMill:
                case BuildingId.WarMill: return "lumber_mill";
                case BuildingId.Blacksmith: return "blacksmith";
                case BuildingId.GuardTower: return "watchtower";
                case BuildingId.WatchTower: return "watchtower";
                case BuildingId.AltarOfKings:
                case BuildingId.AltarOfStorms: return "altar";
                case BuildingId.ArcaneSanctum:
                case BuildingId.SpiritLodge: return "pointytower";
                default: return null;
            }
        }

        static float ModelBaseWidth(BuildingId id)
        {
            switch (id)
            {
                case BuildingId.TownHall:
                case BuildingId.GreatHall:
                case BuildingId.Stronghold: return 5f;
                case BuildingId.Barracks:
                case BuildingId.TrollHut:
                case BuildingId.LumberMill:
                case BuildingId.WarMill: return 4f;
                case BuildingId.GuardTower:
                case BuildingId.WatchTower: return 3f;
                default: return 3f;
            }
        }

        // ===================================================================
        //  Materials
        // ===================================================================

        static long MakeKey(Color c, int flags)
        {
            long r = (long)(c.r * 255f) << 40;
            long g = (long)(c.g * 255f) << 32;
            long b = (long)(c.b * 255f) << 24;
            long a = (long)(c.a * 255f) << 16;
            return r | g | b | a | ((long)flags & 0xFFFF);
        }

        /// <summary>Opaque, cached standard material.</summary>
        public static Material Solid(Color c)
        {
            long key = MakeKey(c, 0);
            Material m;
            if (materialCache.TryGetValue(key, out m)) return m;

            Shader sh = Shader.Find("RtsWarcraft/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
            m = new Material(sh);
            m.color = c;
            materialCache[key] = m;
            return m;
        }

        /// <summary>Translucent, cached material used for ghosts and effects.</summary>
        public static Material Transparent(Color c)
        {
            long key = MakeKey(c, 1);
            Material m;
            if (materialCache.TryGetValue(key, out m)) return m;

            Shader sh = Shader.Find("RtsWarcraft/Transparent");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            m = new Material(sh);
            m.color = c;
            m.renderQueue = 3000;
            materialCache[key] = m;
            return m;
        }

        /// <summary>Unlit additive material (fog plane, beams, glows).</summary>
        public static Material Additive(Color c)
        {
            long key = MakeKey(c, 2);
            Material m;
            if (materialCache.TryGetValue(key, out m)) return m;

            Shader sh = Shader.Find("Mobile/Particles/Additive");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            m = new Material(sh);
            m.color = c;
            m.renderQueue = 3100;
            materialCache[key] = m;
            return m;
        }

        // ===================================================================
        //  Primitive meshes
        // ===================================================================

        public static Mesh BoxMesh(Vector3 size)
        {
            string key = "box_" + size.x.ToString("F2") + "_" + size.y.ToString("F2") + "_" + size.z.ToString("F2");
            Mesh cached;
            if (meshCache.TryGetValue(key, out cached)) return cached;

            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            Vector3[] v =
            {
                // +X
                new Vector3(hx, -hy, hz), new Vector3(hx, -hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz),
                // -X
                new Vector3(-hx, -hy, -hz), new Vector3(-hx, -hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, hy, -hz),
                // +Y
                new Vector3(-hx, hy, hz), new Vector3(hx, hy, hz), new Vector3(hx, hy, -hz), new Vector3(-hx, hy, -hz),
                // -Y
                new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz), new Vector3(-hx, -hy, hz),
                // +Z
                new Vector3(-hx, -hy, hz), new Vector3(hx, -hy, hz), new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz),
                // -Z
                new Vector3(hx, -hy, -hz), new Vector3(-hx, -hy, -hz), new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz)
            };

            int[] tris = new int[36];
            for (int f = 0; f < 6; f++)
            {
                int o = f * 4, t = f * 6;
                tris[t + 0] = o + 0; tris[t + 1] = o + 1; tris[t + 2] = o + 2;
                tris[t + 3] = o + 0; tris[t + 4] = o + 2; tris[t + 5] = o + 3;
            }

            Mesh mesh = new Mesh();
            mesh.name = "Box_" + key;
            mesh.vertices = v;
            mesh.triangles = tris;            // Unity generates shared normals
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshCache[key] = mesh;
            return mesh;
        }

        public static Mesh CylinderMesh(float radius, float height, int segments)
        {
            string key = "cyl_" + radius.ToString("F2") + "_" + height.ToString("F2") + "_" + segments;
            Mesh cached;
            if (meshCache.TryGetValue(key, out cached)) return cached;

            segments = Mathf.Max(4, segments);
            List<Vector3> verts = new List<Vector3>((segments + 1) * 4);
            List<int> tris = new List<int>(segments * 12);

            int ringBottom = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            int ringTop = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius));
            }
            int capTop = verts.Count;
            verts.Add(new Vector3(0f, height, 0f));
            int capBottom = verts.Count;
            verts.Add(new Vector3(0f, 0f, 0f));

            for (int i = 0; i < segments; i++)
            {
                int b0 = ringBottom + i, b1 = ringBottom + i + 1;
                int t0 = ringTop + i, t1 = ringTop + i + 1;
                tris.Add(b0); tris.Add(b1); tris.Add(t1);
                tris.Add(b0); tris.Add(t1); tris.Add(t0);

                tris.Add(capTop); tris.Add(t1); tris.Add(t0);
                tris.Add(capBottom); tris.Add(b1); tris.Add(b0);
            }

            Mesh mesh = new Mesh();
            mesh.name = key;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshCache[key] = mesh;
            return mesh;
        }

        public static Mesh ConeMesh(float radius, float height, int segments)
        {
            string key = "cone_" + radius.ToString("F2") + "_" + height.ToString("F2") + "_" + segments;
            Mesh cached;
            if (meshCache.TryGetValue(key, out cached)) return cached;

            segments = Mathf.Max(4, segments);
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            int ring = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            int apex = verts.Count;
            verts.Add(new Vector3(0f, height, 0f));
            int center = verts.Count;
            verts.Add(new Vector3(0f, 0f, 0f));

            for (int i = 0; i < segments; i++)
            {
                tris.Add(ring + i); tris.Add(ring + i + 1); tris.Add(apex);
                tris.Add(center); tris.Add(ring + i + 1); tris.Add(ring + i);
            }

            Mesh mesh = new Mesh();
            mesh.name = key;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshCache[key] = mesh;
            return mesh;
        }

        public static Mesh SphereMesh(float radius, int rings)
        {
            string key = "sph_" + radius.ToString("F2") + "_" + rings;
            Mesh cached;
            if (meshCache.TryGetValue(key, out cached)) return cached;

            rings = Mathf.Max(3, rings);
            int segments = rings * 2;
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            for (int y = 0; y <= rings; y++)
            {
                float phi = (y / (float)rings) * Mathf.PI;
                for (int x = 0; x <= segments; x++)
                {
                    float theta = (x / (float)segments) * Mathf.PI * 2f;
                    verts.Add(new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta) * radius,
                        Mathf.Cos(phi) * radius,
                        Mathf.Sin(phi) * Mathf.Sin(theta) * radius));
                }
            }
            int stride = segments + 1;
            for (int y = 0; y < rings; y++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int a = y * stride + x;
                    int b = a + stride;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = key;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshCache[key] = mesh;
            return mesh;
        }

        /// <summary>Flat quad on the XZ plane, centred on the origin.</summary>
        public static Mesh QuadXZ(float size)
        {
            string key = "quad_" + size.ToString("F2");
            Mesh cached;
            if (meshCache.TryGetValue(key, out cached)) return cached;

            float h = size * 0.5f;
            Mesh mesh = new Mesh();
            mesh.name = key;
            mesh.vertices = new[]
            {
                new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h),
                new Vector3(h, 0f, h), new Vector3(-h, 0f, h)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshCache[key] = mesh;
            return mesh;
        }

        /// <summary>Flat ring on the XZ plane (selection circles).</summary>
        public static Mesh RingXZ(float outer, float inner)
        {
            string key = "ring_" + outer.ToString("F2") + "_" + inner.ToString("F2");
            Mesh cached;
            if (meshCache.TryGetValue(key, out cached)) return cached;

            const int segments = 28;
            List<Vector3> verts = new List<Vector3>((segments + 1) * 2);
            List<int> tris = new List<int>(segments * 6);
            List<Vector2> uvs = new List<Vector2>((segments + 1) * 2);

            for (int i = 0; i <= segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                verts.Add(new Vector3(ca * outer, 0f, sa * outer));
                verts.Add(new Vector3(ca * inner, 0f, sa * inner));
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                int o = i * 2;
                tris.Add(o); tris.Add(o + 1); tris.Add(o + 3);
                tris.Add(o); tris.Add(o + 3); tris.Add(o + 2);
            }

            Mesh mesh = new Mesh();
            mesh.name = key;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshCache[key] = mesh;
            return mesh;
        }

        // ===================================================================
        //  Mesh helpers
        // ===================================================================

        public static GameObject Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go;
        }

        // ===================================================================
        //  Terrain
        // ===================================================================

        static readonly Color GrassLow = new Color(0.24f, 0.46f, 0.20f);
        static readonly Color GrassHigh = new Color(0.42f, 0.55f, 0.24f);
        static readonly Color Rock = new Color(0.45f, 0.42f, 0.38f);
        static readonly Color Dirt = new Color(0.48f, 0.38f, 0.24f);
        static readonly Color Shore = new Color(0.30f, 0.45f, 0.40f);

        public static void BuildTerrain(GameMap map)
        {
            int size = GameMap.Size;
            GameObject go = Root("Terrain");

            Vector3[] verts = new Vector3[size * size];
            Color[] colors = new Color[size * size];
            Vector2[] uvs = new Vector2[size * size];

            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = z * size + x;
                    Vector3 w = GameMap.TileToWorld(x, z);
                    float h = map.HeightAt(x, z);
                    verts[i] = new Vector3(w.x, h, w.z);
                    uvs[i] = new Vector2(x / (float)size, z / (float)size);
                    colors[i] = TerrainColor(h, x, z, size);
                }
            }

            int[] tris = new int[(size - 1) * (size - 1) * 6];
            int t = 0;
            for (int z = 0; z < size - 1; z++)
            {
                for (int x = 0; x < size - 1; x++)
                {
                    int a = z * size + x;
                    int b = a + 1;
                    int c = a + size;
                    int d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "Terrain";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = TerrainMaterial();
            mr.receiveShadows = true;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static readonly Color Sand = new Color(0.72f, 0.65f, 0.44f);
        static readonly Color WetSand = new Color(0.45f, 0.44f, 0.34f);
        static readonly Color DeepWater = new Color(0.08f, 0.24f, 0.34f);

        static Color TerrainColor(float h, int x, int z, int size)
        {
            bool border = x < 3 || z < 3 || x >= size - 3 || z >= size - 3;
            if (border || h > 2.7f) return Rock;
            if (h < 0.05f) return DeepWater;
            if (h < 0.34f) return WetSand;
            if (h < 0.75f) return Color.Lerp(Sand, GrassLow, Mathf.Clamp01((h - 0.34f) / 0.41f));
            if (h > 2.1f) return Color.Lerp(GrassHigh, Rock, Mathf.Clamp01((h - 2.1f) / 0.6f));

            // Fine deterministic speckle so the ground is not perfectly flat in colour.
            int n = (x * 73856093) ^ (z * 19349663);
            n = (n >> 3) & 255;
            float jitter = (n / 255f) * 0.07f - 0.035f;

            // Low-frequency patches so grass reads as clumps instead of noise.
            int px = x / 5, pz = z / 5;
            int m = (px * 668265263) ^ (pz * 1103515245);
            m = (m >> 5) & 255;
            float patch = (m / 255f) - 0.5f;

            Color c = Color.Lerp(GrassLow, GrassHigh, Mathf.Clamp01(h / 2.2f));
            c = Color.Lerp(c, Sand, Mathf.Clamp01(0.18f + patch * 0.22f) * 0.35f);
            c.r += jitter; c.g += jitter; c.b += jitter * 0.5f;
            return c;
        }

        public static void BuildSky()
        {
            GameObject go = Root("Sky");
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = SphereMesh(380f, 12);
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            Shader sh = Shader.Find("RtsWarcraft/Sky");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            mr.sharedMaterial = new Material(sh);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static void BuildWater(GameMap map)
        {
            GameObject go = Root("Water");
            float extent = GameMap.Size * GameData.TileSize;
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = QuadXZ(extent + 8f);
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            Shader sh = Shader.Find("RtsWarcraft/Water");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            Material m = new Material(sh);
            m.renderQueue = 3000;
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.position = new Vector3(0f, 0.26f, 0f);
        }

        static Material terrainMat;
        static Material TerrainMaterial()
        {
            if (terrainMat != null) return terrainMat;
            Shader sh = Shader.Find("RtsWarcraft/Terrain");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            terrainMat = new Material(sh);
            return terrainMat;
        }

        // ===================================================================
        //  Resource nodes
        // ===================================================================

        static readonly Color GoldColor = new Color(1.00f, 0.82f, 0.20f);
        static readonly Color GoldRock = new Color(0.50f, 0.45f, 0.35f);
        static readonly Color TreeTrunk = new Color(0.36f, 0.25f, 0.15f);
        static readonly Color TreeLeaf = new Color(0.15f, 0.42f, 0.18f);

        static Mesh goldMineMesh;
        static Mesh treeTrunkMesh;
        static Mesh treeLeafMesh;
        static Mesh treeLeafTopMesh;
        static Mesh nuggetMesh;
        static Material[] leafMats;

        public static void BuildResourceNodes(GameMap map)
        {
            GameObject root = Root("Resources");

            if (goldMineMesh == null)
            {
                goldMineMesh = BoxMesh(new Vector3(2.6f, 1.6f, 2.6f));
                treeTrunkMesh = CylinderMesh(0.22f, 2.2f, 6);
                treeLeafMesh = ConeMesh(1.15f, 2.6f, 7);
                treeLeafTopMesh = ConeMesh(0.80f, 1.9f, 7);
                nuggetMesh = BoxMesh(new Vector3(0.5f, 0.5f, 0.5f));
                leafMats = new[]
                {
                    Solid(new Color(0.10f, 0.34f, 0.13f)),
                    Solid(new Color(0.14f, 0.44f, 0.17f)),
                    Solid(new Color(0.19f, 0.50f, 0.21f))
                };
            }

            Material goldMat = Solid(GoldColor);
            Material rockMat = Solid(GoldRock);
            Material trunkMat = Solid(TreeTrunk);

            for (int i = 0; i < map.Nodes.Count; i++)
            {
                ResourceNode n = map.Nodes[i];
                Vector3 p = n.position;
                p.y = map.HeightAtWorld(p);
                GameObject go;

                if (n.kind == ResourceKind.Gold)
                {
                    go = new GameObject("GoldMine_" + i);
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = p;
                    GameObject mineModel = PlaceModel(go.transform, "goldmine", Vector3.zero, Quaternion.identity,
                                                      2.4f, "Model", Color.white, "colormap_ft");
                    if (mineModel == null)
                    {
                        Part(go.transform, goldMineMesh, rockMat, new Vector3(0f, 0.55f, 0f),
                             Quaternion.Euler(0f, 18f, 0f), "Mound").transform.localScale =
                            new Vector3(1.15f, 0.55f, 0.5f * 2.3f);
                        Part(go.transform, goldMineMesh, rockMat, new Vector3(-0.7f, 0.35f, 0.5f),
                             Quaternion.Euler(8f, 64f, 0f), "RockA").transform.localScale =
                            new Vector3(0.5f, 0.4f, 0.5f);
                        Part(go.transform, goldMineMesh, rockMat, new Vector3(0.8f, 0.3f, -0.4f),
                             Quaternion.Euler(-6f, 120f, 0f), "RockB").transform.localScale =
                            new Vector3(0.45f, 0.35f, 0.45f);
                        Part(go.transform, nuggetMesh, goldMat, new Vector3(0.2f, 1.0f, 0.3f),
                             Quaternion.Euler(20f, 30f, 0f), "NuggetA");
                        Part(go.transform, nuggetMesh, goldMat, new Vector3(-0.5f, 0.8f, -0.4f),
                             Quaternion.Euler(0f, 70f, 10f), "NuggetB");
                        Part(go.transform, nuggetMesh, goldMat, new Vector3(0.6f, 0.75f, 0.6f),
                             Quaternion.Euler(10f, 10f, 0f), "NuggetC");
                    }
                }
                else
                {
                    go = new GameObject("Tree_" + i);
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = p;
                    int hv = ((i * 654435761) >> 8) & 255;
                    float sc = 0.85f + (hv / 255f) * 0.45f;
                    go.transform.localScale = new Vector3(sc, sc * (0.9f + (((hv >> 3) & 31) / 31f) * 0.35f), sc);
                    go.transform.localRotation = Quaternion.Euler(0f, (hv * 7) % 360, 0f);
                    GameObject treeModel = PlaceModel(go.transform, hv % 2 == 0 ? "tree" : "tree-high",
                                                        Vector3.zero, Quaternion.identity, 2.6f, "Model",
                                                        new Color(0.55f, 0.78f, 0.52f), "colormap_mf");
                    if (treeModel == null)
                    {
                        Part(go.transform, treeTrunkMesh, trunkMat, Vector3.zero, Quaternion.identity, "Trunk");
                        Material leaf = leafMats[hv % 3];
                        Part(go.transform, treeLeafMesh, leaf, new Vector3(0f, 1.55f, 0f), Quaternion.identity, "LeavesLow");
                        Part(go.transform, treeLeafTopMesh, leaf, new Vector3(0f, 2.6f, 0f), Quaternion.identity, "LeavesTop");
                    }
                }

                SelectableMarker marker = go.AddComponent<SelectableMarker>();
                marker.Init(n);
                n.go = go;
            }
        }

        // ===================================================================
        //  Units
        // ===================================================================

        public static GameObject CreateUnitVisual(UnitDef def, RTSPlayer owner)
        {
            GameObject go = new GameObject(def.displayName);
            if (UnitRoot != null) go.transform.SetParent(UnitRoot, false);

            Color body = Color.Lerp(owner.color, Color.white, 0.35f);
            Color trim = owner.color;
            Color skin = new Color(0.90f, 0.75f, 0.60f);
            Color metal = new Color(0.62f, 0.64f, 0.70f);

            Vector3 s = def.visualScale;

            string umn = UnitModelName(def.id);
            GameObject model = PlaceModel(go.transform, umn, Vector3.zero, Quaternion.identity,
                                          def.visualScale.y * (def.isHero ? 1.35f : 1.15f), "Model",
                                          Color.Lerp(owner.color, Color.white, 0.85f), UnitTexName(umn));
            if (model != null)
            {
                RtsAnimator ra = model.AddComponent<RtsAnimator>();
                ra.Init(umn);

                Part(go.transform, ConeMesh(s.x * 0.16f, s.y * 0.22f, 5), Solid(owner.color),
                     new Vector3(0f, def.visualScale.y * (def.isHero ? 1.5f : 1.3f), 0f),
                     Quaternion.identity, "Plume");
                GameObject mRing = Part(go.transform, RingXZ(def.radius * 1.9f, def.radius * 1.55f),
                                        Transparent(GameData.SelectionGreen),
                                        new Vector3(0f, 0.06f, 0f), Quaternion.identity, "SelectionRing");
                mRing.SetActive(false);
                SphereCollider mCol = go.AddComponent<SphereCollider>();
                mCol.isTrigger = true;
                mCol.center = new Vector3(0f, s.y * 0.5f, 0f);
                mCol.radius = Mathf.Max(def.radius, s.y * 0.5f);
                return go;
            }

            // Torso
            Part(go.transform, BoxMesh(new Vector3(s.x * 0.95f, s.y * 0.45f, s.z * 0.6f)), Solid(body),
                 new Vector3(0f, s.y * 0.42f, 0f), Quaternion.identity, "Torso");

            // Legs
            Part(go.transform, BoxMesh(new Vector3(s.x * 0.3f, s.y * 0.32f, s.z * 0.32f)), Solid(trim),
                 new Vector3(-s.x * 0.22f, s.y * 0.16f, 0f), Quaternion.identity, "LegL");
            Part(go.transform, BoxMesh(new Vector3(s.x * 0.3f, s.y * 0.32f, s.z * 0.32f)), Solid(trim),
                 new Vector3(s.x * 0.22f, s.y * 0.16f, 0f), Quaternion.identity, "LegR");

            // Head
            Part(go.transform, SphereMesh(s.x * 0.30f, 5), Solid(skin),
                 new Vector3(0f, s.y * 0.76f, 0f), Quaternion.identity, "Head");

            // Weapon / caster staff
            if (def.unitClass == UnitClass.Caster && def.maxMana > 0f)
            {
                Part(go.transform, CylinderMesh(s.x * 0.06f, s.y * 0.75f, 5), Solid(TreeTrunk),
                     new Vector3(s.x * 0.55f, s.y * 0.30f, 0.1f), Quaternion.Euler(8f, 0f, -6f), "Staff");
                Part(go.transform, SphereMesh(s.x * 0.16f, 4), Solid(new Color(0.5f, 0.8f, 1f)),
                     new Vector3(s.x * 0.55f, s.y * 1.02f, 0.1f), Quaternion.identity, "Orb");
            }
            else if (def.rangedAttack)
            {
                Part(go.transform, BoxMesh(new Vector3(s.x * 0.08f, s.y * 0.55f, s.z * 0.08f)), Solid(TreeTrunk),
                     new Vector3(s.x * 0.5f, s.y * 0.45f, 0.2f), Quaternion.Euler(0f, 0f, 12f), "Bow");
            }
            else
            {
                Part(go.transform, BoxMesh(new Vector3(s.x * 0.12f, s.y * 0.55f, s.z * 0.12f)), Solid(metal),
                     new Vector3(s.x * 0.55f, s.y * 0.45f, 0.15f), Quaternion.Euler(18f, 0f, -10f), "Weapon");
                Part(go.transform, BoxMesh(new Vector3(s.x * 0.62f, s.y * 0.42f, s.z * 0.10f)), Solid(trim),
                     new Vector3(-s.x * 0.5f, s.y * 0.45f, 0f), Quaternion.identity, "Shield");
            }

            // Hero crown
            if (def.isHero)
            {
                Part(go.transform, ConeMesh(s.x * 0.28f, s.y * 0.22f, 6), Solid(GoldColor),
                     new Vector3(0f, s.y * 0.92f, 0f), Quaternion.identity, "Crown");
            }

            // Team colour plume so factions are readable at a glance.
            Part(go.transform, ConeMesh(s.x * 0.16f, s.y * 0.22f, 5), Solid(trim),
                 new Vector3(0f, s.y * 0.98f + (def.isHero ? s.y * 0.22f : 0f), 0f),
                 Quaternion.identity, "Plume");

            // Selection ring (hidden until selected).
            GameObject ring = Part(go.transform, RingXZ(def.radius * 1.9f, def.radius * 1.55f),
                                   Transparent(GameData.SelectionGreen),
                                   new Vector3(0f, 0.06f, 0f), Quaternion.identity, "SelectionRing");
            ring.SetActive(false);

            // Trigger collider used by mouse picking.
            SphereCollider col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, s.y * 0.5f, 0f);
            col.radius = Mathf.Max(def.radius, s.y * 0.5f);
            col.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast") == -1 ? 0 : 0;

            return go;
        }

        // ===================================================================
        //  Buildings
        // ===================================================================

        public static BuildingController CreateBuilding(RTSPlayer owner, BuildingId id, BuildingDef def, Vector2Int origin)
        {
            Vector3 center = BuildingCenter(origin, def);
            GameMap map = GameManager.I.Map;
            center.y = map.HeightAtWorld(center);

            GameObject go = new GameObject(def.displayName);
            go.transform.SetParent(BuildingRoot, false);
            go.transform.position = center;

            float w = def.sizeX * GameData.TileSize * 0.92f;
            float d = def.sizeZ * GameData.TileSize * 0.92f;
            Color stone = new Color(0.62f, 0.58f, 0.50f);
            Color body = Color.Lerp(stone, owner.color, 0.22f);
            Color roof = Color.Lerp(owner.color, Color.black, 0.12f);
            Color wood = TreeTrunk;

            bool isTower = def.id == BuildingId.GuardTower || def.id == BuildingId.WatchTower;
            bool isHall = def.isResourceDropoff;
            bool isFarm = def.supplyProvided > 0;

            string bmn = BuildingModelName(id);
            // The Quaternius tower FBXs are vertex-coloured, not UV mapped to
            // the Kenney colormap - give them a stone tint instead.
            string btex = (bmn == "watchtower" || bmn == "pointytower") ? null : "colormap_ft";
            GameObject bModel = PlaceModelFit(go.transform, bmn, w, ModelBaseWidth(id), btex);
            if (bModel != null)
            {
                if (isTower)
                {
                    Renderer[] trs = bModel.GetComponentsInChildren<Renderer>();
                    for (int ti = 0; ti < trs.Length; ti++)
                    {
                        Material[] tms = trs[ti].sharedMaterials;
                        for (int tj = 0; tj < tms.Length; tj++)
                        {
                            Material tm = new Material(tms[tj]);
                            tm.color = Color.Lerp(stone, owner.color, 0.15f);
                            tms[tj] = tm;
                        }
                        trs[ti].sharedMaterials = tms;
                    }
                }
                float bRingR = Mathf.Max(w, d) * 0.62f;
                GameObject bRing = Part(go.transform, RingXZ(bRingR, bRingR * 0.86f),
                                        Transparent(GameData.SelectionGreen),
                                        new Vector3(0f, 0.07f, 0f), Quaternion.identity, "SelectionRing");
                bRing.SetActive(false);
                GameObject bScaffold = Part(go.transform, BoxMesh(new Vector3(w * 1.15f, 0.12f, d * 1.15f)),
                                            Transparent(new Color(1f, 1f, 1f, 0.35f)),
                                            new Vector3(0f, 0.1f, 0f), Quaternion.identity, "Scaffold");
                bScaffold.SetActive(false);
                BoxCollider bCol = go.AddComponent<BoxCollider>();
                bCol.center = new Vector3(0f, 1.6f, 0f);
                bCol.size = new Vector3(w, 3.4f, d);
                SelectableMarker bMarker = go.AddComponent<SelectableMarker>();
                BuildingController bCtrl = go.AddComponent<BuildingController>();
                bCtrl.Init(owner, id, def, origin, bRing, bScaffold);
                bMarker.Init(bCtrl);
                return bCtrl;
            }

            if (isTower)
            {
                Part(go.transform, CylinderMesh(w * 0.45f, 4.2f, 8), Solid(body),
                     new Vector3(0f, 0f, 0f), Quaternion.identity, "Shaft");
                Part(go.transform, CylinderMesh(w * 0.58f, 0.6f, 8), Solid(roof),
                     new Vector3(0f, 4.0f, 0f), Quaternion.identity, "Parapet");
                Part(go.transform, ConeMesh(w * 0.55f, 1.8f, 8), Solid(roof),
                     new Vector3(0f, 4.6f, 0f), Quaternion.identity, "Roof");
            }
            else
            {
                float h = isHall ? 3.2f : (isFarm ? 1.8f : 2.6f);
                Part(go.transform, BoxMesh(new Vector3(w, h, d)), Solid(body),
                     new Vector3(0f, h * 0.5f, 0f), Quaternion.identity, "Walls");

                // Pitched roof made of two slabs.
                float rise = 1.4f;
                float half = d * 0.5f;
                float roofLen = Mathf.Sqrt(half * half + rise * rise);
                float angle = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;
                Part(go.transform, BoxMesh(new Vector3(w * 1.05f, 0.16f, roofLen)), Solid(roof),
                     new Vector3(0f, h + rise * 0.5f, -half * 0.5f),
                     Quaternion.Euler(-angle, 0f, 0f), "RoofA");
                Part(go.transform, BoxMesh(new Vector3(w * 1.05f, 0.16f, roofLen)), Solid(roof),
                     new Vector3(0f, h + rise * 0.5f, half * 0.5f),
                     Quaternion.Euler(angle, 0f, 0f), "RoofB");

                // Door
                Part(go.transform, BoxMesh(new Vector3(w * 0.22f, h * 0.55f, 0.12f)), Solid(wood),
                     new Vector3(0f, h * 0.27f, d * 0.5f), Quaternion.identity, "Door");

                if (isHall)
                {
                    Part(go.transform, CylinderMesh(w * 0.10f, 5.0f, 6), Solid(body),
                         new Vector3(-w * 0.35f, 0f, -d * 0.35f), Quaternion.identity, "TowerL");
                    Part(go.transform, CylinderMesh(w * 0.10f, 5.0f, 6), Solid(body),
                         new Vector3(w * 0.35f, 0f, d * 0.35f), Quaternion.identity, "TowerR");
                    Part(go.transform, SphereMesh(0.45f, 5), Solid(GoldColor),
                         new Vector3(0f, h + 1.6f, 0f), Quaternion.identity, "Finial");
                    Part(go.transform, CylinderMesh(0.09f, 2.2f, 5), Solid(wood),
                         new Vector3(w * 0.35f, h + rise + 0.2f, -d * 0.35f), Quaternion.identity, "FlagPole");
                    Part(go.transform, BoxMesh(new Vector3(1.1f, 0.55f, 0.05f)), Solid(owner.color),
                         new Vector3(w * 0.35f + 0.6f, h + rise + 0.95f, -d * 0.35f), Quaternion.identity, "Banner");
                }
                if (isFarm)
                {
                    Part(go.transform, BoxMesh(new Vector3(w * 0.5f, 1.1f, d * 0.5f)), Solid(new Color(0.85f, 0.78f, 0.45f)),
                         new Vector3(w * 0.35f, 0.55f, d * 0.35f), Quaternion.identity, "HayBale");
                }
            }

            // Build progress scaffold (visible while under construction).
            GameObject scaffold = Part(go.transform, BoxMesh(new Vector3(w * 1.15f, 0.12f, d * 1.15f)),
                                       Transparent(new Color(1f, 1f, 1f, 0.35f)),
                                       new Vector3(0f, 0.1f, 0f), Quaternion.identity, "Scaffold");
            scaffold.SetActive(false);

            // Selection ring
            float ringR = Mathf.Max(w, d) * 0.62f;
            GameObject ring = Part(go.transform, RingXZ(ringR, ringR * 0.86f),
                                   Transparent(GameData.SelectionGreen),
                                   new Vector3(0f, 0.07f, 0f), Quaternion.identity, "SelectionRing");
            ring.SetActive(false);

            // Mouse picking volume.
            BoxCollider col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, (isTower ? 2.6f : 1.5f), 0f);
            col.size = new Vector3(w, isTower ? 5.4f : 3.2f, d);

            SelectableMarker marker = go.AddComponent<SelectableMarker>();
            BuildingController controller = go.AddComponent<BuildingController>();
            controller.Init(owner, id, def, origin, ring, scaffold);
            marker.Init(controller);

            return controller;
        }

        public static Vector3 BuildingCenter(Vector2Int origin, BuildingDef def)
        {
            float cx = (origin.x + def.sizeX * 0.5f);
            float cz = (origin.y + def.sizeZ * 0.5f);
            Vector3 w = GameMap.TileToWorld(Mathf.FloorToInt(cx), Mathf.FloorToInt(cz));
            w.x = (cx - GameMap.Size * 0.5f) * GameData.TileSize;
            w.z = (cz - GameMap.Size * 0.5f) * GameData.TileSize;
            return w;
        }

        // ===================================================================
        //  Effects
        // ===================================================================

        public static GameObject CreateSelectionGhost(BuildingDef def, bool valid)
        {
            GameObject go = new GameObject("BuildingGhost");
            go.transform.SetParent(FxRoot, false);
            float w = def.sizeX * GameData.TileSize;
            float d = def.sizeZ * GameData.TileSize;
            Color c = valid ? new Color(0.2f, 1f, 0.2f, 0.35f) : new Color(1f, 0.2f, 0.2f, 0.35f);
            GameObject plate = Part(go.transform, QuadXZ(1f), Transparent(c),
                                    new Vector3(0f, 0.1f, 0f), Quaternion.identity, "Plate");
            plate.transform.localScale = new Vector3(w, 1f, d);

            GameObject volume = Part(go.transform, BoxMesh(new Vector3(w, 2.5f, d)),
                                     Transparent(new Color(1f, 1f, 1f, 0.12f)),
                                     new Vector3(0f, 1.25f, 0f), Quaternion.identity, "Volume");
            return go;
        }

        public static GameObject CreateProjectile(Color color, float size)
        {
            GameObject go = new GameObject("Projectile");
            go.transform.SetParent(FxRoot, false);
            Part(go.transform, SphereMesh(size, 4), Solid(color), Vector3.zero, Quaternion.identity, "Body");
            Part(go.transform, SphereMesh(size * 1.8f, 4), Transparent(new Color(color.r, color.g, color.b, 0.25f)),
                 Vector3.zero, Quaternion.identity, "Glow");
            return go;
        }

        public static GameObject CreateBeam(Vector3 from, Vector3 to, Color color, float width)
        {
            GameObject go = new GameObject("Beam");
            go.transform.SetParent(FxRoot, false);
            Vector3 dir = to - from;
            float len = dir.magnitude;
            GameObject beam = Part(go.transform, BoxMesh(new Vector3(width, width, 1f)), Solid(color),
                                   from + dir * 0.5f,
                                   Quaternion.LookRotation(dir, Vector3.up), "Segment");
            beam.transform.localScale = new Vector3(1f, 1f, Mathf.Max(0.01f, len));
            return go;
        }

        public static GameObject CreateAoEMarker(Vector3 center, float radius, Color color)
        {
            GameObject go = new GameObject("AoE");
            go.transform.SetParent(FxRoot, false);
            go.transform.position = center;
            GameObject disc = Part(go.transform, QuadXZ(1f), Transparent(new Color(color.r, color.g, color.b, 0.35f)),
                                   new Vector3(0f, 0.15f, 0f), Quaternion.identity, "Disc");
            disc.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            return go;
        }
    }
}
