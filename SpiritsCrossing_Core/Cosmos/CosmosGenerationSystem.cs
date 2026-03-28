// SpiritsCrossing — CosmosGenerationSystem.cs
// The living cosmos engine. Replaces arbitrary growth thresholds with
// the actual RUE/Kuramoto math derived by rue_cosmos_generator.py.
//
// What it does each session and frame:
//   SEED     — Loads cosmos_data.json and pushes generated planet configs
//              into PlanetNodeController instances (spectral signature, growth rate)
//
//   GROW     — After each session, advances each planet's logistic growth
//              G(t) = 1 / (1 + exp(-rate * (t - tHalf)))
//              rate = K(r) * F(r) — physically derived, not hand-tuned
//
//   SYNC     — Each frame computes the live Kuramoto order parameter R_universe
//              from actual planet growth states.
//              K_eff = K_base + sourceConnectionLevel * 0.30
//              The player's Source practice accelerates cosmos synchronization.
//
//   RESPOND  — At natural R_universe threshold crossings, fires cosmos events
//              (birth, rebirth) and activates myths from synchronization.
//
//   GENERATE — When a planet is visited, seeds its NPC population with
//              the simulation-derived archetype and drive weights, not hand-authored values.
//
// Setup: Add to Bootstrap scene. VRBootstrapInstaller creates it automatically.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Resonance.Planets;
using SpiritsCrossing.Memory;

namespace SpiritsCrossing.Cosmos
{
    public class CosmosGenerationSystem : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------
        public static CosmosGenerationSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Config")]
        public string cosmosDataFile  = "cosmos_data.json";

        [Header("Kuramoto Universe Model")]
        [Tooltip("Time step for universe-scale phase evolution.")]
        public float kuramotoDt = 0.01f;

        [Header("Growth")]
        [Range(1f, 50f)]
        [Tooltip("Session count at which planet growth reaches 50%.")]
        public float growthTHalf = 25f;

        [Header("Debug")]
        public bool logCosmosEvents = true;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<float> OnUniverseSynchronizationChanged; // R_universe value
        public event Action<string, float> OnPlanetGrowthUpdated;    // planetId, newGrowth
        public event Action             OnCosmosBirth;
        public event Action             OnCosmosRebirth;

        // -------------------------------------------------------------------------
        // Public state
        // -------------------------------------------------------------------------
        public bool IsLoaded { get; private set; }
        public CosmosGenerationCollection Data { get; private set; }

