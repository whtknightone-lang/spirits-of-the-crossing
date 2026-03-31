// SpiritsCrossing — SourceRealmGameLoop.cs
// Source Realm — SourceVeil planet. The deepest and quietest of all realms.
//
// This is not a dragon realm. There is no dragon here. There is only
// the origin — the vibration beneath all vibrations. The player does not
// fight, explore, or perform. They arrive, and they are still.
//
// Elemental mechanics:
//   innerStillness  — grows only through sustained breath coherence and calm.
//                     Movement, spin, and social input SLOW its growth.
//                     This is a measure of how completely the player has stopped.
//
//   sourceAlignment — innerStillness * sourceAlignmentScore from the player.
//                     When stillness meets intentional alignment with the origin,
//                     the realm responds. This cannot be faked by inactivity alone —
//                     the biometric layer (or input proxy) must show coherent breath.
//
//   veilThinning    — sourceAlignment accumulated over deep time.
//                     The boundary between player and source becomes transparent.
//                     Once thin enough, the player perceives the origin directly.
//                     This is the game's most sacred metric.
//
//   communionDepth  — veilThinning * innerStillness — the synthesis.
//                     When the veil is thin AND the player is still, communion happens.
//
// Completion: communionDepth >= completionThreshold sustained for 30 seconds.
// The Source Realm is the slowest completion in the game. It cannot be rushed,
// optimised, or cheated. It is the experience of being fully present.
//
// Seedling tier: threshold lowered, time shortened, companion narrates gently.
// The Dreamspring variant is warm and safe, never silent — always accompanied.

using System;
using UnityEngine;
using SpiritsCrossing.DragonRealms;

namespace SpiritsCrossing
{
    public class SourceRealmGameLoop : MonoBehaviour, IRealmController
    {
        public string RealmId  => "SourceRealm";
        public string PlanetId => "SourceVeil";
        public event Action<RealmOutcome> OnRealmComplete;

        [Header("Realm Completion")]
        public float minRealmDuration             = 240f;   // source asks for the most time
        [Range(0f, 1f)] public float completionThreshold = 0.45f;
        public float sustainRequired              = 30f;    // longest sustained hold of all realms

        [Header("Element Metrics — read-only")]
        [Range(0f, 1f)] public float innerStillness  = 0.00f;
        [Range(0f, 1f)] public float sourceAlignment = 0.00f;
        [Range(0f, 1f)] public float veilThinning    = 0.00f;
        [Range(0f, 1f)] public float communionDepth  = 0.00f;

        [Header("Tuning")]
        public float stillnessGrowthRate  = 0.0006f;  // the slowest growth in the game
        public float stillnessDecayRate   = 0.0020f;  // movement destroys stillness fast
        public float alignmentAccumRate   = 0.0010f;
        public float veilAccumRate        = 0.0004f;  // veil thins very slowly

        [Header("Disturbance Sensitivity")]
        [Tooltip("How strongly movement/spin disrupts stillness. Higher = more fragile.")]
        public float movementPenalty = 0.80f;
        public float spinPenalty     = 0.40f;
        public float socialPenalty   = 0.20f;  // social input is a gentle disturbance

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
            ApplyAgeTierScaling();
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
            // innerStillness: the core measure. Grows when the player is genuinely still.
            // Any movement, spin, or social input decays it.
            float calmSignal   = stats.harmony * stats.integrity;
            float stillGain    = stillnessGrowthRate * calmSignal *
                                 (0.3f + stats.resonance * 0.7f) * Time.deltaTime;

            // Disturbance: movement and spin actively destroy stillness
            float disturbance  = (1f - stats.harmony) * movementPenalty +
                                 stats.elementalCharge * spinPenalty;
            float stillDecay   = stillnessDecayRate * disturbance * Time.deltaTime;

            innerStillness = Mathf.Clamp01(innerStillness + stillGain - stillDecay);

            // sourceAlignment: stillness meeting intentional source connection
            // Requires innerStillness above a baseline — can't align without being still first
            float alignGain = alignmentAccumRate * stats.resonance *
                              (innerStillness > 0.25f ? 1.5f : 0.3f) * Time.deltaTime;
            float alignDecay = stillDecay * 0.4f;
            sourceAlignment = Mathf.Clamp01(sourceAlignment + alignGain - alignDecay);

            // veilThinning: the slowest accumulator. Source alignment thinning the boundary.
            // Once thin, it stays thin — the veil doesn't rebuild quickly.
            float veilGain = veilAccumRate * sourceAlignment *
                             (innerStillness > 0.40f ? 2.0f : 0.5f) * Time.deltaTime;
            float veilRecover = 0.0001f * (1f - innerStillness) * Time.deltaTime;
            veilThinning = Mathf.Clamp01(veilThinning + veilGain - veilRecover);

