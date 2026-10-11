using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  One building: construction progress, hit points, training queue,
    //  rally point and (for towers) an attack.
    // -----------------------------------------------------------------------
    public class BuildingController : MonoBehaviour, IDamageable
    {
        public BuildingId defId;
        public BuildingDef def;
        public RTSPlayer owner;
        public Vector2Int origin;

        public float buildProgress;   // 0..1
        public bool IsComplete { get { return buildProgress >= 1f; } }

        float hp;
        bool alive = true;

        public readonly List<UnitId> trainQueue = new List<UnitId>();
        float trainTimer;

        public readonly List<UpgradeId> researchQueue = new List<UpgradeId>();
        float researchTimer;

        public Vector3 rallyPoint;
        public bool hasRallyPoint;

        GameObject selectionRing;
        GameObject scaffold;
        public HealthBarElement barElement;

        float attackReadyAt;
        IDamageable attackTarget;

        // ===================================================================
        //  IDamageable
        // ===================================================================

        public bool IsAlive { get { return alive; } }
        public float CurrentHp { get { return hp; } }
        public float MaxHpValue { get { return def.maxHp; } }
        public int Armor { get { return def.armor; } }
        public RTSPlayer Owner { get { return owner; } }
        public Vector3 Position { get { return transform.position; } }
        public float SelectionRadius { get { return Mathf.Max(def.sizeX, def.sizeZ) * 0.5f; } }
        public string DisplayName { get { return def.displayName; } }
        public bool IsHero { get { return false; } }
        public bool IsBuilding { get { return true; } }

        // ===================================================================

        public void Init(RTSPlayer p, BuildingId id, BuildingDef d, Vector2Int org,
                         GameObject ring, GameObject scaffoldGo)
        {
            owner = p;
            defId = id;
            def = d;
            origin = org;
            selectionRing = ring;
            scaffold = scaffoldGo;
            buildProgress = 0f;
            hp = 1f;
            rallyPoint = transform.position + new Vector3(0f, 0f, def.sizeZ * 0.5f + 2f);

            GameManager.I.Map.SetOccupied(origin, def.sizeX, def.sizeZ, 1);
            if (scaffold != null) scaffold.SetActive(true);
        }

        public void FinishInstantly()
        {
            buildProgress = 1f;
            hp = def.maxHp;
            if (scaffold != null) scaffold.SetActive(false);
            owner.RecalcSupplyCap();
        }

        /// <summary>Adds construction progress; <paramref name="amount"/> is a
        ///  fraction of the total build time.</summary>
        public void AddBuildProgress(float amount)
        {
            if (IsComplete || !alive) return;
            buildProgress = Mathf.Clamp01(buildProgress + amount);
            hp = def.maxHp * buildProgress;

            if (buildProgress > 0.02f && scaffold != null) scaffold.SetActive(false);

            if (IsComplete)
            {
                hp = def.maxHp;
                owner.RecalcSupplyCap();
                GameManager.I.Events.OnSupplyChanged();
                CombatFx.BuildComplete(transform.position);
                if (owner.isHuman)
                    GameManager.I.Say(def.displayName + " construction complete.", Color.green);
            }
        }

        // ===================================================================
        //  Training
        // ===================================================================

        public bool CanTrain(UnitId id)
        {
            if (!IsComplete || !alive) return false;
            if (trainQueue.Count >= 5) return false;
            if (def.trains == null) return false;
            for (int i = 0; i < def.trains.Length; i++) if (def.trains[i] == id) return true;
            return false;
        }

        public bool Train(UnitId id)
        {
            if (!CanTrain(id)) return false;

            BuildingId req = GameData.RequirementOf(id);
            if (req != BuildingId.None && owner.FindBuilding(req) == null)
            {
                GameManager.I.Say(GameData.GetUnit(id).displayName + " requires a " +
                                  GameData.GetBuilding(req).displayName + ".", Color.red);
                return false;
            }

            UnitDef ud = GameData.GetUnit(id);
            if (!owner.CanAfford(ud.goldCost, ud.lumberCost))
            {
                GameManager.I.Say("Not enough resources.", Color.red);
                return false;
            }
            if (owner.SupplyLeft < ud.supply)
            {
                GameManager.I.Say("Need more farms!", new Color(1f, 0.6f, 0.2f));
                return false;
            }

            owner.Pay(ud.goldCost, ud.lumberCost);
            GameManager.I.Events.OnResourcesChanged();
            trainQueue.Add(id);
            return true;
        }

        public void CancelTraining(int index)
        {
            if (index < 0 || index >= trainQueue.Count) return;
            UnitDef ud = GameData.GetUnit(trainQueue[index]);
            owner.gold += ud.goldCost;
            owner.lumber += ud.lumberCost;
            GameManager.I.Events.OnResourcesChanged();
            trainQueue.RemoveAt(index);
            if (trainQueue.Count > 0) trainTimer = 0f;
        }

        // ===================================================================
        //  Research (Blacksmith upgrades)
        // ===================================================================

        public int CurrentLevel(UpgradeId u)
        {
            if (u == UpgradeId.Weapons) return owner.weaponsLevel;
            if (u == UpgradeId.Armor) return owner.armorLevel;
            return 0;
        }

        public bool CanResearch(UpgradeId u)
        {
            if (!IsComplete || !alive) return false;
            if (researchQueue.Count >= 1) return false;
            int next = CurrentLevel(u) + 1;
            if (next > GameData.MaxUpgradeLevel) return false;
            return owner.CanAfford(GameData.UpgradeGold(u, next), GameData.UpgradeLumber(u, next));
        }

        public bool StartResearch(UpgradeId u)
        {
            if (!CanResearch(u)) return false;
            int next = CurrentLevel(u) + 1;
            owner.Pay(GameData.UpgradeGold(u, next), GameData.UpgradeLumber(u, next));
            GameManager.I.Events.OnResourcesChanged();
            researchQueue.Add(u);
            return true;
        }

        void TickResearch(float dt)
        {
            if (researchQueue.Count == 0) return;
            int next = CurrentLevel(researchQueue[0]) + 1;
            researchTimer += dt;
            if (researchTimer < GameData.UpgradeTime(next)) return;

            researchTimer = 0f;
            UpgradeId done = researchQueue[0];
            researchQueue.RemoveAt(0);

            if (done == UpgradeId.Weapons) owner.weaponsLevel++;
            else owner.armorLevel++;

            GameManager.I.Say((done == UpgradeId.Weapons ? "Weapons" : "Armor") +
                              " upgraded to level " +
                              (done == UpgradeId.Weapons ? owner.weaponsLevel : owner.armorLevel) + "!",
                              new Color(1f, 0.85f, 0.4f));
            CombatFx.BuildComplete(transform.position);
        }

        void TickTraining(float dt)
        {
            if (trainQueue.Count == 0) return;

            UnitDef ud = GameData.GetUnit(trainQueue[0]);
            trainTimer += dt;
            if (trainTimer < ud.trainTime) return;

            trainTimer = 0f;
            trainQueue.RemoveAt(0);
            SpawnTrained(ud);
        }

        void SpawnTrained(UnitDef ud)
        {
            GameManager gm = GameManager.I;
            Vector3 exit = hasRallyPoint ? rallyPoint : transform.position + new Vector3(0f, 0f, def.sizeZ * 0.5f + 2.5f);

            // Find a free tile near the exit.
            Vector2Int t = GameMap.WorldToTile(exit);
            t = gm.Map.NearestWalkable(t.x, t.y, 5);
            Vector3 p = GameMap.TileToWorld(t.x, t.y);
            p.y = gm.Map.HeightAtWorld(p);

            UnitController u = gm.SpawnUnit(owner, ud.id, p);
            if (hasRallyPoint) u.IssueOrder(Order.Move(rallyPoint));
            CombatFx.SpawnPuff(p);
        }

        public void SetRallyPoint(Vector3 p)
        {
            rallyPoint = p;
            hasRallyPoint = true;
            GroundEffect.SpawnMoveMarker(p, owner.color);
        }

        // ===================================================================
        //  Combat (defensive towers)
        // ===================================================================

        void TickAttack(float dt)
        {
            if (def.attackDamage <= 0f || !IsComplete || !alive) return;

            if (attackTarget == null || !attackTarget.IsAlive ||
                CombatMath.FlatDistance(attackTarget.Position, transform.position) > def.attackRange + attackTarget.SelectionRadius)
            {
                attackTarget = CombatMath.NearestEnemy(GameManager.I, owner, transform.position, def.attackRange);
            }

            if (attackTarget == null) return;
            if (Time.time < attackReadyAt) return;

            attackReadyAt = Time.time + def.attackCooldown;
            ProjectileController.LaunchBuilding(this, attackTarget, def.attackDamage, AttackDamageType.Piercing, 22f);
        }

        // ===================================================================
        //  Frame update
        // ===================================================================

        void Update()
        {
            if (!alive || GameManager.I == null || GameManager.I.Map == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f) * GameManager.I.gameSpeed;

            TickTraining(dt);
            TickResearch(dt);
            TickAttack(dt);
        }

        // ===================================================================
        //  Damage
        // ===================================================================

        public void TakeDamage(float amount, AttackDamageType type, object source)
        {
            if (!alive) return;
            float dmg = CombatMath.CalculateDamage(amount, type, def.armor, true);
            hp -= dmg;

            if (!IsComplete)
            {
                buildProgress = Mathf.Clamp01(hp / def.maxHp);
                if (buildProgress <= 0f) { Destroyed(source); return; }
            }

            FloatingText.Spawn(transform.position + Vector3.up * 3.2f,
                               Mathf.RoundToInt(dmg).ToString(), new Color(1f, 0.7f, 0.5f));

            if (hp <= 0f) Destroyed(source);
        }

        public void Heal(float amount)
        {
            if (!alive) return;
            hp = Mathf.Min(def.maxHp, hp + amount);
        }

        void Destroyed(object source)
        {
            if (!alive) return;
            alive = false;
            hp = 0f;

            UnitController killer = source as UnitController;
            if (killer != null) killer.owner.unitsKilled++;

            GameManager.I.Map.SetOccupied(origin, def.sizeX, def.sizeZ, 0);
            CombatFx.BuildingCollapse(transform.position, owner.color);

            if (selectionRing != null) selectionRing.SetActive(false);
            Renderer[] rs = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rs.Length; i++) if (rs[i] != null) rs[i].enabled = false;
            Collider c = GetComponent<Collider>();
            if (c != null) c.enabled = false;

            GameManager.I.Events.OnBuildingDestroyed(this);
            StartCoroutine(SinkAway());
        }

        System.Collections.IEnumerator SinkAway()
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < 0.7f)
            {
                t += Time.deltaTime;
                float k = t / 0.7f;
                transform.position = new Vector3(start.x, start.y - k * 2.5f, start.z);
                transform.rotation = Quaternion.Euler(k * 12f, 0f, k * 8f);
                yield return null;
            }
            Destroy(gameObject);
        }

        // ===================================================================

        public void SetSelected(bool selected)
        {
            if (selectionRing != null) selectionRing.SetActive(selected);
        }

        public float TrainingProgress
        {
            get
            {
                if (trainQueue.Count == 0) return 0f;
                UnitDef ud = GameData.GetUnit(trainQueue[0]);
                return Mathf.Clamp01(trainTimer / Mathf.Max(0.01f, ud.trainTime));
            }
        }

        public float TrainingResearch
        {
            get
            {
                if (researchQueue.Count == 0) return 0f;
                int next = CurrentLevel(researchQueue[0]) + 1;
                return Mathf.Clamp01(researchTimer / Mathf.Max(0.01f, GameData.UpgradeTime(next)));
            }
        }
    }
}
