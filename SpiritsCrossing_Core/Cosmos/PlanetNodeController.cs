// SpiritsCrossing — PlanetNodeController.cs
// The runtime scene-side node for each planet on the cosmos map.
// One instance lives on each planet GameObject in the CosmosMap scene.
//
// RESPONSIBILITIES
//   - Mirrors the persistent PlanetState from UniverseStateManager into Inspector-visible floats
//   - Receives seeding data from CosmosGenerationSystem.SeedPlanetControllers() via RecordEncounter()
//   - Subscribes to USM events so the node updates live when a realm or session completes
//   - Exposes GetGrowthPulse() for CosmosObserverMode animated breathing effect
//   - Broadcasts OnNodeUpdated so visual/audio scripts on the same GameObject can react
//
// SETUP
//   Name each planet GameObject exactly matching its planetId (e.g. "ForestHeart").
//   CosmosGenerationSystem finds nodes by GameObject name and calls RecordEncounter().

using System;
using UnityEngine;
using SpiritsCrossing.Lifecycle;

namespace SpiritsCrossing.Cosmos
{
    public class PlanetNodeController : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Identity")]
        [Tooltip("Must match the planetId used in UniverseState and cosmos_data.json. " +
                 "Defaults to the GameObject name if left empty.")]
        public string planetId;

        [Header("Live State — read-only")]
        [Range(0f, 1f)] public float growth;
        [Range(0f, 1f)] public float healing;
        [Range(0f, 1f)] public float rebirthCharge;
        [Range(0f, 1f)] public float affinityScore;
        public int    visitCount;
        public bool   unlocked;

        [Header("Encounter Metadata")]
        public string lastElement;
        public string lastDominantBand;
        public string lastArchetype;
        public float  lastCelebration;
        public float  lastContrast;
        public float  lastImportance;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        /// <summary>Fired whenever this node's state refreshes — subscribe from visual scripts.</summary>
        public event Action<PlanetNodeController> OnNodeUpdated;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Awake()
        {
            if (string.IsNullOrEmpty(planetId))
                planetId = gameObject.name;
        }

        private void OnEnable()
        {
            if (UniverseStateManager.Instance != null)
            {
                UniverseStateManager.Instance.OnSessionApplied      += HandleSessionApplied;
                UniverseStateManager.Instance.OnRealmOutcomeApplied += HandleRealmOutcome;
            }
        }

        private void OnDisable()
        {
            if (UniverseStateManager.Instance != null)
            {
                UniverseStateManager.Instance.OnSessionApplied      -= HandleSessionApplied;
                UniverseStateManager.Instance.OnRealmOutcomeApplied -= HandleRealmOutcome;
            }
        }

        private void Start()
        {
            // Mirror saved state into Inspector fields on load
            SyncFromPersistence();
        }

        // -------------------------------------------------------------------------
        // Seeding — called by CosmosGenerationSystem.SeedPlanetControllers()
        // -------------------------------------------------------------------------
        /// <summary>
        /// Push simulation-derived encounter data into this node.
        /// Called once per load by CosmosGenerationSystem after cosmos_data.json loads.
        /// </summary>
        public void RecordEncounter(string element, float celebration, float contrast,
                                    float importance, string dominantBand, string archetype)
        {
            lastElement      = element;
            lastCelebration  = celebration;
            lastContrast     = contrast;
            lastImportance   = importance;
            lastDominantBand = dominantBand;
            lastArchetype    = archetype;

            // Nudge persistent state based on encounter quality
            var planet = UniverseStateManager.Instance?.Current.GetOrCreatePlanet(planetId);
            if (planet != null)
            {
                planet.growth        = Mathf.Clamp01(planet.growth        + celebration * 0.04f);
                planet.healing       = Mathf.Clamp01(planet.healing       + celebration * 0.02f
                                                                          - contrast    * 0.01f);
                planet.rebirthCharge = Mathf.Clamp01(planet.rebirthCharge + contrast    * 0.03f
                                                                          + importance  * 0.02f);
            }

            SyncFromPersistence();
        }

