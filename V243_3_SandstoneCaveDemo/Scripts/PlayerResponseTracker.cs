using UnityEngine;

namespace V243.SandstoneCave
{
    public class PlayerResponseTracker : MonoBehaviour
    {
        public BreathMovementInterpreter interpreter;
        public CavePlayerResonanceState LiveState => liveState;

        [SerializeField] private CavePlayerResonanceState liveState = new CavePlayerResonanceState();
        [SerializeField] private float smoothing = 2f;

        [Header("Accumulated Samples")]
        [Range(0f, 1f)] public float peakStillness;
        [Range(0f, 1f)] public float peakFlow;
        [Range(0f, 1f)] public float peakSpin;
        [Range(0f, 1f)] public float peakPairSync;
        [Range(0f, 1f)] public float averageCalm;
        [Range(0f, 1f)] public float averageJoy;
        [Range(0f, 1f)] public float averageWonder;
        [Range(0f, 1f)] public float averageDistortion;
        [Range(0f, 1f)] public float averageSourceAlignment;

        private float sampleTime;

        private void Update()
        {
            if (interpreter == null)
            {
                return;
            }

            liveState.LerpToward(interpreter.state, Time.deltaTime * smoothing);

            peakStillness = Mathf.Max(peakStillness, liveState.breathCoherence * liveState.calm);
            peakFlow = Mathf.Max(peakFlow, liveState.movementFlow);
            peakSpin = Mathf.Max(peakSpin, liveState.spinStability);
            peakPairSync = Mathf.Max(peakPairSync, liveState.socialSync);

            sampleTime += Time.deltaTime;
            float k = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, sampleTime));
            averageCalm = Mathf.Lerp(averageCalm, liveState.calm, k);
            averageJoy = Mathf.Lerp(averageJoy, liveState.joy, k);
            averageWonder = Mathf.Lerp(averageWonder, liveState.wonder, k);
            averageDistortion = Mathf.Lerp(averageDistortion, liveState.distortion, k);
            averageSourceAlignment = Mathf.Lerp(averageSourceAlignment, liveState.sourceAlignment, k);
        }

        public PlayerResponseSample BuildSample()
        {
            return new PlayerResponseSample
            {
                stillnessScore = peakStillness,
                flowScore = peakFlow,
                spinScore = peakSpin,
                pairSyncScore = peakPairSync,
                calmScore = averageCalm,
                joyScore = averageJoy,
                wonderScore = averageWonder,
                distortionScore = averageDistortion,
                sourceAlignmentScore = averageSourceAlignment
            };
        }

        public void ResetTracking()
        {
            liveState = new CavePlayerResonanceState();
            peakStillness = 0f;
            peakFlow = 0f;
            peakSpin = 0f;
            peakPairSync = 0f;
            averageCalm = 0f;
            averageJoy = 0f;
            averageWonder = 0f;
            averageDistortion = 0f;
            averageSourceAlignment = 0f;
            sampleTime = 0f;
        }
    }
}