        /// <summary>Live Kuramoto order parameter (0=chaotic, 1=fully synchronized).</summary>
        public float R_Universe { get; private set; }

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private float[]    _planetPhases;        // live phase array for Kuramoto
        private float[]    _planetGrowth;        // current growth per planet (matches Data.planets order)
        private float      _prevR;
        private bool       _birthFired;
        private bool       _rebirthFired;
        private Dictionary<string, int> _planetIndex = new Dictionary<string, int>();

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return LoadCosmosData();
            InitializePhaseArray();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            if (UniverseStateManager.Instance != null)
                UniverseStateManager.Instance.OnSessionApplied    -= OnSessionApplied;
        }

        // -------------------------------------------------------------------------
        // Loading
        // -------------------------------------------------------------------------
        private IEnumerator LoadCosmosData()
        {
            yield return null;
            string path = Path.Combine(Application.streamingAssetsPath, cosmosDataFile);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CosmosGenerationSystem] {cosmosDataFile} not found. " +
                                 "Run rue_cosmos_generator.py then python setup_streaming_assets.py.");
                yield break;
            }

            try
            {
                Data = JsonUtility.FromJson<CosmosGenerationCollection>(File.ReadAllText(path));
                if (Data == null) { Debug.LogError("[CosmosGenerationSystem] Failed to parse cosmos_data.json."); yield break; }

                // Build index and load growth states from UniverseState
                for (int i = 0; i < Data.planets.Count; i++)
                {
                    var p = Data.planets[i];
                    _planetIndex[p.planetId] = i;
                }

                IsLoaded = true;
                Debug.Log($"[CosmosGenerationSystem] Loaded {Data.planets.Count} planet configs. " +
                          $"Birth={Data.universeSynchronization.birthThreshold:F3} " +
                          $"Rebirth={Data.universeSynchronization.rebirthThreshold:F3}");

                // Seed PlanetNodeController instances in the scene
                SeedPlanetControllers();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CosmosGenerationSystem] Load error: {e.Message}");
            }
        }

        private void SubscribeEvents()
        {
            if (UniverseStateManager.Instance != null)
                UniverseStateManager.Instance.OnSessionApplied += OnSessionApplied;
        }

        // -------------------------------------------------------------------------
        // Phase array initialization
        // -------------------------------------------------------------------------
        private void InitializePhaseArray()
        {
            if (Data == null) return;
            int N = Data.planets.Count;
            _planetPhases = new float[N];
            _planetGrowth  = new float[N];

            for (int i = 0; i < N; i++)
            {
                _planetPhases[i] = Data.planets[i].meanEquilibriumPhase;
                // Load saved growth from UniverseState, or use logistic at session 0
                var planet = UniverseStateManager.Instance?.Current.GetOrCreatePlanet(Data.planets[i].planetId);
                _planetGrowth[i] = planet?.growth ?? Data.planets[i].GrowthAtSession(0, growthTHalf);
            }
        }

        // -------------------------------------------------------------------------
        // Seed scene PlanetNodeControllers with generated data
        // -------------------------------------------------------------------------
        private void SeedPlanetControllers()
        {
            var controllers = FindObjectsOfType<PlanetNodeController>();
            foreach (var ctrl in controllers)
            {
                // Use reflection-free approach: match by planetId field via scene name or tag
                // PlanetNodeController exposes a planetId-like name via its GameObject name
                string id = ctrl.gameObject.name;
                var config = Data.GetPlanet(id) ?? Data.GetPlanet(id.Replace(" ", ""));
                if (config == null) continue;

                // Push generated growth rate into the controller
                // The controller's public fields are: growth, healing, rebirthCharge
                // We set initial growth to the generated logistic value
                int sessions = UniverseStateManager.Instance?.Current.totalSessionCount ?? 0;
                float generatedGrowth = config.GrowthAtSession(sessions, growthTHalf);
                ctrl.GetComponent<PlanetNodeController>()?.RecordEncounter(
                    config.element, generatedGrowth * 0.05f, 0f, generatedGrowth * 0.03f,
                    config.dominantBand, config.npcPopulation.naturalArchetype);

                Debug.Log($"[CosmosGenerationSystem] Seeded {id}: K={config.couplingConstant:F3} " +
                          $"growth={generatedGrowth:F3} archetype={config.npcPopulation.naturalArchetype}");
            }
        }

        // -------------------------------------------------------------------------
        // Session growth integration
        // -------------------------------------------------------------------------
        private void OnSessionApplied(SessionResonanceResult result)
        {
            if (!IsLoaded || Data == null) return;

            int totalSessions = UniverseStateManager.Instance?.Current.totalSessionCount ?? 0;
            string visitedPlanet = result.currentAffinityPlanet;

            // Update all planet growths using logistic curves
            for (int i = 0; i < Data.planets.Count; i++)
            {
                var config = Data.planets[i];
                float newGrowth = config.GrowthAtSession(
                    visitedPlanet == config.planetId ? totalSessions : Mathf.Max(0, totalSessions - 5),
                    growthTHalf);
                _planetGrowth[i] = newGrowth;
                OnPlanetGrowthUpdated?.Invoke(config.planetId, newGrowth);

                // Update UniverseState planet record
                var state = UniverseStateManager.Instance?.Current.GetOrCreatePlanet(config.planetId);
                if (state != null) state.growth = newGrowth;
            }

            // Update universe cycle
            UniverseStateManager.Instance?.Current.RecalculateUniverseCycle();
        }

        // -------------------------------------------------------------------------
        // Frame-by-frame Kuramoto universe synchronization
        // -------------------------------------------------------------------------
        private void Update()
        {
            if (!IsLoaded || _planetPhases == null) return;

            // Effective coupling boosted by player's source connection
            float scl = ResonanceMemorySystem.Instance?.SourceConnectionLevel ?? 0f;
            float K   = Data.universeSynchronization.EffectiveKUniverse(scl);

            // Evolve planet phases under Kuramoto coupling
            int N = _planetPhases.Length;
            float[] newPhases = new float[N];
            for (int i = 0; i < N; i++)
            {
                float coupling = 0f;
                for (int j = 0; j < N; j++)
                    if (i != j) coupling += Mathf.Sin(_planetPhases[j] - _planetPhases[i]);
                // Growth acts as a "weight" — more grown planets contribute more to sync
                newPhases[i] = _planetPhases[i] + kuramotoDt * (K * coupling / N);
            }
            _planetPhases = newPhases;

            // Compute R_universe (Kuramoto order parameter)
            float cosSum = 0f, sinSum = 0f;
            for (int i = 0; i < N; i++)
            {
                cosSum += Mathf.Cos(_planetPhases[i]) * _planetGrowth[i];
                sinSum += Mathf.Sin(_planetPhases[i]) * _planetGrowth[i];
            }
            float weightSum = 0f;
            foreach (float g in _planetGrowth) weightSum += g;
            R_Universe = weightSum > 0f
                ? Mathf.Sqrt(cosSum * cosSum + sinSum * sinSum) / weightSum
                : 0f;

            // Fire threshold events
            var sync = Data.universeSynchronization;
            if (!_birthFired && R_Universe >= sync.birthThreshold)
            {
                _birthFired = true;
                if (logCosmosEvents)
                    Debug.Log($"[CosmosGenerationSystem] COSMOS BIRTH — R={R_Universe:F3}");
                OnCosmosBirth?.Invoke();
                ActivateCosmosMyth("source", R_Universe);
            }
            if (!_rebirthFired && R_Universe >= sync.rebirthThreshold)
            {
                _rebirthFired = true;
                if (logCosmosEvents)
                    Debug.Log($"[CosmosGenerationSystem] COSMOS REBIRTH — R={R_Universe:F3}");
                OnCosmosRebirth?.Invoke();
                ActivateCosmosMyth("elder", R_Universe);
                _birthFired   = false; // allow cycling
                _rebirthFired = false;
            }

            if (Mathf.Abs(R_Universe - _prevR) > 0.005f)
            {
                OnUniverseSynchronizationChanged?.Invoke(R_Universe);
                _prevR = R_Universe;
            }
        }

        private void ActivateCosmosMyth(string key, float strength)
        {
            var myth = UniverseStateManager.Instance?.Current.mythState;
            myth?.Activate(key, "cosmos", strength);
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Get the generated planet config for a planet id.</summary>
        public GeneratedPlanetConfig GetPlanetConfig(string planetId)
            => Data?.GetPlanet(planetId);

        /// <summary>
        /// Get the NPC population drive weights for a planet.
        /// Used by SpiritBrainController.BeginRealm to seed NPC agents.
        /// </summary>
        public NpcPopulationProfile GetNpcPopulation(string planetId)
            => GetPlanetConfig(planetId)?.npcPopulation;

        /// <summary>
        /// Get the equilibrium spectral signature for a planet.
        /// Used by portal scoring, cave atmosphere, and visual systems.
        /// </summary>
        public PlanetSpectralSignature GetPlanetSpectral(string planetId)
            => GetPlanetConfig(planetId)?.equilibriumSpectral;

        /// <summary>
        /// Field strength at a given orbital radius — used by realm systems
        /// to scale difficulty and atmosphere.
        /// </summary>
        public float GetFieldStrength(float orbitalRadius)
            => Data?.sourceField.FieldStrength(orbitalRadius) ?? 1f;

        /// <summary>
        /// Coupling constant K(r) — used by SpiritBrainController to modulate
        /// the oscillator coupling constant in realm scenes.
        /// </summary>
        public float GetCouplingConstant(float orbitalRadius)
            => Data?.sourceField.CouplingConstant(orbitalRadius) ?? 0.2f;

        /// <summary>
        /// Current planet growth for a planet id (from live Kuramoto state).
        /// </summary>
        public float GetLivePlanetGrowth(string planetId)
        {
            if (!_planetIndex.TryGetValue(planetId, out int i)) return 0f;
            return i < _planetGrowth.Length ? _planetGrowth[i] : 0f;
        }
    }
}
