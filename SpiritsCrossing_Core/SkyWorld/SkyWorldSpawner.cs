// SpiritsCrossing — SkyWorldSpawner.cs
// Manages the sky world environment on SkySpiral planet.
//
// FEATURES
//   Cloud Formations   — discovered through wonder and spin. Some shapeshift
//                        in response to the player's emotional state.
//   Aerial Ruins       — ancient floating structures from the wind-age, storm-age,
//                        and star-age. Outer/inner thresholds like forest temples.
//                        Wind organs and star charts are rare inner rewards.
//   Floating Islands   — stable land in the sky. Gardens, crystals, ancient trees.
//                        Nesting grounds for sky creatures.
//   Updrafts           — vertical wind columns that amplify yellow and violet bands.
//                        Spiral updrafts reward spin stability.
//   Wind Currents      — horizontal flow paths. Cloud whale routes trigger rare myths.
//
// SKY CREATURE COMPANIONS
//   eagle          — Air / Tier 1 — appears at all altitudes, scouts ahead
//   wind_serpent   — Air / Tier 2 — appears in cloud layer and highsky, rides thermals
//   cloud_whale    — Air / Tier 3 — appears on whale routes at high altitude, meditative

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Companions;

namespace SpiritsCrossing.SkyWorld
{
    public class SkyWorldSpawner : MonoBehaviour
    {
        public static SkyWorldSpawner Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        [Header("Data")]
        public string skyDataFile = "sky_world_data.json";

        [Header("Timing")]
        public float scanInterval        = 1.0f;
        public float updraftTickInterval = 0.5f;

        [Header("Debug")]
        public bool logDiscoveries   = true;
        public bool logUpdraftTicks  = false;

        // Events
        public event Action<CloudFormationRecord> OnCloudDiscovered;
        public event Action<AerialRuinRecord>     OnAerialRuinOuterDiscovered;
        public event Action<AerialRuinRecord>     OnAerialRuinInnerDiscovered;
        public event Action<AerialChamber>        OnAerialChamberDiscovered;
        public event Action<FloatingIslandRecord> OnIslandDiscovered;
        public event Action<UpdraftRecord>        OnUpdraftEntered;
        public event Action<WindCurrentRecord>    OnWindCurrentFirstContact;
        public event Action<SkyMountainRecord>    OnMountainDiscovered;
        public event Action<MountainCaveRecord>   OnMountainCaveDiscovered;
        public event Action<EnergyWellRecord>     OnEnergyWellActivated;
        public event Action<SkyMountainRecord>    OnSummitReached;

        // State
        public bool IsLoaded { get; private set; }
        public SkyWorldData Data { get; private set; }
        public string ActiveZoneId { get; private set; }

        private readonly HashSet<string> _discoveredClouds      = new();
        private readonly HashSet<string> _discoveredRuinOuters  = new();
        private readonly HashSet<string> _discoveredRuinInners  = new();
        private readonly HashSet<string> _discoveredChambers    = new();
        private readonly HashSet<string> _discoveredIslands     = new();
        private readonly HashSet<string> _enteredUpdrafts       = new();
        private readonly HashSet<string> _touchedWindCurrents   = new();
        private readonly HashSet<string> _discoveredMountains   = new();
        private readonly HashSet<string> _discoveredMtnCaves    = new();
        private readonly HashSet<string> _activatedWells        = new();
        private readonly HashSet<string> _reachedSummits        = new();
        private readonly Dictionary<string, float> _wellChargeTimers = new();

        private float _scanTimer;
        private float _updraftTimer;
        private Transform _playerTransform;

        private IEnumerator Start()
        {
            yield return LoadSkyData();
        }

