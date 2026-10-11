using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  One unit: stats, order queue, movement, combat, harvesting and hero
    //  levelling.  This is the heart of the simulation.
    // -----------------------------------------------------------------------
    public class UnitController : MonoBehaviour, IDamageable
    {
        public UnitId defId;
        public UnitDef def;
        public RTSPlayer owner;

        public readonly List<Order> queue = new List<Order>();
        public Order current;

        // ---- life ----
        public float hp;
        public float mana;
        public bool IsAlive { get { return alive; } }
        bool alive = true;

        // ---- hero ----
        public int level = 1;
        public int xp;
        public float strength, agility, intelligence;

        // ---- buffs ----
        public float invulnUntil;
        public float stealthUntil;
        public bool IsStealthed { get { return Time.time < stealthUntil; } }
        public bool IsInvulnerable { get { return Time.time < invulnUntil; } }

        // ---- combat ----
        public float attackReadyAt;
        float lastAttackAnimAt = -10f;
        float nextAcquireAt;   // throttles the idle/attack-move target scan

        // ---- movement ----
        readonly List<Vector3> path = new List<Vector3>();
        int pathIndex;
        float repathAt;

        // ---- worker ----
        public int carried;
        public ResourceKind carriedKind;
        float harvestProgress;
        ResourceNode assignedNode;

        // ---- build ----
        BuildingController buildTarget;

        // ---- presentation ----
        Transform selectionRing;
        Renderer[] bodyRenderers;
        public HealthBarElement barElement;

        public const float AcquisitionRange = 10f;
        public const float AutoHarvestSearchRange = 22f;

        // ===================================================================
        //  IDamageable
        // ===================================================================

        public float CurrentHp { get { return hp; } }
        public float MaxHpValue { get { return MaxHp; } }
        public int Armor { get { return def.armor; } }
        public RTSPlayer Owner { get { return owner; } }
        public Vector3 Position { get { return transform.position; } }
        public float SelectionRadius { get { return def.radius; } }
        public string DisplayName { get { return def.displayName; } }
        public bool IsHero { get { return def.isHero; } }
        public bool IsBuilding { get { return false; } }

        /// <summary>Hero attribute bonuses scale hit points.</summary>
        public float MaxHp
        {
            get
            {
                float bonus = def.isHero ? (level - 1) * 40f + strength * 4f : 0f;
                return def.maxHp + bonus;
            }
        }

        public float MaxMana
        {
            get
            {
                if (def.maxMana <= 0f) return 0f;
                return def.maxMana + (def.isHero ? (level - 1) * 25f + intelligence * 6f : 0f);
            }
        }

        public float AttackDamage
        {
            get
            {
                float bonus = 0f;
                if (def.isHero)
                {
                    bonus += (level - 1) * 2f;
                    if (def.heroPrimaryAttribute == 0) bonus += strength * 1.2f;
                    else if (def.heroPrimaryAttribute == 1) bonus += agility * 1.2f;
                    else bonus += intelligence * 1.2f;
                }
                return def.attackDamage + bonus;
            }
        }

        public float MoveSpeed
        {
            get
            {
                float s = def.moveSpeed;
                if (IsStealthed) s *= 1.25f;
                return s;
            }
        }

        // ===================================================================

        public void Init(RTSPlayer p, UnitId id, UnitDef d)
        {
            owner = p;
            defId = id;
            def = d;
            hp = MaxHp;
            mana = 0f;

            if (def.isHero)
            {
                strength = 18f;
                agility = def.heroPrimaryAttribute == 1 ? 20f : 15f;
                intelligence = def.heroPrimaryAttribute == 2 ? 20f : 14f;
                if (def.heroPrimaryAttribute == 0) strength = 20f;
                hp = MaxHp;
            }

            Transform ring = transform.Find("SelectionRing");
            selectionRing = ring;

            List<Renderer> rs = new List<Renderer>();
            Renderer[] all = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < all.Length; i++)
                if (all[i].gameObject.name != "SelectionRing") rs.Add(all[i]);
            bodyRenderers = rs.ToArray();

        }

        // ===================================================================
        //  Orders
        // ===================================================================

        public void IssueOrder(Order o)
        {
            queue.Clear();
            queue.Add(o);
            BeginCurrent();
        }

        public void QueueOrder(Order o)
        {
            if (queue.Count == 0) IssueOrder(o);
            else if (queue.Count < 8) queue.Add(o);
        }

        // Set while BeginCurrent() is running so that a nested IssueOrder /
        // Advance (harvest -> return, build -> complete) cannot corrupt the queue.
        bool resolving;
        bool pendingShift;

        void Advance()
        {
            if (resolving) { pendingShift = true; return; }
            Shift();
        }

        void Shift()
        {
            resolving = true;
            pendingShift = false;
            if (queue.Count > 0) queue.RemoveAt(0);
            path.Clear();
            pathIndex = 0;
            harvestProgress = 0f;
            BeginCurrent();
            if (pendingShift)
            {
                pendingShift = false;
                resolving = false;
                Shift();
                return;
            }
            resolving = false;
        }

        public void ClearOrders()
        {
            queue.Clear();
            current = new Order();
            path.Clear();
            pathIndex = 0;
            harvestProgress = 0f;
            pendingShift = false;
        }

        void BeginCurrent()
        {
            current = queue.Count > 0 ? queue[0] : new Order();
            path.Clear();
            pathIndex = 0;
            harvestProgress = 0f;
            switch (current.type)
            {
                case OrderType.Move:
                case OrderType.AttackMove:
                    SetPathTo(current.position);
                    break;

                case OrderType.Harvest:
                    assignedNode = current.node;
                    if (assignedNode != null && assignedNode.Depleted) { Advance(); return; }
                    if (assignedNode != null) SetPathTo(ApproachPoint(assignedNode.position, def.radius + 0.9f));
                    break;

                case OrderType.ReturnResource:
                    break;

                case OrderType.AttackTarget:
                    break;

                case OrderType.Build:
                    buildTarget = FindBuildSite();
                    if (buildTarget == null) SetPathTo(ApproachPoint(current.position, 1.6f));
                    break;

                case OrderType.Repair:
                    if (current.targetBuilding == null) { Advance(); return; }
                    SetPathTo(ApproachPoint(current.targetBuilding.Position, current.targetBuilding.def.sizeZ * 0.6f + 1f));
                    break;

                case OrderType.CastAbility:
                    if (current.targetUnit != null && !current.targetUnit.IsAlive) current.targetUnit = null;
                    if (current.targetBuilding != null && !current.targetBuilding.IsAlive) current.targetBuilding = null;
                    GameManager.I.Abilities.Execute(this, current.ability, current.position,
                                                    current.targetUnit, current.targetBuilding);
                    // Consumed immediately; Shift() runs right after the switch.
                    pendingShift = true;
                    break;
            }
        }

        BuildingController FindBuildSite()
        {
            GameManager gm = GameManager.I;
            for (int i = 0; i < gm.AllBuildings.Count; i++)
            {
                BuildingController b = gm.AllBuildings[i];
                if (b == null || b.IsComplete) continue;
                if (b.defId != current.buildingId) continue;
                if (b.origin != current.buildOrigin) continue;
                return b;
            }
            return null;
        }

        // ===================================================================
        //  Movement
        // ===================================================================

        void SetPathTo(Vector3 target)
        {
            GameManager gm = GameManager.I;
            path.Clear();
            pathIndex = 0;

            List<Vector3> p = gm.Map.FindPath(transform.position, target);
            if (p.Count == 0) p.Add(target);

            // Straight-line shortcut when we can already see the destination.
            Vector2Int a = GameMap.WorldToTile(transform.position);
            Vector2Int b = GameMap.WorldToTile(target);
            if (gm.Map.LineWalkable(a, b))
            {
                path.Add(target);
            }
            else
            {
                path.AddRange(p);
                if (path.Count > 0) path[path.Count - 1] = target;
            }
        }

        /// <summary>A point on the walkable edge of an obstacle.</summary>
        Vector3 ApproachPoint(Vector3 target, float clearance)
        {
            GameManager gm = GameManager.I;
            Vector3 dir = transform.position - target;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 p = target + dir * clearance;
            if (!gm.Map.IsWalkableWorld(p))
            {
                Vector2Int t = GameMap.WorldToTile(p);
                t = gm.Map.NearestWalkable(t.x, t.y, 6);
                p = GameMap.TileToWorld(t.x, t.y);
            }
            p.y = gm.Map.HeightAtWorld(p);
            return p;
        }

        void TickMovement(float dt)
        {
            // ---- no path: stand still, but still resolve crowding ----
            if (pathIndex >= path.Count)
            {
                Vector3 idlePush = SeparationForce(transform.position, def.radius);
                if (idlePush.sqrMagnitude > 0.0001f)
                {
                    Vector3 p = transform.position + idlePush.normalized * (MoveSpeed * 0.35f * dt);
                    p.y = GameManager.I.Map.HeightAtWorld(p);
                    if (GameManager.I.Map.IsWalkableWorld(p)) transform.position = p;
                }
                return;
            }

            // ---- follow the current waypoint ----
            Vector3 target = path[pathIndex];
            target.y = GameManager.I.Map.HeightAtWorld(target);

            Vector3 pos = transform.position;
            Vector3 delta = target - pos;
            delta.y = 0f;
            float dist = delta.magnitude;

            if (dist <= 0.35f)
            {
                pathIndex++;
                if (pathIndex >= path.Count) return;      // arrived
                target = path[pathIndex];
                target.y = GameManager.I.Map.HeightAtWorld(target);
                delta = target - pos;
                delta.y = 0f;
                dist = delta.magnitude;
                if (dist < 0.0001f) return;
            }

            Vector3 dir = delta / dist;

            // Local avoidance so groups do not walk through each other.
            Vector3 push = SeparationForce(pos, def.radius);
            if (push.sqrMagnitude > 0.0001f)
                dir = (dir + push.normalized * Mathf.Clamp01(push.magnitude) * 0.75f).normalized;

            float step = Mathf.Min(MoveSpeed * dt, dist);
            pos += dir * step;

            Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, def.turnRate * dt);

            // Stay glued to the terrain and out of solid tiles.
            pos.y = GameManager.I.Map.HeightAtWorld(pos);
            if (GameManager.I.Map.IsWalkableWorld(pos) || dist < 0.6f) transform.position = pos;

            // Cheap walk cycle: bob and swing the torso.
            Transform torso = transform.Find("Torso");
            if (torso != null)
            {
                float bob = Mathf.Sin(Time.time * 9f) * 0.05f;
                torso.localPosition = new Vector3(0f, def.visualScale.y * 0.42f + bob, 0f);
            }
        }

        Vector3 SeparationForce(Vector3 pos, float radius)
        {
            GameManager gm = GameManager.I;
            Vector3 push = Vector3.zero;
            List<UnitController> all = gm.AllUnits;
            float range = radius * 2.4f;
            float rangeSqr = range * range;
            for (int i = 0; i < all.Count; i++)
            {
                UnitController u = all[i];
                if (u == null || u == this || !u.IsAlive) continue;
                Vector3 d = pos - u.Position;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr > rangeSqr || sqr < 0.0001f) continue;
                float dist = Mathf.Sqrt(sqr);
                push += (d / dist) * (1f - dist / range);
            }
            return push;
        }

        public bool HasArrived()
        {
            return pathIndex >= path.Count;
        }

        // ===================================================================
        //  Frame update (driven by OrderSystem -> GameManager)
        // ===================================================================

        void Update()
        {
            if (!alive || GameManager.I == null || GameManager.I.Map == null) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f) * GameManager.I.gameSpeed;

            Regen(dt);
            TickCooldownVisuals();

            // An order that completed during the previous BeginCurrent() is
            // drained here so we never recurse into the order queue.
            if (pendingShift) { Shift(); return; }

            switch (current.type)
            {
                case OrderType.Move:
                    TickMovement(dt);
                    if (HasArrived()) Advance();
                    break;

                case OrderType.AttackMove:
                    TickAttackMove(dt);
                    break;

                case OrderType.AttackTarget:
                    TickAttackTarget(dt);
                    break;

                case OrderType.Harvest:
                    TickHarvest(dt);
                    break;

                case OrderType.ReturnResource:
                    TickReturn(dt);
                    break;

                case OrderType.Build:
                    TickBuild(dt);
                    break;

                case OrderType.Repair:
                    TickRepair(dt);
                    break;

                default:
                    TickIdle(dt);
                    break;
            }
        }

        void Regen(float dt)
        {
            if (hp < MaxHp) hp = Mathf.Min(MaxHp, hp + def.hpRegen * dt);
            if (MaxMana > 0f && mana < MaxMana) mana = Mathf.Min(MaxMana, mana + def.manaRegen * dt);
        }

        void TickCooldownVisuals()
        {
            // Cheap attack "lunge": pitch the torso forward for a few frames.
            Transform torso = transform.Find("Torso");
            if (torso == null) return;
            float since = Time.time - lastAttackAnimAt;
            float t = since < 0.18f ? Mathf.Sin((since / 0.18f) * Mathf.PI) : 0f;
            torso.localRotation = Quaternion.Euler(t * 22f, 0f, 0f);

            if (IsStealthed) ApplyStealthTint(true);
            else if (stealthApplied) ApplyStealthTint(false);
        }

        bool stealthApplied;

        void ApplyStealthTint(bool on)
        {
            stealthApplied = on;
            SetBodyAlpha(on ? 0.35f : 1f);
        }

        // -------------------------------------------------------------------
        //  Combat orders
        // -------------------------------------------------------------------

        void TickAttackMove(float dt)
        {
            IDamageable enemy = ScanForEnemy();
            if (enemy != null)
            {
                current.type = OrderType.AttackTarget;
                current.targetUnit = enemy as UnitController;
                current.targetBuilding = enemy as BuildingController;
                return;
            }
            TickMovement(dt);
            if (HasArrived()) Advance();
        }

        void TickAttackTarget(float dt)
        {
            IDamageable target = current.Target;
            if (target == null)
            {
                // Target died or went out of sight: fall through to whatever was
                // queued next (or Idle, which auto-acquires a new target).
                Advance();
                return;
            }

            bool inRange = CombatMath.InAttackRange(transform.position, def.attackRange, target);
            if (inRange)
            {
                FaceTowards(target.Position, dt);
                TryAttack(target);
            }
            else
            {
                if (Time.time > repathAt || path.Count == 0)
                {
                    repathAt = Time.time + 0.5f;
                    SetPathTo(ApproachPoint(target.Position, def.attackRange * 0.8f + target.SelectionRadius));
                }
                TickMovement(dt);
            }
        }

        void TickIdle(float dt)
        {
            // Auto-acquire, exactly like Warcraft III.
            IDamageable enemy = ScanForEnemy();
            if (enemy != null)
            {
                current.type = OrderType.AttackTarget;
                current.targetUnit = enemy as UnitController;
                current.targetBuilding = enemy as BuildingController;
                return;
            }
            if (def.unitClass == UnitClass.Worker && carried == 0 && owner.isHuman == false)
            {
                // AI workers keep the economy running on their own.
                ResourceNode n = FindNearestResource();
                if (n != null) IssueOrder(Order.Harvest(n));
            }
            TickMovement(dt); // keeps separation pushing so idle groups do not overlap
        }

        /// <summary>Target scan, throttled so a large army does not do an O(n^2)
        ///  sweep every single frame.</summary>
        IDamageable ScanForEnemy()
        {
            if (Time.time < nextAcquireAt) return null;
            nextAcquireAt = Time.time + 0.2f;
            return CombatMath.NearestEnemy(GameManager.I, owner, transform.position, AcquisitionRange);
        }

        void FaceTowards(Vector3 p, float dt)
        {
            Vector3 dir = p - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, def.turnRate * dt);
        }

        public void TryAttack(IDamageable target)
        {
            if (target == null || !target.IsAlive) return;
            if (Time.time < attackReadyAt) return;
            if (!CombatMath.InAttackRange(transform.position, def.attackRange, target)) return;

            attackReadyAt = Time.time + def.attackCooldown;
            lastAttackAnimAt = Time.time;

            if (IsStealthed) stealthUntil = 0f;

            float dmg = AttackDamage;

            // Blademaster critical strike.
            if (defId == UnitId.Blademaster)
            {
                AbilityDef crit = GameData.GetAbility(AbilityId.CriticalStrike);
                if (Random.value < 0.15f + level * 0.015f)
                {
                    dmg *= crit.power;
                    FloatingText.Spawn(target.Position, "CRIT!", new Color(1f, 0.5f, 0.1f));
                }
            }

            if (def.rangedAttack && def.projectileSpeed > 0f)
            {
                ProjectileController.Launch(this, target, dmg, def.damageType, def.projectileSpeed);
            }
            else
            {
                target.TakeDamage(dmg, def.damageType, this);
                CombatFx.MeleeImpact(target.Position);
            }
        }

        // -------------------------------------------------------------------
        //  Economy
        // -------------------------------------------------------------------

        ResourceNode FindNearestResource()
        {
            GameManager gm = GameManager.I;
            ResourceNode best = null;
            float bestSqr = AutoHarvestSearchRange * AutoHarvestSearchRange;
            for (int i = 0; i < gm.Map.Nodes.Count; i++)
            {
                ResourceNode n = gm.Map.Nodes[i];
                if (n.Depleted) continue;
                if (n.kind == ResourceKind.Gold && n.IsFull) continue;
                float d = (n.position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = n; }
            }
            return best;
        }

        void TickHarvest(float dt)
        {
            ResourceNode n = current.node;
            if (n == null || n.Depleted)
            {
                DetachNode();
                ResourceNode alt = FindNearestResource();
                if (alt != null) { current.node = alt; assignedNode = alt; SetPathTo(ApproachPoint(alt.position, def.radius + 0.9f)); }
                else Advance();
                return;
            }

            float dist = CombatMath.FlatDistance(transform.position, n.position);
            if (dist > def.radius + 1.6f)
            {
                if (path.Count == 0 || Time.time > repathAt)
                {
                    repathAt = Time.time + 0.6f;
                    SetPathTo(ApproachPoint(n.position, def.radius + 0.9f));
                }
                TickMovement(dt);
                return;
            }

            FaceTowards(n.position, dt);

            if (assignedNode != n)
            {
                if (n.IsFull)
                {
                    ResourceNode alt = FindNearestResource();
                    if (alt != null) { current.node = alt; assignedNode = alt; path.Clear(); pathIndex = 0; }
                    return;
                }
                DetachNode();
                assignedNode = n;
                n.harvesters.Add(this);
            }

            harvestProgress += dt;
            if (harvestProgress >= def.harvestTime)
            {
                harvestProgress = 0f;
                int take = Mathf.Min(def.carryCapacity, n.amount);
                n.amount -= take;
                carried += take;
                carriedKind = n.kind;
                lastAttackAnimAt = Time.time;

                if (n.Depleted)
                {
                    GameManager.I.Map.RemoveNode(n);
                    if (n.go != null) Destroy(n.go);
                    DetachNode();
                }

                if (carried >= def.carryCapacity || (n.Depleted && carried > 0))
                    IssueOrder(Order.ReturnResource());
            }
        }

        void TickReturn(float dt)
        {
            BuildingController hall = owner.NearestDropoff(transform.position);
            if (hall == null) { carried = 0; Advance(); return; }

            float reach = hall.def.sizeZ * 0.6f + 2.2f;
            float dist = CombatMath.FlatDistance(transform.position, hall.Position);
            if (dist > reach)
            {
                if (path.Count == 0 || Time.time > repathAt)
                {
                    repathAt = Time.time + 0.7f;
                    SetPathTo(ApproachPoint(hall.Position, reach * 0.85f));
                }
                TickMovement(dt);
                return;
            }

            FaceTowards(hall.Position, dt);
            if (carriedKind == ResourceKind.Gold)
            {
                owner.gold += carried;
                owner.totalGoldGathered += carried;
            }
            else
            {
                owner.lumber += carried;
                owner.totalLumberGathered += carried;
            }
            FloatingText.Spawn(transform.position + Vector3.up * 2f, "+" + carried,
                               carriedKind == ResourceKind.Gold ? new Color(1f, 0.85f, 0.2f) : new Color(0.5f, 1f, 0.5f));
            GameManager.I.Events.OnResourcesChanged();
            carried = 0;
            carriedKind = ResourceKind.None;

            // Resume harvesting automatically.
            ResourceNode next = FindNearestResource();
            if (next != null) IssueOrder(Order.Harvest(next));
            else Advance();
        }

        void DetachNode()
        {
            if (assignedNode != null) assignedNode.harvesters.Remove(this);
            assignedNode = null;
        }

        // -------------------------------------------------------------------
        //  Construction / repair
        // -------------------------------------------------------------------

        void TickBuild(float dt)
        {
            BuildingController site = buildTarget != null ? buildTarget : FindBuildSite();
            buildTarget = site;

            if (site == null)
            {
                if (!HasArrived()) { TickMovement(dt); return; }
                GameManager gm = GameManager.I;
                BuildingDef def2 = GameData.GetBuilding(current.buildingId);
                if (!gm.Map.CanPlaceBuilding(current.buildOrigin, def2.sizeX, def2.sizeZ))
                {
                    if (owner.isHuman) gm.Say("Cannot build there.", Color.red);
                    Advance();
                    return;
                }
                site = gm.SpawnBuilding(owner, current.buildingId, current.buildOrigin, false);
                buildTarget = site;
                GroundEffect.SpawnBuildMarker(site.Position, new Vector3(def2.sizeX, 0f, def2.sizeZ));
            }

            if (site.IsComplete) { Advance(); return; }

            float reach = site.def.sizeZ * 0.6f + 1.8f;
            float dist = CombatMath.FlatDistance(transform.position, site.Position);
            if (dist > reach)
            {
                if (path.Count == 0 || Time.time > repathAt)
                {
                    repathAt = Time.time + 0.7f;
                    SetPathTo(ApproachPoint(site.Position, reach * 0.8f));
                }
                TickMovement(dt);
                return;
            }

            FaceTowards(site.Position, dt);
            if (Time.time >= attackReadyAt)
            {
                attackReadyAt = Time.time + 1.0f;
                lastAttackAnimAt = Time.time;
                site.AddBuildProgress(1.0f / site.def.buildTime * 1.0f);
                if (site.IsComplete) Advance();
            }
        }

        void TickRepair(float dt)
        {
            BuildingController b = current.targetBuilding;
            if (b == null || (!b.IsAlive)) { Advance(); return; }
            if (b.IsComplete && b.CurrentHp >= b.MaxHpValue - 0.5f) { Advance(); return; }

            float reach = b.def.sizeZ * 0.6f + 1.8f;
            float dist = CombatMath.FlatDistance(transform.position, b.Position);
            if (dist > reach)
            {
                if (path.Count == 0 || Time.time > repathAt)
                {
                    repathAt = Time.time + 0.7f;
                    SetPathTo(ApproachPoint(b.Position, reach * 0.8f));
                }
                TickMovement(dt);
                return;
            }

            FaceTowards(b.Position, dt);
            if (Time.time >= attackReadyAt)
            {
                attackReadyAt = Time.time + 1.0f;
                lastAttackAnimAt = Time.time;
                if (!b.IsComplete)
                {
                    b.AddBuildProgress(1.0f / b.def.buildTime);
                }
                else
                {
                    int goldCost = Mathf.Max(1, Mathf.RoundToInt(b.def.goldCost * 0.02f));
                    int lumberCost = Mathf.Max(1, Mathf.RoundToInt(b.def.lumberCost * 0.02f));
                    if (owner.CanAfford(goldCost, lumberCost))
                    {
                        owner.Pay(goldCost, lumberCost);
                        b.Heal(b.MaxHpValue * 0.06f);
                        GameManager.I.Events.OnResourcesChanged();
                    }
                }
            }
        }

        // ===================================================================
        //  Damage / death
        // ===================================================================

        public void TakeDamage(float amount, AttackDamageType type, object source)
        {
            if (!alive || IsInvulnerable) return;

            float dmg = CombatMath.CalculateDamage(amount, type, def.armor, false);
            hp -= dmg;

            FloatingText.Spawn(transform.position + Vector3.up * (def.visualScale.y + 0.4f),
                               Mathf.RoundToInt(dmg).ToString(), new Color(1f, 0.85f, 0.7f));

            if (stealthUntil > 0f) stealthUntil = 0f;

            if (hp <= 0f) Die(source);
        }

        public void Heal(float amount)
        {
            if (!alive) return;
            hp = Mathf.Min(MaxHp, hp + amount);
        }

        void Die(object source)
        {
            if (!alive) return;
            alive = false;
            hp = 0f;
            DetachNode();
            owner.unitsLost++;

            UnitController killer = source as UnitController;
            if (killer != null) killer.owner.unitsKilled++;

            if (def.isHero) GrantXpToNearbyHeroes(owner == GameManager.I.Human ? GameManager.I.Enemy : GameManager.I.Human);

            CombatFx.DeathPuff(transform.position, owner.color);
            if (selectionRing != null) selectionRing.gameObject.SetActive(false);

            if (bodyRenderers != null)
                for (int i = 0; i < bodyRenderers.Length; i++)
                    if (bodyRenderers[i] != null) bodyRenderers[i].enabled = false;

            Collider c = GetComponent<Collider>();
            if (c != null) c.enabled = false;
        }

        void GrantXpToNearbyHeroes(RTSPlayer to)
        {
            if (to == null) return;
            int xpAmount = 60 + (def.isHero ? 200 : 0) + def.goldCost / 6;
            for (int i = 0; i < to.units.Count; i++)
            {
                UnitController h = to.units[i];
                if (!h.def.isHero || !h.IsAlive) continue;
                if (CombatMath.FlatDistance(h.Position, transform.position) > 15f) continue;
                h.AddExperience(xpAmount);
            }
        }

        public void AddExperience(int amount)
        {
            if (!def.isHero) return;
            xp += amount;
            while (level < GameData.MaxHeroLevel && xp >= GameData.XpForLevel(level))
            {
                xp -= GameData.XpForLevel(level);
                level++;
                strength += 2.4f;
                agility += def.heroPrimaryAttribute == 1 ? 3.2f : 2.0f;
                intelligence += def.heroPrimaryAttribute == 2 ? 3.2f : 2.0f;
                hp = MaxHp;
                mana = MaxMana;
                FloatingText.Spawn(transform.position + Vector3.up * 3f, "LEVEL " + level,
                                   new Color(1f, 0.9f, 0.3f));
                GameManager.I.Say(def.displayName + " reached level " + level + "!", Color.yellow);
            }
        }

        public void SpendMana(float amount) { mana = Mathf.Max(0f, mana - amount); }

        // ===================================================================
        //  Presentation
        // ===================================================================

        public void SetSelected(bool selected)
        {
            if (selectionRing != null) selectionRing.gameObject.SetActive(selected);
        }

        Material[] ownedMaterials;

        void SetBodyAlpha(float a)
        {
            if (bodyRenderers == null) return;

            // Instantiate per-unit materials only the first time we need them so
            // that units keep sharing cached materials while fully opaque.
            if (ownedMaterials == null)
            {
                ownedMaterials = new Material[bodyRenderers.Length];
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] == null) continue;
                    Material shared = bodyRenderers[i].sharedMaterial;
                    if (shared == null) continue;
                    ownedMaterials[i] = new Material(shared);
                    bodyRenderers[i].sharedMaterial = ownedMaterials[i];
                }
            }

            for (int i = 0; i < ownedMaterials.Length; i++)
            {
                Material m = ownedMaterials[i];
                if (m == null) continue;
                Color c = m.color;
                if (Mathf.Abs(c.a - a) < 0.01f) continue;
                m.color = new Color(c.r, c.g, c.b, a);
            }
        }

        public void DestroyVisual()
        {
            DetachNode();
            Destroy(gameObject);
        }
    }
}
