// SpiritsCrossing — OceanWorldSpawner.cs
// Manages the ocean world as a living environment on WaterFlow planet.
//
// FEATURES
//   Coral Gardens    — living structures discovered through harmony. Healthy coral
//                      grants passive resonance boosts. Myth triggers on discovery.
//   Tidal Caves      — hidden chambers with outer and inner thresholds (like temples).
//                      Inner caves may contain air pockets and crystal formations.
//   Deep Trenches    — the hardest discoveries. Hydrothermal vents and abyssal creatures.
//                      Pressure intensity adds ambient environmental stress.
//   Ocean Currents   — linear flow features (like blessed rivers). Being inside a current
//                      passively amplifies blue and indigo vibrational bands.
//                      Whale routes on some currents trigger unique myths.
//   Bioluminescent Zones — areas that glow in response to player presence.
//                          Some respond to movement, some to stillness.
//
// SEA CREATURE COMPANIONS
//   dolphin         — Water / Tier 1 — appears in shallows and midwater, playful
//   sea_turtle      — Water / Tier 2 — appears near coral gardens, patient
//   jellyfish_elder — Water / Tier 3 — appears in deep bioluminescent zones, meditative

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Companions;

namespace SpiritsCrossing.OceanWorld
{
    public class OceanWorldSpawner : MonoBehaviour
    {
        public static OceanWorldSpawner Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Data")]
        public string oceanDataFile = "ocean_world_data.json";

        [Header("Current Resonance")]
        public float currentTickInterval = 1.0f;

        [Header("Discovery")]
        public float scanInterval = 1.0f;

        [Header("Bioluminescence")]
        [Tooltip("How quickly bioluminescent zones respond to player presence.")]
        public float bioResponseSpeed = 0.5f;

        [Header("Debug")]
        public bool logDiscoveries  = true;
        public bool logCurrentTicks = false;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<CoralGardenRecord>        OnCoralDiscovered;
        public event Action<TidalCaveRecord>          OnTidalCaveOuterDiscovered;
        public event Action<TidalCaveRecord>          OnTidalCaveInnerReached;
        public event Action<TidalCaveChamber>         OnCaveChamberDiscovered;
        public event Action<DeepTrenchRecord>         OnTrenchDiscovered;
        public event Action<OceanCurrentRecord>       OnCurrentFirstContact;
        public event Action<BioluminescentZoneRecord> OnBioZoneActivated;

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------
        public bool IsLoaded { get; private set; }
        public OceanWorldData Data { get; private set; }
        public string ActiveZoneId { get; private set; }

        private readonly HashSet<string> _discoveredCorals      = new();
        private readonly HashSet<string> _discoveredCaveOuters  = new();
        private readonly HashSet<string> _discoveredCaveInners  = new();
        private readonly HashSet<string> _discoveredChambers    = new();
        private readonly HashSet<string> _discoveredTrenches    = new();
        private readonly HashSet<string> _touchedCurrents       = new();
        private readonly HashSet<string> _activatedBioZones     = new();

        private float _scanTimer;
        private float _currentTimer;
        private Transform _playerTransform;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return LoadOceanData();
        }

        private void Update()
        {
            if (!IsLoaded || string.IsNullOrEmpty(ActiveZoneId)) return;

            _scanTimer    += Time.deltaTime;
            _currentTimer += Time.deltaTime;

            if (_scanTimer >= scanInterval)
            {
                _scanTimer = 0f;
                ScanZone(ActiveZoneId);
            }

            if (_currentTimer >= currentTickInterval)
            {
                _currentTimer = 0f;
                ApplyCurrentResonance(ActiveZoneId);
            }
        }

