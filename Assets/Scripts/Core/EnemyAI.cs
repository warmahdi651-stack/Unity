using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Opponent AI: keeps an economy alive, builds an army and attacks in
    //  waves.  Deliberately simple and readable rather than min-maxed.
    // -----------------------------------------------------------------------
    public class EnemyAI : MonoBehaviour
    {
        RTSPlayer me;
        GameManager gm;

        float nextThink = 3f;
        float waveAt = 150f;
        int waveSize = 4;
        int farmsBuilt;
        bool barracksBuilt;
        bool altarBuilt;

        readonly List<UnitController> army = new List<UnitController>();
        readonly List<UnitController> workers = new List<UnitController>();

        public void Init()
        {
            gm = GameManager.I;
            me = gm.Enemy;
        }

        void Update()
        {
            if (me == null || gm.Map == null || gm.GameOver) return;
            if (Time.time < nextThink) return;
            nextThink = Time.time + 1.5f;

            CollectLists();
            ManageEconomy();
            ManageBuildings();
            ManageArmy();
            ManageAttack();
        }

        // ===================================================================

        void CollectLists()
        {
            UnitId workerId = GameData.WorkerOf(me.faction);
            workers.Clear();
            army.Clear();
            for (int i = 0; i < me.units.Count; i++)
            {
                UnitController u = me.units[i];
                if (u == null || !u.IsAlive) continue;
                if (u.defId == workerId) workers.Add(u);
                else army.Add(u);
            }
        }

        // ===================================================================
        //  Economy
        // ===================================================================

        void ManageEconomy()
        {
            int target = Mathf.Min(16, 6 + (int)(gm.MatchTime / 25f));

            // Train workers from the hall.
            if (workers.Count < target && me.mainHall != null && me.mainHall.IsComplete)
            {
                UnitId workerId = GameData.WorkerOf(me.faction);
                if (me.mainHall.trainQueue.Count == 0 && me.SupplyLeft >= 1)
                    me.mainHall.Train(workerId);
            }

            // Idle workers go back to work.
            for (int i = 0; i < workers.Count; i++)
            {
                UnitController w = workers[i];
                if (w.current.type != OrderType.Idle) continue;
                ResourceNode n = NearestResource(w.Position);
                if (n != null) w.IssueOrder(Order.Harvest(n));
            }

            // Supply
            if (me.SupplyLeft <= 1 && farmsBuilt < 8 && me.CanAfford(60, 20))
                TryBuildNearBase(me.faction == FactionId.Horde ? BuildingId.Burrow : BuildingId.Farm);
        }

        ResourceNode NearestResource(Vector3 from)
        {
            ResourceNode best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < gm.Map.Nodes.Count; i++)
            {
                ResourceNode n = gm.Map.Nodes[i];
                if (n.Depleted) continue;
                float d = (n.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = n; }
            }
            return best;
        }

        // ===================================================================
        //  Buildings
        // ===================================================================

        void ManageBuildings()
        {
            BuildingId barracks = me.faction == FactionId.Horde ? BuildingId.TrollHut : BuildingId.Barracks;
            BuildingId altar = me.faction == FactionId.Horde ? BuildingId.AltarOfStorms : BuildingId.AltarOfKings;

            if (!barracksBuilt && me.FindBuilding(barracks) == null && gm.MatchTime > 30f)
                if (TryBuildNearBase(barracks)) barracksBuilt = true;

            if (barracksBuilt && !altarBuilt && me.FindBuilding(altar) == null && gm.MatchTime > 120f)
                if (TryBuildNearBase(altar)) altarBuilt = true;

            // Farms are tracked lazily so the AI keeps expanding supply.
            farmsBuilt = 0;
            for (int i = 0; i < me.buildings.Count; i++)
            {
                BuildingDef d = me.buildings[i].def;
                if (d.supplyProvided > 0) farmsBuilt++;
            }
        }

        bool TryBuildNearBase(BuildingId id)
        {
            BuildingDef def = GameData.GetBuilding(id);
            if (!me.CanAfford(def.goldCost, def.lumberCost)) return false;
            if (workers.Count == 0) return false;

            Vector3 anchor = me.mainHall != null ? me.mainHall.Position : GameMap.TileToWorld(20, 20);
            Vector2Int spot = FindFreeSpot(anchor, def);
            if (spot.x < 0) return false;

            me.Pay(def.goldCost, def.lumberCost);
            gm.Events.OnResourcesChanged();

            List<UnitController> crew = new List<UnitController>();
            for (int i = 0; i < workers.Count && crew.Count < 3; i++)
                crew.Add(workers[i]);

            gm.Orders.BuildOrder(crew, id, spot);
            return true;
        }

        Vector2Int FindFreeSpot(Vector3 anchor, BuildingDef def)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                float angle = attempt * 0.9f;
                float radius = 5f + attempt * 0.35f;
                Vector3 p = anchor + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Vector2Int t = GameMap.WorldToTile(p);
                if (gm.Map.CanPlaceBuilding(t, def.sizeX, def.sizeZ)) return t;
            }
            return new Vector2Int(-1, -1);
        }

        // ===================================================================
        //  Army
        // ===================================================================

        void ManageArmy()
        {
            if (me.SupplyLeft < 2) return;

            BuildingController barracks = me.FindBuilding(me.faction == FactionId.Horde ? BuildingId.TrollHut : BuildingId.Barracks);
            if (barracks != null && barracks.trainQueue.Count < 2)
            {
                UnitId[] options = me.faction == FactionId.Horde
                    ? new[] { UnitId.Grunt, UnitId.TrollAxethrower, UnitId.Ogre }
                    : new[] { UnitId.Footman, UnitId.Archer, UnitId.Knight };

                UnitId pick = options[Random.Range(0, Mathf.Min(options.Length, 2 + (int)(gm.MatchTime / 180f)))];
                barracks.Train(pick);
            }

            // Hero once the altar exists.
            BuildingController altar = me.FindBuilding(me.faction == FactionId.Horde ? BuildingId.AltarOfStorms : BuildingId.AltarOfKings);
            if (altar != null && altar.trainQueue.Count == 0)
            {
                UnitId hero = me.faction == FactionId.Horde ? UnitId.Blademaster : UnitId.Paladin;
                if (me.CountUnitType(hero) == 0) altar.Train(hero);
            }
        }

        // ===================================================================
        //  Attacks
        // ===================================================================

        void ManageAttack()
        {
            if (gm.MatchTime < waveAt) return;

            // Retreat badly hurt units home.
            Vector3 home = me.mainHall != null ? me.mainHall.Position : GameMap.TileToWorld(20, 20);
            for (int i = 0; i < army.Count; i++)
            {
                UnitController u = army[i];
                if (u.CurrentHp < u.MaxHpValue * 0.25f &&
                    CombatMath.FlatDistance(u.Position, home) > 18f)
                {
                    u.IssueOrder(Order.Move(home));
                }
            }

            if (army.Count < waveSize) return;

            Vector3 target = gm.Human.mainHall != null ? gm.Human.mainHall.Position : FindAnyEnemyPosition();
            gm.Orders.AttackMoveGroup(army, target);

            waveSize += 3;
            waveAt = gm.MatchTime + 90f + Random.Range(0f, 40f);
            gm.Say(me.name + " is attacking!", new Color(1f, 0.45f, 0.4f));
        }

        Vector3 FindAnyEnemyPosition()
        {
            for (int i = 0; i < gm.AllUnits.Count; i++)
            {
                UnitController u = gm.AllUnits[i];
                if (u != null && u.IsAlive && u.Owner == gm.Human) return u.Position;
            }
            for (int i = 0; i < gm.AllBuildings.Count; i++)
            {
                BuildingController b = gm.AllBuildings[i];
                if (b != null && b.IsAlive && b.Owner == gm.Human) return b.Position;
            }
            return GameMap.TileToWorld(20, 20);
        }
    }
}
