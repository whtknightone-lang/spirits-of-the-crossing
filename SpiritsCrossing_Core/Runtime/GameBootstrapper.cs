// SpiritsCrossing — GameBootstrapper.cs
// The single entry point that wires all runtime systems together.
// Place on an empty GameObject in the Bootstrap scene (loaded first).
//
// Scene flow:
//   Bootstrap  →  CosmosMap  →  RitualCave  →  RealmScene  →  CosmosMap
//
// This MonoBehaviour:
//   - initialises and loads UniverseStateManager
//   - subscribes CaveSessionController output to UniverseStateManager
//   - subscribes any active IRealmController to UniverseStateManager
//   - pushes loaded planet state back into any active PlanetNodeController

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpiritsCrossing.Runtime;
using SpiritsCrossing.VR;
using SpiritsCrossing.RUE;

namespace SpiritsCrossing
{
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Scene Names")]
        public string cosmosMapScene  = "CosmosMap";
        public string ritualCaveScene = "SandstoneCave";

        [Header("References (auto-found if null)")]
        public UniverseStateManager  universeStateManager;
        public MythInterpreter       mythInterpreter;
        public VRBootstrapInstaller  vrInstaller;
        public RUEBridge             rueBridge;

        // Cave session controller reference — set when Ritual scene loads
        private V243.SandstoneCave.CaveSessionController _caveController;

        // Active realm controller — set when a Realm scene loads
        private IRealmController _realmController;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            // Ensure UniverseStateManager exists
            if (universeStateManager == null)
                universeStateManager = FindObjectOfType<UniverseStateManager>();

            if (universeStateManager == null)
            {
                var go = new GameObject("UniverseStateManager");
                universeStateManager = go.AddComponent<UniverseStateManager>();
            }

            // Ensure MythInterpreter exists
            if (mythInterpreter == null)
                mythInterpreter = FindObjectOfType<MythInterpreter>();

            if (mythInterpreter == null)
                mythInterpreter = gameObject.AddComponent<MythInterpreter>();

            // Ensure VRBootstrapInstaller exists (handles all 4 VR setup steps)
            if (vrInstaller == null)
                vrInstaller = FindObjectOfType<VRBootstrapInstaller>();

            if (vrInstaller == null)
                vrInstaller = gameObject.AddComponent<VRBootstrapInstaller>();

            // Ensure RUEBridge exists — connects the Python simulation to this Unity session.
            // It will wait silently until the server is reachable, so it is safe to include
            // even when running without the Python server.
            if (rueBridge == null)
                rueBridge = FindObjectOfType<RUEBridge>();

            if (rueBridge == null)
                rueBridge = gameObject.AddComponent<RUEBridge>();

