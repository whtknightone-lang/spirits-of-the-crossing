// SpiritsCrossing — MachineRealmGameLoop.cs
// Machine Realm — MachineOrder planet. The realm of pattern, precision, and rhythm.
//
// This is not destruction or cold technology. The machine is alive. It is the
// universe's memory of structure — the skeleton that all rhythm hangs on.
// The player who enters here discovers that order is not control. It is dance.
//
// Elemental mechanics:
//   patternSync    — rises with spin stability and pair sync (rhythmic precision).
//                    The machine listens for repeating, stable patterns in the
//                    player's movement. Erratic input disrupts it; consistent
//                    rhythm builds it.
//
//   gearMomentum   — accumulated precision over time. Like a flywheel —
//                    once spinning, it sustains itself. But it takes effort
//                    to start. Rewards players who commit to a rhythm.
//
//   systemResonance — patternSync * gearMomentum — the whole machine hums.
//                     When the player's rhythm matches the machine's structure,
//                     hidden mechanisms activate and the world transforms.
//
//   harmonyLock     — how tightly the player's resonance matches the machine's
//                     expected frequency. A secondary measure that modifies
//                     gearMomentum growth.
//
// Completion: systemResonance >= completionThreshold sustained for 15 seconds.
// The Machine Realm rewards precision and commitment. It does not forgive
// inconsistency — but once the player locks in, the payoff is immediate.
//
// Seedling tier: becomes "The Workshop" — a playful tinkering space where
// clicking gears and humming machines respond to any rhythmic input.

using System;
using UnityEngine;
using SpiritsCrossing.DragonRealms;

namespace SpiritsCrossing
{
    public class MachineRealmGameLoop : MonoBehaviour, IRealmController
    {
        public string RealmId  => "MachineRealm";
        public string PlanetId => "MachineOrder";
        public event Action<RealmOutcome> OnRealmComplete;

        [Header("Realm Completion")]
        public float minRealmDuration             = 120f;
        [Range(0f, 1f)] public float completionThreshold = 0.55f;
        public float sustainRequired              = 15f;

        [Header("Element Metrics — read-only")]
        [Range(0f, 1f)] public float patternSync     = 0.00f;
        [Range(0f, 1f)] public float gearMomentum    = 0.00f;
        [Range(0f, 1f)] public float systemResonance = 0.00f;
        [Range(0f, 1f)] public float harmonyLock     = 0.00f;

        [Header("Tuning")]
        public float syncGrowthRate     = 0.0018f;
        public float syncDecayRate      = 0.0025f;  // machine punishes inconsistency
        public float momentumAccumRate  = 0.0012f;  // flywheel spins up slowly
        public float momentumDecayRate  = 0.0004f;  // but retains energy once spinning
        public float lockSensitivity    = 0.0020f;

        [Header("Rhythm Detection")]
        [Tooltip("Expected spin period in seconds. Player spin stability is measured against this.")]
        public float expectedRhythmPeriod = 4.0f;

        [SerializeField] private DragonStats stats;

        private PlayerResponseSample _entrySnapshot;
        private PlanetState          _planetState;
        private float                _realmTimer;
        private bool                 _realmActive;
        private bool                 _completed;
        private float                _sustainedTime;
        private float                _rhythmPhase;

        private void Awake()
        {
            if (stats == null) stats = GetComponent<DragonStats>() ?? gameObject.AddComponent<DragonStats>();
            ApplyAgeTierScaling();
        }

        private void Update()
        {
            if (!_realmActive || stats == null) return;

            _realmTimer  += Time.deltaTime;
            _rhythmPhase += Time.deltaTime;
            TickElement();
            if (!_completed) CheckCompletion();
        }

