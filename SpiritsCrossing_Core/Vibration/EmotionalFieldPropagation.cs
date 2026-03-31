// SpiritsCrossing — EmotionalFieldPropagation.cs
// The central nervous system for the emotional-spiritual spectrum.
//
// Every frame:
//   1. Reads player's live PlayerResonanceState
//   2. Computes EmotionalResonanceSpectrum from it
//   3. Applies current planet's emotional modulation
//   4. Smoothly tracks the modulated spectrum (organic inertia)
//   5. Propagates into VibrationalResonanceSystem (7-band boosts)
//   6. Modulates CompanionBondSystem growth rate by emotional depth
//   7. Fires events on spectrum shifts, full spectrum, depth thresholds
//
// Setup: Add to Bootstrap scene alongside GameBootstrapper.
//        Loads planet_emotional_profiles.json from StreamingAssets.

using System;
using System.Collections;
using UnityEngine;
using SpiritsCrossing.SpiritAI;
using SpiritsCrossing.Companions;
using SpiritsCrossing.Memory;

namespace SpiritsCrossing.Vibration
{
    public class EmotionalFieldPropagation : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------
        public static EmotionalFieldPropagation Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -----------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------
        [Header("Smoothing")]
        [Tooltip("How quickly the emotional spectrum tracks the raw signal. " +
                 "Lower = more organic inertia.")]
        [Range(0.5f, 10f)] public float spectrumSmoothSpeed = 2.5f;

        [Header("Propagation")]
        [Tooltip("How strongly the emotional spectrum boosts vibrational bands. " +
                 "0 = no boost, 1 = full boost each frame.")]
        [Range(0f, 0.10f)] public float vibrationalBoostRate = 0.03f;

        [Tooltip("How much emotional depth multiplies companion bond growth. " +
                 "At depth 0 → 1.0x, at depth 1 → (1 + this)x.")]
        [Range(0f, 2f)] public float companionDepthMultiplier = 0.8f;

        [Header("Full Spectrum Threshold")]
        [Tooltip("Minimum per-band value for full spectrum detection.")]
        [Range(0.1f, 0.5f)] public float fullSpectrumThreshold = 0.30f;

        [Header("Depth Thresholds")]
        public float[] depthThresholds = { 0.4f, 0.6f, 0.8f, 1.0f };

        [Header("Debug")]
        public bool logEverySecond;

        // -----------------------------------------------------------------
        // Events
        // -----------------------------------------------------------------

        /// <summary>Dominant emotional band changed (e.g. "stillness" → "joy").</summary>
        public event Action<string, string> OnSpectrumShift; // fromBand, toBand

        /// <summary>All 5 bands above fullSpectrumThreshold simultaneously.</summary>
        public event Action<float> OnFullSpectrumReached; // coherence

        /// <summary>Full spectrum state ended.</summary>
        public event Action OnFullSpectrumLost;

        /// <summary>Emotional depth crossed a threshold.</summary>
        public event Action<int, float> OnDepthThreshold; // thresholdIndex, depth

        /// <summary>Fires every update with the live modulated spectrum for listeners.</summary>
        public event Action<EmotionalResonanceSpectrum> OnSpectrumUpdated;

        // -----------------------------------------------------------------
        // Public state
        // -----------------------------------------------------------------

        /// <summary>The raw (unmodulated) emotional spectrum from physiology.</summary>
        public EmotionalResonanceSpectrum RawSpectrum { get; private set; }
            = new EmotionalResonanceSpectrum();

        /// <summary>The smoothed, planet-modulated emotional spectrum.</summary>
        public EmotionalResonanceSpectrum LiveSpectrum { get; private set; }
            = new EmotionalResonanceSpectrum();

        /// <summary>Current planet's emotional profile (null if not loaded).</summary>
        public PlanetEmotionalProfile CurrentPlanetProfile { get; private set; }

        /// <summary>Is the player currently in full spectrum state?</summary>
        public bool InFullSpectrum { get; private set; }

        /// <summary>Highest depth threshold crossed so far this session.</summary>
        public int HighestDepthThreshold { get; private set; } = -1;

        // -----------------------------------------------------------------
        // Internal
        // -----------------------------------------------------------------
        private SpiritBrainOrchestrator          _orchestrator;
        private PlanetEmotionalProfileCollection _profiles;
        private string                           _prevDominantBand;
        private bool                             _prevFullSpectrum;
        private float                            _debugTimer;
        private string                           _currentPlanetId;

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return null; // let other systems load
            _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();
            _profiles     = PlanetEmotionalProfileLoader.Load();

