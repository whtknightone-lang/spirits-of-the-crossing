// SpiritsCrossing — FireWorldSpawner.cs
// Manages the fire world environment on DarkContrast planet.
//
// FEATURES
//   Lava Flows      — river-like features that amplify red and orange vibrational bands.
//                     Near a flow, intensity rises. The fire doesn't burn — it transforms.
//   Ember Fields    — open areas where distortion pressure is ambient. Walking through
//                     tests the player's calm-under-fire. Low distortion → panther appears.
//   Forge Caverns   — inner chambers where the fire is controlled. Discovery through
//                     spin stability and willForce. Wind organs of flame inside.
//   Fire Trials     — threshold gates that require sustained intensity to pass.
//                     Completing a trial activates deep myth triggers.
//   Obsidian Towers — ancient volcanic structures. Outer/inner discovery like forest temples.
//                     Griffin nests on the upper ledges. Lion guards the inner sanctum.
//
// FIRE CREATURE COMPANIONS (managed by WorldAnimalSpawnSystem)
//   jaguar         — Fire / Tier 1 — always in zone, precise and fast
//   panther        — Fire / Tier 1 — appears when distortion is low (calm fire)
//   mountain_lion  — Fire / Tier 2 — appears with willForce
//   lion           — Fire / Tier 2 — appears with higher willForce, regal
//   griffin        — Fire / Tier 2 — appears at controlled flame pressure + will
//   fire_drake     — Fire / Tier 3 — appears at phoenixRise — the apex fire spirit

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Companions;

