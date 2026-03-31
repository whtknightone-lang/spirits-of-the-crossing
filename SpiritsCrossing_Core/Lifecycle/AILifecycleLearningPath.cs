// SpiritsCrossing — AILifecycleLearningPath.cs
// Layers a four-node AI learning path across the cosmological cycle:
//   Birth → Death → Source → Rebirth
//
// Waypoint meanings:
//   Birth   — player enters the Born state (from Rebirth or first launch).
//             Captures entry resonance: who they are when they arrive.
//   Death   — player chooses Source Drop-In (voluntary release of the Born state).
//             Captures release state: what they carried to the threshold.
//   Source  — player exits InSource toward Rebirth (Rebirth begins event).
//             Captures peak communion: the deepest state they reached.
//   Rebirth — cycle completes. Full CycleLearningRecord is integrated into
//             ResonanceLearningState, adding cycle-tagged depth to session learning.
//
// The path also monitors resonance collapse events during the Born phase —
// moments when calm or source alignment fall critically low. These "fracture
// signals" inform the AI which drives to soften in the next cycle, so the
// world learns the player's fragile edges as well as their peaks.
//
// Output: CyclePhaseModifier — read each frame by SpiritBrainController
//         to shift drive weights based on the player's cosmological position.
//
// Setup: Add ONE instance to the Bootstrap / GameBootstrapper object alongside
//        LifecycleSystem and ResonanceMemorySystem.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Memory;
using SpiritsCrossing.SpiritAI;

namespace SpiritsCrossing.Lifecycle
{
    public class AILifecycleLearningPath : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------
        public static AILifecycleLearningPath Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Collapse Detection")]
        [Range(0f, 0.5f)]
        [Tooltip("Calm below this threshold during Born = collapse event recorded.")]
        public float collapseCalmThreshold      = 0.15f;

        [Range(0f, 0.5f)]
        [Tooltip("Source alignment below this during Born = collapse event recorded.")]
        public float collapseSourceThreshold    = 0.10f;

        [Tooltip("Minimum seconds between collapse events for the same dimension.")]
        public float collapseDebounce           = 30f;

        [Header("Phase Influence")]
        [Range(0f, 0.5f)]
        [Tooltip("How strongly cycle position shifts AI drive weights. 0 = no effect.")]
        public float phaseInfluenceStrength     = 0.18f;

        [Header("Debug")]
        public bool logWaypoints = true;

        // -------------------------------------------------------------------------
        // Public output — read by SpiritBrainController each update
        // -------------------------------------------------------------------------
        public CyclePhaseModifier CurrentModifier { get; private set; } = new CyclePhaseModifier();

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private CycleLearningRecord          _active        = new CycleLearningRecord();
        private bool                         _collapseArmed = true;
        private readonly Dictionary<string, float> _lastCollapseTime = new Dictionary<string, float>();

        private SpiritBrainOrchestrator      _orchestrator;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------
        private void OnEnable()
        {
            var lc = LifecycleSystem.Instance;
            if (lc == null) return;

            lc.OnPhaseChanged    += OnPhaseChanged;
            lc.OnSourceDropIn    += OnSourceDropIn;
            lc.OnRebirthBegins   += OnRebirthBegins;
            lc.OnRebirthComplete += OnRebirthComplete;
        }

        private void OnDisable()
        {
            var lc = LifecycleSystem.Instance;
            if (lc == null) return;

            lc.OnPhaseChanged    -= OnPhaseChanged;
            lc.OnSourceDropIn    -= OnSourceDropIn;
            lc.OnRebirthBegins   -= OnRebirthBegins;
            lc.OnRebirthComplete -= OnRebirthComplete;
        }

        private void Start()
        {
            _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();

            // Capture birth waypoint now if the game starts in Born phase
            // (first launch, before any lifecycle events have fired)
            var lc = LifecycleSystem.Instance;
            if (lc != null && lc.CurrentPhase == PlayerCyclePhase.Born && !_active.birth.valid)
                CaptureBirthWaypoint();
        }

