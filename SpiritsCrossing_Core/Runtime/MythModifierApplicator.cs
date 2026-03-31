// SpiritsCrossing — MythModifierApplicator.cs
// Distributes MythState scalar modifier outputs downstream after each
// session or realm outcome. This is the downstream half of the myth loop:
//
//   MythInterpreter  →  MythState.RebuildModifiers()
//   MythModifierApplicator  reads those scalars and pushes them into:
//     - CompanionBondSystem.bondGrowthRate  (companionSensitivity)
//     - PlanetState.growth per active myth  (planet growth nudge)
//     - CurrentEnvironmentIntensity         (broadcast for audio/visual)
//
// This deliberately does NOT push portal bias — PortalRevealSystem reads
// MythState directly in ScorePortal() each frame to stay current.
// Companion bond rate and planet growth are session-scale effects, so
// applying them here (once per session/realm) is appropriate.
//
// Setup: Add to the Bootstrap scene alongside GameBootstrapper.
//        VRBootstrapInstaller creates it automatically.

using UnityEngine;
using SpiritsCrossing.Companions;

namespace SpiritsCrossing.Runtime
{
    public class MythModifierApplicator : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------
        public static MythModifierApplicator Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Companion Bond Rate")]
        [Tooltip("Base bond growth rate before myth sensitivity is applied. " +
                 "ResonanceMemorySystem also writes to this; myth adds on top.")]
        [Range(0.5f, 3f)] public float baseBondGrowthRate = 1.0f;

        [Tooltip("Maximum additional bond rate multiplier from full companionSensitivity.")]
        [Range(0f, 1f)] public float maxCompanionSensitivityBoost = 0.50f;

        [Header("Planet Growth Nudge")]
        [Tooltip("Per active myth, nudge that myth's associated planet's growth by " +
                 "this amount × myth strength per session/realm event.")]
        [Range(0f, 0.05f)] public float mythPlanetGrowthNudge = 0.012f;

        [Header("Debug")]
        public bool logApplications;

        // -------------------------------------------------------------------------
        // Public state (read by audio/visual systems)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Current environmental intensity (0–1) derived from storm + fire myth strengths.
        /// Audio and visual systems read this to scale ambient intensity.
        /// </summary>
        public float CurrentEnvironmentIntensity { get; private set; }

        /// <summary>
        /// Current ruin echo strength (0–1) — how strongly discovered ruins
        /// amplify the player's resonance field in ruin zones.
        /// Already consumed by WorldSystem.ScanForDiscoveries().
        /// </summary>
        public float CurrentRuinEchoStrength { get; private set; }

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void OnEnable()
        {
            if (UniverseStateManager.Instance != null)
            {
                UniverseStateManager.Instance.OnSessionApplied      += OnMythableEvent;
                UniverseStateManager.Instance.OnRealmOutcomeApplied += OnRealmMythableEvent;
            }
        }

        private void OnDisable()
        {
            if (UniverseStateManager.Instance != null)
            {
                UniverseStateManager.Instance.OnSessionApplied      -= OnMythableEvent;
                UniverseStateManager.Instance.OnRealmOutcomeApplied -= OnRealmMythableEvent;
            }
        }

        // -------------------------------------------------------------------------
        // Event handlers
        // -------------------------------------------------------------------------
        private void OnMythableEvent(SessionResonanceResult _) => ApplyModifiers();
        private void OnRealmMythableEvent(RealmOutcome _)      => ApplyModifiers();

        // -------------------------------------------------------------------------
        // Apply modifiers downstream
        // -------------------------------------------------------------------------
        private void ApplyModifiers()
        {
            var myth = UniverseStateManager.Instance?.Current.mythState;
            if (myth == null) return;

            // ---- 1. Companion bond rate ----
            // companionSensitivity = elder + source * 0.5 (already computed by RebuildModifiers)
            // We add it on top of whatever ResonanceMemorySystem last set.
            if (CompanionBondSystem.Instance != null)
            {
                float boost = 1f + myth.companionSensitivity * maxCompanionSensitivityBoost;
                // Preserve any value already set by ResonanceMemorySystem (learning multiplier)
                // by using the larger of the two contributions
                float existing = CompanionBondSystem.Instance.bondGrowthRate;
                CompanionBondSystem.Instance.bondGrowthRate =
                    Mathf.Max(existing, baseBondGrowthRate * boost);
            }

            // ---- 2. Per-myth planet growth nudge ----
            // Each active myth nudges its associated planet forward slightly.
            // Myths are literally growing the world they belong to.
            var universe = UniverseStateManager.Instance.Current;
            foreach (var activeMyth in myth.activeMyths)
            {
                string planetId = MythKeyToPlanetId(activeMyth.mythKey);
                if (string.IsNullOrEmpty(planetId)) continue;

                var planet = universe.GetOrCreatePlanet(planetId);
                float nudge = mythPlanetGrowthNudge * activeMyth.strength;
                planet.growth = Mathf.Clamp01(planet.growth + nudge);

                if (logApplications)
                    Debug.Log($"[MythModifierApplicator] Myth '{activeMyth.mythKey}' nudged " +
                              $"{planetId} growth +{nudge:F4} → {planet.growth:F3}");
            }

            // ---- 3. Environment intensity broadcast ----
            CurrentEnvironmentIntensity = myth.environmentalIntensity;
            CurrentRuinEchoStrength     = myth.ruinEchoStrength;

            if (logApplications)
                Debug.Log($"[MythModifierApplicator] EnvIntensity={CurrentEnvironmentIntensity:F3} " +
                          $"RuinEcho={CurrentRuinEchoStrength:F3} " +
                          $"CompanionBond={CompanionBondSystem.Instance?.bondGrowthRate:F3}");
        }

        // -------------------------------------------------------------------------
        // Myth key → planet id mapping
        // -------------------------------------------------------------------------
        private static string MythKeyToPlanetId(string mythKey) => mythKey switch
        {
            "forest"  => "ForestHeart",
            "sky"     => "SkySpiral",
            "ocean"   => "WaterFlow",
            "fire"    => "DarkContrast",
            "machine" => "DarkContrast",  // machine shares the dark/contrast world
            "source"  => "SourceVeil",
            "elder"   => "SourceVeil",    // elder myths deepen the Source world
            _         => string.Empty
        };
    }
}