        // -------------------------------------------------------------------------
        // Loading
        // -------------------------------------------------------------------------
        private IEnumerator LoadOceanData()
        {
            yield return null;
            string path = Path.Combine(Application.streamingAssetsPath, oceanDataFile);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[OceanWorldSpawner] {oceanDataFile} not found. " +
                                 "Generate with a Python ocean_world_generator and run setup_streaming_assets.py.");
                yield break;
            }
            try
            {
                Data     = JsonUtility.FromJson<OceanWorldData>(File.ReadAllText(path));
                IsLoaded = Data != null;
                Debug.Log($"[OceanWorldSpawner] Loaded: {Data?.zones.Count} ocean zones.");
            }
            catch (Exception e) { Debug.LogError($"[OceanWorldSpawner] Load error: {e.Message}"); }
        }

        // -------------------------------------------------------------------------
        // Zone entry / exit
        // -------------------------------------------------------------------------
        public void EnterZone(string zoneId)
        {
            ActiveZoneId     = zoneId;
            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

            SpawnZoneCreatures(zoneId);

            var zone = Data?.GetZone(zoneId);
            Debug.Log($"[OceanWorldSpawner] Entered zone: {zoneId} " +
                      $"({zone?.zoneName ?? "?"}, depth={zone?.depthTier ?? "?"})");
        }

        public void ExitZone()
        {
            ActiveZoneId     = null;
            _playerTransform = null;
        }

        // -------------------------------------------------------------------------
        // Discovery scan
        // -------------------------------------------------------------------------
        private void ScanZone(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null) return;

            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return;

            // Myth modifier: environmental intensity lowers thresholds
            float envIntensity = UniverseStateManager.Instance?.Current?.mythState.environmentalIntensity ?? 0f;
            float thresholdScale = 1f - envIntensity * 0.15f;

            // Coral gardens
            foreach (var coral in zone.coralGardens)
            {
                if (_discoveredCorals.Contains(coral.gardenId)) continue;
                float harmony = playerField.WeightedHarmony(coral.frozenField);
                if (harmony >= coral.discoveryThreshold * thresholdScale)
                    DiscoverCoral(coral);
            }

            // Tidal caves — outer and inner
            foreach (var cave in zone.tidalCaves)
            {
                float harmony = playerField.WeightedHarmony(cave.frozenField);

                if (!_discoveredCaveOuters.Contains(cave.caveId) &&
                    harmony >= cave.outerDiscoveryThreshold * thresholdScale)
                    DiscoverCaveOuter(cave);

                if (_discoveredCaveOuters.Contains(cave.caveId) &&
                    !_discoveredCaveInners.Contains(cave.caveId) &&
                    harmony >= cave.innerDiscoveryThreshold * thresholdScale)
                    DiscoverCaveInner(cave);

                foreach (var chamber in cave.chambers)
                {
                    if (_discoveredChambers.Contains(chamber.chamberId)) continue;
                    float ch = playerField.WeightedHarmony(chamber.chamberField);
                    if (ch >= chamber.discoveryThreshold * thresholdScale)
                        DiscoverChamber(chamber);
                }
            }

            // Deep trenches
            foreach (var trench in zone.deepTrenches)
            {
                if (_discoveredTrenches.Contains(trench.trenchId)) continue;
                float harmony = playerField.WeightedHarmony(trench.frozenField);
                if (harmony >= trench.discoveryThreshold * thresholdScale)
                    DiscoverTrench(trench);
            }

            // Bioluminescent zones — low threshold, welcoming
            foreach (var bio in zone.bioZones)
            {
                if (_activatedBioZones.Contains(bio.bioZoneId)) continue;
                float harmony = playerField.WeightedHarmony(bio.frozenField);
                if (harmony >= bio.activationThreshold * thresholdScale)
                    ActivateBioZone(bio);
            }
        }

        // -------------------------------------------------------------------------
        // Discovery handlers
        // -------------------------------------------------------------------------
        private void DiscoverCoral(CoralGardenRecord coral)
        {
            _discoveredCorals.Add(coral.gardenId);
            GetMythState()?.Activate(coral.mythTrigger, "coral_discovery", 0.55f);
            OnCoralDiscovered?.Invoke(coral);
            if (logDiscoveries)
                Debug.Log($"[OceanWorldSpawner] Coral discovered: {coral.gardenId} " +
                          $"(type={coral.coralType} health={coral.healthLevel:F2})");
        }

        private void DiscoverCaveOuter(TidalCaveRecord cave)
        {
            _discoveredCaveOuters.Add(cave.caveId);
            GetMythState()?.Activate(cave.outerMythTrigger, "cave_outer", 0.50f);
            OnTidalCaveOuterDiscovered?.Invoke(cave);
            if (logDiscoveries)
                Debug.Log($"[OceanWorldSpawner] Tidal cave entrance found: {cave.caveName}");
        }

        private void DiscoverCaveInner(TidalCaveRecord cave)
        {
            _discoveredCaveInners.Add(cave.caveId);
            GetMythState()?.Activate(cave.innerMythTrigger, "cave_inner", 0.75f);
            OnTidalCaveInnerReached?.Invoke(cave);

            // Sea turtle appears near deep caves
            SpawnOceanCreature("sea_turtle", cave.caveId);

            if (logDiscoveries)
                Debug.Log($"[OceanWorldSpawner] Tidal cave inner reached: {cave.caveName} " +
                          $"(airPocket={cave.hasAirPocket} crystals={cave.hasCrystalFormation})");
        }

        private void DiscoverChamber(TidalCaveChamber chamber)
        {
            _discoveredChambers.Add(chamber.chamberId);
            GetMythState()?.Activate(chamber.mythTrigger, "chamber_discovery", 0.60f);
            OnCaveChamberDiscovered?.Invoke(chamber);
            if (logDiscoveries)
                Debug.Log($"[OceanWorldSpawner] Cave chamber: {chamber.chamberName} echo={chamber.echoIntensity:F2}");
        }

        private void DiscoverTrench(DeepTrenchRecord trench)
        {
            _discoveredTrenches.Add(trench.trenchId);
            GetMythState()?.Activate(trench.mythTrigger, "trench_discovery", 0.80f);

            // Reaching a trench is significant — also triggers ocean and elder myths
            GetMythState()?.Activate("ocean", "trench_discovery", 0.70f);
            if (trench.hasHydrothermalVent)
                GetMythState()?.Activate("source", "hydrothermal", 0.60f);

            // Jellyfish elder appears at depth
            SpawnOceanCreature("jellyfish_elder", trench.trenchId);

            OnTrenchDiscovered?.Invoke(trench);
            if (logDiscoveries)
                Debug.Log($"[OceanWorldSpawner] Deep trench discovered: {trench.trenchName} " +
                          $"(depth={trench.depthMetres}m vent={trench.hasHydrothermalVent})");
        }

        private void ActivateBioZone(BioluminescentZoneRecord bio)
        {
            _activatedBioZones.Add(bio.bioZoneId);
            GetMythState()?.Activate(bio.mythTrigger, "bioluminescence", 0.45f);
            OnBioZoneActivated?.Invoke(bio);
            if (logDiscoveries)
                Debug.Log($"[OceanWorldSpawner] Bioluminescent zone active: {bio.bioZoneName} " +
                          $"(color={bio.glowColor} intensity={bio.glowIntensity:F2})");
        }

        // -------------------------------------------------------------------------
        // Ocean currents — passive resonance amplification (like forest rivers)
        // -------------------------------------------------------------------------
        private void ApplyCurrentResonance(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null || _playerTransform == null) return;

            var resonance = VibrationalResonanceSystem.Instance;
            if (resonance == null) return;

            foreach (var current in zone.currents)
            {
                if (!IsPlayerInCurrent(current)) continue;

                // Passive blue and indigo amplification
                resonance.ApplyTransientBoost("blue",   current.blueAmplification   * currentTickInterval);
                resonance.ApplyTransientBoost("indigo", current.indigoAmplification * currentTickInterval);

                // First contact
                if (!_touchedCurrents.Contains(current.currentId))
                {
                    _touchedCurrents.Add(current.currentId);
                    GetMythState()?.Activate(current.mythTrigger, "current_contact", 0.50f);
                    OnCurrentFirstContact?.Invoke(current);
                    Debug.Log($"[OceanWorldSpawner] Current first contact: {current.currentName}");
                }

                // Whale route
                if (current.hasWhaleRoute && !string.IsNullOrEmpty(current.whaleRouteMythTrigger))
                    GetMythState()?.Activate(current.whaleRouteMythTrigger, "whale_route", 0.65f);

                if (logCurrentTicks)
                    Debug.Log($"[OceanWorldSpawner] Current tick: {current.currentName} " +
                              $"+blue={current.blueAmplification:F3} +indigo={current.indigoAmplification:F3}");
            }
        }

        // -------------------------------------------------------------------------
        // Sea creature spawning
        // -------------------------------------------------------------------------
        private void SpawnZoneCreatures(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null) return;

            foreach (var animalId in zone.preferredCompanionIds)
            {
                if (animalId == "jellyfish_elder") continue; // gated by trench discovery
                SpawnOceanCreature(animalId, zoneId);
            }
        }

        private void SpawnOceanCreature(string animalId, string locationContext)
        {
            var ctrl = CompanionBehaviorController.Instance;
            if (ctrl == null) return;

            ctrl.ActivateCompanion(animalId);
            Debug.Log($"[OceanWorldSpawner] Creature active: {animalId} at {locationContext}");
        }

        // -------------------------------------------------------------------------
        // Proximity helpers
        // -------------------------------------------------------------------------
        private bool IsPlayerInCurrent(OceanCurrentRecord current)
        {
            if (_playerTransform == null) return false;
            var obj = GameObject.Find($"Current_{current.currentId}");
            if (obj == null) return false;
            return Vector3.Distance(_playerTransform.position, obj.transform.position)
                   <= current.adjacencyRadius;
        }

        // -------------------------------------------------------------------------
        // Utility
        // -------------------------------------------------------------------------
        private MythState GetMythState()
            => UniverseStateManager.Instance?.Current?.mythState;

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------
        public bool IsCoralDiscovered(string gardenId)    => _discoveredCorals.Contains(gardenId);
        public bool IsCaveOuterFound(string caveId)       => _discoveredCaveOuters.Contains(caveId);
        public bool IsCaveInnerReached(string caveId)     => _discoveredCaveInners.Contains(caveId);
        public bool IsTrenchDiscovered(string trenchId)   => _discoveredTrenches.Contains(trenchId);
        public bool IsBioZoneActive(string bioZoneId)     => _activatedBioZones.Contains(bioZoneId);
        public int  DiscoveryCount(string zoneId = null)  =>
            _discoveredCorals.Count + _discoveredCaveOuters.Count +
            _discoveredTrenches.Count + _activatedBioZones.Count;
    }
}