            // communionDepth: the synthesis — veil thin AND player still
            communionDepth = Mathf.Clamp01(veilThinning * innerStillness * 1.4f);

            // Source rewards the absolutely present
            if (innerStillness > 0.50f)
                stats.ApplyResonance(0.006f * Time.deltaTime);
            if (communionDepth > 0.30f)
                stats.ApplyResonance(0.012f * Time.deltaTime);

            // Passive tick: source realm is always calm — the dragon doesn't push back
            stats.TickPassive(Time.deltaTime, innerStillness > 0.20f, 0.02f);

            // Sustained communion tracking
            if (communionDepth >= completionThreshold)
                _sustainedTime += Time.deltaTime;
            else
                _sustainedTime = Mathf.Max(0f, _sustainedTime - Time.deltaTime * 0.15f);
            // Note: very slow decay — once you reach communion, brief disturbances
            // don't reset you completely. The source is forgiving.
        }

        public void BeginRealm(PlayerResponseSample playerSample, PlanetState planetState)
        {
            _entrySnapshot = playerSample;
            _planetState   = planetState;
            _realmTimer    = 0f;
            _realmActive   = true;
            _completed     = false;
            _sustainedTime = 0f;

            // Source realm gives a warm start to players who arrive with stillness and alignment
            float sourceAffinity = playerSample.stillnessScore       * 0.40f +
                                   playerSample.sourceAlignmentScore * 0.35f +
                                   playerSample.calmScore            * 0.25f;

            innerStillness  = Mathf.Clamp01(0.00f + sourceAffinity * 0.25f);
            sourceAlignment = Mathf.Clamp01(0.00f + sourceAffinity * 0.20f);
            veilThinning    = 0f;

            // Planet memory: the source remembers everyone who has been here
            if (planetState != null && planetState.visitCount > 0)
            {
                innerStillness  = Mathf.Clamp01(innerStillness  + planetState.healing * 0.20f);
                sourceAlignment = Mathf.Clamp01(sourceAlignment + planetState.growth  * 0.15f);
                // Returning visitors: the veil is already slightly thinner
                veilThinning    = Mathf.Clamp01(planetState.growth * 0.10f);
            }

            stats.SeedFromPlayer(playerSample, harmonyBase: 0.75f, resonanceBase: 0.45f);
            // Source starts harmonious — this is a place of peace, not challenge
            stats.elementalCharge = 0.10f; // low charge — no elemental aggression

            ApplyAgeTierScaling();

            Debug.Log($"[SourceRealmGameLoop] Realm begun. stillness={innerStillness:F2} " +
                      $"alignment={sourceAlignment:F2} visits={planetState?.visitCount ?? 0}");
        }

        public RealmOutcome BuildOutcome()
        {
            float celebration = Mathf.Clamp01(communionDepth * 1.5f + veilThinning * 0.3f);
            float contrast    = Mathf.Clamp01((1f - innerStillness) * 0.15f);
            // Source has almost no contrast — it is the absence of struggle
            float importance  = Mathf.Clamp01(veilThinning * 0.6f + sourceAlignment * 0.4f);

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

            if (innerStillness  >= 0.60f) outcome.mythTriggerKeys.Add("source");
            if (veilThinning    >= 0.40f) outcome.mythTriggerKeys.Add("elder");
            if (communionDepth  >= 0.50f) outcome.mythTriggerKeys.Add("ruin");
            if (communionDepth  >= 0.70f) outcome.mythTriggerKeys.Add("rebirth");

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
            Debug.Log($"[SourceRealmGameLoop] Realm complete. Communion={communionDepth:F2} " +
                      $"Veil={veilThinning:F2} Stillness={innerStillness:F2}");
            OnRealmComplete?.Invoke(outcome);
        }

        /// <summary>Scale thresholds and timing for younger age tiers.</summary>
        private void ApplyAgeTierScaling()
        {
            var config = UniverseStateManager.Instance?.Current?.AgeTierConfig;
            if (config == null) return;

            if (config.tier == AgeTier.Seedling)
            {
                // Dreamspring: shorter, warmer, easier to reach communion
                minRealmDuration     = 90f;
                completionThreshold  = 0.30f;
                sustainRequired      = 12f;
                stillnessGrowthRate *= 2.0f;
                movementPenalty     *= 0.4f; // much more forgiving of fidgeting
            }
            else if (config.tier == AgeTier.Explorer)
            {
                minRealmDuration     = 150f;
                completionThreshold  = 0.38f;
                sustainRequired      = 20f;
                stillnessGrowthRate *= 1.4f;
                movementPenalty     *= 0.7f;
            }
        }
    }
}
