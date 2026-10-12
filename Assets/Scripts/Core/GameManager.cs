using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Tiny event hub so UI code never polls gameplay code.
    // -----------------------------------------------------------------------
    public class GameEvents
    {
        // Initialised with no-op delegates: firing an event before any system has
        // subscribed (e.g. supply events while the starting base spawns, before
        // the HUD exists) must never throw a NullReferenceException.
        public Action OnSelectionChanged = delegate { };
        public Action OnResourcesChanged = delegate { };
        public Action OnSupplyChanged = delegate { };
        public Action<UnitController> OnUnitDied = delegate { };
        public Action<BuildingController> OnBuildingDestroyed = delegate { };
        public Action OnGameOver = delegate { };
        public Action<string, Color> OnMessage = delegate { };
    }

    // -----------------------------------------------------------------------
    //  Owns the simulation: map, players, systems and the frame loop.
    // -----------------------------------------------------------------------
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        [Header("Match setup")]
        public int mapSeed = 20020116;
        public bool spawnAiOpponent = true;
        public int startingWorkers = 5;
        public bool startingHeroes = false;
        public bool fogEnabled = true;

        [Header("Simulation")]
        public float gameSpeed = 1f;

        public GameEvents Events { get; private set; }
        public GameMap Map { get; private set; }
        public RTSPlayer Human { get; private set; }
        public RTSPlayer Enemy { get; private set; }
        public List<RTSPlayer> Players { get; private set; }

        public SelectionManager Selection { get; private set; }
        public OrderSystem Orders { get; private set; }
        public FogOfWar Fog { get; private set; }
        public RTSCameraController CameraRig { get; private set; }
        public HUDController HUD { get; private set; }
        public MinimapController Minimap { get; private set; }
        public PlacementController Placement { get; private set; }
        public EnemyAI AI { get; private set; }
        public AbilitySystem Abilities { get; private set; }

        public bool GameOver { get; private set; }
        public float MatchTime { get; private set; }

        /// <summary>Global registry of every living unit (both players).</summary>
        public readonly List<UnitController> AllUnits = new List<UnitController>();
        public readonly List<BuildingController> AllBuildings = new List<BuildingController>();

        readonly List<UnitController> deadQueue = new List<UnitController>();

        // ===================================================================

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Events = new GameEvents();
            Application.targetFrameRate = 120;
        }

        public MainMenuController Menu { get; private set; }
        public bool MatchStarted { get; private set; }

        /// <summary>Phase 1 (called by GameBootstrap): build the map, the camera
        ///  and the main menu over a live orbiting view of the terrain.  No
        ///  players, no simulation yet - that is StartMatch().</summary>
        public void BootstrapWorld()
        {
            MatchTime = 0f;
            GameOver = false;
            MatchStarted = false;
            Players = new List<RTSPlayer>();

            Map = new GameMap(mapSeed);

            WorldFactory.BuildTerrain(Map);
            WorldFactory.BuildWater(Map);
            WorldFactory.BuildSky();
            Sfx.Init();
            WorldFactory.BuildResourceNodes(Map);

            CameraRig = RTSGameUtility.Add<RTSCameraController>(gameObject);
            CameraRig.SetFocus(Vector3.zero);
            CameraRig.menuOrbit = true;

            RTSGameUtility.Add<TouchGestures>(gameObject);

            Menu = RTSGameUtility.Add<MainMenuController>(gameObject);
            Menu.Init();
        }

        /// <summary>Phase 2 (called by the main menu): create players, bases,
        ///  systems and the interface, then run the simulation.</summary>
        public void StartMatch()
        {
            if (MatchStarted || Map == null) return;
            MatchStarted = true;

            Human = new RTSPlayer(0, "You", FactionId.Alliance, true);
            Players.Add(Human);

            if (spawnAiOpponent)
            {
                Enemy = new RTSPlayer(1, "Grommash", FactionId.Horde, false);
                Players.Add(Enemy);
            }

            Orders = new OrderSystem();
            Selection = new SelectionManager();
            Fog = new FogOfWar();
            Abilities = new AbilitySystem();

            HUD = RTSGameUtility.Add<HUDController>(gameObject);
            Minimap = RTSGameUtility.Add<MinimapController>(gameObject);
            Placement = RTSGameUtility.Add<PlacementController>(gameObject);
            if (Enemy != null) AI = RTSGameUtility.Add<EnemyAI>(gameObject);

            SpawnStartingBase(Human, new Vector2Int(16, 16), startingWorkers);
            if (Enemy != null) SpawnStartingBase(Enemy, new Vector2Int(GameMap.Size - 17, GameMap.Size - 17), startingWorkers);

            Fog.Init();
            Selection.Init();
            HUD.Init();
            Minimap.Init();
            Placement.Init();
            if (AI != null) AI.Init();

            if (CameraRig != null)
            {
                CameraRig.menuOrbit = false;
                if (Human.mainHall != null) CameraRig.SetFocus(Human.mainHall.Position);
            }

            if (Application.isMobilePlatform)
            {
                Application.targetFrameRate = 60;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            StartCoroutine(WinCheckLoop());
        }

        void SpawnStartingBase(RTSPlayer p, Vector2Int center, int workers)
        {
            BuildingId hallId = GameData.TownHallOf(p.faction);
            BuildingDef hallDef = GameData.GetBuilding(hallId);
            Vector2Int origin = new Vector2Int(center.x - hallDef.sizeX / 2, center.y - hallDef.sizeZ / 2);

            BuildingController hall = SpawnBuilding(p, hallId, origin, true);
            p.mainHall = hall;

            // Keep the starting yard clear of trees so the base reads cleanly.
            Map.ClearNodesNear(hall.Position, 8f);

            UnitId workerId = GameData.WorkerOf(p.faction);
            for (int i = 0; i < workers; i++)
            {
                float angle = (i / (float)workers) * Mathf.PI * 2f;
                Vector3 pos = hall.Position + new Vector3(Mathf.Cos(angle) * 3.5f, 0f, Mathf.Sin(angle) * 3.5f);
                pos.y = Map.HeightAtWorld(pos);
                SpawnUnit(p, workerId, pos);
            }

            // Every player starts with one hero so the match has a protagonist.
            if (startingHeroes)
            {
                UnitId hero = p.faction == FactionId.Alliance ? UnitId.Paladin : UnitId.Blademaster;
                Vector3 hp = hall.Position + new Vector3(0f, 0f, 5.5f);
                Vector2Int ht = GameMap.WorldToTile(hp);
                ht = Map.NearestWalkable(ht.x, ht.y, 5);
                hp = GameMap.TileToWorld(ht.x, ht.y);
                hp.y = Map.HeightAtWorld(hp);
                UnitController h = SpawnUnit(p, hero, hp);
                h.level = 1;
            }
        }

        // ===================================================================
        //  Spawning
        // ===================================================================

        public UnitController SpawnUnit(RTSPlayer owner, UnitId id, Vector3 position)
        {
            UnitDef def = GameData.GetUnit(id);
            GameObject go = WorldFactory.CreateUnitVisual(def, owner);
            go.transform.position = position;

            UnitController u = go.AddComponent<UnitController>();
            u.Init(owner, id, def);

            owner.units.Add(u);
            AllUnits.Add(u);
            owner.supplyUsed += def.supply;
            Events.OnSupplyChanged();
            return u;
        }

        public BuildingController SpawnBuilding(RTSPlayer owner, BuildingId id, Vector2Int origin, bool instant)
        {
            BuildingDef def = GameData.GetBuilding(id);
            BuildingController b = WorldFactory.CreateBuilding(owner, id, def, origin);
            AllBuildings.Add(b);
            owner.buildings.Add(b);

            if (instant)
            {
                b.FinishInstantly();
                owner.RecalcSupplyCap();
                Events.OnSupplyChanged();
            }
            return b;
        }

        // ===================================================================
        //  Frame loop
        // ===================================================================

        void Update()
        {
            if (Map == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f) * gameSpeed;
            MatchTime += dt;

            if (Orders == null) return;
            Orders.Update(dt);
            Selection.UpdateInput();
            Placement.UpdateInput();
            Abilities.UpdateCooldowns(dt);

            ProcessDeaths();
        }

        void LateUpdate()
        {
            if (Map == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f) * gameSpeed;
            if (Fog != null) Fog.UpdateFog();
            if (HUD != null) HUD.LateTick();
            if (Minimap != null) Minimap.LateTick();
            if (Selection != null) Selection.LateTick(dt);
        }

        void ProcessDeaths()
        {
            for (int i = AllUnits.Count - 1; i >= 0; i--)
            {
                UnitController u = AllUnits[i];
                if (u == null) { AllUnits.RemoveAt(i); continue; }
                if (!u.IsAlive)
                {
                    deadQueue.Add(u);
                    AllUnits.RemoveAt(i);
                }
            }

            for (int i = 0; i < deadQueue.Count; i++)
            {
                UnitController u = deadQueue[i];
                u.owner.RemoveUnit(u);
                Events.OnUnitDied(u);
                Events.OnSupplyChanged();
                u.DestroyVisual();
            }
            deadQueue.Clear();
        }

        IEnumerator WinCheckLoop()
        {
            WaitForSeconds wait = new WaitForSeconds(1f);
            while (true)
            {
                yield return wait;
                if (GameOver) continue;

                if (Enemy != null && !HasPresence(Human) && HasPresence(Enemy)) EndGame(false);
                else if (!HasPresence(Enemy) && Enemy != null) EndGame(true);
                else if (Enemy == null && !HasPresence(Human)) EndGame(false);
            }
        }

        static bool HasPresence(RTSPlayer p)
        {
            if (p == null) return false;
            if (p.buildings.Count > 0) return true;
            for (int i = 0; i < p.units.Count; i++) if (p.units[i].IsAlive) return true;
            return false;
        }

        void EndGame(bool victory)
        {
            GameOver = true;
            if (HUD != null) HUD.ShowGameOver(victory);
            Events.OnGameOver();
        }

        // ===================================================================
        //  Helpers used all over the codebase
        // ===================================================================

        /// <summary>Every enemy unit/building within <paramref name="radius"/> of a point.</summary>
        public void QueryEnemiesInRange(RTSPlayer forPlayer, Vector3 center, float radius,
                                        List<IDamageable> results)
        {
            results.Clear();
            float sqr = radius * radius;
            for (int i = 0; i < AllUnits.Count; i++)
            {
                UnitController u = AllUnits[i];
                if (u == null || !u.IsAlive) continue;
                if (u.owner == forPlayer) continue;
                if ((u.Position - center).sqrMagnitude <= sqr) results.Add(u);
            }
            for (int i = 0; i < AllBuildings.Count; i++)
            {
                BuildingController b = AllBuildings[i];
                if (b == null || !b.IsAlive) continue;
                if (b.owner == forPlayer) continue;
                if ((b.Position - center).sqrMagnitude <= sqr) results.Add(b);
            }
        }

        /// <summary>Closest visible hostile target to <paramref name="from"/>.</summary>
        public IDamageable FindNearestEnemy(RTSPlayer forPlayer, Vector3 from, float maxRange)
        {
            IDamageable best = null;
            float bestSqr = maxRange * maxRange;
            for (int i = 0; i < AllUnits.Count; i++)
            {
                UnitController u = AllUnits[i];
                if (u == null || !u.IsAlive || u.owner == forPlayer) continue;
                if (Fog != null && !Fog.IsVisible(forPlayer, u.Position)) continue;
                float d = (u.Position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = u; }
            }
            for (int i = 0; i < AllBuildings.Count; i++)
            {
                BuildingController b = AllBuildings[i];
                if (b == null || !b.IsAlive || b.owner == forPlayer) continue;
                if (Fog != null && !Fog.IsVisible(forPlayer, b.Position)) continue;
                float d = (b.Position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = b; }
            }
            return best;
        }

        public void Say(string message, Color color)
        {
            if (Events.OnMessage != null) Events.OnMessage(message, color);
        }

        public void Say(string message) { Say(message, Color.white); }
    }
}