        private void TickElement()
        {
            // Machine's own rhythm — the heartbeat of the realm
            float machineRhythm = (Mathf.Sin((_rhythmPhase / expectedRhythmPeriod) * Mathf.PI * 2f) + 1f) * 0.5f;

            // patternSync: how well the player's stable, repeating input matches
            // Spin stability and pair sync are the primary signals
            float stabilitySignal = stats.resonance * 0.5f + stats.integrity * 0.3f +
                                    stats.elementalCharge * 0.2f;
            float syncGain  = syncGrowthRate * stabilitySignal *
                              (0.6f + machineRhythm * 0.4f) * Time.deltaTime;

            // Disruption: low harmony or sudden changes break the pattern
            float disruption = (1f - stats.harmony) * syncDecayRate * Time.deltaTime;
            patternSync = Mathf.Clamp01(patternSync + syncGain - disruption);

            // harmonyLock: how tightly the player matches the machine's expected frequency
            float lockTarget = Mathf.Abs(machineRhythm - patternSync) < 0.30f ? 1f : 0f;
            harmonyLock = Mathf.Lerp(harmonyLock, lockTarget, lockSensitivity * Time.deltaTime * 60f);

            // gearMomentum: the flywheel — builds slowly but retains energy
            // harmonyLock accelerates the build; poor lock causes slow drain
            float momentumGain = momentumAccumRate * patternSync *
                                 (0.5f + harmonyLock * 0.8f) * Time.deltaTime;
            float momentumDrain = momentumDecayRate * (1f - patternSync) * Time.deltaTime;
            gearMomentum = Mathf.Clamp01(gearMomentum + momentumGain - momentumDrain);

            // systemResonance: the synthesis — pattern and momentum together
            systemResonance = Mathf.Clamp01(patternSync * gearMomentum * 1.3f);

            // Machine rewards locked-in players
            if (patternSync > 0.45f)
                stats.ApplyResonance(0.010f * Time.deltaTime);
            if (systemResonance > 0.40f)
                stats.ApplyResonance(0.016f * Time.deltaTime);

            // Flywheel momentum reduces strain — the machine protects its own
            if (gearMomentum > 0.50f)
                stats.harmony = Mathf.Clamp01(stats.harmony + 0.005f * Time.deltaTime);

            stats.TickPassive(Time.deltaTime, patternSync > 0.35f, gearMomentum * 0.6f);

            // Sustained time
            if (systemResonance >= completionThreshold)
                _sustainedTime += Time.deltaTime;
            else
                _sustainedTime = Mathf.Max(0f, _sustainedTime - Time.deltaTime * 0.5f);
        }

        public void BeginRealm(PlayerResponseSample playerSample, PlanetState planetState)
        {
            _entrySnapshot = playerSample;
            _planetState   = planetState;
            _realmTimer    = 0f;
            _realmActive   = true;
            _completed     = false;
            _sustainedTime = 0f;
            _rhythmPhase   = 0f;

            // Machine realm rewards precision-oriented players
            float machineAffinity = playerSample.spinScore     * 0.40f +
                                    playerSample.pairSyncScore * 0.30f +
                                    playerSample.flowScore     * 0.30f;

            patternSync  = Mathf.Clamp01(0.00f + machineAffinity * 0.25f);
            gearMomentum = Mathf.Clamp01(0.00f + machineAffinity * 0.15f);
            harmonyLock  = 0f;

            // Planet memory: the machine remembers all previous configurations
            if (planetState != null && planetState.visitCount > 0)
            {
                patternSync  = Mathf.Clamp01(patternSync  + planetState.growth  * 0.20f);
                gearMomentum = Mathf.Clamp01(gearMomentum + planetState.healing * 0.15f);
            }

            stats.SeedFromPlayer(playerSample, harmonyBase: 0.50f, resonanceBase: 0.35f);
            stats.elementalCharge = Mathf.Clamp01(machineAffinity * 0.55f);

            ApplyAgeTierScaling();

            Debug.Log($"[MachineRealmGameLoop] Realm begun. patternSync={patternSync:F2} " +
                      $"momentum={gearMomentum:F2} visits={planetState?.visitCount ?? 0}");
        }

        public RealmOutcome BuildOutcome()
        {
            float celebration = Mathf.Clamp01(systemResonance * 1.2f + gearMomentum * 0.3f);
            float contrast    = Mathf.Clamp01((1f - patternSync) * 0.4f + (1f - stats.harmony) * 0.2f);
            float importance  = Mathf.Clamp01(gearMomentum * 0.5f + systemResonance * 0.5f);

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

            if (patternSync     >= 0.55f) outcome.mythTriggerKeys.Add("machine");
            if (gearMomentum    >= 0.60f) outcome.mythTriggerKeys.Add("ruin");
            if (systemResonance >= 0.70f) outcome.mythTriggerKeys.Add("elder");
            if (harmonyLock     >= 0.80f) outcome.mythTriggerKeys.Add("source");

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
            Debug.Log($"[MachineRealmGameLoop] Realm complete. System={systemResonance:F2} " +
                      $"Pattern={patternSync:F2} Momentum={gearMomentum:F2}");
            OnRealmComplete?.Invoke(outcome);
        }

        private void ApplyAgeTierScaling()
        {
            var config = UniverseStateManager.Instance?.Current?.AgeTierConfig;
            if (config == null) return;

            if (config.tier == AgeTier.Seedling)
            {
                // Workshop: playful tinkering, any rhythm counts
                minRealmDuration    = 60f;
                completionThreshold = 0.35f;
                sustainRequired     = 8f;
                syncGrowthRate     *= 2.0f;
                syncDecayRate      *= 0.3f;   // very forgiving
                momentumDecayRate  *= 0.2f;   // momentum doesn't drain
            }
            else if (config.tier == AgeTier.Explorer)
            {
                minRealmDuration    = 90f;
                completionThreshold = 0.45f;
                sustainRequired     = 12f;
                syncGrowthRate     *= 1.3f;
                syncDecayRate      *= 0.6f;
            }
        }
    }
}
