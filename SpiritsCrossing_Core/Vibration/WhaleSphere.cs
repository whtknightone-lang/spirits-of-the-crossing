// SpiritsCrossing — WhaleSphere.cs
// Tier 3 · Water · ancient-song
//
// "Oldest ocean memory. Bonds only through deep breath coherence aligned
//  with source. The bond of geological patience."
//
// CHARACTERISTIC FIELD  [red, orange, yellow, green, blue, indigo, violet]
//   Water elemental baseline shaped by: calm(0.20) + sourceAlignment(0.40) + breathCoherence(0.40)
//   → dominant green (heart/calm) + indigo (deep source resonance) + Water's blue
//
// BOND  threshold=0.52  mythTrigger=source  preferredPlanet=WaterFlow
//
// DRIVES  rest + signal dominant. The whale that has lived many lives becomes
//         entirely contemplative — its memory is almost entirely indigo/green.

using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.DragonRealms
{
    [RequireComponent(typeof(UpsilonSphereAI))]
    public class WhaleSphere : MonoBehaviour
    {
        public const string AnimalId        = "whale";
        public const string Element         = "Water";
        public const int    Tier            = 3;
        public const float  BondThreshold   = 0.52f;
        public const string BehaviorMode    = "ancient-song";
        public const string MythTrigger     = "source";
        public const string PreferredPlanet = "WaterFlow";

        public const float R_Calm           = 0.20f;
        public const float R_SourceAlign    = 0.40f;
        public const float R_BreathCoherence = 0.40f;

        [Header("Whale — Sphere Tuning")]
        [Range(1f, 3f)] public float lifespanScale    = 2.0f;
        [Range(0f, 1f)] public float riverSenseStrength = 0.20f;

        [Header("Debug")]
        public bool logBondEvents = true;

        public VibrationalField CharacteristicField => BuildCharacteristicField();

        public bool IsBondThresholdMet(VibrationalField playerField) =>
            CharacteristicField.WeightedHarmony(playerField) >= BondThreshold;

        private UpsilonSphereAI _sphere;

        private void Awake()
        {
            _sphere          = GetComponent<UpsilonSphereAI>();
            _sphere.entityId = AnimalId;

            // Whale lives the longest — geological patience
            _sphere.bornDurationMin      = 80f  * lifespanScale;
            _sphere.bornDurationMax      = 200f * lifespanScale;
            _sphere.dyingDurationMin     = 15f;
            _sphere.dyingDurationMax     = 35f;
            _sphere.respawnDurationMin   = 10f;
            _sphere.respawnDurationMax   = 20f;
            _sphere.bornGrowthMultiplier = 2.8f;
            _sphere.memoryDecay          = 0.9985f; // whale memory is very long
        }

        private void OnEnable()
        {
            if (_sphere == null) return;
            _sphere.OnBorn               += HandleBorn;
            _sphere.OnMemoryConsolidated += HandleMemoryConsolidated;
        }

        private void OnDisable()
        {
            if (_sphere == null) return;
            _sphere.OnBorn               -= HandleBorn;
            _sphere.OnMemoryConsolidated -= HandleMemoryConsolidated;
        }

        private void Update()
        {
            var river = UpsilonRiver.Instance;
            if (river != null && _sphere != null &&
                _sphere.CurrentPhase == RiverSpherePhase.Born)
            {
                // Whale feels the river's memory more than its live field — deep resonance
                _sphere.Sense(river.RiverMemoryField, riverSenseStrength);
                _sphere.Sense(river.RiverField,       riverSenseStrength * 0.4f);
            }
        }

        private void HandleBorn(VibrationalField field, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[WhaleSphere] Born (life {lifeIndex}). " +
                          $"Dominant={field.DominantBandName()} Coherence={field.Coherence():F3}");
        }

        private void HandleMemoryConsolidated(VibrationalField live,
                                               VibrationalField memory, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[WhaleSphere] Memory consolidated life {lifeIndex}. " +
                          $"MemoryBand={memory.DominantBandName()} " +
                          $"SourceAlign={memory.SourceAlignment():F3}");
        }

        // CHARACTERISTIC FIELD
        //   red    = very low  (the whale has no heat or aggression)
        //   orange = very low  (not social — solitary ancient)
        //   yellow = low       (not joyful in the surface sense)
        //   green  = very high (calm 0.20 + breathCoherence 0.40 — the deepest green)
        //   blue   = high      (Water element: flow and depth)
        //   indigo = very high (sourceAlignment 0.40 — the whale hears the source)
        //   violet = moderate  (wonder implicit in the ancient-song)
        public static VibrationalField BuildCharacteristicField() =>
            new VibrationalField(
                red:    0.05f,
                orange: 0.12f,
                yellow: 0.15f,
                green:  0.88f,  // calm(0.20*0.6) + breath(0.40*0.4) over Water's green(0.50)
                blue:   0.80f,  // Water base
                indigo: 0.85f,  // sourceAlignment(0.40*0.45) over Water's indigo(0.70)
                violet: 0.38f   // subtle wonder — the whale knows but does not announce it
            );

        public float HarmonyWith(VibrationalField other) =>
            _sphere != null ? _sphere.HarmonyWith(other) : 0f;

        public float MemoryHarmonyWith(VibrationalField other) =>
            _sphere != null ? _sphere.MemoryHarmonyWith(other) : 0f;

        public int   LifeCount => _sphere != null ? _sphere.LifeCount : 0;
        public float Growth    => _sphere != null ? _sphere.Growth    : 0f;

        public void Sense(VibrationalField field, float strength) =>
            _sphere?.Sense(field, strength);
    }
}
