using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Simple homing projectiles for ranged attacks and towers.
    // -----------------------------------------------------------------------
    public class ProjectileController : MonoBehaviour
    {
        static readonly List<ProjectileController> live = new List<ProjectileController>();

        IDamageable target;
        object source;
        RTSPlayer owner;
        float damage;
        AttackDamageType type;
        float speed;
        float life = 4f;
        Vector3 lastTargetPos;
        float arcHeight;

        public static ProjectileController Launch(UnitController from, IDamageable to, float dmg,
                                                  AttackDamageType type, float speed)
        {
            ProjectileController p = Spawn(from.Owner, from.Position + Vector3.up * (from.def.visualScale.y * 0.75f),
                                           from.Owner.color, 0.16f, from);
            p.target = to;
            p.damage = dmg;
            p.type = type;
            p.speed = speed;
            p.lastTargetPos = to != null ? to.Position : from.Position;
            p.arcHeight = 0.6f;
            return p;
        }

        public static ProjectileController LaunchBuilding(BuildingController from, IDamageable to, float dmg,
                                                          AttackDamageType type, float speed)
        {
            ProjectileController p = Spawn(from.Owner, from.Position + Vector3.up * 4.2f,
                                           from.Owner.color, 0.22f, from);
            p.target = to;
            p.damage = dmg;
            p.type = type;
            p.speed = speed;
            p.lastTargetPos = to != null ? to.Position : from.Position;
            p.arcHeight = 2.5f;
            return p;
        }

        static ProjectileController Spawn(RTSPlayer owner, Vector3 pos, Color color, float size, object source)
        {
            Color c = Color.Lerp(color, Color.white, 0.4f);
            GameObject go = WorldFactory.CreateProjectile(c, size);
            go.transform.position = pos;
            ProjectileController p = go.AddComponent<ProjectileController>();
            p.owner = owner;
            p.source = source;
            live.Add(p);
            return p;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            life -= dt;
            if (life <= 0f) { Finish(false); return; }

            Vector3 aim = target != null && target.IsAlive ? target.Position + Vector3.up * 1f : lastTargetPos;
            if (target != null) lastTargetPos = aim;

            Vector3 dir = aim - transform.position;
            float dist = dir.magnitude;
            float step = speed * dt;

            if (dist <= step + 0.25f)
            {
                transform.position = aim;
                Finish(target != null && target.IsAlive);
                return;
            }

            dir /= dist;
            // Gentle arc so arrows do not travel perfectly flat.
            dir.y += arcHeight * Mathf.Sin(Mathf.Clamp01(1f - dist / 12f)) * 0.35f;
            transform.position += dir.normalized * step;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        void Finish(bool hitTarget)
        {
            if (hitTarget && target != null)
            {
                target.TakeDamage(damage, type, source);
                CombatFx.Impact(transform.position, owner != null ? owner.color : Color.white);
            }
            live.Remove(this);
            Destroy(gameObject);
        }

        /// <summary>Removes every projectile belonging to a player (on defeat).</summary>
        public static void ClearAll()
        {
            for (int i = live.Count - 1; i >= 0; i--)
                if (live[i] != null) Destroy(live[i].gameObject);
            live.Clear();
        }
    }
}
