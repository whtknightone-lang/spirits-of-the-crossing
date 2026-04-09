// SpiritsCrossing — SourceWorldSpawner.cs
// Manages the source world environment on SourceVeil planet.
//
// The quietest, deepest world. Nothing here demands. Everything here waits.
//
// FEATURES
//   Veil Pools      — still water surfaces where the boundary between self and world
//                     dissolves. Passive violet and indigo amplification.
//                     First contact triggers deep source myth.
//   Communion Chambers — enclosed spaces where social sync and source alignment
//                       blend together. Discovery through communionDepth.
//                       Inner chambers require sustained innerStillness.
//   Threshold Gates — ancient archways that only open when the player holds
//                     multiple resonance dimensions simultaneously. The hardest
//                     discoveries in the game — but the most rewarding.
//   Origin Wells    — the deepest point of each zone. Standing at an origin well
//                     amplifies all 7 vibrational bands simultaneously.
//                     Reaching one triggers elder + source myth at full strength.
//   Mist Passages   — transitional corridors that dampen distortion and amplify
//                     calm. The world's way of helping the player prepare.
//
// SOURCE CREATURE COMPANIONS (managed by WorldAnimalSpawnSystem)
//   groundhog       — Earth / Tier 1 — innerStillness creature, the gentle burrower
//   snake           — Earth / Tier 2 — sourceAlignment creature, ancient keeper
//   octopus         — Water / Tier 2 — veilThinning creature, sees hidden connections
//   whale           — Water / Tier 3 — communionDepth creature, the oldest ocean memory
//   elder_white_stag — Earth / Tier 3 — communionDepth creature, the oldest earth memory

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Companions;

