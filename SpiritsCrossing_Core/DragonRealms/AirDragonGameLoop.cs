// SpiritsCrossing — AirDragonGameLoop.cs
// Air Realm — SkySpiral planet. The lightest, most spontaneous of all dragon realms.
//
// Elemental mechanics:
//   skyFlow         — free movement expression. joy * movementFlow.
//                     Rises quickly when the player moves with joy and abandon.
//                     This is the Air Dragon's primary invitation.
//
//   breathExpansion — breath growing larger, more spacious. breathCoherence * wonder.
//                     The experience of breathing into something vast. Awareness
//                     opening outward.
//
//   spinLift        — spinning clarity rising into luminosity. spinStability lifted
//                     by skyFlow. When movement and precision align, the dragon rises.
//
//   skyResonance    — skyFlow * breathExpansion lifted by spinLift.
//                     The synthesis: free movement meeting spacious breath.
//
// Completion: skyResonance >= completionThreshold sustained for 10 seconds.
// The Air Dragon does not test commitment. It tests willingness to let go.
// It is the fastest teacher — if you move freely and breathe openly, it meets
// you immediately. If you grip or control, it disperses.
//
// Contrast with Earth: Earth asks you to descend. Air asks you to release.

using System;
using UnityEngine;
using SpiritsCrossing.DragonRealms;

namespace SpiritsCrossing
{
    public class AirDragonGameLoop : MonoBehaviour, IRealmController
    {
        public string RealmId  => "SkyRealm";
        public string PlanetId => "SkySpiral";
        public event Action<RealmOutcome> OnRealmComplete;

        [Header("Realm Completion")]
        public float minRealmDuration             = 90f;    // air asks for less time — it's present NOW
        [Range(0f, 1f)] public float completionThreshold = 0.50f;
        public float sustainRequired              = 10f;    // half Earth's 20s

        [Header("Element Metrics — read-only")]
        [Range(0f, 1f)] public float skyFlow         = 0.00f;
        [Range(0f, 1f)] public float breathExpansion = 0.00f;
        [Range(0f, 1f)] public float spinLift        = 0.00f;
        [Range(0f, 1f)] public float skyResonance    = 0.00f;

        [Header("Tuning")]
        public float flowGrowthRate      = 0.0020f;  // air grows fast
        public float flowDecayRate       = 0.0012f;  // and drops quickly when stillness breaks
        public float expansionAccumRate  = 0.0015f;
        public float spinLiftAccumRate   = 0.0010f;

        [SerializeField] private DragonStats stats;

        private PlayerResponseSample _entrySnapshot;
        private PlanetState          _planetState;
        private float                _realmTimer;
        private bool                 _realmActive;
        private bool                 _completed;
        private float                _sustainedTime;

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
            // skyFlow: free joyful movement — rises fast, falls fast
            // The Air Dragon rewards the player in the moment they release.
            float flowGain = flowGrowthRate * stats.resonance *
                             (stats.harmony * 0.6f + stats.integrity * 0.4f) * Time.deltaTime;
            float flowDecay = flowDecayRate * (1f - stats.resonance) * Time.deltaTime;
            skyFlow = Mathf.Clamp01(skyFlow + flowGain - flowDecay);

            // breathExpansion: spacious breath + open wonder
            // Requires resonance AND a baseline skyFlow (can't expand before you're moving)
            float expansionGain = expansionAccumRate * stats.resonance *
                                  (skyFlow > 0.20f ? 1.4f : 0.4f) * Time.deltaTime;
            breathExpansion = Mathf.Clamp01(breathExpansion + expansionGain - flowDecay * 0.6f);

            // spinLift: spinning precision rises when flow and expansion combine
            float liftGain = spinLiftAccumRate * skyFlow * breathExpansion * 1.5f * Time.deltaTime;
            spinLift = Mathf.Clamp01(spinLift + liftGain - flowDecay * 0.4f);

            // skyResonance: the synthesis
            // A small spinLift multiplier rewards precision-in-freedom
            skyResonance = Mathf.Clamp01(skyFlow * breathExpansion * (1.0f + spinLift * 0.5f));

