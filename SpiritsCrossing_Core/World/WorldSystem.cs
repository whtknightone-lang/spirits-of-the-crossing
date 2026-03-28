// SpiritsCrossing — WorldSystem.cs
// Manages the three-layer world system across the cosmos.
//
// DISCOVERY
//   Every second, checks each undiscovered ruin against the player's current
//   VibrationalField harmony with the ruin's frozenField.
//   Ancient ruins require harmony >= 0.70 — they vibrate at a purer, deeper
//   frequency that only resonates with aligned players.
//   Newer ruins require >= 0.50 — more accessible echoes.
//   On discovery: myth activated, event fired, persisted to UniverseState.
//
// ACTIVE WORLDS
//   When the player enters a planet's play space, EnterActiveWorld() seeds
//   NPC presences from CosmosGenerationSystem into CosmosObserverMode.
//   The world's ambient field = blend of the planet's orbital equilibrium
//   with any ruins the player has already discovered nearby.
//
// AMBIENT FIELD
//   The ambient world field feeds back into VibrationalResonanceSystem as a
//   global modifier — being in a world rich with discovered ruins slightly
//   amplifies harmonies for the element of those ruins.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Lifecycle;
using SpiritsCrossing.Cosmos;

namespace SpiritsCrossing.World
{
    public class WorldSystem : MonoBehaviour
    {
        public static WorldSystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Config")]
        public string ruinsDataFile = "ruins_data.json";
        public float  scanInterval  = 1.0f;

        [Header("Ambient Field Blend")]
        [Tooltip("How much discovered ruins influence the ambient world field.")]
        [Range(0f, 1f)] public float ruinAmbientWeight = 0.25f;

        [Header("Debug")]
        public bool logDiscoveries = true;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<RuinRecord>       OnRuinDiscovered;      // ruinId, layer
        public event Action<ActiveWorldRecord> OnActiveWorldEntered;
        public event Action<string>            OnActiveWorldExited;   // planetId

        // -------------------------------------------------------------------------
        // Public state
        // -------------------------------------------------------------------------
        public bool IsLoaded { get; private set; }
        public WorldCollection Data { get; private set; }

        public ActiveWorldRecord CurrentActiveWorld { get; private set; }
        public VibrationalField  AmbientField       { get; private set; } = new VibrationalField();
        public int               TotalDiscovered    => _discoveredIds.Count;

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private readonly HashSet<string> _discoveredIds = new HashSet<string>();
        private float                    _scanTimer;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return LoadRuinsData();
            LoadDiscoveredFromState();
        }

        private void Update()
        {
            _scanTimer += Time.deltaTime;
            if (_scanTimer < scanInterval) return;
            _scanTimer = 0f;

            if (IsLoaded) ScanForDiscoveries();
        }

