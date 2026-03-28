// SpiritsCrossing — EarthDragonGameLoop.cs
// Earth Realm — ForestHeart planet. The slowest, most patient of all dragon realms.
//
// Elemental mechanics:
//   rootDepth     — grows only through sustained stillness and deep breathing
//                   the earth dragon tests commitment, not performance
//   ancientEcho   — breathCoherence * wonder — awareness of deep geological time
//                   activated by breath holds and openness
//   forestResonance — rootDepth * ancientEcho — when presence meets memory
//
// Completion: forestResonance >= completionThreshold sustained for 20 seconds
// The Earth Dragon is the most patient teacher. It does not meet the player —
// the player must descend to meet IT.

using System;
using UnityEngine;
using SpiritsCrossing.DragonRealms;

namespace SpiritsCrossing
{
    public class EarthDragonGameLoop : MonoBehaviour, IRealmController
    {
        public string RealmId => "ForestRealm";
        public string PlanetId => "ForestHeart";
        public event Action<RealmOutcome> OnRealmComplete;

        [Header("Realm Completion")]
        public float minRealmDuration            = 180f;   // earth asks for deep time
        [Range(0f, 1f)] public float completionThreshold = 0.55f;
        public float sustainRequired             = 20f;   // seconds of sustained resonance

        [Header("Element Metrics — read-only")]
        [Range(0f, 1f)] public float rootDepth       = 0.00f;
        [Range(0f, 1f)] public float ancientEcho      = 0.00f;
        [Range(0f, 1f)] public float forestResonance  = 0.00f;

        [Header("Tuning")]
        public float rootGrowthRate   = 0.0008f;  // very slow — earth time
        public float rootDecayRate    = 0.0003f;  // loses depth if disturbed
        public float echoAccumRate    = 0.0012f;

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
            // rootDepth grows only when the player is genuinely still and harmonious
            float stillnessSignal = stats.harmony * stats.integrity;
            float rootGain = rootGrowthRate * stillnessSignal * (0.5f + stats.resonance * 0.5f) * Time.deltaTime;

            // Any disharmony gently erodes depth — but not sharply
            float disturbance = (1f - stats.harmony) * rootDecayRate * Time.deltaTime;
            rootDepth = Mathf.Clamp01(rootDepth + rootGain - disturbance);

            // ancientEcho requires breath coherence AND wonder together
            // It is the resonance of sensing deep geological time
            float echoGain = echoAccumRate * stats.resonance *
                             (rootDepth > 0.30f ? 1.5f : 0.5f) * Time.deltaTime;
            ancientEcho = Mathf.Clamp01(ancientEcho + echoGain - disturbance * 0.5f);

            // Forest resonance — the synthesis: presence at depth meeting ancient memory
            forestResonance = Mathf.Clamp01(rootDepth * ancientEcho * 1.3f);

            // Earth rewards the patient
            if (rootDepth > 0.50f)
                stats.ApplyResonance(0.008f * Time.deltaTime);
            if (forestResonance > 0.40f)
                stats.ApplyResonance(0.010f * Time.deltaTime);

            stats.TickPassive(Time.deltaTime, rootDepth > 0.30f, 0.05f);

            // Sustained time tracking
            if (forestResonance >= completionThreshold)
                _sustainedTime += Time.deltaTime;
            else
                _sustainedTime = Mathf.Max(0f, _sustainedTime - Time.deltaTime * 0.3f);
        }

        public void BeginRealm(PlayerResponseSample playerSample, PlanetState planetState)
        {
            _entrySnapshot = playerSample;
            _planetState   = planetState;
            _realmTimer    = 0f;
            _realmActive   = true;
            _completed     = false;
            _sustainedTime = 0f;

            // Earth dragon rewards those who come with stillness
            float earthAffinity = playerSample.stillnessScore * 0.5f +
                                  playerSample.sourceAlignmentScore * 0.3f +
                                  playerSample.wonderScore * 0.2f;
            rootDepth    = Mathf.Clamp01(0.00f + earthAffinity * 0.20f);
            ancientEcho  = Mathf.Clamp01(0.00f + earthAffinity * 0.15f);

            // Planet memory: each visit, the earth remembers — deeper start
            if (planetState != null && planetState.visitCount > 0)
            {
                rootDepth   = Mathf.Clamp01(rootDepth   + planetState.growth   * 0.25f);
                ancientEcho = Mathf.Clamp01(ancientEcho + planetState.healing  * 0.15f);
            }

            stats.SeedFromPlayer(playerSample, harmonyBase: 0.65f, resonanceBase: 0.35f);
            stats.elementalCharge = Mathf.Clamp01(earthAffinity * 0.5f);

            Debug.Log($"[EarthDragonGameLoop] Realm begun. rootDepth={rootDepth:F2} visits={planetState?.visitCount ?? 0}");
        }

        public RealmOutcome BuildOutcome()
        {
            float celebration = Mathf.Clamp01(forestResonance * 1.2f + rootDepth * 0.4f);
            float contrast    = Mathf.Clamp01((1f - rootDepth) * 0.3f + (1f - stats.harmony) * 0.2f);
            float importance  = Mathf.Clamp01(rootDepth * 0.5f + ancientEcho * 0.5f);

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

            if (rootDepth   >= 0.60f) outcome.mythTriggerKeys.Add("forest");
            if (ancientEcho >= 0.55f) outcome.mythTriggerKeys.Add("ruin");
            if (rootDepth   >= 0.75f) outcome.mythTriggerKeys.Add("elder");
            if (forestResonance >= 0.65f) outcome.mythTriggerKeys.Add("source");

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
            Debug.Log($"[EarthDragonGameLoop] Realm complete. Root={rootDepth:F2} Forest={forestResonance:F2}");
            OnRealmComplete?.Invoke(outcome);
        }
    }
}