        private void Update()
        {
            // Lazy-find orchestrator (may spawn after Start)
            if (_orchestrator == null)
                _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();

            // Monitor for collapse events only while player is active in the world
            if (_collapseArmed &&
                LifecycleSystem.Instance?.CurrentPhase == PlayerCyclePhase.Born)
                MonitorCollapses();
        }

        // -------------------------------------------------------------------------
        // Phase event handlers
        // -------------------------------------------------------------------------
        private void OnPhaseChanged(PlayerCyclePhase phase)
        {
            if (phase == PlayerCyclePhase.Born)
                CaptureBirthWaypoint();   // Rebirth just completed — new cycle begins

            ComputeModifier(phase);
        }

        private void OnSourceDropIn()
        {
            CaptureDeathWaypoint();
            _collapseArmed = false;       // no longer in Born — stop collapse monitoring
        }

        private void OnRebirthBegins()
        {
            CaptureSourceWaypoint();      // player is leaving InSource — record peak
        }

        private void OnRebirthComplete(List<string> giftKeys)
        {
            CaptureRebirthWaypoint(giftKeys);
            IntegrateCycle();
            BeginNewCycle();
            _collapseArmed = true;
        }

        // -------------------------------------------------------------------------
        // Waypoint capture
        // -------------------------------------------------------------------------
        private void CaptureBirthWaypoint()
        {
            var sample = SampleResonance();
            float scl  = ResonanceMemorySystem.Instance?.SourceConnectionLevel ?? 0f;
            _active.birth.Capture(CycleWaypointId.Birth, sample, scl);

            if (logWaypoints)
                Debug.Log($"[AILifecycleLearningPath] BIRTH  scl={scl:F3} " +
                          $"calm={sample.calmScore:F2} source={sample.sourceAlignmentScore:F2}");
        }

        private void CaptureDeathWaypoint()
        {
            var sample = SampleResonance();
            float scl  = ResonanceMemorySystem.Instance?.SourceConnectionLevel ?? 0f;
            _active.death.Capture(CycleWaypointId.Death, sample, scl);

            if (logWaypoints)
                Debug.Log($"[AILifecycleLearningPath] DEATH (drop-in)  scl={scl:F3} " +
                          $"calm={sample.calmScore:F2} collapses={_active.collapseEvents.Count}");
        }

        private void CaptureSourceWaypoint()
        {
            var sample    = SampleResonance();
            float scl     = ResonanceMemorySystem.Instance?.SourceConnectionLevel ?? 0f;
            float communion = LifecycleSystem.Instance?.GetLifecycleState()?.communionDepth ?? 0f;
            _active.source.Capture(CycleWaypointId.Source, sample, scl, communion);

            if (logWaypoints)
                Debug.Log($"[AILifecycleLearningPath] SOURCE  communion={communion:F2} " +
                          $"scl={scl:F3} wonder={sample.wonderScore:F2}");
        }

        private void CaptureRebirthWaypoint(List<string> giftKeys)
        {
            var sample = SampleResonance();
            float scl  = ResonanceMemorySystem.Instance?.SourceConnectionLevel ?? 0f;
            _active.rebirth.Capture(CycleWaypointId.Rebirth, sample, scl);
            _active.completedUtc = DateTime.UtcNow.ToString("o");
            _active.cycleIndex   = LifecycleSystem.Instance?.CycleCount ?? 0;

            if (logWaypoints)
                Debug.Log($"[AILifecycleLearningPath] REBIRTH  cycle={_active.cycleIndex} " +
                          $"overallGrowth={_active.OverallGrowth:+0.00;-0.00} " +
                          $"gifts=[{string.Join(", ", giftKeys)}]");
        }

        // -------------------------------------------------------------------------
        // Collapse monitoring — runs each frame while player is Born
        // -------------------------------------------------------------------------
        private void MonitorCollapses()
        {
            var p = _orchestrator?.CurrentPlayerState;
            if (p == null) return;

            CheckCollapse("calm",   p.calm,           collapseCalmThreshold);
            CheckCollapse("source", p.sourceAlignment, collapseSourceThreshold);
        }

