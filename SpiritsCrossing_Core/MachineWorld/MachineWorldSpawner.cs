// SpiritsCrossing — MachineWorldSpawner.cs
// Manages the machine world environment on MachineOrder planet.
//
// FEATURES
//   Gear Chambers    — rotating mechanical spaces discovered through patternSync.
//                     Being inside amplifies yellow and indigo bands (precision + insight).
//   Pattern Halls   — long corridors where the player's breath rhythm is measured.
//                     Matching the hall's harmonic pattern unlocks deeper sections.
//   Clockwork Gardens — living mechanical ecosystems. Gears turn in time with breath.
//                       Discovery through systemResonance. Myth triggers on alignment.
//   Harmonic Engines — deep-world machines that resonate with the planet's heartbeat.
//                      Charging them requires sustained harmonyLock.
//   Signal Towers    — tall structures that broadcast pattern data across the world.
//                      Approaching one amplifies socialSync and spinStability.
//
// MACHINE CREATURE COMPANIONS (managed by WorldAnimalSpawnSystem)
//   jaguar         — Fire / Tier 1 — systemResonance creature, walks between gears
//   raven          — Air  / Tier 1 — patternSync creature, counts and watches
//   hawk           — Air  / Tier 2 — harmonyLock creature, precision focus
//   octopus        — Water / Tier 2 — gearMomentum creature, eight-armed builder
//   razorback_boar — Earth / Tier 2 — gearMomentum creature, the rhythm-keeper

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Companions;

