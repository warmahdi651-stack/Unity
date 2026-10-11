using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Mouse selection: click picking, drag box, control groups, hotkeys and
    //  right-click command dispatch.
    // -----------------------------------------------------------------------
    public class SelectionManager
    {
        public readonly List<UnitController> SelectedUnits = new List<UnitController>();
        public BuildingController SelectedBuilding;
        public ResourceNode SelectedNode;

        /// <summary>World point under the cursor on the last input frame.</summary>
        public static Vector3 LastGroundPoint { get; private set; }

        public bool IsDragging { get; private set; }
        public Vector2 DragStart { get; private set; }
        public Vector2 DragCurrent { get; private set; }

        bool draggingUnits = true;

        readonly List<UnitController>[] controlGroups = new List<UnitController>[10];
        readonly List<UnitController> scratch = new List<UnitController>();

        float mouseDownTime;
        float lastClickTime;
        UnitId lastClickType;

        Camera cam;

        public void Init()
        {
            cam = Camera.main;
            for (int i = 0; i < controlGroups.Length; i++) controlGroups[i] = new List<UnitController>();
            GameManager.I.Events.OnUnitDied += OnUnitDied;
            GameManager.I.Events.OnBuildingDestroyed += OnBuildingDestroyed;
        }

        void OnUnitDied(UnitController u) { SelectedUnits.Remove(u); FireChanged(); }
        void OnBuildingDestroyed(BuildingController b)
        {
            if (SelectedBuilding == b) { SelectedBuilding = null; FireChanged(); }
        }

        void FireChanged()
        {
            if (GameManager.I != null && GameManager.I.Events.OnSelectionChanged != null)
                GameManager.I.Events.OnSelectionChanged();
        }

        public bool HasSelection { get { return SelectedUnits.Count > 0 || SelectedBuilding != null; } }

        public bool SelectionIsOwn
        {
            get
            {
                if (SelectedBuilding != null) return SelectedBuilding.Owner == GameManager.I.Human;
                if (SelectedUnits.Count > 0) return SelectedUnits[0].Owner == GameManager.I.Human;
                return false;
            }
        }

        // ===================================================================
        //  Input
        // ===================================================================

        public void UpdateInput()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Vector3 mouse = Input.mousePosition;
            LastGroundPoint = GroundFromScreen(mouse);

            // HUD swallows clicks that land on the interface.
            bool overUI = HUDController.IsPointerOverUI(mouse);

            // ---------------- left button ----------------
            if (Input.GetMouseButtonDown(0) && !overUI)
            {
                mouseDownTime = Time.unscaledTime;
                DragStart = mouse;
                DragCurrent = mouse;
                IsDragging = false;
            }

            // Deliberately NOT gated on overUI: a drag that started on the world
            // must keep tracking even if the cursor sweeps over the interface.
            if (Input.GetMouseButton(0))
            {
                DragCurrent = mouse;
                if (!IsDragging && ((Vector2)mouse - DragStart).sqrMagnitude > 64f)
                {
                    IsDragging = true;
                    draggingUnits = !Input.GetKey(KeyCode.LeftAlt);
                }
            }

            if (Input.GetMouseButtonUp(0) && !overUI)
            {
                if (IsDragging) FinishDrag();
                else HandleClick(mouse);
                IsDragging = false;
            }

            // ---------------- right button ----------------
            if (Input.GetMouseButtonUp(1) && !overUI)
                HandleRightClick(mouse);

            // ---------------- middle button: rally ----------------
            if (Input.GetMouseButtonUp(2) && !overUI && SelectedBuilding != null && SelectionIsOwn)
                SelectedBuilding.SetRallyPoint(LastGroundPoint);

            HandleKeyboard();
        }

        // ===================================================================
        //  Picking
        // ===================================================================

        SelectableMarker Pick(Vector2 screen)
        {
            Ray ray = cam.ScreenPointToRay(screen);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 600f))
            {
                SelectableMarker m = hit.collider.GetComponentInParent<SelectableMarker>();
                if (m != null) return m;
            }
            return null;
        }

        /// <summary>Ray / terrain-plane intersection, refined once with the real
        ///  terrain height so orders land where the cursor is.</summary>
        public static Vector3 GroundFromScreen(Vector2 screen)
        {
            Camera c = Camera.main;
            if (c == null) return Vector3.zero;
            Ray ray = c.ScreenPointToRay(screen);

            Vector3 p;
            if (!PlaneIntersect(ray, 0f, out p)) return ray.origin + ray.direction * 40f;

            if (GameManager.I != null && GameManager.I.Map != null)
            {
                float h = GameManager.I.Map.HeightAtWorld(p);
                Vector3 refined;
                if (PlaneIntersect(ray, h, out refined)) p = refined;
            }
            return p;
        }

        static bool PlaneIntersect(Ray ray, float height, out Vector3 point)
        {
            point = Vector3.zero;
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return false;
            float t = (height - ray.origin.y) / ray.direction.y;
            if (t < 0f) return false;
            point = ray.origin + ray.direction * t;
            return true;
        }

        void HandleClick(Vector2 screen)
        {
            SelectableMarker hit = Pick(screen);
            bool additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (hit != null && hit.unit != null)
            {
                // Double click selects every unit of that type owned by the player.
                if (Time.unscaledTime - lastClickTime < 0.3f && lastClickType == hit.unit.defId && hit.unit.Owner == GameManager.I.Human)
                {
                    SelectAllOfType(hit.unit.defId);
                    lastClickTime = 0f;
                    return;
                }
                lastClickTime = Time.unscaledTime;
                lastClickType = hit.unit.defId;

                if (additive) ToggleUnit(hit.unit);
                else SelectUnit(hit.unit);
                return;
            }

            if (hit != null && hit.building != null)
            {
                if (additive) { /* buildings do not add to unit groups */ }
                SelectBuilding(hit.building);
                return;
            }

            if (hit != null && hit.node != null)
            {
                if (!additive) ClearSelection();
                SelectedNode = hit.node;
                FireChanged();
                return;
            }

            if (!additive) ClearSelection();
        }

        void FinishDrag()
        {
            Vector2 min = Vector2.Min(DragStart, DragCurrent);
            Vector2 max = Vector2.Max(DragStart, DragCurrent);
            if (max.x - min.x < 6f && max.y - min.y < 6f) return;

            bool additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!additive) ClearSelection();

            if (draggingUnits)
            {
                GameManager gm = GameManager.I;
                for (int i = 0; i < gm.AllUnits.Count; i++)
                {
                    UnitController u = gm.AllUnits[i];
                    if (u == null || !u.IsAlive) continue;
                    if (u.Owner != gm.Human) continue;
                    Vector3 sp = cam.WorldToScreenPoint(u.Position + Vector3.up * u.def.visualScale.y * 0.5f);
                    if (sp.z <= 0f) continue;
                    if (sp.x < min.x || sp.x > max.x || sp.y < min.y || sp.y > max.y) continue;
                    AddUnit(u);
                }
            }
            else
            {
                // Alt-drag: screen select buildings.
                GameManager gm = GameManager.I;
                for (int i = 0; i < gm.AllBuildings.Count; i++)
                {
                    BuildingController b = gm.AllBuildings[i];
                    if (b == null || !b.IsAlive || b.Owner != gm.Human) continue;
                    Vector3 sp = cam.WorldToScreenPoint(b.Position + Vector3.up * 2f);
                    if (sp.z <= 0f) continue;
                    if (sp.x < min.x || sp.x > max.x || sp.y < min.y || sp.y > max.y) continue;
                    SelectBuilding(b);
                    break;
                }
            }

            FireChanged();
        }

        void HandleRightClick(Vector2 screen)
        {
            if (SelectedUnits.Count == 0) return;

            SelectableMarker hit = Pick(screen);
            GameManager.I.Orders.SmartOrder(SelectedUnits, hit);
        }

        // ===================================================================
        //  Keyboard
        // ===================================================================

        void HandleKeyboard()
        {
            // Control groups -------------------------------------------------
            for (int i = 0; i < 10; i++)
            {
                KeyCode code = KeyCode.Alpha0 + i;
                if (!Input.GetKeyDown(code)) continue;

                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                if (ctrl)
                {
                    controlGroups[i].Clear();
                    controlGroups[i].AddRange(SelectedUnits);
                    GameManager.I.Say("Group " + i + " assigned (" + SelectedUnits.Count + " units).", Color.cyan);
                }
                else
                {
                    ClearSelection();
                    for (int j = 0; j < controlGroups[i].Count; j++)
                    {
                        UnitController u = controlGroups[i][j];
                        if (u != null && u.IsAlive) AddUnit(u);
                    }
                    FireChanged();

                    if (alt && SelectedUnits.Count > 0)
                        GameManager.I.CameraRig.FocusOn(SelectedUnits[0].Position);
                }
            }

            // Stop / hold ----------------------------------------------------
            if (Input.GetKeyDown(KeyCode.S) && SelectedUnits.Count > 0)
                GameManager.I.Orders.StopGroup(SelectedUnits);
            if (Input.GetKeyDown(KeyCode.H) && SelectedUnits.Count > 0)
                GameManager.I.Orders.HoldGroup(SelectedUnits);

            // Attack-move: A then left click ---------------------------------
            if (Input.GetKeyDown(KeyCode.A)) pendingAttackMove = true;

            // Hero spell hotkeys (Q / W) -------------------------------------
            if (SelectedUnits.Count > 0)
            {
                UnitController hero = SelectedUnits[0];
                if (hero.Owner == GameManager.I.Human && hero.def.isHero)
                {
                    AbilityId[] abs = GameData.AbilitiesOf(hero.defId);
                    if (Input.GetKeyDown(KeyCode.Q) && abs.Length > 0)
                        GameManager.I.Placement.BeginAbility(hero, abs[0]);
                    if (Input.GetKeyDown(KeyCode.W) && abs.Length > 1)
                        GameManager.I.Placement.BeginAbility(hero, abs[1]);
                }
            }

            // Select all military --------------------------------------------
            if (Input.GetKeyDown(KeyCode.F1)) SelectAllMilitary();
            if (Input.GetKeyDown(KeyCode.F2)) SelectAllWorkers();
            if (Input.GetKeyDown(KeyCode.F3)) SelectIdleWorkers();

            // Space centres on the selection ---------------------------------
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (SelectedUnits.Count > 0) GameManager.I.CameraRig.FocusOn(SelectedUnits[0].Position);
                else if (SelectedBuilding != null) GameManager.I.CameraRig.FocusOn(SelectedBuilding.Position);
            }

            // Game speed ------------------------------------------------------
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus)) SetSpeed(+1);
            if (Input.GetKeyDown(KeyCode.Minus)) SetSpeed(-1);
            if (Input.GetKeyDown(KeyCode.P)) GameManager.I.gameSpeed = GameManager.I.gameSpeed > 0.01f ? 0f : 1f;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (GameManager.I.Placement.IsActive) GameManager.I.Placement.Cancel();
                else ClearSelection();
            }
        }

        static readonly float[] SpeedSteps = { 0.5f, 1f, 2f, 4f };

        void SetSpeed(int dir)
        {
            float cur = GameManager.I.gameSpeed;
            int idx = 1;
            for (int i = 0; i < SpeedSteps.Length; i++)
                if (Mathf.Approximately(cur, SpeedSteps[i])) { idx = i; break; }
            idx = Mathf.Clamp(idx + dir, 0, SpeedSteps.Length - 1);
            GameManager.I.gameSpeed = SpeedSteps[idx];
            GameManager.I.Say("Game speed x" + SpeedSteps[idx].ToString("0.#"), Color.cyan);
        }

        bool pendingAttackMove;

        /// <summary>Called by the HUD each frame; returns true once when the
        ///  player presses A and then clicks the ground.</summary>
        public bool ConsumeAttackMoveRequest()
        {
            if (!pendingAttackMove) return false;
            pendingAttackMove = false;
            return true;
        }

        public bool AttackMoveArmed { get { return pendingAttackMove; } }

        // ===================================================================
        //  Selection helpers
        // ===================================================================

        public void ClearSelection()
        {
            for (int i = 0; i < SelectedUnits.Count; i++) SelectedUnits[i].SetSelected(false);
            SelectedUnits.Clear();
            if (SelectedBuilding != null) { SelectedBuilding.SetSelected(false); SelectedBuilding = null; }
            SelectedNode = null;
            FireChanged();
        }

        void AddUnit(UnitController u)
        {
            if (SelectedUnits.Contains(u)) return;
            SelectedUnits.Add(u);
            u.SetSelected(true);
        }

        void ToggleUnit(UnitController u)
        {
            if (SelectedUnits.Contains(u))
            {
                SelectedUnits.Remove(u);
                u.SetSelected(false);
            }
            else AddUnit(u);
            FireChanged();
        }

        public void SelectUnit(UnitController u)
        {
            ClearSelection();
            AddUnit(u);
            FireChanged();
        }

        public void SelectBuilding(BuildingController b)
        {
            ClearSelection();
            SelectedBuilding = b;
            b.SetSelected(true);
            FireChanged();
        }

        public void SelectAllOfType(UnitId id)
        {
            ClearSelection();
            GameManager gm = GameManager.I;
            for (int i = 0; i < gm.AllUnits.Count; i++)
            {
                UnitController u = gm.AllUnits[i];
                if (u == null || !u.IsAlive || u.defId != id || u.Owner != gm.Human) continue;
                AddUnit(u);
            }
            FireChanged();
        }

        public void SelectAllWorkers()
        {
            ClearSelection();
            GameManager gm = GameManager.I;
            UnitId worker = GameData.WorkerOf(gm.Human.faction);
            for (int i = 0; i < gm.AllUnits.Count; i++)
            {
                UnitController u = gm.AllUnits[i];
                if (u == null || !u.IsAlive || u.defId != worker || u.Owner != gm.Human) continue;
                AddUnit(u);
            }
            FireChanged();
        }

        public void SelectIdleWorkers()
        {
            ClearSelection();
            GameManager gm = GameManager.I;
            UnitId worker = GameData.WorkerOf(gm.Human.faction);
            for (int i = 0; i < gm.AllUnits.Count; i++)
            {
                UnitController u = gm.AllUnits[i];
                if (u == null || !u.IsAlive || u.defId != worker || u.Owner != gm.Human) continue;
                if (u.current.type != OrderType.Idle) continue;
                AddUnit(u);
            }
            FireChanged();
        }

        public void SelectAllMilitary()
        {
            ClearSelection();
            GameManager gm = GameManager.I;
            for (int i = 0; i < gm.AllUnits.Count; i++)
            {
                UnitController u = gm.AllUnits[i];
                if (u == null || !u.IsAlive || u.Owner != gm.Human) continue;
                if (u.def.unitClass == UnitClass.Worker) continue;
                AddUnit(u);
            }
            FireChanged();
        }

        // ===================================================================
        //  Per-frame visuals
        // ===================================================================

        public void LateTick(float dt)
        {
            // Prune dead units from the selection.
            for (int i = SelectedUnits.Count - 1; i >= 0; i--)
            {
                UnitController u = SelectedUnits[i];
                if (u == null || !u.IsAlive) { SelectedUnits.RemoveAt(i); FireChanged(); }
            }
        }
    }
}