        private void CheckCollapse(string dim, float value, float threshold)
        {
            if (value >= threshold) return;

            // Debounce — don't re-record the same dimension within the cooldown window
            if (_lastCollapseTime.TryGetValue(dim, out float last) &&
                Time.realtimeSinceStartup - last < collapseDebounce) return;

            _lastCollapseTime[dim] = Time.realtimeSinceStartup;
            _active.collapseEvents.Add(new CollapseEvent
            {
                dimension    = dim,
                floorReached = value,
                timestampUtc = DateTime.UtcNow.ToString("o"),
            });

            if (logWaypoints)
                Debug.Log($"[AILifecycleLearningPath] Collapse: {dim}={value:F3} " +
                          $"(threshold={threshold:F3})");
        }

        // -------------------------------------------------------------------------
        // Cycle integration — push complete record into ResonanceLearningState
        // -------------------------------------------------------------------------
        private void IntegrateCycle()
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            universe.learningState.IntegrateCycleRecord(_active);

            Debug.Log($"[AILifecycleLearningPath] Cycle {_active.cycleIndex} integrated. " +
                      $"calm↑{_active.CalmGrowth:+0.00;-0.00} " +
                      $"wonder↑{_active.WonderGrowth:+0.00;-0.00} " +
                      $"source↑{_active.SourceGrowth:+0.00;-0.00} " +
                      $"collapses={_active.collapseEvents.Count}");
        }

        private void BeginNewCycle()
        {
            _active = new CycleLearningRecord();
            _lastCollapseTime.Clear();
        }

        // -------------------------------------------------------------------------
        // Modifier computation — how spirits adapt to current cycle position
        //
        // Born (fresh after Rebirth): spirits are welcoming; seek and signal rise
        // InSource (communing):       spirits go quiet; rest rises, attack falls
        // Rebirth (transitioning):    brief surge of explore and signal as the
        //                             player re-enters the world changed
        // -------------------------------------------------------------------------
        private void ComputeModifier(PlayerCyclePhase phase)
        {
            float s   = phaseInfluenceStrength;
            var   mod = new CyclePhaseModifier { phase = phase };

            switch (phase)
            {
                case PlayerCyclePhase.Born:
                    mod.seekShift         = +s * 0.80f;
                    mod.signalShift       = +s * 0.60f;
                    mod.attackShift       = -s * 0.50f;
                    mod.atmosphereIntensity = 0.55f;
                    break;

                case PlayerCyclePhase.InSource:
                    mod.restShift         = +s * 1.00f;
                    mod.signalShift       = +s * 0.40f;
                    mod.attackShift       = -s * 1.00f;
                    mod.exploreShift      = -s * 0.30f;
                    mod.atmosphereIntensity = 0.15f;
                    break;

                case PlayerCyclePhase.Rebirth:
                    mod.exploreShift      = +s * 1.00f;
                    mod.signalShift       = +s * 0.80f;
                    mod.seekShift         = +s * 0.50f;
                    mod.atmosphereIntensity = 1.00f;
                    break;
            }

            CurrentModifier = mod;
        }

        // -------------------------------------------------------------------------
        // Helper — sample current live resonance into a PlayerResponseSample
        // Falls back to the persisted learning signature when live state is absent.
        // -------------------------------------------------------------------------
        private SpiritsCrossing.PlayerResponseSample SampleResonance()
        {
            var p = _orchestrator?.CurrentPlayerState;
            if (p != null)
            {
                return new SpiritsCrossing.PlayerResponseSample
                {
                    calmScore            = p.calm,
                    joyScore             = p.joy,
                    wonderScore          = p.wonder,
                    pairSyncScore        = p.socialSync,
                    flowScore            = p.movementFlow,
                    spinScore            = p.spinStability,
                    sourceAlignmentScore = p.sourceAlignment,
                    stillnessScore       = p.breathCoherence,
                    distortionScore      = p.distortion,
                };
            }

            return UniverseStateManager.Instance?.Current.learningState.signature
                ?? new SpiritsCrossing.PlayerResponseSample();
        }
    }
}
