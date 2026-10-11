using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Android / touch support on top of the legacy input system.
    //
    //    one finger tap      -> left click  (selection / smart command)
    //    one finger drag     -> camera pan
    //    two finger pinch    -> zoom
    //    two finger drag     -> camera pan
    //
    //  A tap that was really a pan or part of a pinch is swallowed so it never
    //  reaches SelectionManager as a click.
    // -----------------------------------------------------------------------
    public class TouchGestures : MonoBehaviour
    {
        static bool suppressClick;

        /// <summary>SelectionManager calls this once per mouse-up.</summary>
        public static bool ConsumeClickSuppression()
        {
            bool v = suppressClick;
            suppressClick = false;
            return v;
        }

        Vector2 prevSingle;
        float panDistance;
        bool singleActive;

        float prevPinchDist = -1f;
        Vector2 prevCentroid;
        bool pinchActive;

        void Update()
        {
            if (!Input.touchSupported) return;

            int n = Input.touchCount;

            // ---------- two fingers: pinch zoom + pan ----------
            if (n >= 2)
            {
                Touch a = Input.touches[0];
                Touch b = Input.touches[1];
                float dist = (a.position - b.position).magnitude;
                Vector2 centroid = (a.position + b.position) * 0.5f;

                if (!pinchActive)
                {
                    pinchActive = true;
                    prevPinchDist = dist;
                    prevCentroid = centroid;
                }
                else
                {
                    if (prevPinchDist > 1f)
                        GameManager.I.CameraRig.ZoomBy(prevPinchDist / Mathf.Max(1f, dist));
                    GameManager.I.CameraRig.PanScreenDelta(centroid - prevCentroid);
                    prevPinchDist = dist;
                    prevCentroid = centroid;
                }

                singleActive = false;
                panDistance = 999f;   // any release after a pinch is not a click
                return;
            }

            if (pinchActive)
            {
                pinchActive = false;
                prevPinchDist = -1f;
                suppressClick = true;
            }

            // ---------- one finger: pan or (on release) tap ----------
            if (n == 1)
            {
                Touch t = Input.touches[0];

                if (t.phase == TouchPhase.Began)
                {
                    singleActive = true;
                    prevSingle = t.position;
                    panDistance = 0f;
                }
                else if (t.phase == TouchPhase.Moved && singleActive)
                {
                    Vector2 delta = t.position - prevSingle;
                    prevSingle = t.position;
                    panDistance += delta.magnitude;

                    if (panDistance > 26f)
                        GameManager.I.CameraRig.PanScreenDelta(delta);
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    if (panDistance > 26f) suppressClick = true;
                    singleActive = false;
                }
            }
            else if (n == 0)
            {
                singleActive = false;
            }
        }
    }
}