        // -------------------------------------------------------------------------
        // USM event handlers
        // -------------------------------------------------------------------------
        private void HandleSessionApplied(SessionResonanceResult result)
        {
            if (!string.IsNullOrEmpty(result.currentAffinityPlanet) &&
                result.currentAffinityPlanet == planetId)
            {
                var planet = UniverseStateManager.Instance?.Current.GetOrCreatePlanet(planetId);
                if (planet != null)
                    planet.affinityScore = Mathf.Clamp01(
                        Mathf.Max(planet.affinityScore, result.currentAffinityScore * 0.1f));
            }
            SyncFromPersistence();
        }

        private void HandleRealmOutcome(RealmOutcome outcome)
        {
            if (outcome.planetId == planetId)
                SyncFromPersistence();
        }

        // -------------------------------------------------------------------------
        // Sync from persistence → Inspector fields
        // -------------------------------------------------------------------------
        private void SyncFromPersistence()
        {
            var planet = UniverseStateManager.Instance?.Current.GetOrCreatePlanet(planetId);
            if (planet == null) return;

            growth        = planet.growth;
            healing       = planet.healing;
            rebirthCharge = planet.rebirthCharge;
            affinityScore = planet.affinityScore;
            visitCount    = planet.visitCount;
            unlocked      = planet.unlocked;

            OnNodeUpdated?.Invoke(this);
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Animated growth pulse (0–1) for CosmosObserverMode breathing effect.
        /// Tightened range: base 0.50, swing ±0.15 — sphere stays compact and readable.
        /// </summary>
        public float GetGrowthPulse()
        {
            // Each planet has a slightly different phase based on its id hash.
            // Abs() guards against negative hash values; null check ensures no NRE.
            int hash  = Mathf.Abs((planetId ?? string.Empty).GetHashCode());
            float phase = (hash % 100) / 100f * Mathf.PI * 2f;
            return growth * (0.50f + 0.15f * Mathf.Sin(Time.time * 0.8f + phase));
        }

        // -------------------------------------------------------------------------
        // Lifecycle layer intensities — one float per phase, layered over the sphere
        // -------------------------------------------------------------------------

        /// <summary>
        /// Birth layer intensity — how strongly this node embodies active living growth.
        /// Driven by growth (primary) and healing (secondary).
        /// </summary>
        public float BirthLayerIntensity => Mathf.Clamp01(growth * 0.6f + healing * 0.4f);

        /// <summary>
        /// Death / Source-drop layer intensity — how charged this node is for release.
        /// Driven by rebirthCharge accumulation.
        /// </summary>
        public float DeathLayerIntensity => Mathf.Clamp01(rebirthCharge);

        /// <summary>
        /// Respawn (Rebirth) layer intensity — how strongly this node amplifies
        /// returning gifts. Combines rebirthCharge, affinityScore, and growth.
        /// </summary>
        public float RespawnLayerIntensity => Mathf.Clamp01(
            rebirthCharge * 0.5f + affinityScore * 0.3f + growth * 0.2f);

        /// <summary>
        /// The player's current cosmological phase — lets visual scripts tint the
        /// active layer without coupling directly to LifecycleSystem.
        /// </summary>
        public PlayerCyclePhase ActiveCyclePhase =>
            LifecycleSystem.Instance?.CurrentPhase ?? PlayerCyclePhase.Born;

        /// <summary>
        /// How much the cosmos map should visually emphasise this planet right now.
        /// Combines growth, affinity, and active myth strength for the planet.
        /// </summary>
        public float GetPresenceIntensity()
        {
            float mythBoost = 0f;
            var myth = UniverseStateManager.Instance?.Current.mythState;
            if (myth != null)
            {
                // Map planet id → myth key and read its strength
                string mythKey = PlanetIdToMythKey(planetId);
                if (!string.IsNullOrEmpty(mythKey))
                    mythBoost = myth.GetStrength(mythKey) * 0.30f;
            }
            return Mathf.Clamp01(growth * 0.50f + affinityScore * 0.30f + mythBoost);
        }

        private static string PlanetIdToMythKey(string id) => id switch
        {
            "ForestHeart"  => "forest",
            "SkySpiral"    => "sky",
            "WaterFlow"    => "ocean",
            "DarkContrast" => "fire",
            "SourceVeil"   => "source",
            _              => string.Empty
        };

        /// <summary>
        /// Manually force a state refresh (e.g. after a save load on scene entry).</summary>
        public void Refresh() => SyncFromPersistence();
    }
}