            // Try to pick up the initial planet from UniverseState
            RefreshCurrentPlanet();
        }

        private void Update()
        {
            UpdateRawSpectrum();
            ApplyPlanetModulation();
            SmoothSpectrum();
            PropagateToVibrationalSystem();
            PropagateToCompanionSystem();
            DetectEvents();
            OnSpectrumUpdated?.Invoke(LiveSpectrum);

            if (logEverySecond)
            {
                _debugTimer += Time.deltaTime;
                if (_debugTimer >= 1f)
                {
                    _debugTimer = 0f;
                    Debug.Log($"[EmotionalFieldPropagation] {LiveSpectrum}");
                }
            }
        }

        // -----------------------------------------------------------------
        // Step 1: Compute raw emotional spectrum from player physiology
        // -----------------------------------------------------------------
        private void UpdateRawSpectrum()
        {
            if (_orchestrator == null)
                _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();

            var state = _orchestrator?.CurrentPlayerState;
            if (state == null) return;

            RawSpectrum = EmotionalResonanceSpectrum.FromPlayerResonanceState(state);
        }

        // -----------------------------------------------------------------
        // Step 2: Apply current planet's emotional modulation
        // -----------------------------------------------------------------
        private EmotionalResonanceSpectrum _modulatedTarget = new EmotionalResonanceSpectrum();

        private void ApplyPlanetModulation()
        {
            RefreshCurrentPlanet();

            if (CurrentPlanetProfile != null)
                _modulatedTarget = CurrentPlanetProfile.ModulateSpectrum(RawSpectrum);
            else
                _modulatedTarget = RawSpectrum;
        }

        // -----------------------------------------------------------------
        // Step 3: Smooth toward modulated target (organic inertia)
        // -----------------------------------------------------------------
        private void SmoothSpectrum()
        {
            LiveSpectrum.LerpToward(_modulatedTarget, Time.deltaTime * spectrumSmoothSpeed);
        }

        // -----------------------------------------------------------------
        // Step 4: Propagate into 7-band VibrationalResonanceSystem
        // -----------------------------------------------------------------
        private void PropagateToVibrationalSystem()
        {
            var vrs = VibrationalResonanceSystem.Instance;
            if (vrs == null) return;

            // Map emotional spectrum to vibrational field boosts
            VibrationalField emotionalField = LiveSpectrum.ToVibrationalField();
            float rate = vibrationalBoostRate;

            // Apply transient boosts — the existing system's lerp will naturally absorb them
            vrs.ApplyTransientBoost("red",    emotionalField.red    * rate);
            vrs.ApplyTransientBoost("orange", emotionalField.orange * rate);
            vrs.ApplyTransientBoost("yellow", emotionalField.yellow * rate);
            vrs.ApplyTransientBoost("green",  emotionalField.green  * rate);
            vrs.ApplyTransientBoost("blue",   emotionalField.blue   * rate);
            vrs.ApplyTransientBoost("indigo", emotionalField.indigo * rate);
            vrs.ApplyTransientBoost("violet", emotionalField.violet * rate);
        }

        // -----------------------------------------------------------------
        // Step 5: Modulate companion bond growth by emotional depth
        // -----------------------------------------------------------------
        private float _baseBondGrowthRate = -1f;

        private void PropagateToCompanionSystem()
        {
            var cbs = CompanionBondSystem.Instance;
            if (cbs == null) return;

            // Capture the base rate once so we don't compound every frame
            if (_baseBondGrowthRate < 0f)
                _baseBondGrowthRate = cbs.bondGrowthRate;

            // Emotional depth multiplier: deeper spectrum = faster bonding
            // Base rate stays at its original value; depth adds up to companionDepthMultiplier
            float depthBoost = 1f + LiveSpectrum.Depth * companionDepthMultiplier;
            cbs.bondGrowthRate = Mathf.Clamp(_baseBondGrowthRate * depthBoost, 0.1f, 5f);
        }

        // -----------------------------------------------------------------
        // Step 6: Detect events — spectrum shifts, full spectrum, depth
        // -----------------------------------------------------------------
        private void DetectEvents()
        {
            // Dominant band shift
            string dominant = LiveSpectrum.DominantBandName;
            if (_prevDominantBand != null && dominant != _prevDominantBand)
            {
                OnSpectrumShift?.Invoke(_prevDominantBand, dominant);
                Debug.Log($"[EmotionalFieldPropagation] Spectrum shift: {_prevDominantBand} → {dominant}");
            }
            _prevDominantBand = dominant;

            // Full spectrum detection
            bool isFullNow = LiveSpectrum.IsFullSpectrum(fullSpectrumThreshold);
            if (isFullNow && !_prevFullSpectrum)
            {
                InFullSpectrum = true;
                OnFullSpectrumReached?.Invoke(LiveSpectrum.Coherence);
                Debug.Log($"[EmotionalFieldPropagation] ★ Full Spectrum reached! " +
                          $"Coherence={LiveSpectrum.Coherence:F3}");

                // Activate source + elder myth at significant strength
                var myth = UniverseStateManager.Instance?.Current?.mythState;
                myth?.Activate("source", "emotional_spectrum", LiveSpectrum.Coherence);
                myth?.Activate("elder",  "emotional_spectrum", LiveSpectrum.Coherence * 0.7f);
            }
            else if (!isFullNow && _prevFullSpectrum)
            {
                InFullSpectrum = false;
                OnFullSpectrumLost?.Invoke();
            }
            _prevFullSpectrum = isFullNow;

            // Depth threshold crossings
            float depth = LiveSpectrum.Depth;
            for (int i = 0; i < depthThresholds.Length; i++)
            {
                if (i > HighestDepthThreshold && depth >= depthThresholds[i])
                {
                    HighestDepthThreshold = i;
                    OnDepthThreshold?.Invoke(i, depth);
                    Debug.Log($"[EmotionalFieldPropagation] Depth threshold {i} " +
                              $"({depthThresholds[i]:F2}) reached: {depth:F3}");
                }
            }
        }

        // -----------------------------------------------------------------
        // Planet tracking — resolves the player's current planet from
        // available UniverseState fields. Priority:
        //   1. Portal decision target planet (if committed to world travel)
        //   2. Current session affinity planet
        //   3. Most recently visited planet (by utcTimestamp)
        // -----------------------------------------------------------------
        private void RefreshCurrentPlanet()
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            string planetId = null;

            // 1. Committed portal destination planet
            var portal = universe.lastPortalDecision;
            if (portal != null && portal.hasCommitted && !string.IsNullOrEmpty(portal.targetPlanetId))
                planetId = portal.targetPlanetId;

            // 2. Fall back to most recent session's affinity planet
            if (string.IsNullOrEmpty(planetId) && universe.sessionHistory.Count > 0)
            {
                var last = universe.sessionHistory[universe.sessionHistory.Count - 1];
                planetId = last.currentAffinityPlanet;
            }

            // 3. Fall back to most recently visited planet
            if (string.IsNullOrEmpty(planetId))
            {
                string latestUtc = null;
                foreach (var ps in universe.planets)
                {
                    if (ps.visitCount > 0 && (latestUtc == null ||
                        string.CompareOrdinal(ps.lastVisitUtc ?? "", latestUtc) > 0))
                    {
                        latestUtc = ps.lastVisitUtc;
                        planetId  = ps.planetId;
                    }
                }
            }

            if (string.IsNullOrEmpty(planetId) || planetId == _currentPlanetId) return;

            _currentPlanetId   = planetId;
            CurrentPlanetProfile = _profiles?.GetProfile(planetId);

            if (CurrentPlanetProfile != null)
                Debug.Log($"[EmotionalFieldPropagation] Planet emotional profile active: " +
                          $"{planetId} ({CurrentPlanetProfile.modulationStyle})");
        }

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Set the active planet manually (for testing or cave transitions).
        /// </summary>
        public void SetActivePlanet(string planetId)
        {
            _currentPlanetId   = planetId;
            CurrentPlanetProfile = _profiles?.GetProfile(planetId);
        }

        /// <summary>
        /// Get the alignment score between the player's current emotional state
        /// and a specific planet. Used by portal systems and planet affinity.
        /// </summary>
        public float GetPlanetAlignment(string planetId)
        {
            var profile = _profiles?.GetProfile(planetId);
            return profile?.AlignmentScore(LiveSpectrum) ?? 0f;
        }

        /// <summary>
        /// Get alignment scores for all planets, sorted descending.
        /// Used by PlanetAffinityInterpreter and portal selection.
        /// </summary>
        public (string planetId, float score)[] GetAllPlanetAlignments()
        {
            if (_profiles == null || _profiles.profiles.Count == 0)
                return Array.Empty<(string, float)>();

            var results = new (string, float)[_profiles.profiles.Count];
            for (int i = 0; i < _profiles.profiles.Count; i++)
            {
                var p = _profiles.profiles[i];
                results[i] = (p.planetId, p.AlignmentScore(LiveSpectrum));
            }
            Array.Sort(results, (a, b) => b.score.CompareTo(a.score));
            return results;
        }

        /// <summary>
        /// Reset depth thresholds (call at session start).
        /// </summary>
        public void ResetDepthTracking()
        {
            HighestDepthThreshold = -1;
        }
    }
}
