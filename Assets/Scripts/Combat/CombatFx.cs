using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Short lived visual feedback: impacts, deaths, spell areas, build
    //  completion.  All pooled / auto-destroying.
    // -----------------------------------------------------------------------
    public static class CombatFx
    {
        static readonly List<GameObject> pool = new List<GameObject>();
        static readonly List<float> expiry = new List<float>();

        public static void MeleeImpact(Vector3 p)
        {
            Puff(p + Vector3.up * 1f, new Color(1f, 0.95f, 0.8f), 0.35f, 0.16f);
        }

        public static void Impact(Vector3 p, Color color)
        {
            Puff(p, color, 0.4f, 0.2f);
        }

        public static void DeathPuff(Vector3 p, Color color)
        {
            Puff(p + Vector3.up * 0.8f, Color.Lerp(color, Color.black, 0.4f), 0.7f, 1.1f);
        }

        public static void SpawnPuff(Vector3 p)
        {
            Puff(p + Vector3.up * 0.6f, new Color(0.9f, 0.95f, 1f), 0.5f, 0.8f);
        }

        public static void BuildComplete(Vector3 p)
        {
            Puff(p + Vector3.up * 1.5f, new Color(1f, 0.9f, 0.5f), 0.9f, 1.4f);
        }

        public static void BuildingCollapse(Vector3 p, Color color)
        {
            Puff(p + Vector3.up * 1.2f, Color.Lerp(color, Color.black, 0.55f), 1.2f, 2.2f);
        }

        public static void CreateBeamEffect(Vector3 from, Vector3 to, Color color, float width)
        {
            GameObject go = WorldFactory.CreateBeam(from, to, color, width);
            Register(go, 0.25f);
        }

        public static void CreateAoE(Vector3 center, float radius, Color color, float life)
        {
            GameObject go = WorldFactory.CreateAoEMarker(center, radius, color);
            Register(go, life);
        }

        static void Puff(Vector3 p, Color color, float life, float size)
        {
            GameObject go = new GameObject("Puff");
            go.transform.SetParent(WorldFactory.FxRoot, false);
            go.transform.position = p;
            WorldFactory.Part(go.transform, WorldFactory.SphereMesh(0.5f, 4),
                              WorldFactory.Transparent(new Color(color.r, color.g, color.b, 0.6f)),
                              Vector3.zero, Quaternion.identity, "Blob")
                      .transform.localScale = Vector3.one * size;
            Register(go, life);
        }

        static void Register(GameObject go, float life)
        {
            pool.Add(go);
            expiry.Add(Time.time + life);
        }

        /// <summary>Called every frame from the HUD tick.</summary>
        public static void Tick()
        {
            for (int i = pool.Count - 1; i >= 0; i--)
            {
                GameObject go = pool[i];
                if (go == null)
                {
                    pool.RemoveAt(i); expiry.RemoveAt(i);
                    continue;
                }
                float remaining = expiry[i] - Time.time;
                if (remaining <= 0f)
                {
                    Object.Destroy(go);
                    pool.RemoveAt(i); expiry.RemoveAt(i);
                    continue;
                }
                // Fade + expand the first child so effects read as bursts.
                if (go.transform.childCount > 0)
                {
                    Transform c = go.transform.GetChild(0);
                    c.localScale = c.localScale * (1f + Time.deltaTime * 1.6f);
                }
            }
        }
    }
}
