// SpiritsCrossing — BisonSphere.cs
// Tier 3 · Earth · ancient-ground
//
// "The oldest earth memory. Bonds through complete grounding —
//  breath, calm, and source together sustained."
//
// CHARACTERISTIC FIELD  [red, orange, yellow, green, blue, indigo, violet]
//   Earth elemental baseline shaped by: calm(0.40) + sourceAlignment(0.30) + breathCoherence(0.30)
//   → dominant green (calm+breath equally weighted) + Earth's orange warmth
//   Compared to the Elder Stag: less source, more body — the herd's ancient ground.
//
// BOND  threshold=0.50  mythTrigger=forest  preferredPlanet=ForestHeart

using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.DragonRealms
{
    [RequireComponent(typeof(UpsilonSphereAI))]
    public class BisonSphere : MonoBehaviour
    {
        public const string AnimalId        = "bison";
        public const string Element         = "Earth";
        public const int    Tier            = 3;
        public const float  BondThreshold   = 0.50f;
        public const string BehaviorMode    = "ancient-ground";
        public const string MythTrigger     = "forest";
        public const string PreferredPlanet = "ForestHeart";

        public const float R_Calm           = 0.40f;
        public const float R_SourceAlign    = 0.30f;
        public const float R_BreathCoherence = 0.30f;

        [Header("Bison — Sphere Tuning")]
        [Range(1f, 3f)] public float lifespanScale     = 1.9f;
        [Range(0f, 1f)] public float riverSenseStrength = 0.22f;

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

            _sphere.bornDurationMin      = 70f  * lifespanScale;
            _sphere.bornDurationMax      = 180f * lifespanScale;
            _sphere.dyingDurationMin     = 12f;
            _sphere.dyingDurationMax     = 28f;
            _sphere.respawnDurationMin   = 10f;
            _sphere.respawnDurationMax   = 18f;
            _sphere.bornGrowthMultiplier = 2.6f;
            _sphere.memoryDecay          = 0.9982f;
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
            // Bison feels both river dimensions — it is grounded in both present and memory
            var river = UpsilonRiver.Instance;
            if (river != null && _sphere != null &&
                _sphere.CurrentPhase == RiverSpherePhase.Born)
            {
                _sphere.Sense(river.RiverField,       riverSenseStrength * 0.6f);
                _sphere.Sense(river.RiverMemoryField, riverSenseStrength * 0.6f);
            }
        }

        private void HandleBorn(VibrationalField field, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[BisonSphere] Born (life {lifeIndex}). " +
                          $"Dominant={field.DominantBandName()} " +
                          $"Coherence={field.Coherence():F3}");
        }

        private void HandleMemoryConsolidated(VibrationalField live,
                                               VibrationalField memory, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[BisonSphere] Memory deepens (life {lifeIndex}). " +
                          $"LifetimeBand={_sphere.LifetimeMemoryBand()} " +
                          $"Growth={_sphere.Growth:F3}");
        }

        // CHARACTERISTIC FIELD
        //   red    = very low   (calm and grounded — no aggression)
        //   orange = high       (Earth warmth — the herd lives in community)
        //   yellow = moderate   (Earth's vitality — the living land)
        //   green  = very high  (calm(0.40*0.6) + breath(0.30*0.4) — full body stillness)
        //   blue   = very low   (Earth: no movement drive in the bison's bonds)
        //   indigo = moderate   (sourceAlignment(0.30*0.45) — quieter than the stag)
        //   violet = low        (the bison is ancient but not mysterious — just real)
        public static VibrationalField BuildCharacteristicField() =>
            new VibrationalField(
                red:    0.04f,
                orange: 0.62f,  // Earth warmth — stronger than stag (herd creature)
                yellow: 0.42f,
                green:  0.92f,  // very deep green — equal calm and breath
                blue:   0.08f,
                indigo: 0.52f,  // sourceAlignment present but quieter than stag
                violet: 0.18f   // not mysterious — profoundly, plainly there
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
