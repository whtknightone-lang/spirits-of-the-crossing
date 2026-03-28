// SpiritsCrossing — PhysicalInputBridge.cs
// The single conversion layer between raw biometric signals and the game's
// PlayerResonanceState / BreathMovementInterpreter API.
//
// Signal → Resonance mapping (mirrors biometric_simulator.py):
//
//   breathCoherence  = BreathCycleTracker.Coherence()
//   movementFlow     = (1 - jerk) * 0.7 + speed * 0.3
//   spinStability    = 1 - rotationVariance * 3
//   calm             = (1 - HRV*8) * (1 - speed*0.7) * coherence
//   joy              = (HR - 55)/60 * 0.6 + (1 - jerk) * 0.4
//   wonder           = breathHold * 3 + (1 - speed) * 0.2
//   distortion       = HRV*6 + jerk*0.5 + (1-coherence)*0.3
//   sourceAlignment  = coherence*0.5 + calm*0.3 + wonder*0.2
//
// Setup:
//   1. Add PhysicalInputBridge to your CaveSystems manager object.
//   2. Assign either a SimulatedPhysicalInputReader or HardwarePhysicalInputReader.
//   3. Assign the BreathMovementInterpreter from the cave scene.
//   4. PhysicalInputBridge calls the interpreter's UpdateBreath/UpdateFlowMovement
//      etc. each frame — the rest of the pipeline runs unchanged.

using UnityEngine;
using V243.SandstoneCave;

namespace SpiritsCrossing.BiometricInput
{
    public class PhysicalInputBridge : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Reader Source")]
        [Tooltip("Assign either SimulatedPhysicalInputReader or HardwarePhysicalInputReader.")]
        public MonoBehaviour readerComponent;

        [Header("Cave Target")]
        [Tooltip("The BreathMovementInterpreter in the current cave scene. Auto-found if null.")]
        public BreathMovementInterpreter breathInterpreter;

        [Header("Smoothing")]
        [Range(0.5f, 20f)] public float smoothSpeed = 4f;

        [Header("Mapping Tuning")]
        [Range(0f, 15f)]  public float hrNeutral     = 70f;    // BPM considered baseline
        [Range(0f, 70f)]  public float hrRange        = 60f;    // BPM above neutral = full joy
        [Range(0f, 20f)]  public float hrvScale       = 8f;     // amplifies HRV → calm penalty
        [Range(0f,  5f)]  public float wonderHoldMult = 3f;     // breath hold → wonder
        [Range(0f,  2f)]  public float distortionJerkWeight  = 0.5f;
        [Range(0f,  2f)]  public float distortionHRVWeight   = 6f;
        [Range(0f,  2f)]  public float distortionIncoherenceWeight = 0.3f;

        [Header("Debug")]
        public bool showDebugLog;

        // -------------------------------------------------------------------------
        // Public outputs (readable by UI, SpiritBrainOrchestrator, etc.)
        // -------------------------------------------------------------------------
        public RawBiometricSignals  CurrentRaw     { get; private set; } = RawBiometricSignals.Default();
        public DerivedBiometricMetrics Derived     { get; private set; } = DerivedBiometricMetrics.Default();
        public PlayerResonanceState CurrentState   { get; private set; } = new PlayerResonanceState();

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private IPhysicalInputReader  _reader;
        private BreathCycleTracker    _breathTracker = new BreathCycleTracker();
        private float                 _rotSum;
        private float                 _rotSumSq;
        private int                   _rotCount;

        private void Start()
        {
            // Resolve reader
            _reader = readerComponent as IPhysicalInputReader;
            if (_reader == null)
                Debug.LogError("[PhysicalInputBridge] readerComponent does not implement IPhysicalInputReader.");

            // Auto-find interpreter
            if (breathInterpreter == null)
                breathInterpreter = FindObjectOfType<BreathMovementInterpreter>();

            if (breathInterpreter == null)
                Debug.LogWarning("[PhysicalInputBridge] No BreathMovementInterpreter found in scene.");
        }

        private void Update()
        {
            if (_reader == null || !_reader.IsConnected) return;

            var raw = _reader.CurrentSignals;
            CurrentRaw = raw;

            // Feed breath tracker
            _breathTracker.Feed(raw.breathAmplitude, Time.time);

            // Rolling rotation variance
            _rotSum   += raw.rotationRate;
            _rotSumSq += raw.rotationRate * raw.rotationRate;
            _rotCount++;

            var derived = ComputeDerived(raw, _breathTracker);
            Derived = derived;

            var state = BuildResonanceState(raw, derived);
            SmoothInto(ref CurrentState, state, Time.deltaTime * smoothSpeed);

            PushToInterpreter(CurrentState);

            if (showDebugLog && Time.frameCount % 60 == 0)
                Debug.Log($"[PhysicalInputBridge] HR={raw.heartRateBPM:F0} " +
                          $"Coher={derived.breathCoherence:F2} Calm={derived.calm:F2} " +
                          $"Joy={derived.joy:F2} Dist={derived.distortion:F2} " +
                          $"Source={derived.sourceAlignment:F2}");
        }

