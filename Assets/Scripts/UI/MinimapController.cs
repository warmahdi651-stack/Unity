using UnityEngine;
using UnityEngine.UI;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Minimap: a procedurally coloured terrain texture with live unit and
    //  building blips.  Clicking or dragging moves the camera.
    // -----------------------------------------------------------------------
    public class MinimapController : MonoBehaviour
    {
        public const int Pixels = 200;

        RectTransform panel;
        RawImage image;
        Texture2D tex;
        Color32[] pixels;
        float nextRedraw;

        Color32 fogColor = new Color32(10, 14, 24, 255);
        Color32 exploredColor = new Color32(40, 54, 42, 255);

        public void Init()
        {
            RectTransform root = GameManager.I.HUD != null ? GameManager.I.HUD.UiRoot : null;
            if (root == null) return;

            GameObject go = new GameObject("Minimap", typeof(RectTransform), typeof(RawImage));
            panel = go.GetComponent<RectTransform>();
            panel.SetParent(root, false);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.sizeDelta = new Vector2(Pixels + 8f, Pixels + 8f);
            panel.anchoredPosition = new Vector2(10f, 10f);

            image = go.GetComponent<RawImage>();
            image.raycastTarget = true;

            tex = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            pixels = new Color32[Pixels * Pixels];
            image.texture = tex;

            DrawTerrain();
        }

        // ===================================================================

        void DrawTerrain()
        {
            GameManager gm = GameManager.I;
            GameMap map = gm.Map;

            for (int py = 0; py < Pixels; py++)
            {
                for (int px = 0; px < Pixels; px++)
                {
                    int tx = Mathf.Clamp(Mathf.RoundToInt(px / (float)Pixels * GameMap.Size), 0, GameMap.Size - 1);
                    int tz = Mathf.Clamp(Mathf.RoundToInt(py / (float)Pixels * GameMap.Size), 0, GameMap.Size - 1);

                    float h = map.HeightAt(tx, tz);
                    bool blocked = !map.IsWalkableTile(tx, tz);

                    Color32 c;
                    if (h > 3.35f) c = new Color32(96, 92, 88, 255);
                    else if (h < 0.2f) c = new Color32(26, 86, 116, 255);
                    else if (blocked) c = new Color32(24, 58, 28, 255);
                    else
                    {
                        byte g = (byte)Mathf.Clamp(70 + h * 22f, 0f, 190f);
                        c = new Color32((byte)(g * 0.42f), g, (byte)(g * 0.36f), 255);
                    }
                    pixels[py * Pixels + px] = c;
                }
            }

            // Resource nodes.
            for (int i = 0; i < map.Nodes.Count; i++)
            {
                ResourceNode n = map.Nodes[i];
                int px = Mathf.Clamp(Mathf.RoundToInt(n.tile.x / (float)GameMap.Size * Pixels), 0, Pixels - 1);
                int py = Mathf.Clamp(Mathf.RoundToInt(n.tile.y / (float)GameMap.Size * Pixels), 0, Pixels - 1);
                Color32 c = n.kind == ResourceKind.Gold
                    ? new Color32(255, 208, 40, 255)
                    : new Color32(24, 120, 40, 255);
                int r = n.kind == ResourceKind.Gold ? 2 : 1;
                Blot(px, py, r, c);
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);
        }

        void Blot(int px, int py, int radius, Color32 c)
        {
            for (int y = py - radius; y <= py + radius; y++)
            {
                if (y < 0 || y >= Pixels) continue;
                for (int x = px - radius; x <= px + radius; x++)
                {
                    if (x < 0 || x >= Pixels) continue;
                    pixels[y * Pixels + x] = c;
                }
            }
        }

        // ===================================================================

        public void LateTick()
        {
            if (tex == null) return;
            HandleInput();

            if (Time.time < nextRedraw) return;
            nextRedraw = Time.time + 0.1f;

            DrawTerrain();

            GameManager gm = GameManager.I;
            FogOfWar fog = gm.Fog;

            // Dim everything the player has not explored.
            if (fog != null)
            {
                for (int py = 0; py < Pixels; py += 1)
                {
                    for (int px = 0; px < Pixels; px += 1)
                    {
                        int tx = Mathf.Clamp(Mathf.RoundToInt(px / (float)Pixels * GameMap.Size), 0, GameMap.Size - 1);
                        int tz = Mathf.Clamp(Mathf.RoundToInt(py / (float)Pixels * GameMap.Size), 0, GameMap.Size - 1);
                        Vector3 world = GameMap.TileToWorld(tx, tz);
                        if (!fog.IsExplored(world)) pixels[py * Pixels + px] = fogColor;
                    }
                }
            }

            // Buildings
            for (int i = 0; i < gm.AllBuildings.Count; i++)
            {
                BuildingController b = gm.AllBuildings[i];
                if (b == null || !b.IsAlive) continue;
                if (b.Owner != gm.Human && (fog == null || !fog.IsVisible(gm.Human, b.Position))) continue;
                int bpx, bpy;
                MapPoint(b.Position, out bpx, out bpy);
                Color32 bc = To32(b.Owner.color);
                Blot(bpx, bpy, 2, bc);
            }

            // Units
            for (int i = 0; i < gm.AllUnits.Count; i++)
            {
                UnitController u = gm.AllUnits[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Owner != gm.Human && (fog == null || !fog.IsVisible(gm.Human, u.Position))) continue;
                int upx, upy;
                MapPoint(u.Position, out upx, out upy);
                Color32 uc = To32(u.Owner.color);
                if (u.def.isHero) uc = new Color32(255, 235, 120, 255);
                Blot(upx, upy, u.def.isHero ? 2 : 1, uc);
            }

            DrawViewRect();

            tex.SetPixels32(pixels);
            tex.Apply(false);
        }

        /// <summary>White rectangle showing the camera's current view.</summary>
        void DrawViewRect()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            float minX = 1e5f, minY = 1e5f, maxX = -1e5f, maxY = -1e5f;
            bool any = false;
            for (int i = 0; i < 4; i++)
            {
                Ray r = cam.ViewportPointToRay(new Vector3(i % 2, i / 2, 0f));
                if (Mathf.Abs(r.direction.y) < 1e-4f) continue;
                float t = -r.origin.y / r.direction.y;
                if (t < 0f) continue;
                Vector3 p = r.origin + r.direction * t;
                int px, py;
                MapPoint(p, out px, out py);
                minX = Mathf.Min(minX, px); maxX = Mathf.Max(maxX, px);
                minY = Mathf.Min(minY, py); maxY = Mathf.Max(maxY, py);
                any = true;
            }
            if (!any) return;
            int x0 = Mathf.Clamp((int)minX, 0, Pixels - 1), x1 = Mathf.Clamp((int)maxX, 0, Pixels - 1);
            int y0 = Mathf.Clamp((int)minY, 0, Pixels - 1), y1 = Mathf.Clamp((int)maxY, 0, Pixels - 1);
            Color32 c = new Color32(230, 230, 230, 255);
            for (int x = x0; x <= x1; x++)
            {
                pixels[y0 * Pixels + x] = c;
                pixels[y1 * Pixels + x] = c;
            }
            for (int y = y0; y <= y1; y++)
            {
                pixels[y * Pixels + x0] = c;
                pixels[y * Pixels + x1] = c;
            }
        }

        static Color32 To32(Color c)
        {
            return new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 255);
        }

        void MapPoint(Vector3 world, out int px, out int py)
        {
            float half = GameMap.Size * GameData.TileSize * 0.5f;
            px = Mathf.Clamp(Mathf.RoundToInt((world.x + half) / (half * 2f) * Pixels), 0, Pixels - 1);
            py = Mathf.Clamp(Mathf.RoundToInt((world.z + half) / (half * 2f) * Pixels), 0, Pixels - 1);
        }

        void HandleInput()
        {
            if (panel == null) return;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, Input.mousePosition, null, out local);
            Rect r = panel.rect;
            bool inside = r.Contains(local);

            if (inside && (Input.GetMouseButtonDown(0) || (Input.GetMouseButton(0) && WasInside)))
            {
                float nx = Mathf.Clamp01(local.x / r.width);
                float ny = Mathf.Clamp01(local.y / r.height);

                float half = GameMap.Size * GameData.TileSize * 0.5f;
                Vector3 world = new Vector3(nx * half * 2f - half, 0f, ny * half * 2f - half);
                world.y = GameManager.I.Map.HeightAtWorld(world);
                GameManager.I.CameraRig.FocusOn(world);
            }

            WasInside = inside;
        }

        bool WasInside;
    }
}
