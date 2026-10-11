using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Small helpers shared across the project.
    // -----------------------------------------------------------------------
    public static class RTSGameUtility
    {
        /// <summary>AddComponent + immediate Init hook, keeping Bootstrap tidy.</summary>
        public static T Add<T>(GameObject go) where T : Component
        {
            return go.AddComponent<T>();
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return m.ToString("00") + ":" + s.ToString("00");
        }
    }
}