            // Wire RUEBridge events into the cosmos and myth systems.
            RUEBridge.OnWorldStateReceived += OnRUEWorldState;
            RUEBridge.OnMythTierChanged    += OnRUEMythTierChanged;
            RUEBridge.OnMayanCycleTurn     += OnRUEMayanCycleTurn;
        }

        private IEnumerator Start()
        {
            universeStateManager.Load();

            Debug.Log($"[GameBootstrapper] Universe loaded. Sessions={universeStateManager.Current.totalSessionCount} " +
                      $"Planets={universeStateManager.Current.planets.Count} " +
                      $"ActiveMyths={universeStateManager.Current.mythState.activeMyths.Count}");

            SceneManager.sceneLoaded += OnSceneLoaded;

            yield return null;

            // Load cosmos map after bootstrap
            if (SceneManager.GetActiveScene().name != cosmosMapScene)
                SceneManager.LoadScene(cosmosMapScene);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RUEBridge.OnWorldStateReceived -= OnRUEWorldState;
            RUEBridge.OnMythTierChanged    -= OnRUEMythTierChanged;
            RUEBridge.OnMayanCycleTurn     -= OnRUEMayanCycleTurn;
        }

        // -------------------------------------------------------------------------
        // Scene wiring — called each time a new scene loads
        // -------------------------------------------------------------------------
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Ritual cave
            var cave = FindObjectOfType<V243.SandstoneCave.CaveSessionController>();
            if (cave != null && cave != _caveController)
            {
                if (_caveController != null)
                    _caveController.OnSessionComplete -= OnCaveSessionComplete;

                _caveController = cave;
                _caveController.OnSessionComplete += OnCaveSessionComplete;

                // Push persistent resonance into the cave's planet affinity scorer
                InjectPersistentResonanceIntoCave(cave);
                Debug.Log("[GameBootstrapper] Wired CaveSessionController.");
            }

            // Realm controller (any MonoBehaviour implementing IRealmController)
            var realm = FindRealmController();
            if (realm != null && realm != _realmController)
            {
                if (_realmController != null)
                    _realmController.OnRealmComplete -= OnRealmComplete;

                _realmController = realm;
                _realmController.OnRealmComplete += OnRealmComplete;

                // Give the realm the current player snapshot and planet history
                var sample      = universeStateManager.Current.persistentResonance;
                var planetState = universeStateManager.GetPlanet(_realmController.PlanetId);
                _realmController.BeginRealm(sample, planetState);

                Debug.Log($"[GameBootstrapper] Wired realm: {_realmController.RealmId}");
            }
        }

        // -------------------------------------------------------------------------
        // Event handlers
        // -------------------------------------------------------------------------
        private void OnCaveSessionComplete(SessionResonanceResult result)
        {
            Debug.Log($"[GameBootstrapper] Cave session complete. Planet={result.currentAffinityPlanet} Portal={result.portalUnlocked}");
            universeStateManager.ApplySessionResult(result);

            // Ritual layer → portal layer handoff:
            // Only allow portal commits once the ritual has produced a valid portal unlock.
            if (result.portalUnlocked)
                PortalRevealSystem.Instance?.EnablePortalCommits();
        }

        private void OnRealmComplete(RealmOutcome outcome)
        {
            Debug.Log($"[GameBootstrapper] Realm complete. Realm={outcome.realmId} Celebration={outcome.celebration:F2}");
            universeStateManager.ApplyRealmOutcome(outcome);

            // Return to cosmos map after realm
            SceneManager.LoadScene(cosmosMapScene);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------
        private IRealmController FindRealmController()
        {
            foreach (var mb in FindObjectsOfType<MonoBehaviour>())
                if (mb is IRealmController rc) return rc;
            return null;
        }

        // -------------------------------------------------------------------------
        // RUE Bridge event handlers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Called every simulation tick with the full world state from Python.
        /// Drives planet visuals, portal availability, and cosmos map state.
        /// </summary>
        private void OnRUEWorldState(RUE.WorldState state)
        {
            // Update each planet in the persistent universe state from live simulation data.
            foreach (var planet in state.planets)
            {
                var ps = universeStateManager?.GetPlanet(planet.name);
                if (ps == null) continue;

                // Myth tier from simulation drives portal unlock eligibility.
                if (planet.IsAwakened)
                    PortalRevealSystem.Instance?.NotifyPlanetAwakened(planet.name, planet.myth_tier);
            }

            // Mayan cycle progress can drive ambient visual intensity on cosmos map.
            // CosmosMapDirector.Instance?.SetCycleProgress(state.mayan_cycle_progress);
        }

        /// <summary>
        /// Fired when a planet's myth tier advances (seedling → explorer → voyager).
        /// Feed this into MythInterpreter so game-side myth modifiers apply.
        /// </summary>
        private void OnRUEMythTierChanged(string planetName, string tier)
        {
            Debug.Log($"[GameBootstrapper] RUE myth tier: {planetName} → {tier}");
            mythInterpreter?.ActivateMythFromRUE(planetName, tier);
        }

        /// <summary>
        /// Fired at every Mayan Long Count cycle turn (every 5200 simulation steps).
        /// Triggers the rebirth event in UniverseStateManager.
        /// </summary>
        private void OnRUEMayanCycleTurn(int age)
        {
            Debug.Log($"[GameBootstrapper] *** MAYAN CYCLE TURNS — universe age {age} ***");
            universeStateManager?.TriggerRebirth();
        }

        private void InjectPersistentResonanceIntoCave(V243.SandstoneCave.CaveSessionController cave)
        {
            // Push persistent resonance back into the affinity interpreter's current sample
            // so returning players start with accumulated history rather than from zero.
            if (cave.planetAffinityInterpreter == null) return;
            var persistent = universeStateManager.Current.persistentResonance;
            cave.planetAffinityInterpreter.currentSample = new V243.SandstoneCave.PlayerResponseSample
            {
                stillnessScore       = persistent.stillnessScore,
                flowScore            = persistent.flowScore,
                spinScore            = persistent.spinScore,
                pairSyncScore        = persistent.pairSyncScore,
                calmScore            = persistent.calmScore,
                joyScore             = persistent.joyScore,
                wonderScore          = persistent.wonderScore,
                distortionScore      = persistent.distortionScore,
                sourceAlignmentScore = persistent.sourceAlignmentScore
            };
        }
    }
}
