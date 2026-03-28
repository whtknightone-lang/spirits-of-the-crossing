using UnityEngine;
using SpiritsCrossing.VR;

namespace V243.SandstoneCave
{
    public class BreathMovementInterpreter : MonoBehaviour
    {
        public CavePlayerResonanceState state = new CavePlayerResonanceState();

        [Header("Debug Input")]
        public bool useDebugKeyboardInput = true;
        public float debugSmoothSpeed = 2f;

        [Header("VR")]
        [Tooltip("When true and VR is active, keyboard debug input is suppressed and " +
                 "VRInputAdapter gesture scores drive resonance instead.")]
        public bool useVRGesturesWhenActive = true;

        private void Update()
        {
            // ---- VR gesture path ----
            var vr = VRInputAdapter.Instance;
            if (useVRGesturesWhenActive && vr != null && vr.IsVRActive)
            {
                var g = vr.Gestures;
                // Map gesture scores directly to resonance dimensions.
                // PhysicalInputBridge will also run and blend into these via UpdateBreath() etc.,
                // but gesture-based overrides give immediate expressive feedback.
                var vrTarget = new CavePlayerResonanceState
                {
                    breathCoherence  = Mathf.Max(state.breathCoherence,  g.HeadBowed * 0.6f),
                    movementFlow     = Mathf.Max(state.movementFlow,     g.MirroredFlow),
                    spinStability    = Mathf.Max(state.spinStability,    g.IsSpinning),
                    socialSync       = Mathf.Max(state.socialSync,       g.ArmsOutstretched),
                    calm             = Mathf.Max(state.calm,             g.HeadBowed * 0.8f + g.HandOnHeart * 0.4f),
                    joy              = Mathf.Max(state.joy,              g.HandOnHeart * 0.7f + g.MirroredFlow * 0.3f),
                    wonder           = Mathf.Max(state.wonder,           g.HandsTogether * 0.8f + g.BothHandsRaised * 0.5f),
                    distortion       = state.distortion,   // distortion comes from biometrics only
                    sourceAlignment  = Mathf.Max(state.sourceAlignment,  g.BothHandsRaised * 0.9f + g.HandsTogether * 0.4f)
                };
                state.LerpToward(vrTarget, Time.deltaTime * debugSmoothSpeed * 1.5f);
                return;
            }

            // ---- Flat keyboard debug path ----
            if (!useDebugKeyboardInput) return;

            CavePlayerResonanceState target = new CavePlayerResonanceState
            {
                breathCoherence = Input.GetKey(KeyCode.Alpha1) ? 0.9f : 0.3f,
                movementFlow    = Input.GetKey(KeyCode.Alpha2) ? 0.85f : 0.2f,
                spinStability   = Input.GetKey(KeyCode.Alpha3) ? 0.85f : 0.1f,
                socialSync      = Input.GetKey(KeyCode.Alpha4) ? 0.8f : 0.15f,
                calm            = Input.GetKey(KeyCode.Q) ? 0.9f : 0.35f,
                joy             = Input.GetKey(KeyCode.W) ? 0.85f : 0.25f,
                wonder          = Input.GetKey(KeyCode.E) ? 0.85f : 0.3f,
                distortion      = Input.GetKey(KeyCode.R) ? 0.7f : 0.15f,
                sourceAlignment = Input.GetKey(KeyCode.T) ? 0.9f : 0.25f
            };
            state.LerpToward(target, Time.deltaTime * debugSmoothSpeed);
        }

        public void UpdateBreath(float inhaleExhaleRegularity) => state.breathCoherence = Mathf.Clamp01(inhaleExhaleRegularity);
        public void UpdateFlowMovement(float smoothArcScore) => state.movementFlow = Mathf.Clamp01(smoothArcScore);
        public void UpdateSpin(float rotationStability) => state.spinStability = Mathf.Clamp01(rotationStability);
        public void UpdateSocialSync(float pairedRhythmScore) => state.socialSync = Mathf.Clamp01(pairedRhythmScore);
        public void UpdateCalm(float score) => state.calm = Mathf.Clamp01(score);
        public void UpdateJoy(float score) => state.joy = Mathf.Clamp01(score);
        public void UpdateWonder(float score) => state.wonder = Mathf.Clamp01(score);
        public void UpdateDistortion(float score) => state.distortion = Mathf.Clamp01(score);
        public void UpdateSourceAlignment(float score) => state.sourceAlignment = Mathf.Clamp01(score);
    }
}
