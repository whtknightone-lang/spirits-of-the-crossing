// SpiritsCrossing — KrakenSphere.cs
// Tier 3 · Water · deep-mystery
//
// "Ancient of the abyss. Not dark — vast. Bonds through wonder held
//  steady at the edge of the unknown."
//
// CHARACTERISTIC FIELD  [red, orange, yellow, green, blue, indigo, violet]
//   Water elemental baseline shaped by: wonder(0.50) + sourceAlignment(0.35) + breathCoherence(0.15)
//   → dominant violet (vast wonder) + indigo (abyss/source) + Water's blue
//   The kraken is the most violet entity in the game. It is pure mystery.
//
// BOND  threshold=0.55  mythTrigger=elder  preferredPlanet=WaterFlow

using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.DragonRealms
{
    [RequireComponent(typeof(UpsilonSphereAI))]
    public class KrakenSphere : MonoBehaviour
    {
        public const string AnimalId        = "kraken";
        public const string Element         = "Water";
        public const int    Tier            = 3;
        public const float  BondThreshold   = 0.55f;
        public const string BehaviorMode    = "deep-mystery";
        public const string MythTrigger     = "elder";
        public const string PreferredPlanet = "WaterFlow";

        public const float R_Wonder          = 0.50f;
        public const float R_SourceAlign     = 0.35f;
        public const float R_BreathCoherence = 0.15f;

        [Header("Kraken — Sphere Tuning")]
        [Range(1f, 3f)] public float lifespanScale     = 1.9f;
        [Range(0f, 1f)] public float riverSenseStrength = 0.25f;

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
            _sphere.dyingDurationMax     = 30f;
            _sphere.respawnDurationMin   = 10f;
            _sphere.respawnDurationMax   = 18f;
            _sphere.bornGrowthMultiplier = 2.5f;
            // Kraken memory is deep — wonder accumulates strongly across lives
            _sphere.memoryDecay          = 0.9975f;
        }

        private void OnEnable()
        {
            if (_sphere == null) return;
            _sphere.OnBorn += HandleBorn;
            _sphere.OnDying += HandleDying;
        }

        private void OnDisable()
        {
            if (_sphere == null) return;
            _sphere.OnBorn -= HandleBorn;
            _sphere.OnDying -= HandleDying;
        }

        private void Update()
        {
            // Kraken senses the deep river — preferentially the memory field (what has been)
            var river = UpsilonRiver.Instance;
            if (river != null && _sphere != null &&
                _sphere.CurrentPhase == RiverSpherePhase.Born)
            {
                _sphere.Sense(river.RiverMemoryField, riverSenseStrength * 0.8f);
                _sphere.Sense(river.RiverField,       riverSenseStrength * 0.3f);
            }
        }

        private void HandleBorn(VibrationalField field, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[KrakenSphere] Born from the deep (life {lifeIndex}). " +
                          $"Dominant={field.DominantBandName()} " +
                          $"SourceAlignment={field.SourceAlignment():F3}");
        }

        private void HandleDying(VibrationalField field, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[KrakenSphere] Returning to the abyss (life {lifeIndex}). " +
                          $"Memory={_sphere.MemoryField.DominantBandName()}");
        }

        // CHARACTERISTIC FIELD
        //   red    = very low  (the kraken is not aggressive — it is vast)
        //   orange = very low  (not social — the abyss does not need warmth)
        //   yellow = low       (no surface vitality)
        //   green  = moderate  (breathCoherence 0.15 — stillness at the bottom)
        //   blue   = high      (Water depth)
        //   indigo = very high (sourceAlignment 0.35 — the kraken knows the abyss)
        //   violet = very high (wonder 0.50 — the dominant note: pure vast mystery)
        public static VibrationalField BuildCharacteristicField() =>
            new VibrationalField(
                red:    0.08f,
                orange: 0.10f,
                yellow: 0.12f,
                green:  0.42f,
                blue:   0.78f,  // Water base
                indigo: 0.80f,  // sourceAlignment over Water's indigo
                violet: 0.90f   // wonder(0.50*0.6) + sourceAlignment(0.35*0.4) = highest violet
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