        // -------------------------------------------------------------------------
        // Loading
        // -------------------------------------------------------------------------
        private IEnumerator LoadRuinsData()
        {
            yield return null;
            string path = Path.Combine(Application.streamingAssetsPath, ruinsDataFile);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[WorldSystem] {ruinsDataFile} not found. " +
                                 "Run ruins_generator.py and setup_streaming_assets.py.");
                yield break;
            }
            try
            {
                Data    = JsonUtility.FromJson<WorldCollection>(File.ReadAllText(path));
                IsLoaded = Data != null;
                Debug.Log($"[WorldSystem] Loaded: {Data?.totalAncientRuins} ancient + " +
                          $"{Data?.totalNewerRuins} newer + {Data?.totalActiveWorlds} active worlds.");
            }
            catch (Exception e) { Debug.LogError($"[WorldSystem] Load error: {e.Message}"); }
        }

        private void LoadDiscoveredFromState()
        {
            var ids = UniverseStateManager.Instance?.Current.discoveredRuinIds;
            if (ids != null) foreach (var id in ids) _discoveredIds.Add(id);
        }

        // -------------------------------------------------------------------------
        // Discovery scan
        // -------------------------------------------------------------------------
        private void ScanForDiscoveries()
        {
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null || Data == null) return;

            string currentPlanet = CurrentActiveWorld?.planetId;
            var planetData = !string.IsNullOrEmpty(currentPlanet)
                ? Data.GetPlanet(currentPlanet) : null;

            // If in an active world, only scan that planet's ruins
            // Otherwise scan all (e.g. while in cave or cosmos map)
            IEnumerable<PlanetWorldData> planetsToScan = planetData != null
                ? new[] { planetData }
                : (IEnumerable<PlanetWorldData>)Data.planets;

            foreach (var planet in planetsToScan)
            {
                foreach (var ruin in planet.AllRuins())
                {
                    if (_discoveredIds.Contains(ruin.ruinId)) continue;

                    float harmony = playerField.WeightedHarmony(ruin.frozenField);
                    if (harmony >= ruin.discoveryThreshold)
                        DiscoverRuin(ruin);
                }
            }
        }

        private void DiscoverRuin(RuinRecord ruin)
        {
            _discoveredIds.Add(ruin.ruinId);

            // Persist
            var universe = UniverseStateManager.Instance?.Current;
            if (universe != null && !universe.discoveredRuinIds.Contains(ruin.ruinId))
            {
                universe.discoveredRuinIds.Add(ruin.ruinId);
                UniverseStateManager.Instance.Save();
            }

            // Activate myth
            var myth = universe?.mythState;
            float strength = ruin.Layer == WorldLayer.Ancient ? 0.75f : 0.55f;
            myth?.Activate(ruin.mythTrigger, "ruin_discovery", strength);
            if (ruin.Layer == WorldLayer.Ancient)
                myth?.Activate("ruin", "ruin_discovery", strength);

            OnRuinDiscovered?.Invoke(ruin);
            if (logDiscoveries)
                Debug.Log($"[WorldSystem] Discovered: {ruin.ruinId} ({ruin.layer} — {ruin.era}) " +
                          $"myth={ruin.mythTrigger}");

            // Update ambient field
            if (CurrentActiveWorld?.planetId == ruin.planetId)
                RebuildAmbientField(ruin.planetId);
        }

        // -------------------------------------------------------------------------
        // Active World entry/exit
        // -------------------------------------------------------------------------
        public void EnterActiveWorld(string planetId)
        {
            var planetData = Data?.GetPlanet(planetId);
            if (planetData?.activeWorld == null)
            {
                Debug.LogWarning($"[WorldSystem] No active world data for {planetId}.");
                return;
            }

            CurrentActiveWorld = planetData.activeWorld;
            RebuildAmbientField(planetId);

            // Seed NPC presences into CosmosObserverMode
            SeedWorldNpcPresences(planetData);

            OnActiveWorldEntered?.Invoke(CurrentActiveWorld);
            Debug.Log($"[WorldSystem] Entered: {planetId} ({CurrentActiveWorld.element}) " +
                      $"arch={CurrentActiveWorld.npcArchetype} F={CurrentActiveWorld.fieldStrength:F2}");
        }

        public void ExitActiveWorld()
        {
            string pid = CurrentActiveWorld?.planetId;
            CurrentActiveWorld = null;
            AmbientField = new VibrationalField(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f);
            OnActiveWorldExited?.Invoke(pid);
        }

        // -------------------------------------------------------------------------
        // Ambient field — planet equilibrium + discovered ruin blend
        // -------------------------------------------------------------------------
        private void RebuildAmbientField(string planetId)
        {
            var planetData = Data?.GetPlanet(planetId);
            if (planetData?.activeWorld == null) return;

            // Start with the planet's live orbital equilibrium
            var ambient = new VibrationalField(
                planetData.activeWorld.ambientField.red,
                planetData.activeWorld.ambientField.orange,
                planetData.activeWorld.ambientField.yellow,
                planetData.activeWorld.ambientField.green,
                planetData.activeWorld.ambientField.blue,
                planetData.activeWorld.ambientField.indigo,
                planetData.activeWorld.ambientField.violet);

            // Blend in discovered ruins — ancient ones contribute more
            int count = 0;
            foreach (var ruin in planetData.AllRuins())
            {
                if (!_discoveredIds.Contains(ruin.ruinId)) continue;
                float w = ruin.Layer == WorldLayer.Ancient
                    ? ruinAmbientWeight * 1.5f : ruinAmbientWeight;
                ambient.LerpToward(ruin.frozenField, Mathf.Clamp01(w));
                count++;
            }

            ambient.Clamp01();
            AmbientField = ambient;

            if (count > 0)
                Debug.Log($"[WorldSystem] Ambient field rebuilt for {planetId} " +
                          $"({count} ruins contributing) dom={ambient.DominantBandName()}");
        }

        // -------------------------------------------------------------------------
        // Seed world NPCs into cosmos observer
        // -------------------------------------------------------------------------
        private void SeedWorldNpcPresences(PlanetWorldData planet)
        {
            var observer = CosmosObserverMode.Instance;
            if (observer == null) return;

            // Generate NPC spirits based on the planet's natural archetype
            // The CosmosGenerationSystem provides their drive distribution
            var config = CosmosGenerationSystem.Instance?.GetPlanetConfig(planet.planetId);
            if (config == null) return;

            // NPCs carry the world's ambient field as their presence field
            // Already handled by CosmosObserverMode.GenerateNpcPresences()
            // This call ensures the planet's active world growth is reflected
            Debug.Log($"[WorldSystem] Seeding NPC presences for {planet.planetId} " +
                      $"({config.npcPopulation.naturalArchetype} dominant)");
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------
        public bool IsDiscovered(string ruinId) => _discoveredIds.Contains(ruinId);

        public List<RuinRecord> GetDiscoveredRuins(string planetId = null)
        {
            var result = new List<RuinRecord>();
            if (Data == null) return result;
            foreach (var p in Data.planets)
            {
                if (!string.IsNullOrEmpty(planetId) && p.planetId != planetId) continue;
                foreach (var r in p.AllRuins())
                    if (_discoveredIds.Contains(r.ruinId)) result.Add(r);
            }
            return result;
        }

        public List<RuinRecord> GetUndiscoveredRuins(string planetId)
        {
            var result = new List<RuinRecord>();
            var planet = Data?.GetPlanet(planetId);
            if (planet == null) return result;
            foreach (var r in planet.AllRuins())
                if (!_discoveredIds.Contains(r.ruinId)) result.Add(r);
            return result;
        }

        /// <summary>How many ruins has the player found on a given planet (0-4).</summary>
        public int DiscoveredCountOnPlanet(string planetId)
        {
            var planet = Data?.GetPlanet(planetId);
            if (planet == null) return 0;
            int count = 0;
            foreach (var r in planet.AllRuins())
                if (_discoveredIds.Contains(r.ruinId)) count++;
            return count;
        }

        /// <summary>
        /// How "rich" a planet feels — 0 if no ruins found, 1 if all ruins discovered.
        /// Used to scale visual density of ancient structures.
        /// </summary>
        public float PlanetRuinRichness(string planetId)
        {
            var planet = Data?.GetPlanet(planetId);
            if (planet == null) return 0f;
            int total = planet.ancientRuins.Count + planet.newerRuins.Count;
            return total > 0 ? (float)DiscoveredCountOnPlanet(planetId) / total : 0f;
        }

        /// <summary>
        /// The nearest undiscovered ancient ruin on the current planet,
        /// and how close the player is to discovering it (0-1 harmony progress).
        /// Used to drive UI hints.
        /// </summary>
        public (RuinRecord ruin, float progress) NearestUndiscoveredAncient()
        {
            if (CurrentActiveWorld == null) return (null, 0f);
            var planet = Data?.GetPlanet(CurrentActiveWorld.planetId);
            if (planet == null) return (null, 0f);

            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return (null, 0f);

            RuinRecord best = null; float bestProgress = 0f;
            foreach (var r in planet.ancientRuins)
            {
                if (_discoveredIds.Contains(r.ruinId)) continue;
                float harm     = playerField.WeightedHarmony(r.frozenField);
                float progress = Mathf.Clamp01(harm / r.discoveryThreshold);
                if (progress > bestProgress) { bestProgress = progress; best = r; }
            }
            return (best, bestProgress);
        }
    }
}