            // Air rewards immediate expression
            if (skyFlow > 0.40f)
                stats.ApplyResonance(0.010f * Time.deltaTime);
            if (skyResonance > 0.35f)
                stats.ApplyResonance(0.014f * Time.deltaTime);

            stats.TickPassive(Time.deltaTime, skyFlow > 0.30f, 0.08f); // air ticks faster

            // Sustained time
            if (skyResonance >= completionThreshold)
                _sustainedTime += Time.deltaTime;
            else
                _sustainedTime = Mathf.Max(0f, _sustainedTime - Time.deltaTime * 0.6f);
            // Note: Air decays the sustained timer faster than Earth — it tests presence
            // in the moment, not commitment over time. 0.6x decay vs Earth's 0.3x.
        }

        public void BeginRealm(PlayerResponseSample playerSample, PlanetState planetState)
        {
            _entrySnapshot = playerSample;
            _planetState   = planetState;
            _realmTimer    = 0f;
            _realmActive   = true;
            _completed     = false;
            _sustainedTime = 0f;

            // Air dragon gives an immediate warm start to players who come with flow and joy
            float airAffinity = playerSample.flowScore         * 0.40f +
                                playerSample.joyScore          * 0.35f +
                                playerSample.wonderScore       * 0.25f;

            skyFlow         = Mathf.Clamp01(0.00f + airAffinity * 0.35f);
            breathExpansion = Mathf.Clamp01(0.00f + airAffinity * 0.25f);
            spinLift        = Mathf.Clamp01(0.00f + playerSample.spinScore * airAffinity * 0.20f);

            // Planet memory: the sky remembers those who danced here before
            if (planetState != null && planetState.visitCount > 0)
            {
                skyFlow         = Mathf.Clamp01(skyFlow         + planetState.growth  * 0.30f);
                breathExpansion = Mathf.Clamp01(breathExpansion + planetState.healing * 0.20f);
            }

            stats.SeedFromPlayer(playerSample, harmonyBase: 0.60f, resonanceBase: 0.40f);
            stats.elementalCharge = Mathf.Clamp01(airAffinity * 0.60f);
            // Air starts charged — it meets you where you are

            Debug.Log($"[AirDragonGameLoop] Realm begun. skyFlow={skyFlow:F2} " +
                      $"breathExpansion={breathExpansion:F2} visits={planetState?.visitCount ?? 0}");
        }

        public RealmOutcome BuildOutcome()
        {
            float celebration = Mathf.Clamp01(skyResonance * 1.3f + skyFlow * 0.3f);
            float contrast    = Mathf.Clamp01((1f - skyFlow) * 0.25f + (1f - stats.harmony) * 0.20f);
            float importance  = Mathf.Clamp01(breathExpansion * 0.5f + skyResonance * 0.5f);

            var outcome = new RealmOutcome
            {
                realmId        = RealmId,
                planetId       = PlanetId,
                celebration    = celebration,
                contrast       = contrast,
                importance     = importance,
                harmonyFinal   = stats.harmony,
                resonanceFinal = stats.resonance,
                skyFlowFinal   = skyFlow,
                realmCompleted = _completed,
                utcTimestamp   = DateTime.UtcNow.ToString("o")
            };

            if (skyFlow        >= 0.55f) outcome.mythTriggerKeys.Add("sky");
            if (breathExpansion >= 0.50f) outcome.mythTriggerKeys.Add("forest");  // open breath
            if (spinLift       >= 0.45f) outcome.mythTriggerKeys.Add("storm");    // precision-in-freedom
            if (skyResonance   >= 0.65f) outcome.mythTriggerKeys.Add("elder");
            if (skyResonance   >= 0.80f) outcome.mythTriggerKeys.Add("source");

            return outcome;
        }

        private void CheckCompletion()
        {
            if (_realmTimer < minRealmDuration) return;
            if (_sustainedTime >= sustainRequired) CompleteRealm();
        }

        public void CompleteRealm()
        {
            if (_completed) return;
            _completed   = true;
            _realmActive = false;
            var outcome = BuildOutcome();
            Debug.Log($"[AirDragonGameLoop] Realm complete. Sky={skyResonance:F2} Flow={skyFlow:F2} Lift={spinLift:F2}");
            OnRealmComplete?.Invoke(outcome);
        }
    }
}
