// SpiritsCrossing — PhysicalInputSignals.cs
// Data types for raw biometric signals and derived resonance metrics.
// Used by IPhysicalInputReader (both simulated and hardware paths)
// and consumed by PhysicalInputBridge to produce PlayerResonanceState.

using System;
using UnityEngine;

namespace SpiritsCrossing.BiometricInput
{
    // -------------------------------------------------------------------------
    // Raw sensor signals — updated at sensor sample rate (hardware or simulated)
    // -------------------------------------------------------------------------
    [Serializable]
    public class RawBiometricSignals
    {
        [Header("Breath")]
        [Range(0f, 1f)]  public float breathAmplitude;     // current 0–1 inhale depth
        [Range(0f, 1f)]  public float breathRateHz;        // breaths per second (0.15–0.6 typical)
        public bool                   isBreathHold;        // true during a voluntary hold

        [Header("Heart Rate")]
        [Range(30f, 220f)] public float heartRateBPM;      // current BPM
        [Range(0f, 1f)]    public float heartRateVariability; // normalized HRV (0=rigid, 1=chaotic)

        [Header("Movement")]
        public Vector3   acceleration;                     // raw m/s² (device/body)
        [Range(0f, 1f)]  public float movementSpeed;       // 0–1 normalized speed
        [Range(0f, 1f)]  public float movementJerk;        // 0–1 rate of acceleration change
        [Range(0f, 1f)]  public float rotationRate;        // 0–1 angular velocity
        public bool                   isStill;             // below stillness threshold

        [Header("Pairing")]
        [Range(0f, 1f)]  public float pairSyncScore;       // 0–1 breath/movement sync with partner

        [Header("Meta")]
        public float     sampleTimestamp;  // Time.time at last update
        public bool      isConnected;      // at least one sensor active

        public static RawBiometricSignals Default() => new RawBiometricSignals
        {
            breathAmplitude      = 0.5f,
            breathRateHz         = 0.25f,
            heartRateBPM         = 70f,
            heartRateVariability = 0.3f,
            movementSpeed        = 0.1f,
            movementJerk         = 0.05f,
            rotationRate         = 0.05f,
            isConnected          = false,
        };
    }

    // -------------------------------------------------------------------------
    // Derived metrics computed by PhysicalInputBridge from raw signals.
    // These map directly to PlayerResonanceState dimensions.
    // All values 0–1.
    // -------------------------------------------------------------------------
    [Serializable]
    public class DerivedBiometricMetrics
    {
        // Computed over a rolling window (e.g. last 10 breath cycles)
        [Range(0f, 1f)] public float breathCoherence;   // regularity of cycle period

        // Valence/arousal model
        [Range(0f, 1f)] public float arousal;           // (HR + movement energy)
        [Range(0f, 1f)] public float valence;           // (smoothness + coherence − distortion)

        // Direct resonance outputs (fed into PlayerResonanceState)
        [Range(0f, 1f)] public float calm;
        [Range(0f, 1f)] public float joy;
        [Range(0f, 1f)] public float wonder;
        [Range(0f, 1f)] public float distortion;
        [Range(0f, 1f)] public float sourceAlignment;
        [Range(0f, 1f)] public float movementFlow;
        [Range(0f, 1f)] public float spinStability;

        public static DerivedBiometricMetrics Default() => new DerivedBiometricMetrics
        {
            breathCoherence = 0.5f,
            arousal         = 0.3f,
            valence         = 0.5f,
            calm            = 0.4f,
            joy             = 0.3f,
            wonder          = 0.1f,
            distortion      = 0.15f,
            sourceAlignment = 0.3f,
            movementFlow    = 0.3f,
            spinStability   = 0.7f,
        };
    }

    // -------------------------------------------------------------------------
    // Rolling breath cycle tracker — used by both hardware mic reader
    // and simulated reader to compute coherence the same way.
    // -------------------------------------------------------------------------
    public class BreathCycleTracker
    {
        private readonly float[] _cyclePeriods;
        private int              _writeIndex;
        private int              _count;
        private float            _lastCrossingTime;
        private bool             _wasAboveMid;
        private const float      MID = 0.5f;

        public BreathCycleTracker(int windowSize = 10)
        {
            _cyclePeriods = new float[windowSize];
        }

        public void Feed(float amplitude, float currentTime)
        {
            bool aboveMid = amplitude > MID;
            if (aboveMid && !_wasAboveMid && _lastCrossingTime > 0f)
            {
                float period = currentTime - _lastCrossingTime;
                if (period > 0.5f && period < 15f) // sanity bounds
                {
                    _cyclePeriods[_writeIndex % _cyclePeriods.Length] = period;
                    _writeIndex++;
                    _count = Mathf.Min(_count + 1, _cyclePeriods.Length);
                }
                _lastCrossingTime = currentTime;
            }
            else if (aboveMid && _lastCrossingTime <= 0f)
            {
                _lastCrossingTime = currentTime;
            }
            _wasAboveMid = aboveMid;
        }

        public float Coherence()
        {
            if (_count < 2) return 0.5f;
            float mean = 0f;
            for (int i = 0; i < _count; i++) mean += _cyclePeriods[i];
            mean /= _count;
            float variance = 0f;
            for (int i = 0; i < _count; i++)
            {
                float d = _cyclePeriods[i] - mean;
                variance += d * d;
            }
            variance /= _count;
            float normStd = Mathf.Sqrt(variance) / Mathf.Max(0.01f, mean);
            return Mathf.Clamp01(1f - normStd);
        }

        public void Reset()
        {
            _writeIndex       = 0;
            _count            = 0;
            _lastCrossingTime = 0f;
            _wasAboveMid      = false;
            Array.Clear(_cyclePeriods, 0, _cyclePeriods.Length);
        }
    }
}
