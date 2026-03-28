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
using SpiritsCrossing.VR;

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
