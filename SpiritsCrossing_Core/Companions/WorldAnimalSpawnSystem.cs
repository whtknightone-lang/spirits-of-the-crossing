// SpiritsCrossing — WorldAnimalSpawnSystem.cs
// The unified animal population engine for all 6 worlds.
//
// Loads spirit_animal_world_map.json and evaluates spawn conditions for every
// resident and visitor animal on the current world against the player's live
// resonance state and emotional spectrum.
//
// This replaces per-spawner manual animal lists. World spawners (Forest, Ocean,
// Sky, Fire, Machine, Source) still handle environmental features (rivers, temples,
// trenches, etc.) but delegate animal population to this system.
//
// Spawn conditions are parsed from the JSON strings and evaluated against:
//   - PlayerResonanceState fields (calm, joy, wonder, sourceAlignment, etc.)
//   - EmotionalResonanceSpectrum (stillness, peace, love, source, etc.)
//   - World-specific environmental state (discoveries, time of day)
//   - CompanionBondSystem bond levels
//
// Animals don't snap in/out — they fade via VibrationalResonanceSystem harmony.
// This system only decides which animals are ELIGIBLE to appear. The actual
// distance/behavior is driven continuously by CompanionBehaviorController.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SpiritsCrossing.SpiritAI;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.Companions
{
    // -------------------------------------------------------------------------
    // JSON data types matching spirit_animal_world_map.json
    // -------------------------------------------------------------------------
    [Serializable]
    public class WorldAnimalEntry
    {
        public string animalId;
        public string spawnCondition;
        public string storyGrimm;
        public string storyIndigenous;
        public string storyAsian;
        public string storyArabian;
    }

    [Serializable]
    public class WorldAnimalMap
    {
        public string worldId;
        public string realmId;
        public List<WorldAnimalEntry> residents = new List<WorldAnimalEntry>();
        public List<string>           visitors  = new List<string>();
    }

    [Serializable]
    public class WorldAnimalMapCollection
    {
        public List<WorldAnimalMap> worldAnimals = new List<WorldAnimalMap>();

        public WorldAnimalMap GetWorld(string worldId)
        {
            foreach (var w in worldAnimals)
                if (w.worldId == worldId) return w;
            return null;
        }
    }

    // =========================================================================
    public class WorldAnimalSpawnSystem : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------
        public static WorldAnimalSpawnSystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -----------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------
        [Header("Data")]
        public string worldMapFile = "spirit_animal_world_map.json";

        [Header("Timing")]
        [Tooltip("How often spawn conditions are re-evaluated (seconds).")]
        public float evaluationInterval = 2.0f;

        [Tooltip("How often visitor eligibility is checked (slower — cross-world).")]
        public float visitorInterval = 8.0f;

        [Header("Visitor Spawning")]
        [Tooltip("Minimum player bond level with a visitor animal before it can cross worlds.")]
        [Range(0f, 1f)] public float visitorBondThreshold = 0.25f;

        [Tooltip("Minimum emotional depth for visitors to appear.")]
        [Range(0f, 1f)] public float visitorDepthThreshold = 0.3f;

        [Header("Debug")]
        public bool logSpawnChanges = true;

        // -----------------------------------------------------------------
        // Events
        // -----------------------------------------------------------------
        public event Action<string, string> OnAnimalActivated;   // animalId, worldId
        public event Action<string, string> OnAnimalDeactivated; // animalId, worldId
        public event Action<string, string> OnVisitorArrived;    // animalId, worldId

        // -----------------------------------------------------------------
        // Public state
        // -----------------------------------------------------------------
        public bool IsLoaded { get; private set; }
        public WorldAnimalMapCollection Data { get; private set; }
        public string ActiveWorldId { get; private set; }

        /// <summary>Currently active animal IDs on this world.</summary>
        public IReadOnlyCollection<string> ActiveAnimals => _activeAnimals;

        /// <summary>Currently active visitor animal IDs.</summary>
        public IReadOnlyCollection<string> ActiveVisitors => _activeVisitors;

        // -----------------------------------------------------------------
        // Internal
        // -----------------------------------------------------------------
        private readonly HashSet<string> _activeAnimals  = new HashSet<string>();
        private readonly HashSet<string> _activeVisitors = new HashSet<string>();
        private SpiritBrainOrchestrator _orchestrator;
        private float _evalTimer;
        private float _visitorTimer;

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return null;
            LoadWorldMap();
            _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();
        }

        private void Update()
        {
            if (!IsLoaded || string.IsNullOrEmpty(ActiveWorldId)) return;

            _evalTimer    += Time.deltaTime;
            _visitorTimer += Time.deltaTime;

            if (_evalTimer >= evaluationInterval)
            {
                _evalTimer = 0f;
                EvaluateResidents();
            }

            if (_visitorTimer >= visitorInterval)
            {
                _visitorTimer = 0f;
                EvaluateVisitors();
            }
        }

        // -----------------------------------------------------------------
        // Loading
        // -----------------------------------------------------------------
        private void LoadWorldMap()
        {
            string path = Path.Combine(Application.streamingAssetsPath, worldMapFile);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[WorldAnimalSpawnSystem] {worldMapFile} not found.");
                return;
            }
            try
            {
                Data     = JsonUtility.FromJson<WorldAnimalMapCollection>(File.ReadAllText(path));
                IsLoaded = Data != null;
                Debug.Log($"[WorldAnimalSpawnSystem] Loaded {Data?.worldAnimals.Count} world animal maps.");
            }
            catch (Exception e) { Debug.LogError($"[WorldAnimalSpawnSystem] Load error: {e.Message}"); }
        }

        // -----------------------------------------------------------------
        // World entry / exit
        // -----------------------------------------------------------------
        public void EnterWorld(string worldId)
        {
            ActiveWorldId = worldId;
            _activeAnimals.Clear();
            _activeVisitors.Clear();
            _evalTimer    = evaluationInterval; // trigger immediate evaluation
            _visitorTimer = visitorInterval;

            Debug.Log($"[WorldAnimalSpawnSystem] Entered world: {worldId}");
        }

        public void ExitWorld()
        {
            // Deactivate all animals
            foreach (var id in _activeAnimals)
                OnAnimalDeactivated?.Invoke(id, ActiveWorldId);
            foreach (var id in _activeVisitors)
                OnAnimalDeactivated?.Invoke(id, ActiveWorldId);

            _activeAnimals.Clear();
            _activeVisitors.Clear();
            ActiveWorldId = null;
        }

        // -----------------------------------------------------------------
        // Resident evaluation
        // -----------------------------------------------------------------
        private void EvaluateResidents()
        {
            var worldMap = Data?.GetWorld(ActiveWorldId);
            if (worldMap == null) return;

            var playerState = GetPlayerState();
            if (playerState == null) return;

            var spectrum = EmotionalFieldPropagation.Instance?.LiveSpectrum;

            foreach (var entry in worldMap.residents)
            {
                bool eligible = EvaluateSpawnCondition(
                    entry.spawnCondition, playerState, spectrum);

                bool wasActive = _activeAnimals.Contains(entry.animalId);

                if (eligible && !wasActive)
                {
                    _activeAnimals.Add(entry.animalId);
                    ActivateAnimal(entry.animalId);
                    OnAnimalActivated?.Invoke(entry.animalId, ActiveWorldId);

                    if (logSpawnChanges)
                        Debug.Log($"[WorldAnimalSpawnSystem] ✦ {entry.animalId} appears " +
                                  $"({entry.spawnCondition}) on {ActiveWorldId}");
                }
                else if (!eligible && wasActive)
                {
                    // Don't immediately remove — let harmony decay handle the visual fade.
                    // Only deactivate if the condition has been false for a sustained period.
                    // For now, keep active once spawned (animals don't vanish abruptly).
                }
            }
        }

        // -----------------------------------------------------------------
        // Visitor evaluation — animals that cross between worlds
        // -----------------------------------------------------------------
        private void EvaluateVisitors()
        {
            var worldMap = Data?.GetWorld(ActiveWorldId);
            if (worldMap == null) return;

            var spectrum = EmotionalFieldPropagation.Instance?.LiveSpectrum;
            float emotionalDepth = spectrum?.Depth ?? 0f;

            // Visitors require minimum emotional depth
            if (emotionalDepth < visitorDepthThreshold) return;

            foreach (var visitorId in worldMap.visitors)
            {
                if (_activeVisitors.Contains(visitorId)) continue;
                if (_activeAnimals.Contains(visitorId)) continue; // already a resident

                // Visitors require a minimum bond to cross worlds
                float bond = CompanionBondSystem.Instance?.GetBondLevel(visitorId) ?? 0f;
                if (bond < visitorBondThreshold) continue;

                // Visitors are more likely when the emotional spectrum aligns
                // with their element
                var profile = CompanionBondSystem.Instance?.GetProfile(visitorId);
                if (profile == null) continue;

                float elementAffinity = GetElementalAffinity(profile.element, spectrum);
                if (elementAffinity < 0.25f) continue;

                _activeVisitors.Add(visitorId);
                ActivateAnimal(visitorId);
                OnVisitorArrived?.Invoke(visitorId, ActiveWorldId);

                if (logSpawnChanges)
                    Debug.Log($"[WorldAnimalSpawnSystem] ✧ Visitor {visitorId} ({profile.element}) " +
                              $"crosses into {ActiveWorldId} (bond={bond:F2} affinity={elementAffinity:F2})");
            }
        }

        // -----------------------------------------------------------------
        // Spawn condition evaluator
        //
        // Parses the condition strings from spirit_animal_world_map.json and
        // evaluates them against live state. Conditions are mapped to the
        // closest existing resonance/emotional dimensions.
        // -----------------------------------------------------------------
        private bool EvaluateSpawnCondition(
            string condition, PlayerResonanceState state, EmotionalResonanceSpectrum spectrum)
        {
            if (string.IsNullOrEmpty(condition)) return false;

            // --- Always-present animals ---
            if (condition == "always_in_zone" ||
                condition == "always_in_shallows")
                return true;

            // --- Resonance threshold conditions ---
            // Format: "fieldName_above_X.XX" or "fieldName_below_X.XX"
            if (condition.Contains("_above_") || condition.Contains("_below_"))
                return EvaluateThresholdCondition(condition, state, spectrum);

            // --- Compound conditions (and) ---
            if (condition.Contains("_and_"))
            {
                string[] parts = condition.Split(new[] { "_and_" }, StringSplitOptions.None);
                foreach (var part in parts)
                    if (!EvaluateSpawnCondition(part.Trim(), state, spectrum)) return false;
                return true;
            }

            // --- Location-gated conditions ---
            // These depend on world spawner discoveries. We check WorldSystem
            // and world-specific spawners for relevant state.
            switch (condition)
            {
                case "dawn_near_sanctum":
                    return IsDawnWindow() && HasDiscoveredSanctum();

                case "near_temple_causeway":
                case "near_temple_tower":
                    return HasDiscoveredTemple();

                case "summit_or_nesting_ground":
                    return HasDiscoveredSummitOrNest();

                case "near_aerial_ruin":
                    return HasDiscoveredAerialRuin();

                case "trench_discovered":
                    return HasDiscoveredTrench();

                case "bioluminescent_zone_active":
                    return HasActiveBioZone();

                case "near_coral_garden":
                    return HasDiscoveredCoral();

                case "near_river_or_pond":
                    return HasNearbyWater();

                case "near_coastline":
                    return HasNearbyCoastline();

                case "near_desert":
                    return IsDesertZone();

                default:
                    // Unknown condition — default to resonance check if it looks like one,
                    // otherwise allow spawning at reduced probability
                    return UnityEngine.Random.value < 0.3f;
            }
        }

        private bool EvaluateThresholdCondition(
            string condition, PlayerResonanceState state, EmotionalResonanceSpectrum spectrum)
        {
            bool isAbove = condition.Contains("_above_");
            string[] split = condition.Split(new[] { isAbove ? "_above_" : "_below_" },
                StringSplitOptions.None);

            if (split.Length != 2) return false;

            string fieldName = split[0];
            if (!float.TryParse(split[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float threshold))
                return false;

            float value = ResolveFieldValue(fieldName, state, spectrum);

            return isAbove ? value >= threshold : value <= threshold;
        }

        /// <summary>
        /// Maps spawn condition field names to live resonance/emotional values.
        /// World-specific fields (forestResonance, skyResonance, etc.) are mapped
        /// to the closest combination of existing dimensions.
        /// </summary>
        private float ResolveFieldValue(
            string fieldName, PlayerResonanceState state, EmotionalResonanceSpectrum spectrum)
        {
            return fieldName switch
            {
                // --- Direct PlayerResonanceState fields ---
                "sourceAlignment" => state.sourceAlignment,
                "calm"            => state.calm,
                "joy"             => state.joy,
                "wonder"          => state.wonder,
                "movementFlow"    => state.movementFlow,
                "spinStability"   => state.spinStability,
                "socialSync"      => state.socialSync,
                "breathCoherence" => state.breathCoherence,
                "distortion"      => state.distortion,

                // --- Emotional spectrum fields ---
                "stillness" => spectrum?.stillness ?? state.calm,
                "peace"     => spectrum?.peace     ?? state.calm * 0.7f,
                "love"      => spectrum?.love      ?? state.socialSync * 0.5f,
                "source"    => spectrum?.source    ?? state.sourceAlignment,

                // --- World-specific resonance (mapped to closest dimensions) ---

                // Forest: calm + breath + source (the green world)
                "rootDepth"        => state.calm * 0.5f + state.breathCoherence * 0.3f + state.sourceAlignment * 0.2f,
                "forestResonance"  => state.calm * 0.4f + state.breathCoherence * 0.3f + state.joy * 0.3f,

                // Sky: wonder + spin + movement (the blue/violet world)
                "skyResonance"     => state.wonder * 0.4f + state.spinStability * 0.3f + state.movementFlow * 0.3f,
                "spinLift"         => state.spinStability * 0.6f + state.movementFlow * 0.4f,

                // Ocean: flow + social + calm (the blue/indigo world)
                "deepResonance"    => state.sourceAlignment * 0.4f + state.calm * 0.3f + state.breathCoherence * 0.3f,
                "tidalFlow"        => state.movementFlow * 0.5f + state.socialSync * 0.3f + state.breathCoherence * 0.2f,

                // Fire: spin + distortion + will (the red/violet world)
                "phoenixRise"      => state.spinStability * 0.3f + state.wonder * 0.3f +
                                      state.sourceAlignment * 0.2f + (1f - state.calm) * 0.2f,
                "willForce"        => state.spinStability * 0.4f + state.movementFlow * 0.3f +
                                      (1f - state.calm) * 0.3f,
                "flamePressure"    => state.distortion * 0.5f + (1f - state.calm) * 0.3f +
                                      state.spinStability * 0.2f,

                // Machine: spin + pattern + precision (the yellow/indigo world)
                "patternSync"      => state.spinStability * 0.4f + state.breathCoherence * 0.3f + state.calm * 0.3f,
                "harmonyLock"      => state.breathCoherence * 0.4f + state.spinStability * 0.3f + state.sourceAlignment * 0.3f,
                "gearMomentum"     => state.movementFlow * 0.4f + state.spinStability * 0.4f + state.joy * 0.2f,
                "systemResonance"  => state.breathCoherence * 0.3f + state.spinStability * 0.3f +
                                      state.calm * 0.2f + state.sourceAlignment * 0.2f,

                // Source: stillness + source + communion (the violet/indigo world)
                "veilThinning"     => state.sourceAlignment * 0.4f + state.wonder * 0.3f + state.calm * 0.3f,
                "innerStillness"   => state.breathCoherence * 0.4f + state.calm * 0.4f + state.sourceAlignment * 0.2f,
                "communionDepth"   => state.sourceAlignment * 0.3f + state.socialSync * 0.3f +
                                      state.breathCoherence * 0.2f + state.calm * 0.2f,

                _ => 0f
            };
        }

        // -----------------------------------------------------------------
        // Environmental state queries — delegate to world spawners
        // -----------------------------------------------------------------
        private bool IsDawnWindow()
        {
            var clock = SpiritsCrossing.Autonomous.CosmosClockSystem.Instance;
            float hour = clock != null ? clock.CurrentHour : (Time.time / 3600f % 24f);
            return hour >= 5.5f && hour <= 7.0f;
        }

        private bool HasDiscoveredSanctum()
        {
            var forest = ForestWorld.ForestWorldSpawner.Instance;
            if (forest == null) return false;
            // Check if any sanctum has been unlocked
            var temples = forest.GetApproachedTemples();
            foreach (var t in temples)
                if (forest.IsSanctumUnlocked(t.templeId)) return true;
            return false;
        }

        private bool HasDiscoveredTemple()
        {
            var forest = ForestWorld.ForestWorldSpawner.Instance;
            return forest != null && forest.GetApproachedTemples().Count > 0;
        }

        private bool HasDiscoveredSummitOrNest()
        {
            var sky = SkyWorld.SkyWorldSpawner.Instance;
            return sky != null; // simplified — sky spawner tracks summit/nest internally
        }

        private bool HasDiscoveredAerialRuin()
        {
            var sky = SkyWorld.SkyWorldSpawner.Instance;
            return sky != null; // simplified — sky spawner tracks ruins internally
        }

        private bool HasDiscoveredTrench()
        {
            var ocean = OceanWorld.OceanWorldSpawner.Instance;
            return ocean != null && ocean.DiscoveryCount() > 3; // has done deep exploration
        }

        private bool HasActiveBioZone()
        {
            var ocean = OceanWorld.OceanWorldSpawner.Instance;
            return ocean != null; // simplified — ocean spawner tracks bio zones internally
        }

        private bool HasDiscoveredCoral()
        {
            var ocean = OceanWorld.OceanWorldSpawner.Instance;
            return ocean != null && ocean.DiscoveryCount() > 0;
        }

        /// <summary>
        /// Water features exist on most worlds — forest rivers, ocean shallows,
        /// machine water clocks, source veil pools. This returns true on any
        /// world that has water features, which is nearly all of them.
        /// </summary>
        private bool HasNearbyWater()
        {
            // Forest: rivers are always present
            if (ForestWorld.ForestWorldSpawner.Instance != null) return true;
            // Ocean: water everywhere
            if (OceanWorld.OceanWorldSpawner.Instance != null) return true;
            // Source: veil pools
            if (SourceWorld.SourceWorldSpawner.Instance != null) return true;
            // Machine: water clocks and fountains exist
            if (MachineWorld.MachineWorldSpawner.Instance != null) return true;
            // Sky: rain pools on floating islands — less common but possible
            if (SkyWorld.SkyWorldSpawner.Instance != null) return true;
            // Fire: rare oasis zones
            return false;
        }

        /// <summary>
        /// Coastlines exist primarily on the ocean world but also where
        /// land meets water on forest and source worlds.
        /// </summary>
        private bool HasNearbyCoastline()
        {
            // Ocean world: always has coastlines
            if (OceanWorld.OceanWorldSpawner.Instance != null) return true;
            // Forest: river edges serve as coastline-like features
            if (ForestWorld.ForestWorldSpawner.Instance != null) return true;
            return false;
        }

        /// <summary>
        /// Desert zones exist on the fire world (ashlands, obsidian plateau)
        /// and potentially as dry zones on other worlds.
        /// </summary>
        private bool IsDesertZone()
        {
            if (FireWorld.FireWorldSpawner.Instance != null) return true;
            return false;
        }

        // -----------------------------------------------------------------
        // Elemental affinity from emotional spectrum (for visitor evaluation)
        // -----------------------------------------------------------------
        private float GetElementalAffinity(string element, EmotionalResonanceSpectrum spectrum)
        {
            if (spectrum == null) return 0f;
            return element switch
            {
                "Earth" => spectrum.stillness * 0.4f + spectrum.peace * 0.3f + spectrum.love * 0.3f,
                "Air"   => spectrum.joy * 0.4f + spectrum.source * 0.3f + spectrum.peace * 0.3f,
                "Water" => spectrum.love * 0.4f + spectrum.peace * 0.3f + spectrum.joy * 0.3f,
                "Fire"  => spectrum.joy * 0.4f + spectrum.source * 0.3f + spectrum.stillness * 0.3f,
                _       => spectrum.Fullness
            };
        }

        // -----------------------------------------------------------------
        // Animal activation (delegates to CompanionBondSystem / VibrationalResonanceSystem)
        // -----------------------------------------------------------------
        private void ActivateAnimal(string animalId)
        {
            // Register the animal in VibrationalResonanceSystem so harmony tracking begins
            var vrs = VibrationalResonanceSystem.Instance;
            if (vrs != null)
            {
                var profile = CompanionBondSystem.Instance?.GetProfile(animalId);
                if (profile != null)
                    vrs.RegisterByElement(animalId, profile.element);
            }
        }

        // -----------------------------------------------------------------
        // Player state source
        // -----------------------------------------------------------------
        private PlayerResonanceState GetPlayerState()
        {
            if (_orchestrator == null)
                _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();
            return _orchestrator?.CurrentPlayerState;
        }

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>Is a specific animal currently active on this world?</summary>
        public bool IsAnimalActive(string animalId) =>
            _activeAnimals.Contains(animalId) || _activeVisitors.Contains(animalId);

        /// <summary>Get the myth story for an animal in the current world.</summary>
        public WorldAnimalEntry GetAnimalEntry(string animalId)
        {
            var worldMap = Data?.GetWorld(ActiveWorldId);
            if (worldMap == null) return null;
            foreach (var e in worldMap.residents)
                if (e.animalId == animalId) return e;
            return null;
        }

        /// <summary>Get all currently eligible animals and their myth stories.</summary>
        public List<WorldAnimalEntry> GetActiveAnimalEntries()
        {
            var result = new List<WorldAnimalEntry>();
            var worldMap = Data?.GetWorld(ActiveWorldId);
            if (worldMap == null) return result;
            foreach (var e in worldMap.residents)
                if (_activeAnimals.Contains(e.animalId)) result.Add(e);
            return result;
        }

        /// <summary>
        /// Get all animals for a specific world (for menu/encyclopedia display).
        /// Returns both residents and visitors.
        /// </summary>
        public (List<WorldAnimalEntry> residents, List<string> visitors) GetWorldPopulation(string worldId)
        {
            var worldMap = Data?.GetWorld(worldId);
            if (worldMap == null) return (new List<WorldAnimalEntry>(), new List<string>());
            return (worldMap.residents, worldMap.visitors);
        }
    }
}