        private void Update()
        {
            if (!IsLoaded || string.IsNullOrEmpty(ActiveZoneId)) return;

            _scanTimer    += Time.deltaTime;
            _updraftTimer += Time.deltaTime;

            if (_scanTimer >= scanInterval)     { _scanTimer = 0f; ScanZone(ActiveZoneId); }
            if (_updraftTimer >= updraftTickInterval) { _updraftTimer = 0f; TickUpdraftsAndWinds(ActiveZoneId); }
        }

        private IEnumerator LoadSkyData()
        {
            yield return null;
            string path = Path.Combine(Application.streamingAssetsPath, skyDataFile);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SkyWorldSpawner] {skyDataFile} not found.");
                yield break;
            }
            try
            {
                Data     = JsonUtility.FromJson<SkyWorldData>(File.ReadAllText(path));
                IsLoaded = Data != null;
                Debug.Log($"[SkyWorldSpawner] Loaded: {Data?.zones.Count} sky zones.");
            }
            catch (Exception e) { Debug.LogError($"[SkyWorldSpawner] Load error: {e.Message}"); }
        }

        public void EnterZone(string zoneId)
        {
            ActiveZoneId     = zoneId;
            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            SpawnZoneCreatures(zoneId);
            var zone = Data?.GetZone(zoneId);
            Debug.Log($"[SkyWorldSpawner] Entered: {zoneId} ({zone?.zoneName ?? "?"} alt={zone?.altitudeTier ?? "?"})");
        }

        public void ExitZone() { ActiveZoneId = null; _playerTransform = null; }

        // -------------------------------------------------------------------------
        // Discovery scan
        // -------------------------------------------------------------------------
        private void ScanZone(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null) return;
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return;

            float envIntensity = UniverseStateManager.Instance?.Current?.mythState.environmentalIntensity ?? 0f;
            float ts = 1f - envIntensity * 0.15f;

            // Clouds
            foreach (var cloud in zone.cloudFormations)
            {
                if (_discoveredClouds.Contains(cloud.formationId)) continue;
                if (playerField.WeightedHarmony(cloud.frozenField) >= cloud.discoveryThreshold * ts)
                {
                    _discoveredClouds.Add(cloud.formationId);
                    GetMythState()?.Activate(cloud.mythTrigger, "cloud_discovery", 0.50f);
                    OnCloudDiscovered?.Invoke(cloud);
                    if (logDiscoveries)
                        Debug.Log($"[SkyWorldSpawner] Cloud discovered: {cloud.formationId} ({cloud.formationType})");
                }
            }

            // Aerial ruins — outer / inner / chambers
            foreach (var ruin in zone.aerialRuins)
            {
                float h = playerField.WeightedHarmony(ruin.frozenField);

                if (!_discoveredRuinOuters.Contains(ruin.ruinId) && h >= ruin.outerDiscoveryThreshold * ts)
                {
                    _discoveredRuinOuters.Add(ruin.ruinId);
                    GetMythState()?.Activate(ruin.outerMythTrigger, "aerial_ruin_outer", 0.55f);
                    OnAerialRuinOuterDiscovered?.Invoke(ruin);
                    if (ruin.hasWindOrgan) SpawnSkyCreature("wind_serpent", ruin.ruinId);
                    if (logDiscoveries) Debug.Log($"[SkyWorldSpawner] Aerial ruin approach: {ruin.ruinName} ({ruin.era})");
                }

                if (_discoveredRuinOuters.Contains(ruin.ruinId) &&
                    !_discoveredRuinInners.Contains(ruin.ruinId) &&
                    h >= ruin.innerDiscoveryThreshold * ts)
                {
                    _discoveredRuinInners.Add(ruin.ruinId);
                    GetMythState()?.Activate(ruin.innerMythTrigger, "aerial_ruin_inner", 0.75f);
                    if (ruin.hasStarChart) GetMythState()?.Activate("starlight", "star_chart", 0.65f);
                    OnAerialRuinInnerDiscovered?.Invoke(ruin);
                    if (logDiscoveries) Debug.Log($"[SkyWorldSpawner] Aerial ruin inner: {ruin.ruinName}");
                }

                foreach (var chamber in ruin.chambers)
                {
                    if (_discoveredChambers.Contains(chamber.chamberId)) continue;
                    if (playerField.WeightedHarmony(chamber.chamberField) >= chamber.discoveryThreshold * ts)
                    {
                        _discoveredChambers.Add(chamber.chamberId);
                        GetMythState()?.Activate(chamber.mythTrigger, "aerial_chamber", 0.60f);
                        OnAerialChamberDiscovered?.Invoke(chamber);
                        if (logDiscoveries) Debug.Log($"[SkyWorldSpawner] Aerial chamber: {chamber.chamberName}");
                    }
                }
            }

            // Floating islands
            foreach (var island in zone.floatingIslands)
            {
                if (_discoveredIslands.Contains(island.islandId)) continue;
                if (playerField.WeightedHarmony(island.frozenField) >= island.discoveryThreshold * ts)
                {
                    _discoveredIslands.Add(island.islandId);
                    GetMythState()?.Activate(island.mythTrigger, "island_discovery", 0.55f);
                    if (island.hasAncientTree) GetMythState()?.Activate("forest", "sky_ancient_tree", 0.45f);
                    if (island.hasNestingGround) SpawnSkyCreature("eagle", island.islandId);
                    OnIslandDiscovered?.Invoke(island);
                    if (logDiscoveries) Debug.Log($"[SkyWorldSpawner] Floating island: {island.islandName} ({island.biome})");
                }
            }

            // Mountains — the on-ramp to air play
            foreach (var mtn in zone.mountains)
                ScanMountain(mtn, playerField, ts);
        }

        // -------------------------------------------------------------------------
        // Mountain scanning — caves, wells, summit
        // -------------------------------------------------------------------------
        private void ScanMountain(SkyMountainRecord mtn, VibrationalField playerField, float ts)
        {
            // Discover the mountain itself (low threshold — mountains are visible)
            if (!_discoveredMountains.Contains(mtn.mountainId))
            {
                float h = playerField.WeightedHarmony(mtn.frozenField);
                if (h >= mtn.discoveryThreshold * ts)
                {
                    _discoveredMountains.Add(mtn.mountainId);
                    GetMythState()?.Activate(mtn.mythTrigger, "mountain_discovery", 0.50f);
                    OnMountainDiscovered?.Invoke(mtn);
                    if (logDiscoveries)
                        Debug.Log($"[SkyWorldSpawner] Mountain discovered: {mtn.mountainName} " +
                                  $"(peak={mtn.peakType} alt={mtn.peakAltitude}m)");
                }
                else return; // can't explore caves/wells before discovering the mountain
            }

            var resonance = VibrationalResonanceSystem.Instance;

            // Caves inside the mountain
            foreach (var cave in mtn.caves)
            {
                if (_discoveredMtnCaves.Contains(cave.caveId)) continue;
                float ch = playerField.WeightedHarmony(cave.frozenField);
                if (ch >= cave.discoveryThreshold * ts)
                {
                    _discoveredMtnCaves.Add(cave.caveId);
                    GetMythState()?.Activate(cave.mythTrigger, "mountain_cave", 0.55f);
                    OnMountainCaveDiscovered?.Invoke(cave);

                    if (logDiscoveries)
                        Debug.Log($"[SkyWorldSpawner] Mountain cave: {cave.caveName} " +
                                  $"(type={cave.caveType} echo={cave.echoIntensity:F2})");
                }

                // Active cave resonance boost — player inside gets breath/calm amplification
                if (_discoveredMtnCaves.Contains(cave.caveId) && IsPlayerNearObject($"Cave_{cave.caveId}", 8f))
                {
                    if (resonance != null)
                    {
                        resonance.ApplyTransientBoost("green",  cave.breathAmplification * 0.5f * Time.deltaTime);
                        resonance.ApplyTransientBoost("blue",   cave.calmAmplification   * 0.5f * Time.deltaTime);
                    }

                    // Wind tunnel lift — pushes player toward summit
                    if (cave.hasWindTunnel && resonance != null)
                        resonance.ApplyTransientBoost("yellow", cave.windTunnelLift * 0.3f * Time.deltaTime);
                }
            }

            // Energy wells — charge up and launch
            foreach (var well in mtn.energyWells)
            {
                if (!IsPlayerNearObject($"Well_{well.wellId}", 6f)) continue;

                // Charge the well while player stands near it
                if (!_wellChargeTimers.ContainsKey(well.wellId))
                    _wellChargeTimers[well.wellId] = 0f;

                _wellChargeTimers[well.wellId] += Time.deltaTime;
                well.currentCharge = Mathf.Clamp01(_wellChargeTimers[well.wellId] / well.chargeTimeSeconds);

                // Passive band boost while charging
                if (resonance != null)
                {
                    resonance.ApplyTransientBoost("yellow", well.yellowAmplification * well.currentCharge * Time.deltaTime);
                    resonance.ApplyTransientBoost("violet", well.violetAmplification * well.currentCharge * Time.deltaTime);
                }

                // Well fully charged — activate!
                if (well.currentCharge >= 1f && !_activatedWells.Contains(well.wellId))
                {
                    _activatedWells.Add(well.wellId);
                    GetMythState()?.Activate(well.mythTrigger, "energy_well", 0.65f);
                    GetMythState()?.Activate("sky", "energy_well_launch", 0.55f);
                    OnEnergyWellActivated?.Invoke(well);

                    // Seedling: auto-launch narration
                    var config = UniverseStateManager.Instance?.Current?.AgeTierConfig;
                    if (config?.tier == AgeTier.Seedling && well.autoLaunchForSeedling)
                        Debug.Log($"[SkyWorldSpawner] SEEDLING LAUNCH: {well.seedlingNarration ?? "Hold on tight! Here we go!"}");

                    if (logDiscoveries)
                        Debug.Log($"[SkyWorldSpawner] Energy well activated: {well.wellName} " +
                                  $"(type={well.wellType} launch={well.launchIntensity:F2} alt+={well.launchAltitudeGain}m)");
                }
            }

            // Summit reached — check if player is near the peak
            if (!_reachedSummits.Contains(mtn.mountainId) && IsPlayerNearObject($"Summit_{mtn.mountainId}", 5f))
            {
                _reachedSummits.Add(mtn.mountainId);
                if (!string.IsNullOrEmpty(mtn.summitMythTrigger))
                    GetMythState()?.Activate(mtn.summitMythTrigger, "summit", 0.75f);
                GetMythState()?.Activate("sky", "summit_reached", 0.60f);

                if (mtn.hasSummitNest) SpawnSkyCreature("eagle", mtn.mountainId);
                if (mtn.hasSummitShrine) GetMythState()?.Activate("elder", "summit_shrine", 0.70f);

                OnSummitReached?.Invoke(mtn);
                Debug.Log($"[SkyWorldSpawner] Summit reached: {mtn.mountainName}!");
            }
        }

        // -------------------------------------------------------------------------
        // Updrafts and wind currents — passive band amplification
        // -------------------------------------------------------------------------
        private void TickUpdraftsAndWinds(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null || _playerTransform == null) return;
            var resonance = VibrationalResonanceSystem.Instance;
            if (resonance == null) return;

            // Updrafts
            foreach (var updraft in zone.updrafts)
            {
                if (!IsPlayerInUpdraft(updraft)) continue;

                resonance.ApplyTransientBoost("yellow", updraft.yellowAmplification * updraftTickInterval);
                resonance.ApplyTransientBoost("violet", updraft.violetAmplification * updraftTickInterval);

                if (!_enteredUpdrafts.Contains(updraft.updraftId))
                {
                    _enteredUpdrafts.Add(updraft.updraftId);
                    GetMythState()?.Activate(updraft.mythTrigger, "updraft", 0.45f);
                    OnUpdraftEntered?.Invoke(updraft);
                    if (logDiscoveries) Debug.Log($"[SkyWorldSpawner] Updraft entered: {updraft.updraftName}");
                }
            }

            // Wind currents
            foreach (var wind in zone.windCurrents)
            {
                if (!IsPlayerInWindCurrent(wind)) continue;

                resonance.ApplyTransientBoost("yellow", wind.yellowAmplification * updraftTickInterval);
                resonance.ApplyTransientBoost("violet", wind.violetAmplification * updraftTickInterval);

                if (!_touchedWindCurrents.Contains(wind.currentId))
                {
                    _touchedWindCurrents.Add(wind.currentId);
                    GetMythState()?.Activate(wind.mythTrigger, "wind_current", 0.50f);
                    OnWindCurrentFirstContact?.Invoke(wind);
                    Debug.Log($"[SkyWorldSpawner] Wind current contact: {wind.currentName}");
                }

                if (wind.hasCloudWhaleRoute && !string.IsNullOrEmpty(wind.whaleRouteMythTrigger))
                {
                    GetMythState()?.Activate(wind.whaleRouteMythTrigger, "cloud_whale_route", 0.70f);
                    SpawnSkyCreature("cloud_whale", wind.currentId);
                }
            }
        }

        // -------------------------------------------------------------------------
        // Sky creatures
        // -------------------------------------------------------------------------
        private void SpawnZoneCreatures(string zoneId)
        {
            var zone = Data?.GetZone(zoneId);
            if (zone == null) return;
            foreach (var id in zone.preferredCompanionIds)
            {
                if (id == "cloud_whale") continue; // gated by whale route
                SpawnSkyCreature(id, zoneId);
            }
        }

        private void SpawnSkyCreature(string animalId, string context)
        {
            var ctrl = CompanionBehaviorController.Instance;
            if (ctrl == null) return;
            ctrl.ActivateCompanion(animalId);
            Debug.Log($"[SkyWorldSpawner] Creature active: {animalId} at {context}");
        }

        // -------------------------------------------------------------------------
        // Proximity
        // -------------------------------------------------------------------------
        private bool IsPlayerInUpdraft(UpdraftRecord updraft)
        {
            if (_playerTransform == null) return false;
            var obj = GameObject.Find($"Updraft_{updraft.updraftId}");
            if (obj == null) return false;
            return Vector3.Distance(_playerTransform.position, obj.transform.position) <= updraft.radius;
        }

        private bool IsPlayerInWindCurrent(WindCurrentRecord wind)
        {
            if (_playerTransform == null) return false;
            var obj = GameObject.Find($"WindCurrent_{wind.currentId}");
            if (obj == null) return false;
            return Vector3.Distance(_playerTransform.position, obj.transform.position) <= wind.adjacencyRadius;
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
        public bool IsCloudDiscovered(string id)      => _discoveredClouds.Contains(id);
        public bool IsRuinApproached(string id)       => _discoveredRuinOuters.Contains(id);
        public bool IsRuinInnerReached(string id)     => _discoveredRuinInners.Contains(id);
        public bool IsIslandDiscovered(string id)     => _discoveredIslands.Contains(id);
        public bool IsMountainDiscovered(string id)   => _discoveredMountains.Contains(id);
        public bool IsMountainCaveFound(string id)    => _discoveredMtnCaves.Contains(id);
        public bool IsEnergyWellActivated(string id)  => _activatedWells.Contains(id);
        public bool IsSummitReached(string id)        => _reachedSummits.Contains(id);
        public float GetWellCharge(string wellId)     => _wellChargeTimers.TryGetValue(wellId, out var t) ? t : 0f;
    }
}
