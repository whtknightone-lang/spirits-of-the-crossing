// Spirits of the Crossing — RUE Bridge
// Unity-side HTTP client that connects to the Python simulation server.
//
// SETUP:
//   1. Start the Python server:
//      cd source/
//      ../.venv/bin/uvicorn server.app:app --host 127.0.0.1 --port 8765
//
//   2. Attach RUEBridge to a persistent GameObject in your Bootstrap scene.
//   3. Call RUEBridge.Instance.Step(playerResonance) each game tick.
//   4. Subscribe to OnWorldStateReceived to react to the simulation.
//
// INTEGRATION POINTS:
//   - CaveSessionController should call SetPlayerResonance() as the player
//     progresses through the ritual.
//   - CosmosMapDirector should subscribe to OnWorldStateReceived and drive
//     planet visuals from PlanetState.myth_tier.
//   - PortalRevealSystem should check WorldState.GetPlanet(name).IsAwakened
//     to unlock portals.

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SpiritsCrossing.RUE
{
    public class RUEBridge : MonoBehaviour
    {
        // ---------------------------------------------------------------
        // Configuration
        // ---------------------------------------------------------------

        [Header("Server")]
        [Tooltip("Address of the Python RUE server")]
        public string serverUrl = "http://127.0.0.1:8765";

        [Header("Tick Rate")]
        [Tooltip("Seconds between automatic simulation steps (0 = manual only)")]
        public float autoStepInterval = 0.1f;   // 10 steps/sec by default

        [Header("Debug")]
        public bool logMythActivations = true;
        public bool logEveryStep       = false;

        // ---------------------------------------------------------------
        // Events — subscribe to these in your scene systems
        // ---------------------------------------------------------------

        /// <summary>Fires after every successful /step response.</summary>
        public static event Action<WorldState> OnWorldStateReceived;

        /// <summary>Fires when a planet's myth tier changes.</summary>
        public static event Action<string /*planetName*/, string /*tier*/> OnMythTierChanged;

        /// <summary>Fires when the Mayan cycle completes (rebirth).</summary>
        public static event Action<int /*age*/> OnMayanCycleTurn;

        // ---------------------------------------------------------------
        // Singleton
        // ---------------------------------------------------------------

        public static RUEBridge Instance { get; private set; }

        // ---------------------------------------------------------------
        // State
        // ---------------------------------------------------------------

        private WorldState _lastState;
        private PlayerResonance _currentResonance = new PlayerResonance();
        private bool _serverReady = false;
        private float _timeSinceLastStep = 0f;

        // ---------------------------------------------------------------
        // Unity lifecycle
        // ---------------------------------------------------------------

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            StartCoroutine(CheckServerReady());
        }

        void Update()
        {
            if (!_serverReady || autoStepInterval <= 0f) return;

            _timeSinceLastStep += Time.deltaTime;
            if (_timeSinceLastStep >= autoStepInterval)
            {
                _timeSinceLastStep = 0f;
                StartCoroutine(StepCoroutine(_currentResonance));
            }
        }

        // ---------------------------------------------------------------
        // Public API — call these from your game systems
        // ---------------------------------------------------------------

        /// <summary>
        /// Update the player's resonance state.
        /// Call this from CaveSessionController as the ritual progresses.
        /// </summary>
        public void SetPlayerResonance(
            float syncLevel,
            float energy,
            string archetype      = null,
            string planetAffinity = null,
            float breathPhase     = 0f)
        {
            _currentResonance.sync_level     = syncLevel;
            _currentResonance.energy         = energy;
            _currentResonance.archetype      = archetype;
            _currentResonance.planet_affinity = planetAffinity;
            _currentResonance.breath_phase   = breathPhase;
        }

        /// <summary>Manually trigger one simulation step.</summary>
        public void Step() => StartCoroutine(StepCoroutine(_currentResonance));

        /// <summary>Get the last received world state (may be null at startup).</summary>
        public WorldState LastState => _lastState;

        /// <summary>Restart the universe.</summary>
        public void ResetUniverse() => StartCoroutine(PostCoroutine("/reset", "", null));

        // ---------------------------------------------------------------
        // Internal coroutines
        // ---------------------------------------------------------------

        IEnumerator CheckServerReady()
        {
            while (!_serverReady)
            {
                using var req = UnityWebRequest.Get(serverUrl + "/status");
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    _serverReady = true;
                    Debug.Log("[RUEBridge] Server ready: " + req.downloadHandler.text);
                }
                else
                {
                    Debug.Log("[RUEBridge] Waiting for Python server at " + serverUrl + " ...");
                    yield return new WaitForSeconds(2f);
                }
            }
        }

        IEnumerator StepCoroutine(PlayerResonance resonance)
        {
            string json = JsonUtility.ToJson(resonance);
            yield return PostCoroutine("/step", json, OnStepResponse);
        }

        IEnumerator PostCoroutine(string path, string json, Action<string> callback)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            using var req = new UnityWebRequest(serverUrl + path, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                callback?.Invoke(req.downloadHandler.text);
            else
                Debug.LogWarning("[RUEBridge] " + path + " error: " + req.error);
        }

        void OnStepResponse(string json)
        {
            var state = JsonUtility.FromJson<WorldState>(json);
            if (state == null) return;

            // detect myth tier changes
            if (_lastState != null && logMythActivations)
                CheckMythChanges(_lastState, state);

            // detect Mayan cycle turn
            if (_lastState != null &&
                state.mayan_cycle_progress < _lastState.mayan_cycle_progress)
            {
                OnMayanCycleTurn?.Invoke(state.age);
                Debug.Log($"[RUEBridge] *** MAYAN CYCLE TURNS — age {state.age} ***");
            }

            _lastState = state;
            OnWorldStateReceived?.Invoke(state);

            if (logEveryStep)
                Debug.Log($"[RUEBridge] step={state.step} sync={state.global_sync:F3} energy={state.global_energy:F1}");
        }

        void CheckMythChanges(WorldState prev, WorldState next)
        {
            if (prev.planets == null || next.planets == null) return;
            foreach (var planet in next.planets)
            {
                var prevPlanet = prev.GetPlanet(planet.name);
                if (prevPlanet == null) continue;
                if (prevPlanet.myth_tier != planet.myth_tier && planet.myth_tier != null)
                {
                    Debug.Log($"[RUEBridge] {planet.name} myth → {planet.myth_tier}  ({planet.myth_summary})");
                    OnMythTierChanged?.Invoke(planet.name, planet.myth_tier);
                }
            }
        }
    }
}
