// SpiritsCrossing — ElderWhiteStagSphere.cs
// Tier 3 · Earth · sacred-ancient
//
// "The oldest living thing in the forest. Pure white. Does not bond quickly.
//  The Stag does not come to you — you become still enough that it no longer
//  needs to leave. The rarest earth bond."
//
// CHARACTERISTIC FIELD  [red, orange, yellow, green, blue, indigo, violet]
//   Earth elemental baseline shaped by: calm(0.35) + sourceAlignment(0.45) + breathCoherence(0.20)
//   → dominant green (stillness) + indigo (source sight) over Earth's warmth
//   The rarest bond threshold in the game: 0.58
//
// BOND  threshold=0.58  mythTrigger=elder  preferredPlanet=ForestHeart

using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.DragonRealms
{
    [RequireComponent(typeof(UpsilonSphereAI))]
    public class ElderWhiteStagSphere : MonoBehaviour
    {
        public const string AnimalId        = "elder_white_stag";
        public const string Element         = "Earth";
        public const int    Tier            = 3;
        public const float  BondThreshold   = 0.58f;  // rarest bond in the game
        public const string BehaviorMode    = "sacred-ancient";
        public const string MythTrigger     = "elder";
        public const string PreferredPlanet = "ForestHeart";

        public const float R_Calm           = 0.35f;
        public const float R_SourceAlign    = 0.45f;
        public const float R_BreathCoherence = 0.20f;

        [Header("Elder White Stag — Sphere Tuning")]
        [Range(1f, 3f)] public float lifespanScale     = 2.2f;
        [Range(0f, 1f)] public float riverSenseStrength = 0.15f; // very quiet — the stag does not seek

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

            // The stag lives the longest of all Earth creatures — geological patience
            _sphere.bornDurationMin      = 90f  * lifespanScale;
            _sphere.bornDurationMax      = 220f * lifespanScale;
            _sphere.dyingDurationMin     = 20f;
            _sphere.dyingDurationMax     = 40f;
            _sphere.respawnDurationMin   = 12f;
            _sphere.respawnDurationMax   = 22f;
            _sphere.bornGrowthMultiplier = 3.0f;  // the stag's maturity compounds the most
            _sphere.memoryDecay          = 0.9990f; // longest memory of any entity
            _sphere.inputGain            = 0.15f;   // very low — the stag barely notices most input
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
            // The stag barely senses the river — it is already older than the river
            var river = UpsilonRiver.Instance;
            if (river != null && _sphere != null &&
                _sphere.CurrentPhase == RiverSpherePhase.Born)
            {
                _sphere.Sense(river.RiverMemoryField, riverSenseStrength);
            }
        }

        private void HandleBorn(VibrationalField field, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[ElderWhiteStagSphere] The Stag appears (life {lifeIndex}). " +
                          $"Dominant={field.DominantBandName()} " +
                          $"SourceAlign={field.SourceAlignment():F3}");
        }

        private void HandleMemoryConsolidated(VibrationalField live,
                                               VibrationalField memory, int lifeIndex)
        {
            if (logBondEvents)
                Debug.Log($"[ElderWhiteStagSphere] Memory deepens (life {lifeIndex}). " +
                          $"LifetimeBand={_sphere.LifetimeMemoryBand()} " +
                          $"Growth={_sphere.Growth:F3}");
        }

        // CHARACTERISTIC FIELD
        //   red    = near-zero (the stag has no heat — only stillness)
        //   orange = moderate  (Earth's warmth — it is part of the forest community)
        //   yellow = moderate  (Earth's vitality — the forest itself is alive)
        //   green  = very high (calm(0.35*0.6) + breath(0.20*0.4) over Earth's green(0.90))
        //   blue   = very low  (Earth: no movement drive)
        //   indigo = high      (sourceAlignment(0.45*0.45) — the stag sees the source)
        //   violet = moderate  (subtle wonder — the ancient that has seen everything)
        public static VibrationalField BuildCharacteristicField() =>
            new VibrationalField(
                red:    0.04f,
                orange: 0.52f,  // Earth warmth
                yellow: 0.38f,
                green:  0.95f,  // highest green in the game — deep stillness + breath
                blue:   0.08f,
                indigo: 0.68f,  // sourceAlignment lifts Earth's indigo significantly
                violet: 0.32f   // the ancient does not announce its wonder — it simply is
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
