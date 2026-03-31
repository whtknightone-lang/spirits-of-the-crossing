// SpiritsCrossing — HarpyEagleSphere.cs
// Tier 3 · Air · fierce-wise
//
// "Apex of air spirits. Combines fierce precision with ancient source sight.
//  The most demanding air bond."
//
// CHARACTERISTIC FIELD  [red, orange, yellow, green, blue, indigo, violet]
//   Air elemental baseline shaped by: wonder(0.25) + spinStability(0.35) + sourceAlignment(0.40)
//   → dominant blue (precision/spin) + violet (wonder+source) + indigo (deep air wisdom)
//   The harpy eagle is Air at its most sovereign — stillness and strike together.
//
// BOND  threshold=0.55  mythTrigger=elder  preferredPlanet=SkySpiral

using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.DragonRealms
{
    [RequireComponent(typeof(UpsilonSphereAI))]
    public class HarpyEagleSphere : MonoBehaviour
    {
        public const string AnimalId        = "harpy_eagle";
        public const string Element         = "Air";
        public const int    Tier            = 3;
        public const float  BondThreshold   = 0.55f;
        public const string BehaviorMode    = "fierce-wise";
        public const string MythTrigger     = "elder";
        public const string PreferredPlanet = "SkySpiral";

        public const float R_Wonder       = 0.25f;
        public const float R_SpinStab     = 0.35f;
        public const float R_SourceAlign  = 0.40f;

        [Header("Harpy Eagle — Sphere Tuning")]
        [Range(1f, 3f)] public float lifespanScale     = 1.7f;
        [Range(0f, 1f)] public float riverSenseStrength = 0.28f;

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

            _sphere.bornDurationMin      = 55f  * lifespanScale;
            _sphere.bornDurationMax      = 130f * lifespanScale;
            _sphere.dyingDurationMin     = 10f;
            _sphere.dyingDurationMax     = 22f;
            _sphere.respawnDurationMin   = 7f;
            _sphere.respawnDurationMax   = 14f;
            _sphere.bornGrowthMultiplier = 2.4f;
        }

        private void OnEnable()
        {
            if (_sphere == null) return;
            _sphere.OnBorn  += HandleBorn;
            _sphere.OnDying += HandleDying;
        }

        private void OnDisable()
        {
            if (_sphere == null) return;
            _sphere.OnBorn  -= HandleBorn;
            _sphere.OnDying -= HandleDying;
        }

        private void Update()
        {
            // Harpy eagle senses the live river strongly — it is alert, watching now
            var river = UpsilonRiver.Instance;
            if (river != null && _sphere != null &&
                _sphere.CurrentPhase == RiverSpherePhase.Born)
            {
                _sphere.Sense(river.RiverField,       riverSenseStrength);
                _sphere.Sense(river.RiverMemoryField, riverSenseStrength * 0.3f);
            }
        }

        private void HandleBorn(VibrationalField field, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[HarpyEagleSphere] Born (life {lifeIndex}). " +
                          $"Dominant={field.DominantBandName()} " +
                          $"Coherence={field.Coherence():F3}");
        }

        private void HandleDying(VibrationalField field, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[HarpyEagleSphere] Dying (life {lifeIndex}). " +
                          $"Memory={_sphere.MemoryField.DominantBandName()} " +
                          $"SourceAlign={_sphere.MemoryField.SourceAlignment():F3}");
        }

        // CHARACTERISTIC FIELD
        //   red    = low-moderate  (fierce — not aggressive, but intense)
        //   orange = low           (not social — sovereign and solitary)
        //   yellow = moderate      (Air vitality)
        //   green  = moderate-high (Air's calm foundation + some breathCoherence implied)
        //   blue   = very high     (spinStability(0.35*0.45) + movementFlow over Air's blue(0.78))
        //   indigo = very high     (sourceAlignment(0.40*0.45) over Air's indigo(0.45))
        //   violet = high          (wonder(0.25*0.6) + sourceAlignment(0.40*0.4) over Air's violet)
        public static VibrationalField BuildCharacteristicField() =>
            new VibrationalField(
                red:    0.30f,
                orange: 0.22f,
                yellow: 0.42f,
                green:  0.62f,
                blue:   0.88f,  // spinStability dominates: precision and aerial mastery
                indigo: 0.72f,  // sourceAlignment lifts Air's already-high indigo
                violet: 0.78f   // wonder + source — the elder eye that sees everything
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
