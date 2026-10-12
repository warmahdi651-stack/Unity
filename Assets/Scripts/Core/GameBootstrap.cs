using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  The only component that has to exist in the scene.  It creates the
    //  lighting and then hands over to GameManager.Bootstrap(), which builds
    //  the terrain, players, systems and interface entirely from code.
    //
    //  Scene setup: create an empty GameObject called "GameBootstrap" and
    //  attach this script.  That is all - no prefabs, no lighting, no camera.
    // -----------------------------------------------------------------------
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Match")]
        [Tooltip("Deterministic map layout. Change for a different map.")]
        public int mapSeed = 20020116;

        [Tooltip("Play against the computer opponent.")]
        public bool spawnAiOpponent = true;

        [Tooltip("Starting workers per player.")]
        [Range(1, 12)] public int startingWorkers = 5;

        [Tooltip("Give every player a starting hero.")]
        public bool startingHeroes = false;

        [Tooltip("Log simulation information to the console.")]
        public bool verbose = true;

        GameManager manager;

        void Awake()
        {
            CreateLighting();

            manager = gameObject.AddComponent<GameManager>();
            manager.mapSeed = mapSeed;
            manager.spawnAiOpponent = spawnAiOpponent;
            manager.startingWorkers = startingWorkers;
            manager.startingHeroes = startingHeroes;
            manager.BootstrapWorld();

            SpawnStartingExtras();

            if (verbose)
            {
                Debug.Log("[RTS-Warcraft] Bootstrap complete. Map " + GameMap.Size + "x" + GameMap.Size +
                          ", seed " + mapSeed + ", resources: " + manager.Map.Nodes.Count + " nodes.");
                manager.Say("Welcome, commander. Build your economy and destroy the Horde.",
                            new Color(0.6f, 0.85f, 1f));
                manager.Say("Drag to select, right-click to command, A = attack-move.",
                            new Color(0.8f, 0.8f, 0.8f));
            }
        }

        // ===================================================================

        void CreateLighting()
        {
            if (FindObjectOfType<Light>() != null) return;

            GameObject sun = new GameObject("Directional Light");
            Light l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1.0f, 0.95f, 0.84f);
            l.intensity = 1.2f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(56f, -32f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.40f, 0.44f, 0.52f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.58f, 0.66f, 0.76f);
            RenderSettings.fogStartDistance = 110f;
            RenderSettings.fogEndDistance = 300f;
        }

        /// <summary>Anything that needs the world to exist first.</summary>
        void SpawnStartingExtras()
        {
            // Point the camera at the player's town hall.
            if (manager.Human != null && manager.Human.mainHall != null)
                manager.CameraRig.SetFocus(manager.Human.mainHall.Position);

            // A couple of neutral gold guards so scouting matters (visual only).
            // Left empty on purpose: expand here if you want creeps.
        }
    }
}
