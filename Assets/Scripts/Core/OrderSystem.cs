using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  A single command stored on a unit.
    // -----------------------------------------------------------------------
    public struct Order
    {
        public OrderType type;
        public Vector3 position;
        public UnitController targetUnit;
        public BuildingController targetBuilding;
        public ResourceNode node;
        public BuildingId buildingId;
        public Vector2Int buildOrigin;
        public AbilityId ability;

        public IDamageable Target
        {
            get
            {
                if (targetUnit != null && targetUnit.IsAlive) return targetUnit;
                if (targetBuilding != null && targetBuilding.IsAlive) return targetBuilding;
                return null;
            }
        }

        public static Order Move(Vector3 p)
        {
            Order o = new Order(); o.type = OrderType.Move; o.position = p; return o;
        }

        public static Order AttackMove(Vector3 p)
        {
            Order o = new Order(); o.type = OrderType.AttackMove; o.position = p; return o;
        }

        public static Order AttackTarget(UnitController u)
        {
            Order o = new Order(); o.type = OrderType.AttackTarget; o.targetUnit = u; o.position = u.Position; return o;
        }

        public static Order AttackTarget(BuildingController b)
        {
            Order o = new Order(); o.type = OrderType.AttackTarget; o.targetBuilding = b; o.position = b.Position; return o;
        }

        public static Order Harvest(ResourceNode n)
        {
            Order o = new Order(); o.type = OrderType.Harvest; o.node = n; o.position = n.position; return o;
        }

        public static Order ReturnResource()
        {
            Order o = new Order(); o.type = OrderType.ReturnResource; return o;
        }

        public static Order Repair(BuildingController b)
        {
            Order o = new Order(); o.type = OrderType.Repair; o.targetBuilding = b; o.position = b.Position; return o;
        }

        public static Order Build(BuildingId id, Vector2Int origin)
        {
            Order o = new Order();
            o.type = OrderType.Build; o.buildingId = id; o.buildOrigin = origin;
            o.position = WorldFactory.BuildingCenter(origin, GameData.GetBuilding(id));
            return o;
        }
    }

    // -----------------------------------------------------------------------
    //  Translates player intent into per-unit orders: formations, harvest
    //  assignment, build delegation and so on.
    // -----------------------------------------------------------------------
    public class OrderSystem
    {
        readonly List<UnitController> sorted = new List<UnitController>();

        public void Update(float dt)
        {
            // Kept as an explicit hook: order timeouts, formation refreshing etc.
        }

        // ===================================================================
        //  Formations
        // ===================================================================

        /// <summary>Spots around <paramref name="center"/> for a group of units,
        ///  assigned so that units do not cross the whole map to swap places.</summary>
        public void FormationPositions(List<UnitController> units, Vector3 center, List<Vector3> result)
        {
            result.Clear();
            int n = units.Count;
            if (n == 0) return;

            int cols = Mathf.CeilToInt(Mathf.Sqrt(n));
            int rows = Mathf.CeilToInt(n / (float)cols);
            float spacing = 1.9f;

            sorted.Clear();
            sorted.AddRange(units);
            sorted.Sort(delegate (UnitController a, UnitController b)
            {
                float da = (a.Position - center).sqrMagnitude;
                float db = (b.Position - center).sqrMagnitude;
                return da.CompareTo(db);
            });

            GameManager gm = GameManager.I;
            int index = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (index >= n) break;
                    float ox = (c - (cols - 1) * 0.5f) * spacing;
                    float oz = (r - (rows - 1) * 0.5f) * spacing;
                    Vector3 p = center + new Vector3(ox, 0f, oz);

                    Vector2Int t = GameMap.WorldToTile(p);
                    t = gm.Map.NearestWalkable(t.x, t.y, 6);
                    Vector3 w = GameMap.TileToWorld(t.x, t.y);
                    w.y = gm.Map.HeightAtWorld(w);
                    result.Add(w);
                    index++;
                }
            }
        }

        // ===================================================================
        //  Group commands
        // ===================================================================

        public void MoveGroup(List<UnitController> units, Vector3 target)
        {
            if (units.Count == 0) return;
            GroundEffect.BeginOrderBatch();
            List<Vector3> spots = new List<Vector3>();
            FormationPositions(units, target, spots);
            for (int i = 0; i < units.Count && i < spots.Count; i++)
            {
                units[i].IssueOrder(Order.Move(spots[i]));
                GroundEffect.SpawnMoveMarker(spots[i], units[i].Owner.color);
            }
        }

        public void AttackMoveGroup(List<UnitController> units, Vector3 target)
        {
            if (units.Count == 0) return;
            List<Vector3> spots = new List<Vector3>();
            FormationPositions(units, target, spots);
            for (int i = 0; i < units.Count && i < spots.Count; i++)
                units[i].IssueOrder(Order.AttackMove(spots[i]));
            GroundEffect.SpawnAttackMoveMarker(target);
        }

        public void AttackTargetGroup(List<UnitController> units, IDamageable target)
        {
            if (target == null) return;
            GroundEffect.BeginOrderBatch();
            GroundEffect.SpawnAttackMarker(target.Position);
            UnitController tu = target as UnitController;
            BuildingController tb = target as BuildingController;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController u = units[i];
                u.IssueOrder(tu != null ? Order.AttackTarget(tu) : Order.AttackTarget(tb));
            }
        }

        /// <summary>Right-click behaviour, mirroring Warcraft III.</summary>
        public void SmartOrder(List<UnitController> units, SelectableMarker hit)
        {
            GameManager gm = GameManager.I;
            RTSPlayer me = gm.Human;

            if (hit != null && hit.unit != null)
            {
                if (hit.unit.Owner != me) AttackTargetGroup(units, hit.unit);
                else MoveGroup(units, hit.unit.Position);
                return;
            }

            if (hit != null && hit.building != null)
            {
                BuildingController b = hit.building;
                if (b.Owner != me) AttackTargetGroup(units, b);
                else
                {
                    bool repairable = !b.IsComplete || b.CurrentHp < b.MaxHpValue - 1f;
                    bool anyWorker = false;
                    for (int i = 0; i < units.Count; i++)
                        if (units[i].def.unitClass == UnitClass.Worker) anyWorker = true;

                    if (anyWorker && repairable) RepairGroup(units, b);
                    else MoveGroup(units, b.Position + new Vector3(0f, 0f, b.def.sizeZ * 0.6f + 1f));
                }
                return;
            }

            if (hit != null && hit.node != null)
            {
                HarvestGroup(units, hit.node);
                return;
            }

            // Empty ground: move, or attack-move for units that already fight.
            Vector3 p = GroundPoint(units, hit);
            MoveGroup(units, p);
        }

        Vector3 GroundPoint(List<UnitController> units, SelectableMarker hit)
        {
            return SelectionManager.LastGroundPoint;
        }

        public void HarvestGroup(List<UnitController> units, ResourceNode node)
        {
            if (node == null) return;
            GroundEffect.BeginOrderBatch();
            GameManager gm = GameManager.I;
            List<UnitController> workers = new List<UnitController>();
            for (int i = 0; i < units.Count; i++)
                if (units[i].def.unitClass == UnitClass.Worker) workers.Add(units[i]);

            if (workers.Count == 0)
            {
                // Non-workers simply walk over there (and will attack it if hostile logic applies).
                MoveGroup(units, node.position);
                return;
            }

            for (int i = 0; i < workers.Count; i++)
            {
                ResourceNode target = node;
                if (target.IsFull || target.Depleted)
                    target = gm.Map.Nodes.Count > 0 ? FindNearestNode(workers[i].Position, target.kind, target) : null;
                if (target == null) continue;
                workers[i].IssueOrder(Order.Harvest(target));
            }

            // Everyone else just moves along.
            for (int i = 0; i < units.Count; i++)
                if (units[i].def.unitClass != UnitClass.Worker) units[i].IssueOrder(Order.Move(node.position));

            GroundEffect.SpawnHarvestMarker(node.position, node.kind);
        }

        ResourceNode FindNearestNode(Vector3 from, ResourceKind kind, ResourceNode avoid)
        {
            GameManager gm = GameManager.I;
            ResourceNode best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < gm.Map.Nodes.Count; i++)
            {
                ResourceNode n = gm.Map.Nodes[i];
                if (n.Depleted || n.kind != kind || n == avoid) continue;
                float d = (n.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = n; }
            }
            return best;
        }

        public void RepairGroup(List<UnitController> units, BuildingController b)
        {
            for (int i = 0; i < units.Count; i++)
                if (units[i].def.unitClass == UnitClass.Worker)
                    units[i].IssueOrder(Order.Repair(b));
        }

        public void BuildOrder(List<UnitController> workers, BuildingId id, Vector2Int origin)
        {
            BuildingDef def = GameData.GetBuilding(id);
            Vector3 center = WorldFactory.BuildingCenter(origin, def);
            Order build = Order.Build(id, origin);

            int assigned = 0;
            for (int i = 0; i < workers.Count && assigned < 4; i++)
            {
                if (workers[i].def.unitClass != UnitClass.Worker) continue;
                workers[i].IssueOrder(build);
                assigned++;
            }
            if (assigned == 0 && workers.Count > 0) workers[0].IssueOrder(build);
        }

        public void StopGroup(List<UnitController> units)
        {
            for (int i = 0; i < units.Count; i++) units[i].IssueOrder(new Order());
        }

        /// <summary>Hold position: keep the current spot but stop the queue.</summary>
        public void HoldGroup(List<UnitController> units)
        {
            for (int i = 0; i < units.Count; i++)
            {
                UnitController u = units[i];
                u.IssueOrder(Order.Move(u.Position));
            }
        }

        public void PatrolToggle(List<UnitController> units)
        {
            // Simplified: patrol == attack-move back and forth is not implemented,
            // so this falls back to attack-move which keeps units aggressive.
            for (int i = 0; i < units.Count; i++)
                units[i].IssueOrder(Order.AttackMove(units[i].Position));
        }
    }

    // -----------------------------------------------------------------------
    //  Short lived decals that confirm a command was received.
    // -----------------------------------------------------------------------
    public static class GroundEffect
    {
        static readonly List<GameObject> pool = new List<GameObject>();
        static readonly List<float> expiry = new List<float>();
        static readonly List<float> life = new List<float>();
        static readonly List<Vector3> baseScale = new List<Vector3>();

        const int MaxMarkers = 14;
        static int spawnedThisOrder;

        /// <summary>Called before a group order so one command cannot flood the
        ///  screen (and the pool) with 100 markers.</summary>
        public static void BeginOrderBatch() { spawnedThisOrder = 0; }

        public static void SpawnMoveMarker(Vector3 p, Color color)
        {
            Spawn(WorldFactory.CreateAoEMarker(Lift(p), 1.4f, new Color(color.r, color.g, color.b, 0.8f)), 0.6f);
        }

        public static void SpawnAttackMoveMarker(Vector3 p)
        {
            Spawn(WorldFactory.CreateAoEMarker(Lift(p), 1.8f, new Color(1f, 0.25f, 0.2f, 0.7f)), 0.7f);
        }

        public static void SpawnAttackMarker(Vector3 p)
        {
            Spawn(WorldFactory.CreateAoEMarker(Lift(p), 1.2f, new Color(1f, 0.1f, 0.1f, 0.9f)), 0.5f);
        }

        public static void SpawnHarvestMarker(Vector3 p, ResourceKind kind)
        {
            Color c = kind == ResourceKind.Gold ? new Color(1f, 0.85f, 0.2f, 0.8f) : new Color(0.4f, 0.9f, 0.4f, 0.8f);
            Spawn(WorldFactory.CreateAoEMarker(Lift(p), 1.5f, c), 0.6f);
        }

        public static void SpawnBuildMarker(Vector3 p, Vector3 size)
        {
            GameObject g = WorldFactory.CreateAoEMarker(Lift(p), Mathf.Max(size.x, size.z) * 0.6f,
                                                        new Color(0.3f, 0.7f, 1f, 0.6f));
            Spawn(g, 1.0f);
        }

        static Vector3 Lift(Vector3 p)
        {
            if (GameManager.I != null && GameManager.I.Map != null)
                p.y = GameManager.I.Map.HeightAtWorld(p);
            return p;
        }

        static void Spawn(GameObject go, float lifetime)
        {
            if (spawnedThisOrder >= MaxMarkers)
            {
                Object.Destroy(go);
                return;
            }
            spawnedThisOrder++;
            pool.Add(go);
            expiry.Add(Time.time + lifetime);
            life.Add(lifetime);
            Transform tr = go != null && go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
            baseScale.Add(tr != null ? tr.localScale : Vector3.one);
        }

        /// <summary>Called from the HUD tick; recycles expired markers.</summary>
        public static void Tick()
        {
            for (int i = pool.Count - 1; i >= 0; i--)
            {
                GameObject go = pool[i];
                if (go == null)
                {
                    pool.RemoveAt(i); expiry.RemoveAt(i); life.RemoveAt(i); baseScale.RemoveAt(i);
                    continue;
                }

                float remaining = expiry[i] - Time.time;
                if (remaining <= 0f)
                {
                    Object.Destroy(go);
                    pool.RemoveAt(i); expiry.RemoveAt(i); life.RemoveAt(i); baseScale.RemoveAt(i);
                    continue;
                }

                float t = Mathf.Clamp01(remaining / Mathf.Max(0.001f, life[i])); // 1 -> 0
                float grow = 1f + (1f - t) * 0.9f;
                Vector3 bs = baseScale[i];
                Transform tr = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
                if (tr != null) tr.localScale = new Vector3(bs.x * grow, bs.y, bs.z * grow);
            }
        }
    }
}
