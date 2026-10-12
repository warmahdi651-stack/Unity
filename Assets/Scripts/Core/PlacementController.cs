using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Building placement (ghost preview, validity, rotation) and spell
    //  targeting for hero abilities.
    // -----------------------------------------------------------------------
    public class PlacementController : MonoBehaviour
    {
        public BuildingId ActiveBuilding { get; private set; }
        public AbilityId ActiveAbility { get; private set; }
        public bool CanPlaceHere { get; private set; }

        public bool IsActive { get { return ActiveBuilding != BuildingId.None || ActiveAbility != AbilityId.None; } }

        GameObject ghost;
        Vector2Int hoverOrigin;
        Vector3 abilityPoint;

        UnitController caster;

        public void Init()
        {
            ActiveBuilding = BuildingId.None;
            ActiveAbility = AbilityId.None;
        }

        // ===================================================================
        //  Entering a mode
        // ===================================================================

        public void Begin(BuildingId id)
        {
            SelectionManager sel = GameManager.I.Selection;
            if (sel.SelectedUnits.Count == 0)
            {
                GameManager.I.Say("Select a worker first.", Color.yellow);
                return;
            }

            bool hasWorker = false;
            for (int i = 0; i < sel.SelectedUnits.Count; i++)
                if (sel.SelectedUnits[i].def.unitClass == UnitClass.Worker) hasWorker = true;

            if (!hasWorker)
            {
                GameManager.I.Say("Only workers can build.", Color.yellow);
                return;
            }

            BuildingDef def = GameData.GetBuilding(id);
            if (!GameManager.I.Human.CanAfford(def.goldCost, def.lumberCost))
            {
                GameManager.I.Say("Not enough resources for " + def.displayName + ".", Color.red);
                return;
            }

            Cancel();
            ActiveBuilding = id;
            ghost = WorldFactory.CreateSelectionGhost(def, true);
        }

        public void BeginAbility(UnitController u, AbilityId id)
        {
            AbilityDef a = GameData.GetAbility(id);
            if (id == AbilityId.None || u == null) return;

            if (!GameManager.I.Abilities.CanCast(u, id))
            {
                if (u.mana < a.manaCost) GameManager.I.Say("Not enough mana.", new Color(0.5f, 0.7f, 1f));
                else GameManager.I.Say(a.displayName + " is not ready yet.", Color.yellow);
                return;
            }

            Cancel();

            // Self-cast spells resolve instantly, no targeting click needed.
            if (a.range <= 0f && a.radius <= 0f)
            {
                Order o = new Order();
                o.type = OrderType.CastAbility;
                o.ability = id;
                o.position = u.Position;
                o.targetUnit = u;
                u.IssueOrder(o);
                return;
            }

            ActiveAbility = id;
            caster = u;
        }

        public void Cancel()
        {
            ActiveBuilding = BuildingId.None;
            ActiveAbility = AbilityId.None;
            caster = null;
            if (ghost != null) { Destroy(ghost); ghost = null; }
        }

        // ===================================================================
        //  Per-frame
        // ===================================================================

        public void UpdateInput()
        {
            if (!IsActive) return;

            Vector3 ground = SelectionManager.LastGroundPoint;
            bool overUI = HUDController.IsPointerOverUI(Input.mousePosition);

            if (Input.GetMouseButtonUp(1))
            {
                Cancel();
                return;
            }

            if (ActiveAbility != AbilityId.None)
            {
                abilityPoint = ground;
                if (ghost != null) { Destroy(ghost); ghost = null; }

                AbilityDef a = GameData.GetAbility(ActiveAbility);
                ghost = WorldFactory.CreateAoEMarker(ground, Mathf.Max(1f, a.radius > 0f ? a.radius : 1.2f), a.iconColor);

                if (Input.GetMouseButtonUp(0) && !overUI) CastAbility(ground);
                return;
            }

            // ---- building placement ----
            BuildingDef def = GameData.GetBuilding(ActiveBuilding);
            Vector2Int tile = GameMap.WorldToTile(ground);
            hoverOrigin = new Vector2Int(tile.x - def.sizeX / 2, tile.y - def.sizeZ / 2);

            GameMap map = GameManager.I.Map;
            bool valid = map.CanPlaceBuilding(hoverOrigin, def.sizeX, def.sizeZ);
            CanPlaceHere = valid;

            if (ghost == null) ghost = WorldFactory.CreateSelectionGhost(def, valid);
            RefreshGhost(def, valid);

            if (Input.GetMouseButtonUp(0) && !overUI)
            {
                if (!valid)
                {
                    GameManager.I.Say("Cannot build there.", Color.red);
                    return;
                }
                Commit();
            }
        }

        void RefreshGhost(BuildingDef def, bool valid)
        {
            Vector3 center = WorldFactory.BuildingCenter(hoverOrigin, def);
            center.y = GameManager.I.Map.HeightAtWorld(center);
            ghost.transform.position = center;

            Color c = valid ? new Color(0.2f, 1f, 0.2f, 0.4f) : new Color(1f, 0.2f, 0.2f, 0.4f);
            for (int i = 0; i < ghost.transform.childCount; i++)
            {
                MeshRenderer mr = ghost.transform.GetChild(i).GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null) continue;
                if (i == 0) mr.sharedMaterial = WorldFactory.Transparent(c);
            }
        }

        void Commit()
        {
            BuildingDef def = GameData.GetBuilding(ActiveBuilding);
            RTSPlayer me = GameManager.I.Human;

            if (!me.Pay(def.goldCost, def.lumberCost))
            {
                GameManager.I.Say("Not enough resources.", Color.red);
                Cancel();
                return;
            }
            GameManager.I.Events.OnResourcesChanged();

            GameManager.I.Orders.BuildOrder(GameManager.I.Selection.SelectedUnits, ActiveBuilding, hoverOrigin);
            GameManager.I.Say("Building " + def.displayName + "...", Color.cyan);
            Cancel();
        }

        void CastAbility(Vector3 point)
        {
            if (caster == null || !caster.IsAlive) { Cancel(); return; }

            AbilityDef a = GameData.GetAbility(ActiveAbility);
            UnitController targetUnit = null;

            // Prefer an explicit unit target under the cursor for single-target spells.
            if (a.radius <= 0f)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, 600f))
                    {
                        SelectableMarker m = hit.collider.GetComponentInParent<SelectableMarker>();
                        if (m != null) targetUnit = m.unit;
                    }
                }
            }

            Order o = new Order();
            o.type = OrderType.CastAbility;
            o.ability = ActiveAbility;
            o.position = point;
            o.targetUnit = targetUnit;
            caster.IssueOrder(o);

            Cancel();
        }
    }
}
