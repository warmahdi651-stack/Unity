using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Hero spells: mana, cooldowns, targeting and effects.
    // -----------------------------------------------------------------------
    public class AbilitySystem
    {
        readonly Dictionary<int, float> cooldowns = new Dictionary<int, float>();
        readonly List<int> keyScratch = new List<int>();
        readonly List<IDamageable> hits = new List<IDamageable>();

        static int Key(UnitController u, AbilityId id)
        {
            return u.GetInstanceID() * 31 + (int)id;
        }

        public void UpdateCooldowns(float dt)
        {
            if (cooldowns.Count == 0) return;

            keyScratch.Clear();
            foreach (KeyValuePair<int, float> kv in cooldowns)
            {
                if (kv.Value <= dt) keyScratch.Add(kv.Key);
                else cooldowns[kv.Key] = kv.Value - dt;
            }
            for (int i = 0; i < keyScratch.Count; i++) cooldowns.Remove(keyScratch[i]);
        }

        public float CooldownRemaining(UnitController u, AbilityId id)
        {
            float v;
            if (cooldowns.TryGetValue(Key(u, id), out v)) return Mathf.Max(0f, v);
            return 0f;
        }

        public bool IsReady(UnitController u, AbilityDef a)
        {
            if (a.manaCost > 0 && u.mana < a.manaCost) return false;
            return CooldownRemaining(u, a.id) <= 0f;
        }

        public bool CanCast(UnitController u, AbilityId id)
        {
            if (u == null || !u.IsAlive) return false;
            AbilityDef a = GameData.GetAbility(id);
            return IsReady(u, a);
        }

        void StartCooldown(UnitController u, AbilityDef a)
        {
            if (a.cooldown > 0f) cooldowns[Key(u, a.id)] = a.cooldown;
            u.SpendMana(a.manaCost);
        }

        // ===================================================================
        //  Execution
        // ===================================================================

        public bool Execute(UnitController caster, AbilityId id, Vector3 point,
                            UnitController targetUnit, BuildingController targetBuilding)
        {
            if (caster == null || !caster.IsAlive) return false;
            AbilityDef a = GameData.GetAbility(id);
            if (a.id == AbilityId.None) return false;
            if (!IsReady(caster, a)) return false;

            bool cast = false;
            switch (id)
            {
                case AbilityId.HolyLight: cast = CastHolyLight(caster, a, targetUnit); break;
                case AbilityId.DivineShield: cast = CastDivineShield(caster, a); break;
                case AbilityId.Blizzard: cast = CastBlizzard(caster, a, point); break;
                case AbilityId.SummonWaterElemental: cast = CastSummon(caster, a, UnitId.Knight, new Color(0.3f, 0.6f, 1f)); break;
                case AbilityId.WindWalk: cast = CastWindWalk(caster, a); break;
                case AbilityId.ChainLightning: cast = CastChainLightning(caster, a, targetUnit); break;
                case AbilityId.FeralSpirit: cast = CastSummon(caster, a, UnitId.Grunt, new Color(0.4f, 1f, 0.5f)); break;
                case AbilityId.CriticalStrike: cast = false; break; // passive
            }

            if (cast) StartCooldown(caster, a);
            return cast;
        }

        // -------------------------------------------------------------------

        bool CastHolyLight(UnitController u, AbilityDef a, UnitController target)
        {
            if (target == null) target = CombatMath.NearestWoundedFriendly(GameManager.I, u.Owner, u.Position, a.range);
            if (target == null) return false;

            float dist = CombatMath.FlatDistance(u.Position, target.Position);
            if (dist > a.range + target.SelectionRadius) return false;

            CombatFx.CreateBeamEffect(u.Position + Vector3.up * 1.4f, target.Position + Vector3.up * 1f,
                                      new Color(1f, 0.95f, 0.6f), 0.35f);

            if (target.Owner == u.Owner)
            {
                target.Heal(a.power + u.level * 20f);
                FloatingText.Spawn(target.Position + Vector3.up * 2.4f,
                                   "+" + Mathf.RoundToInt(a.power + u.level * 20f), new Color(0.5f, 1f, 0.5f));
            }
            else
            {
                target.TakeDamage(a.power + u.level * 20f, AttackDamageType.Magic, u);
            }
            return true;
        }

        bool CastDivineShield(UnitController u, AbilityDef a)
        {
            u.invulnUntil = Time.time + a.power;
            CombatFx.CreateAoE(u.Position, 2.2f, new Color(1f, 0.95f, 0.6f), a.power);
            FloatingText.Spawn(u.Position + Vector3.up * 2.6f, "DIVINE SHIELD", new Color(1f, 0.95f, 0.6f));
            return true;
        }

        bool CastBlizzard(UnitController u, AbilityDef a, Vector3 point)
        {
            if (CombatMath.FlatDistance(u.Position, point) > a.range + a.radius) return false;

            CombatFx.CreateAoE(point, a.radius, new Color(0.7f, 0.9f, 1f), 2.5f);

            GameManager.I.QueryEnemiesInRange(u.Owner, point, a.radius, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                IDamageable d = hits[i];
                for (int wave = 0; wave < 3; wave++)
                    d.TakeDamage((a.power + u.level * 10f) / 3f, AttackDamageType.Magic, u);
            }
            return true;
        }

        bool CastSummon(UnitController u, AbilityDef a, UnitId summonId, Color tint)
        {
            Vector3 p = u.Position + u.transform.forward * 2.5f;
            GameManager gm = GameManager.I;
            Vector2Int t = GameMap.WorldToTile(p);
            t = gm.Map.NearestWalkable(t.x, t.y, 4);
            p = GameMap.TileToWorld(t.x, t.y);
            p.y = gm.Map.HeightAtWorld(p);

            UnitDef def = GameData.GetUnit(summonId);
            UnitController summon = gm.SpawnUnit(u.Owner, summonId, p);
            summon.Heal(a.power);
            CombatFx.SpawnPuff(p);
            FloatingText.Spawn(p + Vector3.up * 2.4f, a.displayName, tint);

            IDamageable enemy = CombatMath.NearestEnemy(gm, u.Owner, p, 14f);
            if (enemy != null)
                summon.IssueOrder(enemy is UnitController ? Order.AttackTarget((UnitController)enemy)
                                                          : Order.AttackTarget((BuildingController)enemy));
            return true;
        }

        bool CastWindWalk(UnitController u, AbilityDef a)
        {
            u.stealthUntil = Time.time + a.power;
            FloatingText.Spawn(u.Position + Vector3.up * 2.6f, "WIND WALK", new Color(0.8f, 1f, 0.85f));
            return true;
        }

        bool CastChainLightning(UnitController u, AbilityDef a, UnitController first)
        {
            if (first == null)
            {
                IDamageable d = CombatMath.NearestEnemy(GameManager.I, u.Owner, u.Position, a.range);
                first = d as UnitController;
                if (first == null && d is BuildingController)
                {
                    d.TakeDamage(a.power + u.level * 15f, AttackDamageType.Magic, u);
                    CombatFx.CreateBeamEffect(u.Position + Vector3.up * 1.5f, d.Position + Vector3.up * 1.5f,
                                              new Color(0.6f, 0.85f, 1f), 0.28f);
                    return true;
                }
            }
            if (first == null) return false;
            if (CombatMath.FlatDistance(u.Position, first.Position) > a.range) return false;

            Vector3 from = u.Position + Vector3.up * 1.5f;
            UnitController current = first;
            float dmg = a.power + u.level * 15f;
            List<UnitController> chained = new List<UnitController>();

            for (int bounce = 0; bounce < 3 && current != null; bounce++)
            {
                CombatFx.CreateBeamEffect(from, current.Position + Vector3.up * 1.2f,
                                          new Color(0.6f, 0.85f, 1f), 0.28f - bounce * 0.06f);
                current.TakeDamage(dmg, AttackDamageType.Magic, u);
                dmg *= 0.7f;
                chained.Add(current);
                from = current.Position + Vector3.up * 1.2f;
                current = NextChainTarget(u, from, chained, 8f);
            }
            return true;
        }

        UnitController NextChainTarget(UnitController caster, Vector3 from, List<UnitController> used, float range)
        {
            GameManager gm = GameManager.I;
            UnitController best = null;
            float bestSqr = range * range;
            for (int i = 0; i < gm.AllUnits.Count; i++)
            {
                UnitController u = gm.AllUnits[i];
                if (u == null || !u.IsAlive || u.Owner == caster.Owner) continue;
                if (used.Contains(u)) continue;
                float d = (u.Position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = u; }
            }
            return best;
        }
    }
}
