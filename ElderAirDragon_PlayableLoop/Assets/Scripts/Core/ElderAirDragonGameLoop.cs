
using System;
using UnityEngine;
using ArtificialUniverse.Dragon;
using SpiritsCrossing;

namespace ArtificialUniverse.Core
{
    public class ElderAirDragonGameLoop : MonoBehaviour, IRealmController
    {
        // -------------------------------------------------------------------------
        // IRealmController
        // -------------------------------------------------------------------------
        public string RealmId => "SkyRealm";
        public string PlanetId => "SkySpiral";
        public event Action<RealmOutcome> OnRealmComplete;

        [Header("Realm Completion")]
        [Tooltip("Minimum session seconds before a completion can be triggered.")]
        public float minRealmDuration = 120f;
        [Range(0f, 1f)] public float completionResonanceThreshold = 0.70f;

        // -------------------------------------------------------------------------
        // Dragon references
        // -------------------------------------------------------------------------
        [SerializeField] private ElderAirDragonController playerDragon;
        [SerializeField] private ElderAirDragonStats playerStats;

        [Header("Loop Metrics")]
        [Range(0f, 1f)] public float skyFlow = 0.5f;
        [Range(0f, 1f)] public float stormPressure = 0.2f;
        [Range(0f, 1f)] public float elderPresence = 0.6f;

        // -------------------------------------------------------------------------
        // Internal state
        // -------------------------------------------------------------------------
        private PlayerResponseSample _entrySnapshot;
        private PlanetState          _planetState;
        private float                _realmTimer;
        private bool                 _realmActive;
        private bool                 _completed;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------
        private void Awake()
        {
            if (playerDragon == null) playerDragon = FindObjectOfType<ElderAirDragonController>();
            if (playerStats == null && playerDragon != null) playerStats = playerDragon.GetComponent<ElderAirDragonStats>();
        }

        private void Update()
        {
            if (playerStats == null || playerDragon == null) return;

            float motion = playerDragon.MotionAmount;
            bool  calm   = playerDragon.IsMeditating;

            skyFlow       = Mathf.Clamp01(skyFlow       + motion               * 0.002f - stormPressure * 0.001f);
            stormPressure = Mathf.Clamp01(stormPressure + (1f - playerStats.harmony) * 0.003f - (calm ? 0.004f : 0f));
            elderPresence = Mathf.Clamp01(elderPresence + playerStats.resonance * 0.002f - stormPressure * 0.001f);

            if (stormPressure > 0.8f)
                playerStats.ApplyStrain(0.04f * Time.deltaTime);

            if (calm && skyFlow > 0.5f)
                playerStats.ApplyResonance(0.02f * Time.deltaTime);

            if (_realmActive)
            {
                _realmTimer += Time.deltaTime;
                CheckCompletion();
            }
        }

        // -------------------------------------------------------------------------
        // IRealmController implementation
        // -------------------------------------------------------------------------
        public void BeginRealm(PlayerResponseSample playerSample, PlanetState planetState)
        {
            _entrySnapshot = playerSample;
            _planetState   = planetState;
            _realmTimer    = 0f;
            _realmActive   = true;
            _completed     = false;

            // Seed dragon harmony from player's persistent joy/calm
            if (playerStats != null)
            {
                playerStats.harmony   = Mathf.Clamp01(0.5f + playerSample.calmScore  * 0.3f);
                playerStats.resonance = Mathf.Clamp01(0.3f + playerSample.wonderScore * 0.4f);
            }

            // Planet history modifies starting sky flow
            if (planetState != null)
                skyFlow = Mathf.Clamp01(0.4f + planetState.growth * 0.3f);

            Debug.Log($"[ElderAirDragonGameLoop] Realm begun. Planet visits={planetState?.visitCount ?? 0}");
        }

        public RealmOutcome BuildOutcome()
        {
            float harmony   = playerStats != null ? playerStats.harmony   : 0.5f;
            float resonance = playerStats != null ? playerStats.resonance : 0.5f;

            float celebration = Mathf.Clamp01((harmony + skyFlow)     * 0.5f);
            float contrast    = Mathf.Clamp01(stormPressure);
            float importance  = Mathf.Clamp01(resonance * 0.6f + elderPresence * 0.4f);

            var outcome = new RealmOutcome
            {
                realmId        = RealmId,
                planetId       = PlanetId,
                celebration    = celebration,
                contrast       = contrast,
                importance     = importance,
                harmonyFinal   = harmony,
                resonanceFinal = resonance,
                skyFlowFinal   = skyFlow,
                realmCompleted = _completed,
                utcTimestamp   = DateTime.UtcNow.ToString("o")
            };

            // Emit elder myth trigger if presence is high
            if (elderPresence >= 0.75f)
                outcome.mythTriggerKeys.Add("elder");
            if (stormPressure >= 0.70f)
                outcome.mythTriggerKeys.Add("storm");
            if (skyFlow >= 0.80f)
                outcome.mythTriggerKeys.Add("sky");

            return outcome;
        }

        // -------------------------------------------------------------------------
        // Completion logic
        // -------------------------------------------------------------------------
        private void CheckCompletion()
        {
            if (_completed || _realmTimer < minRealmDuration) return;

            bool resonanceReached = playerStats != null &&
                                    playerStats.resonance >= completionResonanceThreshold;
            if (resonanceReached)
                CompleteRealm();
        }

        // Called externally (e.g. from a portal trigger or manual test) to end the realm.
        public void CompleteRealm()
        {
            if (_completed) return;
            _completed   = true;
            _realmActive = false;

            var outcome = BuildOutcome();
            Debug.Log($"[ElderAirDragonGameLoop] Realm complete. Celebration={outcome.celebration:F2} Contrast={outcome.contrast:F2}");
            OnRealmComplete?.Invoke(outcome);
        }
    }
}
