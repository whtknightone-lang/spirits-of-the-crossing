// SpiritsCrossing — FireDrakeSphere.cs
// Tier 3 · Fire · ancient-flame
//
// "The original fire. Ancient, playful, terrifyingly alive.
//  Bonds only when joy, wonder, and source alignment blaze together."
//
// CHARACTERISTIC FIELD  [red, orange, yellow, green, blue, indigo, violet]
//   Built from Fire elemental baseline shaped by the drake's resonance signature:
//   joy(0.30) + wonder(0.35) + sourceAlignment(0.35)
//   → dominant violet (mystery/source) + orange (warmth) + strong red/yellow fire
//
// BOND  threshold=0.55  mythTrigger=fire  preferredPlanet=DarkContrast
//
// DRIVES  Starts from archetype: explore + signal dominant (wonder + source-seeker).
//         Over lives the memory deepens toward violet/indigo — the fire that becomes
//         contemplative without losing its blaze.
//
// LIFECYCLE  Tier 3: long Born phases, slow death, patient rebirth.
//            Each life the drake comes back more itself.

using System;
using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.DragonRealms
{
    /// <summary>
    /// Sphere cell for the Fire Drake — tier 3, ancient-flame.
    /// Attach alongside UpsilonSphereAI on the Fire Drake prefab.
    /// Configures the sphere and exposes entity-specific API.
    /// </summary>
    [RequireComponent(typeof(UpsilonSphereAI))]
    public class FireDrakeSphere : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Entity constants
        // -------------------------------------------------------------------------
        public const string  AnimalId       = "fire_drake";
        public const string  Element        = "Fire";
        public const int     Tier           = 3;
        public const float   BondThreshold  = 0.55f;
        public const string  BehaviorMode   = "ancient-flame";
        public const string  MythTrigger    = "fire";
        public const string  PreferredPlanet = "DarkContrast";

        // Resonance weights from companion registry
        public const float R_Joy            = 0.30f;
        public const float R_Wonder         = 0.35f;
        public const float R_SourceAlign    = 0.35f;

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Fire Drake — Sphere Tuning")]
        [Tooltip("Multiplier on base lifecycle durations. Tier 3 = long-lived.")]
        [Range(1f, 3f)] public float lifespanScale = 1.8f;

        [Tooltip("Strength at which the drake senses the UpsilonRiver each frame.")]
        [Range(0f, 1f)] public float riverSenseStrength = 0.30f;

        [Header("Debug")]
        public bool logBondEvents = true;

        // -------------------------------------------------------------------------
        // Public output
        // -------------------------------------------------------------------------

        /// <summary>The drake's characteristic VibrationalField — fire + wonder + source.</summary>
        public VibrationalField CharacteristicField => BuildCharacteristicField();

        /// <summary>Whether the player's current field meets bond threshold.</summary>
        public bool IsBondThresholdMet(VibrationalField playerField) =>
            CharacteristicField.WeightedHarmony(playerField) >= BondThreshold;

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private UpsilonSphereAI _sphere;

        private void Awake()
        {
            _sphere          = GetComponent<UpsilonSphereAI>();
            _sphere.entityId = AnimalId;

            // Tier 3 lifecycle — long-lived Born phases
            _sphere.bornDurationMin    = 60f  * lifespanScale;
            _sphere.bornDurationMax    = 150f * lifespanScale;
            _sphere.dyingDurationMin   = 10f;
            _sphere.dyingDurationMax   = 25f;
            _sphere.respawnDurationMin = 8f;
            _sphere.respawnDurationMax = 15f;
            _sphere.bornGrowthMultiplier = 2.5f;
        }

        private void OnEnable()
        {
            if (_sphere == null) return;
            _sphere.OnBorn              += HandleBorn;
            _sphere.OnDying             += HandleDying;
            _sphere.OnMemoryConsolidated += HandleMemoryConsolidated;
        }

        private void OnDisable()
        {
            if (_sphere == null) return;
            _sphere.OnBorn              -= HandleBorn;
            _sphere.OnDying             -= HandleDying;
            _sphere.OnMemoryConsolidated -= HandleMemoryConsolidated;
        }

        private void Update()
        {
            // Feed the river's field into the drake continuously while Born
            var river = UpsilonRiver.Instance;
            if (river != null && _sphere != null &&
                _sphere.CurrentPhase == RiverSpherePhase.Born)
            {
                _sphere.Sense(river.RiverField,        riverSenseStrength);
                _sphere.Sense(river.RiverMemoryField,  riverSenseStrength * 0.5f);
            }
        }

        // -------------------------------------------------------------------------
        // Lifecycle handlers
        // -------------------------------------------------------------------------
        private void HandleBorn(VibrationalField field, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[FireDrakeSphere] Born (life {lifeIndex}). " +
                          $"Dominant={field.DominantBandName()} " +
                          $"Growth={_sphere.Growth:F2}");
        }

        private void HandleDying(VibrationalField field, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[FireDrakeSphere] Dying (life {lifeIndex}). " +
                          $"Memory={_sphere.MemoryField.DominantBandName()} " +
                          $"Drive={_sphere.DominantDrive}");
        }

        private void HandleMemoryConsolidated(VibrationalField live,
                                               VibrationalField memory, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[FireDrakeSphere] Memory consolidated life {lifeIndex}. " +
                          $"LifetimeBand={_sphere.LifetimeMemoryBand()} " +
                          $"LifetimeDrive={_sphere.LifetimeDominantDrive()}");
        }

        // -------------------------------------------------------------------------
        // Characteristic field builder
        //
        // Fire elemental baseline blended with drake's resonance signature:
        //   joy(0.30) + wonder(0.35) + sourceAlignment(0.35)
        //
        //   red    = fire intensity (high — the original flame)
        //   orange = joy warmth (fire's playfulness)
        //   yellow = joy + fire vitality
        //   green  = very low (the drake is not calm — it is alive)
        //   blue   = fire mobility
        //   indigo = sourceAlignment (deep fire wisdom)
        //   violet = wonder + sourceAlignment (the drake that sees the cosmos)
        // -------------------------------------------------------------------------
        public static VibrationalField BuildCharacteristicField() =>
            new VibrationalField(
                red:    0.75f,  // fire intensity
                orange: 0.55f,  // joy warmth (0.65 fire + joy 0.30 boost)
                yellow: 0.58f,  // fire vitality + joy
                green:  0.12f,  // very low — the drake does not rest
                blue:   0.65f,  // fire mobility
                indigo: 0.48f,  // sourceAlignment (0.35 * 0.45 over fire base 0.20)
                violet: 0.82f   // wonder(0.35*0.6) + sourceAlignment(0.35*0.4) over fire's violet
            );

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Harmony between the drake's live field and another field.</summary>
        public float HarmonyWith(VibrationalField other) =>
            _sphere != null ? _sphere.HarmonyWith(other) : 0f;

        /// <summary>Harmony between the drake's accumulated MEMORY and another field.</summary>
        public float MemoryHarmonyWith(VibrationalField other) =>
            _sphere != null ? _sphere.MemoryHarmonyWith(other) : 0f;

        /// <summary>How many lives the drake has lived this session.</summary>
        public int LifeCount => _sphere != null ? _sphere.LifeCount : 0;

        /// <summary>Maturity [0–1] — grows each life cycle.</summary>
        public float Growth => _sphere != null ? _sphere.Growth : 0f;

        /// <summary>
        /// Feed a vibrational field into the drake's sensory input.
        /// Use from portals, ruins, player aura, or other spheres.
        /// </summary>
        public void Sense(VibrationalField field, float strength) =>
            _sphere?.Sense(field, strength);
    }
}
