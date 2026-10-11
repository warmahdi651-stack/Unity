using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Fog of war.  A low-resolution visibility grid is baked into a texture
    //  every few frames and drawn on a transparent plane above the terrain
    //  using the RtsWarcraft/FogOfWar shader.
    //
    //  0 = never seen (black), 1 = explored but dark, 2 = currently visible.
    // -----------------------------------------------------------------------
    public class FogOfWar
    {
        public const int Cells = 64;
        public const float CellSize = (GameMap.Size * GameData.TileSize) / Cells; // 2 world units

        byte[] visible;
        byte[] explored;
        Color32[] pixels;
        Texture2D tex;
        GameObject plane;
        float nextUpdate;

        static readonly Color32 Black = new Color32(2, 3, 8, 246);
        static readonly Color32 Dark = new Color32(12, 20, 34, 106);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public void Init()
        {
            visible = new byte[Cells * Cells];
            explored = new byte[Cells * Cells];
            pixels = new Color32[Cells * Cells];

            tex = new Texture2D(Cells, Cells, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Black;
            tex.SetPixels32(pixels);
            tex.Apply(false);

            plane = new GameObject("FogOfWar");
            plane.transform.SetParent(WorldFactory.FxRoot, false);
            plane.transform.position = new Vector3(0f, 7.2f, 0f);

            float world = GameMap.Size * GameData.TileSize;
            plane.transform.localScale = new Vector3(world, 1f, world);

            MeshFilter mf = plane.AddComponent<MeshFilter>();
            mf.sharedMesh = WorldFactory.QuadXZ(1f);
            MeshRenderer mr = plane.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            Shader sh = Shader.Find("RtsWarcraft/FogOfWar");
            Material mat;
            if (sh != null)
            {
                mat = new Material(sh);
            }
            else
            {
                // Shader did not compile (or was stripped): degrade gracefully.
                Shader alt = Shader.Find("Unlit/Transparent");
                mat = new Material(alt != null ? alt : Shader.Find("Standard"));
            }
            mat.mainTexture = tex;
            mr.sharedMaterial = mat;
        }

        // ===================================================================

        public static int CellOf(float worldCoord)
        {
            float half = (GameMap.Size * GameData.TileSize) * 0.5f;
            int c = Mathf.FloorToInt((worldCoord + half) / CellSize);
            return Mathf.Clamp(c, 0, Cells - 1);
        }

        public bool IsVisible(RTSPlayer viewer, Vector3 worldPos)
        {
            if (visible == null) return true;
            int cx = CellOf(worldPos.x);
            int cz = CellOf(worldPos.z);
            return visible[cz * Cells + cx] >= 2;
        }

        public bool IsExplored(Vector3 worldPos)
        {
            if (explored == null) return true;
            int cx = CellOf(worldPos.x);
            int cz = CellOf(worldPos.z);
            return explored[cz * Cells + cx] != 0;
        }

        // ===================================================================

        public void UpdateFog()
        {
            if (visible == null) return;
            if (Time.time < nextUpdate) return;
            nextUpdate = Time.time + 0.12f;

            for (int i = 0; i < visible.Length; i++) visible[i] = 0;

            GameManager gm = GameManager.I;
            if (gm == null) return;

            RevealFor(gm.Human);
            RevealFor(gm.Enemy);

            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = visible[i];
                if (v >= 2) { pixels[i] = Clear; explored[i] = 1; }
                else if (explored[i] != 0) pixels[i] = Dark;
                else pixels[i] = Black;
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);
        }

        void RevealFor(RTSPlayer p)
        {
            if (p == null) return;

            for (int i = 0; i < p.units.Count; i++)
            {
                UnitController u = p.units[i];
                if (u == null || !u.IsAlive) continue;
                // A stealthed unit still sees for its own player; invisibility
                // is handled by the targeting rules, not by the fog grid.
                Reveal(u.Position, u.def.sightRange, 1.0f);
            }

            for (int i = 0; i < p.buildings.Count; i++)
            {
                BuildingController b = p.buildings[i];
                if (b == null || !b.IsAlive || !b.IsComplete) continue;
                Reveal(b.Position, b.def.sightRange, 1.0f);
            }
        }

        void Reveal(Vector3 center, float radius, float strength)
        {
            float rCells = radius / CellSize;
            int cx = CellOf(center.x);
            int cz = CellOf(center.z);
            int r = Mathf.CeilToInt(rCells);

            int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(Cells - 1, cx + r);
            int z0 = Mathf.Max(0, cz - r), z1 = Mathf.Min(Cells - 1, cz + r);

            for (int z = z0; z <= z1; z++)
            {
                int dz = z - cz;
                for (int x = x0; x <= x1; x++)
                {
                    int dx = x - cx;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d > rCells) continue;

                    byte value = d <= rCells * 0.72f ? (byte)2 : (byte)1;
                    int idx = z * Cells + x;
                    if (value > visible[idx]) visible[idx] = value;
                    explored[idx] = 1;
                }
            }
        }

        /// <summary>Debug helper: reveal the whole map (cheat).</summary>
        public void RevealAll()
        {
            for (int i = 0; i < visible.Length; i++) { visible[i] = 2; explored[i] = 1; }
        }
    }
}
