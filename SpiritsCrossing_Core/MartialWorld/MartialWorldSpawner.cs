// SpiritsCrossing — MartialWorldSpawner.cs
// Manages the Martial World environment on MartialCrossing planet.
//
// FEATURES
//   Ball Courts    — Mayan sacred ball courts where rhythm and timing are tested.
//                    The Hero Twins defeated the Lords of Xibalba here.
//   Dojos          — Rain-soaked training grounds where Chaak's tears fall.
//                    Kung Fu, Karate, hand-to-hand. Stillness into explosion.
//   Shadow Gardens — Moonlit obsidian gardens for the ninja path.
//                    Low distortion + high flow = moving without disturbance.
//   Blade Pavilions — Open-air steel and wind. Fencing, swords, precision.
//   Krishna Fields — The golden battlefield of Kurukshetra.
//                    The deepest teaching: to fight is duty, duty is love.
//   Maize Forges   — The Popol Vuh creation cycle: Mud → Wood → Maize.
//                    Warriors are broken and remade until they can worship.
//
// MASTERY TIERS (23 levels, Mayan cosmology)
//   Zone discoveries, trial victories, and sustained practice advance the
//   player through Xibalba (1–9) → Middleworld (10) → Heaven (11–23).
//
// MARTIAL COMPANIONS (managed by WorldAnimalSpawnSystem)
//   jaguar_martial  — Martial / Tier 1 — Crunching Jaguar, raw power
//   crane           — Martial / Tier 1 — balance, patience, the crane stance
//   plumed_serpent  — Martial / Tier 2 — Quetzalcoatl, wisdom in motion
//   martial_hawk    — Martial / Tier 2 — precision, the strike
//   fox_sacred      — Martial / Tier 3 — sacred animal who found the maize
//   quetzal         — Martial / Tier 3 — feathered serpent spirit, mastery

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Companions;

namespace SpiritsCrossing.MartialWorld
{
    // =========================================================================
    public class MartialWorldSpawner : MonoBehaviour
    {
        public static MartialWorldSpawner Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        [Header("Data")]
        public string martialDataFile = "martial_world_data.json";

        [Header("Timing")]
        public float scanInterval       = 1.0f;
        public float chaakRainInterval   = 1.0f;

        [Header("Debug")]
        public bool logDiscoveries   = true;
        public bool logChaakRain     = false;
        public bool logMasteryChanges = true;

        // ---------------------------------------------------------------------
        // Events
        // ---------------------------------------------------------------------
        public event Action<BallCourtRecord>       OnBallCourtDiscovered;
        public event Action<BallCourtRecord>       OnBallCourtVictory;
        public event Action<DojoRecord>            OnDojoDiscovered;
        public event Action<DojoRecord>            OnDojoMastered;
        public event Action<ShadowGardenRecord>    OnShadowGardenDiscovered;
        public event Action<BladePavilionRecord>   OnBladePavilionDiscovered;
        public event Action<BladePavilionRecord>   OnBladePrecisionAchieved;
        public event Action<KrishnaFieldRecord>    OnKrishnaFieldDiscovered;
        public event Action<KrishnaFieldRecord>    OnKrishnaTeaching;
        public event Action<MaizeForgeRecord>      OnForgePhaseAdvanced;
        public event Action<MaizeForgeRecord>      OnForgeComplete;
        public event Action<int, MasteryRealm>     OnMasteryTierAdvanced; // tier, realm

        // ---------------------------------------------------------------------
        // State
        // ---------------------------------------------------------------------
        public bool IsLoaded { get; private set; }
        public MartialWorldData Data { get; private set; }
        public string ActiveZoneId { get; private set; }
        public MasteryTierState Mastery { get; private set; } = new MasteryTierState();

        private readonly HashSet<string> _discoveredCourts          = new();
        private readonly HashSet<string> _victoryCourts             = new();
        private readonly HashSet<string> _discoveredDojos           = new();
        private readonly HashSet<string> _masteredDojos             = new();
        private readonly HashSet<string> _discoveredShadowGardens   = new();
        private readonly HashSet<string> _discoveredBladePavilions  = new();
        private readonly HashSet<string> _precisionBladePavilions   = new();
        private readonly HashSet<string> _discoveredKrishnaFields   = new();
        private readonly HashSet<string> _krishnaTeachingReceived   = new();
        private readonly HashSet<string> _completedForges           = new();
        private readonly Dictionary<string, float> _courtTimers     = new();
        private readonly Dictionary<string, float> _forgeTimers     = new();