namespace SpiritsCrossing.FireWorld
{
    // -------------------------------------------------------------------------
    // Data types (matches fire_world_data.json when generated)
    // -------------------------------------------------------------------------
    [Serializable] public class LavaFlowRecord
    {
        public string flowId;
        public string flowName;
        public float  adjacencyRadius = 8f;
        public float  redAmplification   = 0.04f;
        public float  orangeAmplification = 0.03f;
        public float  distortionPressure = 0.02f;
        public string mythTrigger = "fire";
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class EmberFieldRecord
    {
        public string fieldId;
        public string fieldName;
        public float  ambientDistortion = 0.15f;
        public float  discoveryThreshold = 0.40f;
        public string mythTrigger = "fire";
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class ForgeCavernRecord
    {
        public string cavernId;
        public string cavernName;
        public float  outerDiscoveryThreshold = 0.45f;
        public float  innerDiscoveryThreshold = 0.65f;
        public string outerMythTrigger = "fire";
        public string innerMythTrigger = "fire";
        public bool   hasFlameOrgan;
        public bool   griffinNestPresent;
        public bool   lionGuardsInner;
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class FireTrialRecord
    {
        public string trialId;
        public string trialName;
        public float  intensityThreshold = 0.55f;
        public float  sustainDurationSeconds = 15f;
        public string completionMythTrigger = "fire";
        public string rewardMythTrigger = "elder";
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class FireWorldZone
    {
        public string zoneId;
        public string zoneName;
        public string element = "Fire";
        public string volcanicTier; // "caldera", "slopes", "ashlands", "obsidian_plateau"
        public List<LavaFlowRecord>    lavaFlows   = new List<LavaFlowRecord>();
        public List<EmberFieldRecord>  emberFields = new List<EmberFieldRecord>();
        public List<ForgeCavernRecord> forgeCaverns = new List<ForgeCavernRecord>();
        public List<FireTrialRecord>   fireTrials  = new List<FireTrialRecord>();
    }

    [Serializable] public class FireWorldData
    {
        public List<FireWorldZone> zones = new List<FireWorldZone>();
        public FireWorldZone GetZone(string id) { foreach (var z in zones) if (z.zoneId == id) return z; return null; }
    }

    // =========================================================================
    public class FireWorldSpawner : MonoBehaviour
    {
        public static FireWorldSpawner Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        [Header("Data")]
        public string fireDataFile = "fire_world_data.json";

        [Header("Timing")]
        public float scanInterval     = 1.0f;
        public float lavaTickInterval = 1.0f;

        [Header("Debug")]
        public bool logDiscoveries  = true;
        public bool logLavaTicks    = false;

        // Events
        public event Action<EmberFieldRecord>  OnEmberFieldDiscovered;
        public event Action<ForgeCavernRecord> OnForgeCavernOuterDiscovered;
        public event Action<ForgeCavernRecord> OnForgeCavernInnerReached;
        public event Action<FireTrialRecord>   OnFireTrialCompleted;

        // State
        public bool IsLoaded { get; private set; }
        public FireWorldData Data { get; private set; }
        public string ActiveZoneId { get; private set; }

        private readonly HashSet<string> _discoveredEmbers       = new();
        private readonly HashSet<string> _discoveredCavernOuters = new();
        private readonly HashSet<string> _discoveredCavernInners = new();
        private readonly HashSet<string> _completedTrials        = new();
        private readonly HashSet<string> _touchedFlows           = new();
        private readonly Dictionary<string, float> _trialTimers  = new();

        private float _scanTimer;
        private float _lavaTimer;
        private Transform _playerTransform;

        private IEnumerator Start()
        {
            yield return LoadFireData();
        }

        private void Update()
        {
            if (!IsLoaded || string.IsNullOrEmpty(ActiveZoneId)) return;

            _scanTimer += Time.deltaTime;
            _lavaTimer += Time.deltaTime;

            if (_scanTimer >= scanInterval)     { _scanTimer = 0f; ScanZone(ActiveZoneId); }
            if (_lavaTimer >= lavaTickInterval) { _lavaTimer = 0f; TickLavaFlows(ActiveZoneId); }

            TickFireTrials(ActiveZoneId);
        }

        private IEnumerator LoadFireData()
        {
            yield return null;
            string path = Path.Combine(Application.streamingAssetsPath, fireDataFile);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[FireWorldSpawner] {fireDataFile} not found. " +
                                 "Generate with fire_world_generator.py.");
                yield break;
            }
            try
            {
                Data     = JsonUtility.FromJson<FireWorldData>(File.ReadAllText(path));
                IsLoaded = Data != null;
                Debug.Log($"[FireWorldSpawner] Loaded: {Data?.zones.Count} fire zones.");
            }
            catch (Exception e) { Debug.LogError($"[FireWorldSpawner] Load error: {e.Message}"); }
        }

        public void EnterZone(string zoneId)
        {
            ActiveZoneId     = zoneId;
            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

            // Delegate animal spawning to WorldAnimalSpawnSystem
            WorldAnimalSpawnSystem.Instance?.EnterWorld("DarkContrast");

            var zone = Data?.GetZone(zoneId);
            Debug.Log($"[FireWorldSpawner] Entered: {zoneId} ({zone?.zoneName ?? "?"} tier={zone?.volcanicTier ?? "?"})");
        }

        public void ExitZone()
        {
            WorldAnimalSpawnSystem.Instance?.ExitWorld();
            ActiveZoneId = null;
            _playerTransform = null;
        }

        // -----------------------------------------------------------------
        // Discovery scan
        // -----------------------------------------------------------------
        private void ScanZone(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null) return;
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return;

            float envIntensity = UniverseStateManager.Instance?.Current?.mythState.environmentalIntensity ?? 0f;
            float ts = 1f - envIntensity * 0.15f;

            // Ember fields
            foreach (var ember in zone.emberFields)
            {
                if (_discoveredEmbers.Contains(ember.fieldId)) continue;
                if (playerField.WeightedHarmony(ember.frozenField) >= ember.discoveryThreshold * ts)
                {
                    _discoveredEmbers.Add(ember.fieldId);
                    GetMythState()?.Activate(ember.mythTrigger, "ember_discovery", 0.55f);
                    OnEmberFieldDiscovered?.Invoke(ember);
                    if (logDiscoveries)
                        Debug.Log($"[FireWorldSpawner] Ember field discovered: {ember.fieldName}");
                }
            }

            // Forge caverns — outer / inner
            foreach (var cavern in zone.forgeCaverns)
            {
                float h = playerField.WeightedHarmony(cavern.frozenField);

                if (!_discoveredCavernOuters.Contains(cavern.cavernId) &&
                    h >= cavern.outerDiscoveryThreshold * ts)
                {
                    _discoveredCavernOuters.Add(cavern.cavernId);
                    GetMythState()?.Activate(cavern.outerMythTrigger, "forge_outer", 0.55f);
                    OnForgeCavernOuterDiscovered?.Invoke(cavern);
                    if (logDiscoveries)
                        Debug.Log($"[FireWorldSpawner] Forge cavern approach: {cavern.cavernName}");
                }

                if (_discoveredCavernOuters.Contains(cavern.cavernId) &&
                    !_discoveredCavernInners.Contains(cavern.cavernId) &&
                    h >= cavern.innerDiscoveryThreshold * ts)
                {
                    _discoveredCavernInners.Add(cavern.cavernId);
                    GetMythState()?.Activate(cavern.innerMythTrigger, "forge_inner", 0.75f);
                    OnForgeCavernInnerReached?.Invoke(cavern);
                    if (logDiscoveries)
                        Debug.Log($"[FireWorldSpawner] Forge cavern inner reached: {cavern.cavernName} " +
                                  $"(flameOrgan={cavern.hasFlameOrgan} lion={cavern.lionGuardsInner})");
                }
            }
        }

        // -----------------------------------------------------------------
        // Lava flows — passive band amplification
        // -----------------------------------------------------------------
        private void TickLavaFlows(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null || _playerTransform == null) return;
            var resonance = VibrationalResonanceSystem.Instance;
            if (resonance == null) return;

            foreach (var flow in zone.lavaFlows)
            {
                if (!IsPlayerNearObject($"LavaFlow_{flow.flowId}", flow.adjacencyRadius)) continue;

                resonance.ApplyTransientBoost("red",    flow.redAmplification    * lavaTickInterval);
                resonance.ApplyTransientBoost("orange", flow.orangeAmplification * lavaTickInterval);

                if (!_touchedFlows.Contains(flow.flowId))
                {
                    _touchedFlows.Add(flow.flowId);
                    GetMythState()?.Activate(flow.mythTrigger, "lava_contact", 0.50f);
                    Debug.Log($"[FireWorldSpawner] Lava flow contact: {flow.flowName}");
                }

                if (logLavaTicks)
                    Debug.Log($"[FireWorldSpawner] Lava tick: {flow.flowName}");
            }
        }

        // -----------------------------------------------------------------
        // Fire trials — sustained intensity gates
        // -----------------------------------------------------------------
        private void TickFireTrials(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null || _playerTransform == null) return;
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return;

            foreach (var trial in zone.fireTrials)
            {
                if (_completedTrials.Contains(trial.trialId)) continue;
                if (!IsPlayerNearObject($"Trial_{trial.trialId}", 5f)) continue;

                float h = playerField.WeightedHarmony(trial.frozenField);
                if (h >= trial.intensityThreshold)
                {
                    if (!_trialTimers.ContainsKey(trial.trialId))
                        _trialTimers[trial.trialId] = 0f;
                    _trialTimers[trial.trialId] += Time.deltaTime;

                    if (_trialTimers[trial.trialId] >= trial.sustainDurationSeconds)
                    {
                        _completedTrials.Add(trial.trialId);
                        GetMythState()?.Activate(trial.completionMythTrigger, "fire_trial", 0.75f);
                        GetMythState()?.Activate(trial.rewardMythTrigger, "fire_trial_reward", 0.65f);
                        OnFireTrialCompleted?.Invoke(trial);
                        Debug.Log($"[FireWorldSpawner] Fire trial completed: {trial.trialName}!");
                    }
                }
                else
                {
                    // Reset timer if intensity drops
                    _trialTimers.Remove(trial.trialId);
                }
            }
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------
        private bool IsPlayerNearObject(string objectName, float radius)
        {
            if (_playerTransform == null) return false;
            var obj = GameObject.Find(objectName);
            if (obj == null) return false;
            return Vector3.Distance(_playerTransform.position, obj.transform.position) <= radius;
        }

        private MythState GetMythState() => UniverseStateManager.Instance?.Current?.mythState;

        // Public API
        public bool IsEmberDiscovered(string fieldId)      => _discoveredEmbers.Contains(fieldId);
        public bool IsCavernApproached(string cavernId)    => _discoveredCavernOuters.Contains(cavernId);
        public bool IsCavernInnerReached(string cavernId)  => _discoveredCavernInners.Contains(cavernId);
        public bool IsTrialCompleted(string trialId)       => _completedTrials.Contains(trialId);
        public int  CompletedTrialCount()                  => _completedTrials.Count;
    }
}
