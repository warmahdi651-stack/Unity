using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Damage numbers and pick-ups.  The HUD owns the actual uGUI objects;
    //  this static entry point keeps gameplay code free of UI references.
    // -----------------------------------------------------------------------
    public static class FloatingText
    {
        public static void Spawn(Vector3 worldPosition, string text, Color color)
        {
            if (GameManager.I == null || GameManager.I.HUD == null) return;
            if (GameManager.I.HUD.FloatingTextEnabled == false) return;
            GameManager.I.HUD.SpawnFloatingText(worldPosition, text, color);
        }
    }

    // -----------------------------------------------------------------------
    //  One screen-space health bar owned by the HUD bar pool.
    // -----------------------------------------------------------------------
    public class HealthBarElement
    {
        public GameObject root;
        public RectTransform rect;
        public RectTransform hpFill;
        public RectTransform manaFill;
        public RectTransform background;
        public float hpFullWidth;
        public float manaFullWidth;
        public IDamageable target;
        public bool inUse;
        public float forceVisibleUntil;
    }
}
