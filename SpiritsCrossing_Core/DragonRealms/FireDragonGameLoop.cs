// SpiritsCrossing — FireDragonGameLoop.cs
// Fire Realm — DarkContrast planet. The most adversarial of the four dragon realms.
//
// Elemental mechanics:
//   flamePressure  — rises when player has low harmony / high distortion
//                    reduced by willForce and spin
//   willForce      — grows from player spin + assertive movement
//                    the player's active response to the fire's challenge
//   phoenixRise    — willForce accumulated above flamePressure threshold
//                    the transformative breakthrough measure
//
// Completion: phoenixRise >= completionThreshold
// The realm requires the player to meet the fire directly, not avoid it.
// High distortion at entry is expected — that's the dragon's language.

using System;
using UnityEngine;
using SpiritsCrossing.DragonRealms;

namespace SpiritsCrossing
{
    public class FireDragonGameLoop : MonoBehaviour, IRealmController
    {
        public string RealmId => "FireRealm";
        public string PlanetId => "DarkContrast";
        public event Action<RealmOutcome> OnRealmComplete;

        [Header("Realm Completion")]
        public float minRealmDuration           = 90f;
        [Range(0f, 1f)] public float completionThreshold = 0.65f;

        [Header("Element Metrics — read-only")]
        [Range(0f, 1f)] public float flamePressure = 0.60f;
        [Range(0f, 1f)] public float willForce      = 0.10f;
        [Range(0f, 1f)] public float phoenixRise    = 0.00f;

        [Header("Tuning")]
        public float flamePressureRise   = 0.004f;  // pressure per second when unchecked
        public float willForceRise       = 0.003f;  // will per second from spin/motion
        public float phoenixAccumRate    = 0.002f;  // how fast phoenix builds over flame

        [SerializeField] private DragonStats stats;

        private PlayerResponseSample _entrySnapshot;
        private PlanetState          _planetState;
        private float                _realmTimer;
        private bool                 _realmActive;
        private bool                 _completed;

        private void Awake()
        {
            if (stats == null) stats = GetComponent<DragonStats>() ?? gameObject.AddComponent<DragonStats>();
        }

        private void Update()
        {
            if (!_realmActive || stats == null) return;

            _realmTimer += Time.deltaTime;
            TickElement();
            if (!_completed) CheckCompletion();
        }

        private void TickElement()
        {
            // flamePressure rises naturally (fire always pushes) but is dampened by willForce
            float pressureRise = flamePressureRise * (1f - stats.harmony) * (1f + (1f - stats.resonance) * 0.5f);
            float pressureDamp = willForce * 0.6f * Time.deltaTime;
            flamePressure = Mathf.Clamp01(flamePressure + pressureRise * Time.deltaTime - pressureDamp);

            // willForce grows from spin energy and elementalCharge
            float willGain = willForceRise * (stats.elementalCharge + stats.resonance) * Time.deltaTime;
            willForce = Mathf.Clamp01(willForce + willGain - flamePressure * 0.002f * Time.deltaTime);

            // phoenixRise accumulates when willForce > flamePressure (player overcoming fire)
            float phoenixDelta = (willForce - flamePressure) * phoenixAccumRate * Time.deltaTime;
            phoenixRise = Mathf.Clamp01(phoenixRise + phoenixDelta);

            // Heavy flame pressure strains dragon
            if (flamePressure > 0.80f)
                stats.ApplyStrain(0.05f * Time.deltaTime);

            // High will sustains resonance
            if (willForce > 0.50f)
                stats.ApplyResonance(0.025f * Time.deltaTime);

            stats.TickPassive(Time.deltaTime, flamePressure < 0.30f, willForce);
        }

        public void BeginRealm(PlayerResponseSample playerSample, PlanetState planetState)
        {
            _entrySnapshot = playerSample;
            _planetState   = planetState;
            _realmTimer    = 0f;
            _realmActive   = true;
            _completed     = false;

            // Fire dragon seeds harder if player brings distortion — fire recognises intensity
            float fireAffinity = playerSample.spinScore * 0.5f + playerSample.distortionScore * 0.3f;
            flamePressure = Mathf.Clamp01(0.55f + (1f - fireAffinity) * 0.25f);
            willForce     = Mathf.Clamp01(0.05f + fireAffinity * 0.30f);
            phoenixRise   = 0f;

            // Planet history: if visited before, fire has been somewhat tamed
            if (planetState != null && planetState.visitCount > 0)
                flamePressure = Mathf.Clamp01(flamePressure - planetState.healing * 0.2f);

            stats.SeedFromPlayer(playerSample, harmonyBase: 0.35f, resonanceBase: 0.20f);
            Debug.Log($"[FireDragonGameLoop] Realm begun. flamePressure={flamePressure:F2} visits={planetState?.visitCount ?? 0}");
        }

        public RealmOutcome BuildOutcome()
        {
            float celebration = Mathf.Clamp01(phoenixRise * 1.2f);  // phoenix = success
            float contrast    = Mathf.Clamp01(flamePressure * 0.8f + (1f - stats.harmony) * 0.2f);
            float importance  = Mathf.Clamp01(willForce * 0.5f + phoenixRise * 0.5f);

            var outcome = new RealmOutcome
            {
                realmId        = RealmId,
                planetId       = PlanetId,
                celebration    = celebration,
                contrast       = contrast,
                importance     = importance,
                harmonyFinal   = stats.harmony,
                resonanceFinal = stats.resonance,
                skyFlowFinal   = 0f,
                realmCompleted = _completed,
                utcTimestamp   = DateTime.UtcNow.ToString("o")
            };

            if (phoenixRise >= 0.70f)  outcome.mythTriggerKeys.Add("fire");
            if (phoenixRise >= 0.85f)  outcome.mythTriggerKeys.Add("elder");
            if (flamePressure >= 0.70f) outcome.mythTriggerKeys.Add("storm");

            return outcome;
        }

        private void CheckCompletion()
        {
            if (_realmTimer < minRealmDuration) return;
            if (phoenixRise >= completionThreshold) CompleteRealm();
        }

        public void CompleteRealm()
        {
            if (_completed) return;
            _completed   = true;
            _realmActive = false;
            var outcome = BuildOutcome();
            Debug.Log($"[FireDragonGameLoop] Realm complete. Phoenix={phoenixRise:F2} Flame={flamePressure:F2}");
            OnRealmComplete?.Invoke(outcome);
        }
    }
}
