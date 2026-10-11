using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Anything that can take damage: units and buildings.
    // -----------------------------------------------------------------------
    public interface IDamageable
    {
        bool IsAlive { get; }
        float CurrentHp { get; }
        float MaxHpValue { get; }
        int Armor { get; }
        RTSPlayer Owner { get; }
        Vector3 Position { get; }
        float SelectionRadius { get; }
        string DisplayName { get; }
        bool IsHero { get; }
        bool IsBuilding { get; }

        void TakeDamage(float amount, AttackDamageType type, object source);
        void Heal(float amount);
    }

    // -----------------------------------------------------------------------
    //  Pure combat maths + target searching.  No Unity calls that need a
    //  scene, so this class is trivially unit-testable.
    // -----------------------------------------------------------------------
    public static class CombatMath
    {
        /// <summary>Warcraft 3 armour formula: each armour point multiplies the
        ///  incoming damage by 0.94, negative armour amplifies it.</summary>
        public static float ApplyArmor(float damage, int armor)
        {
            return damage * Mathf.Pow(0.94f, armor);
        }

        /// <summary>Damage type vs. armour class modifier table.</summary>
        public static float TypeMultiplier(AttackDamageType attack, bool targetIsBuilding)
        {
            switch (attack)
            {
                case AttackDamageType.Piercing: return targetIsBuilding ? 0.35f : 1.0f;
                case AttackDamageType.Siege: return targetIsBuilding ? 1.5f : 0.5f;
                case AttackDamageType.Magic: return targetIsBuilding ? 0.75f : 1.0f;
                default: return targetIsBuilding ? 1.0f : 1.0f;
            }
        }

        public static float CalculateDamage(float raw, AttackDamageType type, int armor, bool targetIsBuilding)
        {
            float d = raw * TypeMultiplier(type, targetIsBuilding);
            d = ApplyArmor(d, armor);
            // Attacks always do at least 1 damage so sieges can never stall.
            return Mathf.Max(1f, d);
        }

        public static bool IsHostile(RTSPlayer a, RTSPlayer b)
        {
            if (a == null || b == null) return false;
            return a != b;
        }

        public static bool IsHostile(IDamageable a, IDamageable b)
        {
            if (a == null || b == null) return false;
            return IsHostile(a.Owner, b.Owner);
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// True when the attacker can hit the target: edge-to-edge distance
        /// (so big buildings are reachable) must be within attack range.
        /// </summary>
        public static bool InAttackRange(Vector3 attacker, float attackRange, IDamageable target)
        {
            if (target == null) return false;
            float d = FlatDistance(attacker, target.Position);
            return d <= attackRange + target.SelectionRadius;
        }

        // -------------------------------------------------------------------
        //  Target search
        // -------------------------------------------------------------------

        /// <summary>Closest hostile inside <paramref name="range"/> that the
        ///  player can currently see.</summary>
        public static IDamageable NearestEnemy(GameManager gm, RTSPlayer forPlayer, Vector3 from, float range)
        {
            if (gm == null) return null;
            IDamageable best = null;
            float bestDist = float.MaxValue;

            List<UnitController> units = gm.AllUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController u = units[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Owner == forPlayer) continue;
                if (u.IsStealthed && !IsDetectionNearby(gm, forPlayer, u.Position)) continue;
                if (!IsSeen(gm, forPlayer, u)) continue;

                float d = FlatDistance(from, u.Position) - u.SelectionRadius;
                if (d <= range && d < bestDist) { bestDist = d; best = u; }
            }

            List<BuildingController> buildings = gm.AllBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingController b = buildings[i];
                if (b == null || !b.IsAlive) continue;
                if (b.Owner == forPlayer) continue;
                if (!IsSeen(gm, forPlayer, b.Position)) continue;

                float d = FlatDistance(from, b.Position) - b.SelectionRadius;
                if (d <= range && d < bestDist) { bestDist = d; best = b; }
            }
            return best;
        }

        static bool IsSeen(GameManager gm, RTSPlayer p, Vector3 pos)
        {
            return gm.Fog == null || gm.Fog.IsVisible(p, pos);
        }

        /// <summary>Own units are always known to their owner, even in fog.</summary>
        static bool IsSeen(GameManager gm, RTSPlayer viewer, IDamageable target)
        {
            if (target.Owner == viewer) return true;
            return IsSeen(gm, viewer, target.Position);
        }

        static bool IsDetectionNearby(GameManager gm, RTSPlayer viewer, Vector3 pos)
        {
            // Simplified true-sight: any friendly unit within 8 tiles reveals stealth.
            for (int i = 0; i < gm.AllUnits.Count; i++)
            {
                UnitController u = gm.AllUnits[i];
                if (u == null || !u.IsAlive || u.Owner != viewer) continue;
                if (FlatDistance(u.Position, pos) < 8f) return true;
            }
            return false;
        }

        /// <summary>Closest friendly unit that is missing hit points.</summary>
        public static UnitController NearestWoundedFriendly(GameManager gm, RTSPlayer forPlayer, Vector3 from, float range)
        {
            UnitController best = null;
            float bestSqr = range * range;
            for (int i = 0; i < gm.AllUnits.Count; i++)
            {
                UnitController u = gm.AllUnits[i];
                if (u == null || !u.IsAlive || u.Owner != forPlayer) continue;
                if (u.CurrentHp >= u.MaxHpValue - 0.5f) continue;
                float d = (u.Position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = u; }
            }
            return best;
        }
    }
}