        private float    _scanTimer;
        private float    _chaakTimer;
        private float    _accumulatedMastery;  // mastery score buffer for tier advancement
        private Transform _playerTransform;

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return LoadMartialData();
        }

        private void Update()
        {
            if (!IsLoaded || string.IsNullOrEmpty(ActiveZoneId)) return;

            _scanTimer  += Time.deltaTime;
            _chaakTimer += Time.deltaTime;

            if (_scanTimer >= scanInterval)       { _scanTimer  = 0f; ScanZone(ActiveZoneId); }
            if (_chaakTimer >= chaakRainInterval) { _chaakTimer = 0f; TickChaakRain(ActiveZoneId); }

            TickBallCourts(ActiveZoneId);
            TickMaizeForges(ActiveZoneId);
        }

        private IEnumerator LoadMartialData()
        {
            yield return null;
            string path = Path.Combine(Application.streamingAssetsPath, martialDataFile);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[MartialWorldSpawner] {martialDataFile} not found. " +
                                 "Generate with martial_world_generator.py.");
                yield break;
            }
            try
            {
                Data     = JsonUtility.FromJson<MartialWorldData>(File.ReadAllText(path));
                IsLoaded = Data != null;
                Debug.Log($"[MartialWorldSpawner] Loaded: {Data?.zones.Count} martial zones.");
            }
            catch (Exception e) { Debug.LogError($"[MartialWorldSpawner] Load error: {e.Message}"); }
        }

        // ---------------------------------------------------------------------
        // Zone entry/exit
        // ---------------------------------------------------------------------
        public void EnterZone(string zoneId)
        {
            ActiveZoneId     = zoneId;
            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

            // Delegate animal spawning to WorldAnimalSpawnSystem
            WorldAnimalSpawnSystem.Instance?.EnterWorld("MartialCrossing");

            var zone = Data?.GetZone(zoneId);
            Debug.Log($"[MartialWorldSpawner] Entered: {zoneId} ({zone?.zoneName ?? "?"} tier={zone?.martialTier ?? "?"})");
        }

        public void ExitZone()
        {
            WorldAnimalSpawnSystem.Instance?.ExitWorld();
            ActiveZoneId = null;
            _playerTransform = null;
        }

        // -----------------------------------------------------------------
        // Discovery scan — checks all feature types in the active zone
        // -----------------------------------------------------------------
        private void ScanZone(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null) return;
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return;

            float envIntensity = UniverseStateManager.Instance?.Current?.mythState.environmentalIntensity ?? 0f;
            float ts = 1f - envIntensity * 0.15f; // myth-driven threshold softening

            // --- Ball Courts ---
            foreach (var court in zone.ballCourts)
            {
                if (court.minMasteryTier > Mastery.currentTier) continue;
                if (_discoveredCourts.Contains(court.courtId)) continue;
                if (playerField.WeightedHarmony(court.frozenField) >= court.rhythmThreshold * ts)
                {
                    _discoveredCourts.Add(court.courtId);
                    GetMythState()?.Activate(court.mythTrigger, "ball_court_discovery", 0.55f);
                    OnBallCourtDiscovered?.Invoke(court);
                    if (logDiscoveries)
                        Debug.Log($"[MartialWorldSpawner] Ball court discovered: {court.courtName}");
                }
            }

            // --- Dojos ---
            foreach (var dojo in zone.dojos)
            {
                if (dojo.minMasteryTier > Mastery.currentTier) continue;
                float h = playerField.WeightedHarmony(dojo.frozenField);

                if (!_discoveredDojos.Contains(dojo.dojoId) && h >= dojo.discoveryThreshold * ts)
                {
                    _discoveredDojos.Add(dojo.dojoId);
                    GetMythState()?.Activate(dojo.mythTrigger, "dojo_discovery", 0.50f);
                    OnDojoDiscovered?.Invoke(dojo);
                    if (logDiscoveries)
                        Debug.Log($"[MartialWorldSpawner] Dojo discovered: {dojo.dojoName} ({dojo.discipline})");
                }

                if (_discoveredDojos.Contains(dojo.dojoId) &&
                    !_masteredDojos.Contains(dojo.dojoId) &&
                    h >= dojo.innerMasteryThreshold * ts)
                {
                    _masteredDojos.Add(dojo.dojoId);
                    GetMythState()?.Activate(dojo.mythTrigger, "dojo_mastery", 0.70f);
                    AccumulateMastery(dojo.masteryReward);
                    OnDojoMastered?.Invoke(dojo);
                    if (logDiscoveries)
                        Debug.Log($"[MartialWorldSpawner] Dojo mastered: {dojo.dojoName}");
                }
            }

            // --- Shadow Gardens ---
            foreach (var garden in zone.shadowGardens)
            {
                if (garden.minMasteryTier > Mastery.currentTier) continue;
                if (_discoveredShadowGardens.Contains(garden.gardenId)) continue;

                // Shadow arts: must have LOW distortion AND high flow
                float h = playerField.WeightedHarmony(garden.frozenField);
                bool lowDistortion = (playerField.red < garden.maxDistortionAllowed); // distortion maps to agitated bands
                bool highFlow = (playerField.yellow >= garden.minFlowRequired);

                if (h >= garden.discoveryThreshold * ts && lowDistortion && highFlow)
                {
                    _discoveredShadowGardens.Add(garden.gardenId);
                    GetMythState()?.Activate(garden.mythTrigger, "shadow_discovery", 0.60f);
                    AccumulateMastery(garden.masteryReward);
                    OnShadowGardenDiscovered?.Invoke(garden);
                    if (logDiscoveries)
                        Debug.Log($"[MartialWorldSpawner] Shadow garden discovered: {garden.gardenName}");
                }
            }

            // --- Blade Pavilions ---
            foreach (var pavilion in zone.bladePavilions)
            {
                if (pavilion.minMasteryTier > Mastery.currentTier) continue;
                float h = playerField.WeightedHarmony(pavilion.frozenField);

                if (!_discoveredBladePavilions.Contains(pavilion.pavilionId) &&
                    h >= pavilion.discoveryThreshold * ts)
                {
                    _discoveredBladePavilions.Add(pavilion.pavilionId);
                    GetMythState()?.Activate(pavilion.mythTrigger, "blade_discovery", 0.55f);
                    OnBladePavilionDiscovered?.Invoke(pavilion);
                    if (logDiscoveries)
                        Debug.Log($"[MartialWorldSpawner] Blade pavilion discovered: {pavilion.pavilionName} ({pavilion.bladeStyle})");
                }

                if (_discoveredBladePavilions.Contains(pavilion.pavilionId) &&
                    !_precisionBladePavilions.Contains(pavilion.pavilionId) &&
                    h >= pavilion.precisionThreshold * ts)
                {
                    _precisionBladePavilions.Add(pavilion.pavilionId);
                    GetMythState()?.Activate(pavilion.mythTrigger, "blade_precision", 0.70f);
                    AccumulateMastery(pavilion.masteryReward);
                    OnBladePrecisionAchieved?.Invoke(pavilion);
                    if (logDiscoveries)
                        Debug.Log($"[MartialWorldSpawner] Blade precision achieved: {pavilion.pavilionName}");
                }
            }

            // --- Krishna's Field ---
            foreach (var field in zone.krishnaFields)
            {
                if (field.minMasteryTier > Mastery.currentTier) continue;
                float h = playerField.WeightedHarmony(field.frozenField);

                if (!_discoveredKrishnaFields.Contains(field.fieldId) &&
                    h >= field.discoveryThreshold * ts)
                {
                    _discoveredKrishnaFields.Add(field.fieldId);
                    GetMythState()?.Activate(field.discoveryMythTrigger, "krishna_discovery", 0.65f);
                    OnKrishnaFieldDiscovered?.Invoke(field);
                    if (logDiscoveries)
                        Debug.Log($"[MartialWorldSpawner] Krishna's Field discovered: {field.fieldName}");
                }

                // Krishna speaks when sourceAlignment is very high
                var resonance = VibrationalResonanceSystem.Instance?.PlayerField;
                float sourceAlign = resonance?.violet ?? 0f; // violet = deepest source band
                if (_discoveredKrishnaFields.Contains(field.fieldId) &&
                    !_krishnaTeachingReceived.Contains(field.fieldId) &&
                    sourceAlign >= field.teachingThreshold)
                {
                    _krishnaTeachingReceived.Add(field.fieldId);
                    GetMythState()?.Activate(field.teachingMythTrigger, "krishna_teaching", 0.85f);
                    GetMythState()?.Activate("elder", "krishna_teaching", 0.60f);
                    AccumulateMastery(field.masteryReward);
                    OnKrishnaTeaching?.Invoke(field);
                    if (logDiscoveries)
                        Debug.Log($"[MartialWorldSpawner] Krishna speaks at {field.fieldName}: " +
                                  $"\"Fight, Arjuna. Not because you want to. Because it is yours to do.\"");
                }
            }
        }

        // -----------------------------------------------------------------
        // Chaak's Rain — passive blue band amplification at dojos
        // -----------------------------------------------------------------
        private void TickChaakRain(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null || _playerTransform == null) return;
            var resonance = VibrationalResonanceSystem.Instance;
            if (resonance == null) return;

            foreach (var dojo in zone.dojos)
            {
                if (!dojo.chaakRainFalls) continue;
                if (!IsPlayerNearObject($"Dojo_{dojo.dojoId}", 12f)) continue;

                // Chaak's rain amplifies the blue band — god of rain and sacrifice
                resonance.ApplyTransientBoost("blue", dojo.blueAmplification * chaakRainInterval);

                // Calm-under-rain bonus: if player stays calm while the rain intensifies,
                // the martial truth deepens. Sacrifice through discipline.
                var playerField = resonance.PlayerField;
                if (playerField != null && playerField.green >= dojo.calmBonusThreshold)
                {
                    resonance.ApplyTransientBoost("indigo", 0.01f * chaakRainInterval);
                }

                if (logChaakRain)
                    Debug.Log($"[MartialWorldSpawner] Chaak's rain at {dojo.dojoName} (intensity={dojo.rainIntensity:F2})");
            }
        }

        // -----------------------------------------------------------------
        // Ball Court trials — sustained rhythm to defeat the Lords
        // -----------------------------------------------------------------
        private void TickBallCourts(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null || _playerTransform == null) return;
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return;

            foreach (var court in zone.ballCourts)
            {
                if (_victoryCourts.Contains(court.courtId)) continue;
                if (!_discoveredCourts.Contains(court.courtId)) continue;
                if (!IsPlayerNearObject($"BallCourt_{court.courtId}", 8f)) continue;

                float h = playerField.WeightedHarmony(court.frozenField);
                if (h >= court.victoryThreshold)
                {
                    if (!_courtTimers.ContainsKey(court.courtId))
                        _courtTimers[court.courtId] = 0f;
                    _courtTimers[court.courtId] += Time.deltaTime;

                    if (_courtTimers[court.courtId] >= court.sustainDurationSeconds)
                    {
                        _victoryCourts.Add(court.courtId);
                        GetMythState()?.Activate(court.mythTrigger, "ball_court_victory", 0.75f);
                        GetMythState()?.Activate(court.victoryMythTrigger, "ball_court_hero", 0.65f);
                        AccumulateMastery(court.masteryReward);
                        OnBallCourtVictory?.Invoke(court);
                        Debug.Log($"[MartialWorldSpawner] Ball court victory: {court.courtName}! " +
                                  $"The Hero Twins defeat the Lords of Xibalba.");
                    }
                }
                else
                {
                    _courtTimers.Remove(court.courtId);
                }
            }
        }

        // -----------------------------------------------------------------
        // Maize Forge — the Popol Vuh creation cycle: Mud → Wood → Maize
        // -----------------------------------------------------------------
        private void TickMaizeForges(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null || _playerTransform == null) return;
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return;

            foreach (var forge in zone.maizeForges)
            {
                if (forge.minMasteryTier > Mastery.currentTier) continue;
                if (_completedForges.Contains(forge.forgeId)) continue;
                if (!IsPlayerNearObject($"MaizeForge_{forge.forgeId}", 6f)) continue;

                float h = playerField.WeightedHarmony(forge.frozenField);

                // Determine the threshold for the current creation phase
                float phaseThreshold = forge.currentPhase switch
                {
                    CreationPhase.Mud   => forge.mudPhaseThreshold,
                    CreationPhase.Wood  => forge.woodPhaseThreshold,
                    CreationPhase.Maize => forge.maizePhaseThreshold,
                    _                   => 1f,
                };

                if (h >= phaseThreshold)
                {
                    if (!_forgeTimers.ContainsKey(forge.forgeId))
                        _forgeTimers[forge.forgeId] = 0f;
                    _forgeTimers[forge.forgeId] += Time.deltaTime;

                    if (_forgeTimers[forge.forgeId] >= forge.sustainDurationSeconds)
                    {
                        _forgeTimers.Remove(forge.forgeId);

                        if (forge.currentPhase == CreationPhase.Maize)
                        {
                            // Final creation complete — warrior is fully formed
                            _completedForges.Add(forge.forgeId);
                            GetMythState()?.Activate(forge.completionMythTrigger, "maize_forge_complete", 0.80f);
                            GetMythState()?.Activate("martial", "maize_forge_complete", 0.75f);
                            AccumulateMastery(forge.masteryReward);
                            OnForgeComplete?.Invoke(forge);
                            Debug.Log($"[MartialWorldSpawner] Maize Forge complete: {forge.forgeName}! " +
                                      $"The warrior is remade from maize — body and spirit unified.");
                        }
                        else
                        {
                            // Advance to next creation phase
                            forge.currentPhase = forge.currentPhase == CreationPhase.Mud
                                                 ? CreationPhase.Wood
                                                 : CreationPhase.Maize;

                            GetMythState()?.Activate(forge.phaseMythTrigger, "forge_phase", 0.55f);
                            OnForgePhaseAdvanced?.Invoke(forge);
                            Debug.Log($"[MartialWorldSpawner] Forge phase advanced: {forge.forgeName} → {forge.currentPhase}");
                        }
                    }
                }
                else
                {
                    // If player drops below threshold, the creation falls apart
                    // (like the mud people dissolving in water)
                    if (_forgeTimers.ContainsKey(forge.forgeId))
                    {
                        _forgeTimers[forge.forgeId] = Mathf.Max(0f,
                            _forgeTimers[forge.forgeId] - Time.deltaTime * 0.5f);
                    }
                }
            }
        }

        // -----------------------------------------------------------------
        // Mastery tier advancement
        // -----------------------------------------------------------------
        private void AccumulateMastery(float amount)
        {
            _accumulatedMastery += amount;

            int prevTier = Mastery.currentTier;
            if (Mastery.TryAdvance(_accumulatedMastery))
            {
                _accumulatedMastery = 0f; // reset buffer after advancement
                OnMasteryTierAdvanced?.Invoke(Mastery.currentTier, Mastery.CurrentRealm);

                if (logMasteryChanges)
                {
                    string realmName = Mastery.CurrentRealm switch
                    {
                        MasteryRealm.Xibalba    => "Xibalba (Underworld)",
                        MasteryRealm.Middleworld => "Middleworld (Balance)",
                        MasteryRealm.Upperworld => "Heaven (Upperworld)",
                        _                       => "Unknown",
                    };
                    Debug.Log($"[MartialWorldSpawner] Mastery tier advanced: {prevTier} → {Mastery.currentTier} " +
                              $"[{realmName}] progress={Mastery.RealmProgress:F2}");
                }

                // Crossing from Xibalba to Middleworld is a major mythic moment
                if (prevTier <= 9 && Mastery.currentTier == 10)
                {
                    GetMythState()?.Activate("martial", "middleworld_achieved", 0.85f);
                    GetMythState()?.Activate("elder", "middleworld_achieved", 0.50f);
                    Debug.Log("[MartialWorldSpawner] *** MIDDLEWORLD ACHIEVED — Balance found. " +
                              "The warrior stands between Xibalba and Heaven. ***");
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

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------
        public bool IsCourtDiscovered(string courtId)           => _discoveredCourts.Contains(courtId);
        public bool IsCourtVictorious(string courtId)           => _victoryCourts.Contains(courtId);
        public bool IsDojoDiscovered(string dojoId)             => _discoveredDojos.Contains(dojoId);
        public bool IsDojoMastered(string dojoId)               => _masteredDojos.Contains(dojoId);
        public bool IsShadowGardenDiscovered(string gardenId)   => _discoveredShadowGardens.Contains(gardenId);
        public bool IsBladePavilionDiscovered(string pavilionId) => _discoveredBladePavilions.Contains(pavilionId);
        public bool IsBladePrecisionAchieved(string pavilionId)  => _precisionBladePavilions.Contains(pavilionId);
        public bool IsKrishnaFieldDiscovered(string fieldId)     => _discoveredKrishnaFields.Contains(fieldId);
        public bool IsKrishnaTeachingReceived(string fieldId)    => _krishnaTeachingReceived.Contains(fieldId);
        public bool IsForgeComplete(string forgeId)              => _completedForges.Contains(forgeId);
        public int  CompletedCourtCount()                        => _victoryCourts.Count;
        public int  MasteredDojoCount()                          => _masteredDojos.Count;
        public int  CompletedForgeCount()                        => _completedForges.Count;
    }
}