namespace SpiritsCrossing.SourceWorld
{
    // -------------------------------------------------------------------------
    // Data types
    // -------------------------------------------------------------------------
    [Serializable] public class VeilPoolRecord
    {
        public string poolId;
        public string poolName;
        public float  adjacencyRadius = 5f;
        public float  violetAmplification = 0.04f;
        public float  indigoAmplification = 0.035f;
        public float  calmAmplification   = 0.02f;
        public string mythTrigger = "source";
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class CommunionChamberRecord
    {
        public string chamberId;
        public string chamberName;
        public float  outerDiscoveryThreshold = 0.40f;
        public float  innerDiscoveryThreshold = 0.60f;
        public string outerMythTrigger = "source";
        public string innerMythTrigger = "elder";
        public bool   hasReflectionSurface;
        public bool   hasSoundResonance;
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class ThresholdGateRecord
    {
        public string gateId;
        public string gateName;
        // Threshold gates require MULTIPLE dimensions simultaneously
        public float  requiredCalm            = 0.4f;
        public float  requiredSourceAlignment = 0.4f;
        public float  requiredBreathCoherence = 0.3f;
        public float  requiredWonder          = 0.3f;
        public float  sustainDurationSeconds  = 10f;
        public string completionMythTrigger   = "elder";
        public string rewardMythTrigger       = "source";
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class OriginWellRecord
    {
        public string wellId;
        public string wellName;
        public float  discoveryThreshold = 0.55f;
        public float  adjacencyRadius = 4f;
        // Origin wells amplify ALL bands — the only feature in the game that does this
        public float  universalAmplification = 0.025f;
        public string discoveryMythTrigger = "source";
        public string rewardMythTrigger    = "elder";
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class MistPassageRecord
    {
        public string passageId;
        public string passageName;
        public float  adjacencyRadius = 6f;
        public float  distortionDampen = 0.03f;
        public float  calmAmplification = 0.02f;
        public string mythTrigger = "source";
    }

    [Serializable] public class SourceWorldZone
    {
        public string zoneId;
        public string zoneName;
        public string element = "Source";
        public string veilDepth; // "surface_mist", "mid_veil", "deep_source", "origin"
        public List<VeilPoolRecord>         veilPools        = new List<VeilPoolRecord>();
        public List<CommunionChamberRecord> communionChambers = new List<CommunionChamberRecord>();
        public List<ThresholdGateRecord>    thresholdGates   = new List<ThresholdGateRecord>();
        public List<OriginWellRecord>       originWells      = new List<OriginWellRecord>();
        public List<MistPassageRecord>      mistPassages     = new List<MistPassageRecord>();
    }

    [Serializable] public class SourceWorldData
    {
        public List<SourceWorldZone> zones = new List<SourceWorldZone>();
        public SourceWorldZone GetZone(string id) { foreach (var z in zones) if (z.zoneId == id) return z; return null; }
    }

    // =========================================================================
    public class SourceWorldSpawner : MonoBehaviour
    {
        public static SourceWorldSpawner Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        [Header("Data")]
        public string sourceDataFile = "source_world_data.json";

        [Header("Timing")]
        public float scanInterval     = 1.5f;  // slightly slower — the source world is patient
        public float poolTickInterval = 1.0f;

        [Header("Debug")]
        public bool logDiscoveries = true;
        public bool logPoolTicks   = false;

        // Events
        public event Action<VeilPoolRecord>         OnVeilPoolContacted;
        public event Action<CommunionChamberRecord> OnCommunionChamberOuterDiscovered;
        public event Action<CommunionChamberRecord> OnCommunionChamberInnerReached;
        public event Action<ThresholdGateRecord>    OnThresholdGateCompleted;
        public event Action<OriginWellRecord>       OnOriginWellDiscovered;

        // State
        public bool IsLoaded { get; private set; }
        public SourceWorldData Data { get; private set; }
        public string ActiveZoneId { get; private set; }

        private readonly HashSet<string> _touchedPools         = new();
        private readonly HashSet<string> _discoveredChamberOuters = new();
        private readonly HashSet<string> _discoveredChamberInners = new();
        private readonly HashSet<string> _completedGates       = new();
        private readonly HashSet<string> _discoveredWells      = new();
        private readonly Dictionary<string, float> _gateTimers = new();

        private float _scanTimer;
        private float _poolTimer;
        private Transform _playerTransform;

        private IEnumerator Start() { yield return LoadSourceData(); }

        private void Update()
        {
            if (!IsLoaded || string.IsNullOrEmpty(ActiveZoneId)) return;

            _scanTimer += Time.deltaTime;
            _poolTimer += Time.deltaTime;

            if (_scanTimer >= scanInterval)     { _scanTimer = 0f; ScanZone(ActiveZoneId); }
            if (_poolTimer >= poolTickInterval) { _poolTimer = 0f; TickPassiveFields(ActiveZoneId); }

            TickThresholdGates(ActiveZoneId);
        }

        private IEnumerator LoadSourceData()
        {
            yield return null;
            string path = Path.Combine(Application.streamingAssetsPath, sourceDataFile);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SourceWorldSpawner] {sourceDataFile} not found.");
                yield break;
            }
            try
            {
                Data     = JsonUtility.FromJson<SourceWorldData>(File.ReadAllText(path));
                IsLoaded = Data != null;
                Debug.Log($"[SourceWorldSpawner] Loaded: {Data?.zones.Count} source zones.");
            }
            catch (Exception e) { Debug.LogError($"[SourceWorldSpawner] Load error: {e.Message}"); }
        }

        public void EnterZone(string zoneId)
        {
            ActiveZoneId     = zoneId;
            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            WorldAnimalSpawnSystem.Instance?.EnterWorld("SourceVeil");

            // Wake the river — billions of spheres begin oscillating as the player enters
            if (UpsilonRiver.Instance != null)
                UpsilonRiver.Instance.enabled = true;

            var zone = Data?.GetZone(zoneId);
            Debug.Log($"[SourceWorldSpawner] Entered: {zoneId} ({zone?.zoneName ?? "?"} depth={zone?.veilDepth ?? "?"})");
        }

        public void ExitZone()
        {
            WorldAnimalSpawnSystem.Instance?.ExitWorld();
            ActiveZoneId = null;
            _playerTransform = null;

            // Quiet the river when the player leaves the Source
            if (UpsilonRiver.Instance != null)
                UpsilonRiver.Instance.enabled = false;
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

            float ts = 1f - (UniverseStateManager.Instance?.Current?.mythState.environmentalIntensity ?? 0f) * 0.15f;

            // Communion chambers — outer / inner
            foreach (var chamber in zone.communionChambers)
            {
                float h = playerField.WeightedHarmony(chamber.frozenField);
                if (!_discoveredChamberOuters.Contains(chamber.chamberId) &&
                    h >= chamber.outerDiscoveryThreshold * ts)
                {
                    _discoveredChamberOuters.Add(chamber.chamberId);
                    GetMythState()?.Activate(chamber.outerMythTrigger, "communion_outer", 0.55f);
                    OnCommunionChamberOuterDiscovered?.Invoke(chamber);
                    if (logDiscoveries) Debug.Log($"[SourceWorldSpawner] Communion chamber approach: {chamber.chamberName}");
                }
                if (_discoveredChamberOuters.Contains(chamber.chamberId) &&
                    !_discoveredChamberInners.Contains(chamber.chamberId) &&
                    h >= chamber.innerDiscoveryThreshold * ts)
                {
                    _discoveredChamberInners.Add(chamber.chamberId);
                    GetMythState()?.Activate(chamber.innerMythTrigger, "communion_inner", 0.80f);
                    OnCommunionChamberInnerReached?.Invoke(chamber);
                    if (logDiscoveries)
                        Debug.Log($"[SourceWorldSpawner] Communion chamber inner: {chamber.chamberName} " +
                                  $"(reflection={chamber.hasReflectionSurface} sound={chamber.hasSoundResonance})");
                }
            }

            // Origin wells
            foreach (var well in zone.originWells)
            {
                if (_discoveredWells.Contains(well.wellId)) continue;
                if (playerField.WeightedHarmony(well.frozenField) >= well.discoveryThreshold * ts)
                {
                    _discoveredWells.Add(well.wellId);
                    GetMythState()?.Activate(well.discoveryMythTrigger, "origin_well", 0.85f);
                    GetMythState()?.Activate(well.rewardMythTrigger, "origin_well_elder", 0.75f);
                    OnOriginWellDiscovered?.Invoke(well);

                    // Origin wells are the deepest point of the Source — their discovery
                    // surges the river back to life and imprints the well's frozen field
                    UpsilonRiver.Instance?.SurgeRespawn();
                    UpsilonRiver.Instance?.ImposeDragonField(well.frozenField, 0.80f);

                    Debug.Log($"[SourceWorldSpawner] ★ Origin well discovered: {well.wellName}");
                }
            }
        }

        // -----------------------------------------------------------------
        // Passive fields — veil pools, mist passages, origin well amplification
        // -----------------------------------------------------------------
        private void TickPassiveFields(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null || _playerTransform == null) return;
            var resonance = VibrationalResonanceSystem.Instance;
            if (resonance == null) return;

            // Veil pools
            foreach (var pool in zone.veilPools)
            {
                if (!IsPlayerNearObject($"VeilPool_{pool.poolId}", pool.adjacencyRadius)) continue;

                resonance.ApplyTransientBoost("violet", pool.violetAmplification * poolTickInterval);
                resonance.ApplyTransientBoost("indigo", pool.indigoAmplification * poolTickInterval);
                resonance.ApplyTransientBoost("green",  pool.calmAmplification   * poolTickInterval);

                if (!_touchedPools.Contains(pool.poolId))
                {
                    _touchedPools.Add(pool.poolId);
                    GetMythState()?.Activate(pool.mythTrigger, "veil_pool_contact", 0.60f);
                    OnVeilPoolContacted?.Invoke(pool);
                    Debug.Log($"[SourceWorldSpawner] Veil pool contact: {pool.poolName}");
                }
                if (logPoolTicks) Debug.Log($"[SourceWorldSpawner] Pool tick: {pool.poolName}");
            }

            // Mist passages — dampen distortion, amplify calm
            foreach (var mist in zone.mistPassages)
            {
                if (!IsPlayerNearObject($"Mist_{mist.passageId}", mist.adjacencyRadius)) continue;
                // Dampen distortion by boosting green (calm) — the indirect approach
                resonance.ApplyTransientBoost("green", mist.calmAmplification * poolTickInterval);
            }

            // Origin wells — amplify ALL bands (unique to source world)
            foreach (var well in zone.originWells)
            {
                if (!_discoveredWells.Contains(well.wellId)) continue;
                if (!IsPlayerNearObject($"OriginWell_{well.wellId}", well.adjacencyRadius)) continue;

                float amp = well.universalAmplification * poolTickInterval;
                resonance.ApplyTransientBoost("red",    amp);
                resonance.ApplyTransientBoost("orange", amp);
                resonance.ApplyTransientBoost("yellow", amp);
                resonance.ApplyTransientBoost("green",  amp);
                resonance.ApplyTransientBoost("blue",   amp);
                resonance.ApplyTransientBoost("indigo", amp);
                resonance.ApplyTransientBoost("violet", amp);
            }
        }

        // -----------------------------------------------------------------
        // Threshold gates — require sustained multi-dimensional resonance
        // -----------------------------------------------------------------
        private void TickThresholdGates(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null || _playerTransform == null) return;

            var state = SpiritsCrossing.SpiritAI.SpiritBrainOrchestrator
                .FindObjectOfType<SpiritsCrossing.SpiritAI.SpiritBrainOrchestrator>()
                ?.CurrentPlayerState;
            if (state == null) return;

            foreach (var gate in zone.thresholdGates)
            {
                if (_completedGates.Contains(gate.gateId)) continue;
                if (!IsPlayerNearObject($"Gate_{gate.gateId}", 5f)) continue;

                // ALL required dimensions must be met simultaneously
                bool allMet = state.calm            >= gate.requiredCalm &&
                              state.sourceAlignment >= gate.requiredSourceAlignment &&
                              state.breathCoherence >= gate.requiredBreathCoherence &&
                              state.wonder          >= gate.requiredWonder;

                if (allMet)
                {
                    if (!_gateTimers.ContainsKey(gate.gateId))
                        _gateTimers[gate.gateId] = 0f;
                    _gateTimers[gate.gateId] += Time.deltaTime;

                    if (_gateTimers[gate.gateId] >= gate.sustainDurationSeconds)
                    {
                        _completedGates.Add(gate.gateId);
                        GetMythState()?.Activate(gate.completionMythTrigger, "threshold_gate", 0.85f);
                        GetMythState()?.Activate(gate.rewardMythTrigger, "threshold_reward", 0.80f);
                        OnThresholdGateCompleted?.Invoke(gate);
                        Debug.Log($"[SourceWorldSpawner] ★ Threshold gate completed: {gate.gateName}!");
                    }
                }
                else
                {
                    // Reset — must hold ALL dimensions simultaneously
                    _gateTimers.Remove(gate.gateId);
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
        public bool IsPoolTouched(string poolId)          => _touchedPools.Contains(poolId);
        public bool IsChamberApproached(string chamberId)  => _discoveredChamberOuters.Contains(chamberId);
        public bool IsChamberInnerReached(string chamberId) => _discoveredChamberInners.Contains(chamberId);
        public bool IsGateCompleted(string gateId)         => _completedGates.Contains(gateId);
        public bool IsWellDiscovered(string wellId)        => _discoveredWells.Contains(wellId);
        public int  DiscoveryCount() => _touchedPools.Count + _discoveredChamberOuters.Count +
                                        _completedGates.Count + _discoveredWells.Count;

        /// <summary>
        /// Overall zone completion as 0–1: ratio of discovered features to total features.
        /// Called by UpsilonSourceLayer to drive band targets.
        /// </summary>
        public float ZoneCompletion01()
        {
            var zone = Data?.GetZone(ActiveZoneId);
            if (zone == null) return 0f;
            int total = zone.veilPools.Count + zone.communionChambers.Count +
                        zone.thresholdGates.Count + zone.originWells.Count;
            if (total == 0) return 0f;
            int found = _touchedPools.Count + _discoveredChamberOuters.Count +
                        _completedGates.Count + _discoveredWells.Count;
            return Mathf.Clamp01((float)found / total);
        }

        /// <summary>
        /// Average progress across all threshold gates in the current zone (0–1).
        /// Completed gates count as 1.0; in-progress gates use elapsed/sustainDuration.
        /// Called by UpsilonSourceLayer to drive band targets.
        /// </summary>
        public float AverageGateProgress01()
        {
            var zone = Data?.GetZone(ActiveZoneId);
            if (zone == null || zone.thresholdGates.Count == 0) return 0f;
            float total = 0f;
            foreach (var gate in zone.thresholdGates)
            {
                if (_completedGates.Contains(gate.gateId))
                    total += 1f;
                else if (_gateTimers.TryGetValue(gate.gateId, out float elapsed))
                    total += Mathf.Clamp01(elapsed / Mathf.Max(0.001f, gate.sustainDurationSeconds));
            }
            return Mathf.Clamp01(total / zone.thresholdGates.Count);
        }

        /// <summary>
        /// A 0–1 discovery bias weighted toward deep source features (veil pools,
        /// inner chambers, origin wells). Used by UpsilonSourceLayer to drive
        /// the violet/indigo band targets.
        /// </summary>
        public float SourceDiscoveryBias()
        {
            var zone = Data?.GetZone(ActiveZoneId);
            if (zone == null) return 0f;
            int total = zone.veilPools.Count + zone.communionChambers.Count + zone.originWells.Count;
            if (total == 0) return 0f;
            int deep = _touchedPools.Count + _discoveredChamberInners.Count + _discoveredWells.Count;
            return Mathf.Clamp01((float)deep / total);
        }
    }
}