        // -------------------------------------------------------------------------
        // Signal → derived metrics  (same formula as biometric_simulator.py)
        // -------------------------------------------------------------------------
        private DerivedBiometricMetrics ComputeDerived(RawBiometricSignals r,
                                                        BreathCycleTracker tracker)
        {
            float coherence = tracker.Coherence();
            float hrv       = r.heartRateVariability;
            float speed     = r.movementSpeed;
            float jerk      = r.movementJerk;

            // Rotation variance (rolling, reset after 200 samples)
            float rotVar = 0f;
            if (_rotCount > 4)
            {
                float mean = _rotSum / _rotCount;
                float sqMean = _rotSumSq / _rotCount;
                rotVar = Mathf.Max(0f, sqMean - mean * mean);
            }
            if (_rotCount > 200) { _rotSum = 0f; _rotSumSq = 0f; _rotCount = 0; }

            float spinStability = Mathf.Clamp01(1f - rotVar * 3f);

            // Arousal: HR deviation + speed
            float arousal = Mathf.Clamp01(
                (r.heartRateBPM - hrNeutral) / Mathf.Max(1f, hrRange) * 0.6f +
                speed * 0.4f);

            // Valence: coherence + smoothness - distortion signal
            float rawDist  = hrv * distortionHRVWeight +
                             jerk * distortionJerkWeight +
                             (1f - coherence) * distortionIncoherenceWeight;
            float distortion = Mathf.Clamp01(rawDist);
            float valence    = Mathf.Clamp01(coherence * 0.5f + (1f - jerk) * 0.3f - distortion * 0.2f);

            // Calm: low HR variability + low speed + coherence
            float calm = Mathf.Clamp01(
                (1f - hrv * hrvScale) *
                (1f - speed * 0.7f)  *
                coherence);

            // Joy: arousal + valence (Russell's circumplex model)
            float joy = Mathf.Clamp01(
                arousal * 0.6f +
                (1f - jerk) * 0.4f);

            // Wonder: breath hold + sudden stillness
            float wonder = Mathf.Clamp01(
                (r.isBreathHold ? wonderHoldMult * 0.25f : 0f) +
                (r.isStill      ? 0.3f : 0f));

            // Source alignment: deep coherence + low HR + stillness
            float source = Mathf.Clamp01(
                coherence * 0.5f + calm * 0.3f + wonder * 0.2f);

            // Movement flow: smoothness-weighted speed
            float flow = Mathf.Clamp01((1f - jerk) * 0.7f + speed * 0.3f);

            return new DerivedBiometricMetrics
            {
                breathCoherence = coherence,
                arousal         = arousal,
                valence         = valence,
                calm            = calm,
                joy             = joy,
                wonder          = wonder,
                distortion      = distortion,
                sourceAlignment = source,
                movementFlow    = flow,
                spinStability   = spinStability,
            };
        }

        // -------------------------------------------------------------------------
        // Build PlayerResonanceState from derived metrics
        // -------------------------------------------------------------------------
        private static PlayerResonanceState BuildResonanceState(RawBiometricSignals r,
                                                                  DerivedBiometricMetrics d)
        {
            return new PlayerResonanceState
            {
                breathCoherence = d.breathCoherence,
                movementFlow    = d.movementFlow,
                spinStability   = d.spinStability,
                socialSync      = r.pairSyncScore,
                calm            = d.calm,
                joy             = d.joy,
                wonder          = d.wonder,
                distortion      = d.distortion,
                sourceAlignment = d.sourceAlignment,
            };
        }

        // -------------------------------------------------------------------------
        // Push to BreathMovementInterpreter (existing cave API — no modifications needed)
        // -------------------------------------------------------------------------
        private void PushToInterpreter(PlayerResonanceState s)
        {
            if (breathInterpreter == null) return;

            breathInterpreter.UpdateBreath(s.breathCoherence);
            breathInterpreter.UpdateFlowMovement(s.movementFlow);
            breathInterpreter.UpdateSpin(s.spinStability);
            breathInterpreter.UpdateSocialSync(s.socialSync);
            breathInterpreter.UpdateCalm(s.calm);
            breathInterpreter.UpdateJoy(s.joy);
            breathInterpreter.UpdateWonder(s.wonder);
            breathInterpreter.UpdateDistortion(s.distortion);
            breathInterpreter.UpdateSourceAlignment(s.sourceAlignment);
        }

        // -------------------------------------------------------------------------
        // Smooth lerp into target
        // -------------------------------------------------------------------------
        private static void SmoothInto(ref PlayerResonanceState current,
                                        PlayerResonanceState target, float t)
        {
            t = Mathf.Clamp01(t);
            current.breathCoherence = Mathf.Lerp(current.breathCoherence, target.breathCoherence, t);
            current.movementFlow    = Mathf.Lerp(current.movementFlow,    target.movementFlow,    t);
            current.spinStability   = Mathf.Lerp(current.spinStability,   target.spinStability,   t);
            current.socialSync      = Mathf.Lerp(current.socialSync,      target.socialSync,      t);
            current.calm            = Mathf.Lerp(current.calm,            target.calm,            t);
            current.joy             = Mathf.Lerp(current.joy,             target.joy,             t);
            current.wonder          = Mathf.Lerp(current.wonder,          target.wonder,          t);
            current.distortion      = Mathf.Lerp(current.distortion,      target.distortion,      t);
            current.sourceAlignment = Mathf.Lerp(current.sourceAlignment, target.sourceAlignment, t);
        }

        // -------------------------------------------------------------------------
        // Utility: swap reader at runtime (e.g. hardware disconnected → simulation)
        // -------------------------------------------------------------------------
        public void SetReader(IPhysicalInputReader reader)
        {
            _reader = reader;
            _breathTracker.Reset();
            Debug.Log($"[PhysicalInputBridge] Reader swapped to {reader.GetType().Name}");
        }
    }
}
