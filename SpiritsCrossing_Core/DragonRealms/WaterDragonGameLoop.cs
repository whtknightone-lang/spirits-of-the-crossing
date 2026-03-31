// SpiritsCrossing — WaterDragonGameLoop.cs
// Water Realm — WaterFlow planet. The most fluid and socially resonant realm.
//
// Elemental mechanics:
//   tidalFlow    — pulses like waves (natural oscillation + player flow input)
//                  high flow score sustains the tide, resistance disrupts it
//   currentDepth — slowly accumulates with sustained calm + breath coherence
//                  represents how deep the player descends into the ocean realm
//   deepResonance — tidalFlow * currentDepth — the synthesis of flowing presence
//
// Completion: deepResonance >= completionThreshold (sustained, not just peak)
// The realm rewards patience and fluidity — it cannot be rushed.

using System;
using UnityEngine;
using SpiritsCrossing.DragonRealms;

namespace SpiritsCrossing
{
    public class WaterDragonGameLoop : MonoBehaviour, IRealmController
    {
        public string RealmId => "OceanRealm";
        public string PlanetId => "WaterFlow";
        public event Action<RealmOutcome> OnRealmComplete;

        [Header("Realm Completion")]
        public float minRealmDuration            = 150f;   // water is slow and patient
        [Range(0f, 1f)] public float completionThreshold = 0.60f;

        [Header("Element Metrics — read-only")]
        [Range(0f, 1f)] public float tidalFlow    = 0.50f;
        [Range(0f, 1f)] public float currentDepth = 0.10f;
        [Range(0f, 1f)] public float deepResonance = 0.05f;

        [Header("Tuning")]
        public float tidalPeriod      = 8.0f;   // seconds per tidal cycle
        public float depthAccumRate   = 0.0015f; // slow depth accumulation per second

        [SerializeField] private DragonStats stats;

        private PlayerResponseSample _entrySnapshot;
        private PlanetState          _planetState;
        private float                _realmTimer;
        private bool                 _realmActive;
        private bool                 _completed;
        private float                _tidalPhase;
        private float                _sustainedDeepTime; // time spent above threshold

        private void Awake()
        {
            if (stats == null) stats = GetComponent<DragonStats>() ?? gameObject.AddComponent<DragonStats>();
        }

        private void Update()
        {
            if (!_realmActive || stats == null) return;

            _realmTimer  += Time.deltaTime;
            _tidalPhase  += Time.deltaTime;
            TickElement();
            if (!_completed) CheckCompletion();
        }

        private void TickElement()
        {
            // Natural tidal oscillation — the ocean breathes on its own schedule
            float naturalTide = (Mathf.Sin((_tidalPhase / tidalPeriod) * Mathf.PI * 2f) + 1f) * 0.5f;

            // Player flow input modulates the tide amplitude
            float flowBoost  = stats.resonance * 0.4f + stats.harmony * 0.3f;
            tidalFlow = Mathf.Clamp01(naturalTide * (0.6f + flowBoost * 0.6f));

            // Depth accumulates when the player is calm and coherent
            // Distortion / jerk pulls back toward the surface
            float depthGain = depthAccumRate * stats.harmony * (0.5f + tidalFlow * 0.5f) * Time.deltaTime;
            float depthLoss = 0.0005f * (1f - stats.integrity) * Time.deltaTime;
            currentDepth = Mathf.Clamp01(currentDepth + depthGain - depthLoss);

            // Deep resonance is the product of being present in the flow AND at depth
            deepResonance = Mathf.Clamp01(tidalFlow * currentDepth * 1.2f);

            // Tidal bonuses and penalties
            if (tidalFlow > 0.70f)
                stats.ApplyResonance(0.015f * Time.deltaTime);
            if (currentDepth > 0.60f)
                stats.ApplyResonance(0.010f * Time.deltaTime);

            stats.TickPassive(Time.deltaTime, currentDepth > 0.40f, tidalFlow * 0.5f);

            // Track sustained time at depth
            if (deepResonance >= completionThreshold)
                _sustainedDeepTime += Time.deltaTime;
            else
                _sustainedDeepTime = Mathf.Max(0f, _sustainedDeepTime - Time.deltaTime * 0.5f);
        }

        public void BeginRealm(PlayerResponseSample playerSample, PlanetState planetState)
        {
            _entrySnapshot    = playerSample;
            _planetState      = planetState;
            _realmTimer       = 0f;
            _realmActive      = true;
            _completed        = false;
            _tidalPhase       = 0f;
            _sustainedDeepTime = 0f;

            // Water dragon seeds more generously for fluid players
            float waterAffinity = playerSample.flowScore * 0.5f + playerSample.calmScore * 0.3f +
                                  playerSample.pairSyncScore * 0.2f;
            tidalFlow    = Mathf.Clamp01(0.40f + waterAffinity * 0.30f);
            currentDepth = Mathf.Clamp01(0.05f + waterAffinity * 0.15f);

            // Planet history rewards returning visitors
            if (planetState != null && planetState.visitCount > 0)
                currentDepth = Mathf.Clamp01(currentDepth + planetState.growth * 0.15f);

            // Myth memory: accumulated ocean myth means the water accepts the player sooner.
            // The tidal flow starts stronger and depth opens more readily.
            float oceanMythStrength = UniverseStateManager.Instance?.Current.mythState
                                          .GetStrength("ocean") ?? 0f;
            if (oceanMythStrength > 0f)
            {
                tidalFlow    = Mathf.Clamp01(tidalFlow    + oceanMythStrength * 0.15f);
                currentDepth = Mathf.Clamp01(currentDepth + oceanMythStrength * 0.12f);
            }

            stats.SeedFromPlayer(playerSample, harmonyBase: 0.55f, resonanceBase: 0.30f);
            Debug.Log($"[WaterDragonGameLoop] Realm begun. tidalFlow={tidalFlow:F2} " +
                      $"depth={currentDepth:F2} oceanMythBoost={oceanMythStrength:F2}");
        }

        public RealmOutcome BuildOutcome()
        {
            float celebration = Mathf.Clamp01(deepResonance * 1.1f + currentDepth * 0.4f);
            float contrast    = Mathf.Clamp01((1f - tidalFlow) * 0.5f + (1f - stats.harmony) * 0.2f);
            float importance  = Mathf.Clamp01(currentDepth * 0.6f + stats.resonance * 0.4f);

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

            if (currentDepth >= 0.65f)  outcome.mythTriggerKeys.Add("ocean");
            if (currentDepth >= 0.80f)  outcome.mythTriggerKeys.Add("source");
            if (deepResonance >= 0.70f) outcome.mythTriggerKeys.Add("elder");

            return outcome;
        }

        // Completion requires sustained time at depth, not just a momentary peak
        private void CheckCompletion()
        {
            if (_realmTimer < minRealmDuration) return;
            if (_sustainedDeepTime >= 12f) CompleteRealm(); // 12 seconds sustained
        }

        public void CompleteRealm()
        {
            if (_completed) return;
            _completed   = true;
            _realmActive = false;
            var outcome = BuildOutcome();
            Debug.Log($"[WaterDragonGameLoop] Realm complete. Depth={currentDepth:F2} DeepRes={deepResonance:F2}");
            OnRealmComplete?.Invoke(outcome);
        }
    }
}