namespace SpiritsCrossing.MachineWorld
{
    // -------------------------------------------------------------------------
    // Data types
    // -------------------------------------------------------------------------
    [Serializable] public class GearChamberRecord
    {
        public string chamberId;
        public string chamberName;
        public float  discoveryThreshold = 0.35f;
        public float  yellowAmplification = 0.03f;
        public float  indigoAmplification = 0.025f;
        public float  adjacencyRadius = 6f;
        public string mythTrigger = "pattern";
        public string gearType; // "planetary", "escapement", "differential", "harmonic"
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class PatternHallRecord
    {
        public string hallId;
        public string hallName;
        public float  outerDiscoveryThreshold = 0.40f;
        public float  innerDiscoveryThreshold = 0.60f;
        public float  breathPatternFrequency = 0.25f; // Hz — the hall's natural rhythm
        public string outerMythTrigger = "pattern";
        public string innerMythTrigger = "pattern";
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class ClockworkGardenRecord
    {
        public string gardenId;
        public string gardenName;
        public float  discoveryThreshold = 0.35f;
        public string gardenType; // "spring", "pendulum", "orrery", "water_clock"
        public bool   hasLivingMechanism;
        public string mythTrigger = "harmony";
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class HarmonicEngineRecord
    {
        public string engineId;
        public string engineName;
        public float  activationThreshold = 0.55f;
        public float  chargeTimeSeconds = 20f;
        public float  currentCharge;
        public string activationMythTrigger = "pattern";
        public string rewardMythTrigger = "source";
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class SignalTowerRecord
    {
        public string towerId;
        public string towerName;
        public float  adjacencyRadius = 10f;
        public float  socialSyncAmplification = 0.03f;
        public float  spinAmplification = 0.025f;
        public string mythTrigger = "signal";
        public VibrationalField frozenField = new VibrationalField();
    }

    [Serializable] public class MachineWorldZone
    {
        public string zoneId;
        public string zoneName;
        public string element = "Earth";
        public string machineTier; // "surface_works", "mid_mechanism", "deep_engine", "core_orrery"
        public List<GearChamberRecord>     gearChambers     = new List<GearChamberRecord>();
        public List<PatternHallRecord>     patternHalls     = new List<PatternHallRecord>();
        public List<ClockworkGardenRecord> clockworkGardens = new List<ClockworkGardenRecord>();
        public List<HarmonicEngineRecord>  harmonicEngines  = new List<HarmonicEngineRecord>();
        public List<SignalTowerRecord>     signalTowers     = new List<SignalTowerRecord>();
    }

    [Serializable] public class MachineWorldData
    {
        public List<MachineWorldZone> zones = new List<MachineWorldZone>();
        public MachineWorldZone GetZone(string id) { foreach (var z in zones) if (z.zoneId == id) return z; return null; }
    }

    // =========================================================================
    public class MachineWorldSpawner : MonoBehaviour
    {
        public static MachineWorldSpawner Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        [Header("Data")]
        public string machineDataFile = "machine_world_data.json";

        [Header("Timing")]
        public float scanInterval  = 1.0f;
        public float fieldTickInterval = 1.0f;

        [Header("Debug")]
        public bool logDiscoveries = true;
        public bool logFieldTicks  = false;

        // Events
        public event Action<GearChamberRecord>     OnGearChamberDiscovered;
        public event Action<PatternHallRecord>     OnPatternHallOuterDiscovered;
        public event Action<PatternHallRecord>     OnPatternHallInnerReached;
        public event Action<ClockworkGardenRecord> OnClockworkGardenDiscovered;
        public event Action<HarmonicEngineRecord>  OnHarmonicEngineActivated;
        public event Action<SignalTowerRecord>     OnSignalTowerContacted;

        // State
        public bool IsLoaded { get; private set; }
        public MachineWorldData Data { get; private set; }
        public string ActiveZoneId { get; private set; }

        private readonly HashSet<string> _discoveredGears     = new();
        private readonly HashSet<string> _discoveredHallOuters = new();
        private readonly HashSet<string> _discoveredHallInners = new();
        private readonly HashSet<string> _discoveredGardens   = new();
        private readonly HashSet<string> _activatedEngines    = new();
        private readonly HashSet<string> _contactedTowers     = new();
        private readonly Dictionary<string, float> _engineTimers = new();

        private float _scanTimer;
        private float _fieldTimer;
        private Transform _playerTransform;

        private IEnumerator Start() { yield return LoadMachineData(); }

        private void Update()
        {
            if (!IsLoaded || string.IsNullOrEmpty(ActiveZoneId)) return;

            _scanTimer  += Time.deltaTime;
            _fieldTimer += Time.deltaTime;

            if (_scanTimer >= scanInterval)      { _scanTimer = 0f; ScanZone(ActiveZoneId); }
            if (_fieldTimer >= fieldTickInterval) { _fieldTimer = 0f; TickFields(ActiveZoneId); }

            TickEngines(ActiveZoneId);
        }

        private IEnumerator LoadMachineData()
        {
            yield return null;
            string path = Path.Combine(Application.streamingAssetsPath, machineDataFile);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[MachineWorldSpawner] {machineDataFile} not found.");
                yield break;
            }
            try
            {
                Data     = JsonUtility.FromJson<MachineWorldData>(File.ReadAllText(path));
                IsLoaded = Data != null;
                Debug.Log($"[MachineWorldSpawner] Loaded: {Data?.zones.Count} machine zones.");
            }
            catch (Exception e) { Debug.LogError($"[MachineWorldSpawner] Load error: {e.Message}"); }
        }

        public void EnterZone(string zoneId)
        {
            ActiveZoneId     = zoneId;
            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            WorldAnimalSpawnSystem.Instance?.EnterWorld("MachineOrder");
            var zone = Data?.GetZone(zoneId);
            Debug.Log($"[MachineWorldSpawner] Entered: {zoneId} ({zone?.zoneName ?? "?"} tier={zone?.machineTier ?? "?"})");
        }

        public void ExitZone()
        {
            WorldAnimalSpawnSystem.Instance?.ExitWorld();
            ActiveZoneId = null;
            _playerTransform = null;
        }

        private void ScanZone(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null) return;
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return;

            float ts = 1f - (UniverseStateManager.Instance?.Current?.mythState.environmentalIntensity ?? 0f) * 0.15f;

            // Gear chambers
            foreach (var gear in zone.gearChambers)
            {
                if (_discoveredGears.Contains(gear.chamberId)) continue;
                if (playerField.WeightedHarmony(gear.frozenField) >= gear.discoveryThreshold * ts)
                {
                    _discoveredGears.Add(gear.chamberId);
                    GetMythState()?.Activate(gear.mythTrigger, "gear_discovery", 0.50f);
                    OnGearChamberDiscovered?.Invoke(gear);
                    if (logDiscoveries) Debug.Log($"[MachineWorldSpawner] Gear chamber: {gear.chamberName} ({gear.gearType})");
                }
            }

            // Pattern halls — outer / inner
            foreach (var hall in zone.patternHalls)
            {
                float h = playerField.WeightedHarmony(hall.frozenField);
                if (!_discoveredHallOuters.Contains(hall.hallId) && h >= hall.outerDiscoveryThreshold * ts)
                {
                    _discoveredHallOuters.Add(hall.hallId);
                    GetMythState()?.Activate(hall.outerMythTrigger, "hall_outer", 0.50f);
                    OnPatternHallOuterDiscovered?.Invoke(hall);
                    if (logDiscoveries) Debug.Log($"[MachineWorldSpawner] Pattern hall approach: {hall.hallName}");
                }
                if (_discoveredHallOuters.Contains(hall.hallId) && !_discoveredHallInners.Contains(hall.hallId) &&
                    h >= hall.innerDiscoveryThreshold * ts)
                {
                    _discoveredHallInners.Add(hall.hallId);
                    GetMythState()?.Activate(hall.innerMythTrigger, "hall_inner", 0.70f);
                    OnPatternHallInnerReached?.Invoke(hall);
                    if (logDiscoveries) Debug.Log($"[MachineWorldSpawner] Pattern hall inner: {hall.hallName}");
                }
            }

            // Clockwork gardens
            foreach (var garden in zone.clockworkGardens)
            {
                if (_discoveredGardens.Contains(garden.gardenId)) continue;
                if (playerField.WeightedHarmony(garden.frozenField) >= garden.discoveryThreshold * ts)
                {
                    _discoveredGardens.Add(garden.gardenId);
                    GetMythState()?.Activate(garden.mythTrigger, "garden_discovery", 0.55f);
                    OnClockworkGardenDiscovered?.Invoke(garden);
                    if (logDiscoveries) Debug.Log($"[MachineWorldSpawner] Clockwork garden: {garden.gardenName} ({garden.gardenType})");
                }
            }
        }

        private void TickFields(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null || _playerTransform == null) return;
            var resonance = VibrationalResonanceSystem.Instance;
            if (resonance == null) return;

            // Gear chamber amplification
            foreach (var gear in zone.gearChambers)
            {
                if (!_discoveredGears.Contains(gear.chamberId)) continue;
                if (!IsPlayerNearObject($"Gear_{gear.chamberId}", gear.adjacencyRadius)) continue;
                resonance.ApplyTransientBoost("yellow", gear.yellowAmplification * fieldTickInterval);
                resonance.ApplyTransientBoost("indigo", gear.indigoAmplification * fieldTickInterval);
            }

            // Signal towers
            foreach (var tower in zone.signalTowers)
            {
                if (!IsPlayerNearObject($"Tower_{tower.towerId}", tower.adjacencyRadius)) continue;
                resonance.ApplyTransientBoost("blue",  tower.socialSyncAmplification * fieldTickInterval);
                resonance.ApplyTransientBoost("yellow", tower.spinAmplification * fieldTickInterval);
                if (!_contactedTowers.Contains(tower.towerId))
                {
                    _contactedTowers.Add(tower.towerId);
                    GetMythState()?.Activate(tower.mythTrigger, "tower_contact", 0.50f);
                    OnSignalTowerContacted?.Invoke(tower);
                    Debug.Log($"[MachineWorldSpawner] Signal tower contact: {tower.towerName}");
                }
            }
        }

        private void TickEngines(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null || _playerTransform == null) return;
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return;

            foreach (var engine in zone.harmonicEngines)
            {
                if (_activatedEngines.Contains(engine.engineId)) continue;
                if (!IsPlayerNearObject($"Engine_{engine.engineId}", 6f)) continue;

                float h = playerField.WeightedHarmony(engine.frozenField);
                if (h >= engine.activationThreshold)
                {
                    if (!_engineTimers.ContainsKey(engine.engineId))
                        _engineTimers[engine.engineId] = 0f;
                    _engineTimers[engine.engineId] += Time.deltaTime;
                    engine.currentCharge = Mathf.Clamp01(_engineTimers[engine.engineId] / engine.chargeTimeSeconds);

                    if (engine.currentCharge >= 1f)
                    {
                        _activatedEngines.Add(engine.engineId);
                        GetMythState()?.Activate(engine.activationMythTrigger, "engine_activated", 0.70f);
                        GetMythState()?.Activate(engine.rewardMythTrigger, "engine_reward", 0.60f);
                        OnHarmonicEngineActivated?.Invoke(engine);
                        Debug.Log($"[MachineWorldSpawner] Harmonic engine activated: {engine.engineName}!");
                    }
                }
                else { _engineTimers.Remove(engine.engineId); }
            }
        }

        private bool IsPlayerNearObject(string objectName, float radius)
        {
            if (_playerTransform == null) return false;
            var obj = GameObject.Find(objectName);
            if (obj == null) return false;
            return Vector3.Distance(_playerTransform.position, obj.transform.position) <= radius;
        }

        private MythState GetMythState() => UniverseStateManager.Instance?.Current?.mythState;

        // Public API
        public bool IsGearDiscovered(string chamberId) => _discoveredGears.Contains(chamberId);
        public bool IsHallApproached(string hallId)    => _discoveredHallOuters.Contains(hallId);
        public bool IsHallInnerReached(string hallId)  => _discoveredHallInners.Contains(hallId);
        public bool IsGardenDiscovered(string gardenId) => _discoveredGardens.Contains(gardenId);
        public bool IsEngineActivated(string engineId) => _activatedEngines.Contains(engineId);
        public int  DiscoveryCount() => _discoveredGears.Count + _discoveredHallOuters.Count +
                                        _discoveredGardens.Count + _activatedEngines.Count;
    }
}
