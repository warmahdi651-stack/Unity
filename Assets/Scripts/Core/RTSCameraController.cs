using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Classic RTS camera: fixed pitch, WASD / arrow / edge panning, wheel
    //  zoom, Q/E yaw and click-to-focus.
    // -----------------------------------------------------------------------
    public class RTSCameraController : MonoBehaviour
    {
        [Header("Zoom")]
        public float minZoom = 10f;
        public float maxZoom = 52f;
        public float zoomSpeed = 12f;

        [Header("Pan")]
        public float panSpeed = 26f;
        public float edgeScrollPixels = 14f;
        public bool edgeScrollEnabled = true;

        [Header("Pitch / yaw")]
        public float pitch = 52f;
        public float yaw;

        public Vector3 focus;

        Camera cam;
        float currentZoom = 26f;
        float targetZoom = 26f;
        Vector3 dragOrigin;
        bool dragging;

        public Camera Cam { get { return cam; } }

        void Awake()
        {
            cam = Camera.main;
            if (cam == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.30f, 0.42f, 0.55f);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            cam.allowHDR = false;
            cam.allowMSAA = true;

            targetZoom = currentZoom = 26f;
            focus = Vector3.zero;
            Apply();
        }

        public void SetFocus(Vector3 p)
        {
            focus = ClampToMap(p);
            Apply();
        }

        public void FocusOn(Vector3 p)
        {
            focus = ClampToMap(p);
            Apply();
        }

        void Update()
        {
            if (cam == null) return;

            // ---- zoom ----
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.0001f)
            {
                targetZoom = Mathf.Clamp(targetZoom - wheel * zoomSpeed, minZoom, maxZoom);
            }
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * 9f);

            // ---- pan ----
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.z -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
            // 'A' is the attack-move hotkey, so only the arrow key pans left.
            if (Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;

            if (move.sqrMagnitude > 0.001f)
            {
                Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
                focus += yawRot * (move.normalized * panSpeed * (currentZoom / 26f) * Time.deltaTime);
            }

            // ---- middle mouse drag ----
            if (Input.GetMouseButtonDown(2))
            {
                dragging = true;
                dragOrigin = Input.mousePosition;
            }
            if (dragging && Input.GetMouseButton(2))
            {
                Vector3 delta = Input.mousePosition - dragOrigin;
                dragOrigin = Input.mousePosition;
                Quaternion yawRot = Quaternion.Euler(0f, yaw + 180f, 0f);
                float scale = currentZoom / Screen.height * 2.2f;
                focus += yawRot * new Vector3(delta.x * scale, 0f, delta.y * scale);
            }
            if (Input.GetMouseButtonUp(2)) dragging = false;

            // ---- edge scroll ----
            if (edgeScrollEnabled && !HUDController.IsPointerOverUI(Input.mousePosition))
            {
                Vector3 m = Input.mousePosition;
                Vector3 edge = Vector3.zero;
                if (m.x < edgeScrollPixels) edge.x -= 1f;
                if (m.x > Screen.width - edgeScrollPixels) edge.x += 1f;
                if (m.y < edgeScrollPixels) edge.y -= 1f;
                if (m.y > Screen.height - edgeScrollPixels) edge.y += 1f;

                if (edge.sqrMagnitude > 0.001f)
                {
                    Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
                    focus += yawRot * (edge.normalized * panSpeed * 0.9f * (currentZoom / 26f) * Time.deltaTime);
                }
            }

            // ---- rotate ----
            if (Input.GetKey(KeyCode.Q)) yaw -= 90f * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) yaw += 90f * Time.deltaTime;
            if (Input.GetKeyDown(KeyCode.R)) yaw = 0f;

            focus = ClampToMap(focus);
            Apply();
        }

        void Apply()
        {
            if (cam == null) return;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 offset = rot * new Vector3(0f, 0f, -currentZoom);
            cam.transform.position = focus + Vector3.up * (currentZoom * 0.25f) + offset;
            cam.transform.rotation = rot;
        }

        Vector3 ClampToMap(Vector3 p)
        {
            float half = GameMap.Size * GameData.TileSize * 0.5f;
            p.x = Mathf.Clamp(p.x, -half + 4f, half - 4f);
            p.z = Mathf.Clamp(p.z, -half + 4f, half - 4f);
            if (GameManager.I != null && GameManager.I.Map != null) p.y = GameManager.I.Map.HeightAtWorld(p);
            return p;
        }

        /// <summary>Keeps the focus on a moving unit (used by "space").</summary>
        public void Follow(Vector3 p) { focus = ClampToMap(p); }
    }
}
