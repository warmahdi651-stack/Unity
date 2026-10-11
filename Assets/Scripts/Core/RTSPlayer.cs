using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  One player (human or AI): resources, supply, unit registry.
    // -----------------------------------------------------------------------
    public class RTSPlayer
    {
        public readonly int playerId;
        public readonly string name;
        public readonly FactionId faction;
        public readonly Color color;
        public readonly bool isHuman;

        public int gold;
        public int lumber;
        public int supplyUsed;
        public int supplyCap = GameData.StartSupplyCap;

        public bool defeated;

        public readonly List<UnitController> units = new List<UnitController>();
        public readonly List<BuildingController> buildings = new List<BuildingController>();

        /// <summary>Town hall used as the default resource drop-off.</summary>
        public BuildingController mainHall;

        // Stats for the HUD
        public int totalGoldGathered;
        public int totalLumberGathered;
        public int unitsLost;
        public int unitsKilled;

        // AI bookkeeping
        public AiState aiState = AiState.Econ;
        public float aiTimer;
        public int workersTarget = 8;

        public RTSPlayer(int playerId, string name, FactionId faction, bool isHuman)
        {
            this.playerId = playerId;
            this.name = name;
            this.faction = faction;
            this.isHuman = isHuman;
            this.color = faction == FactionId.Alliance ? GameData.AllianceColor : GameData.HordeColor;
            this.gold = GameData.StartGold;
            this.lumber = GameData.StartLumber;
        }

        public int SupplyLeft { get { return Mathf.Max(0, supplyCap - supplyUsed); } }

        public bool CanAfford(int g, int l) { return gold >= g && lumber >= l; }

        public bool Pay(int g, int l)
        {
            if (!CanAfford(g, l)) return false;
            gold -= g;
            lumber -= l;
            return true;
        }

        public void RecalcSupplyCap()
        {
            int cap = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingController b = buildings[i];
                if (b == null || !b.IsComplete) continue;
                cap += b.def.supplyProvided;
            }
            supplyCap = Mathf.Clamp(cap, GameData.StartSupplyCap, GameData.MaxSupply);
        }

        public void AddSupply(int amount) { supplyUsed += amount; }

        public void RemoveUnit(UnitController u)
        {
            units.Remove(u);
            supplyUsed = Mathf.Max(0, supplyUsed - u.def.supply);
        }

        public void RemoveBuilding(BuildingController b)
        {
            buildings.Remove(b);
            if (mainHall == b) mainHall = null;
            RecalcSupplyCap();
        }

        public int CountUnitType(UnitId id)
        {
            int c = 0;
            for (int i = 0; i < units.Count; i++) if (units[i].defId == id) c++;
            return c;
        }

        public BuildingController FindBuilding(BuildingId id)
        {
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i].defId == id && buildings[i].IsComplete) return buildings[i];
            return null;
        }

        /// <summary>Buildings that can train <paramref name="unit"/>.</summary>
        public List<BuildingController> FindTrainers(UnitId unit)
        {
            List<BuildingController> result = new List<BuildingController>();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingController b = buildings[i];
                if (!b.IsComplete) continue;
                if (b.def.trains == null) continue;
                for (int j = 0; j < b.def.trains.Length; j++)
                    if (b.def.trains[j] == unit) { result.Add(b); break; }
            }
            return result;
        }

        /// <summary>Closest finished resource drop-off to a world position.</summary>
        public BuildingController NearestDropoff(Vector3 p)
        {
            BuildingController best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingController b = buildings[i];
                if (!b.IsComplete || !b.def.isResourceDropoff) continue;
                float d = (b.Position - p).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = b; }
            }
            return best != null ? best : mainHall;
        }

        public UnitController NearestWorker(Vector3 p)
        {
            UnitId worker = GameData.WorkerOf(faction);
            UnitController best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController u = units[i];
                if (u.defId != worker || !u.IsAlive) continue;
                float d = (u.Position - p).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = u; }
            }
            return best;
        }
    }
}
