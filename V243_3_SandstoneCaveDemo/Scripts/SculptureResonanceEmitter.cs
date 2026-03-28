using UnityEngine;

namespace V243.SandstoneCave
{
    public class SculptureResonanceEmitter : MonoBehaviour
    {
        public SculptureResonanceProfile profile = new SculptureResonanceProfile();
        public float radius = 6f;
        public Transform targetPlayer;
        public PlayerResponseTracker responseTracker;

        private void Update()
        {
            if (targetPlayer == null || responseTracker == null)
            {
                return;
            }

            CavePlayerResonanceState influence = EvaluateInfluence(targetPlayer.position, transform.position);
            responseTracker.LiveState.LerpToward(influence, Time.deltaTime * 0.1f);
        }

        public CavePlayerResonanceState EvaluateInfluence(Vector3 playerPos, Vector3 sculpturePos)
        {
            float d = Vector3.Distance(playerPos, sculpturePos);
            float w = Mathf.Clamp01(1f - d / Mathf.Max(0.001f, radius));

            return new CavePlayerResonanceState
            {
                calm = profile.calmBias * w,
                joy = profile.joyBias * w,
                wonder = profile.wonderBias * w,
                socialSync = profile.socialCouplingBias * w,
                sourceAlignment = profile.upwardLiftBias * w,
                distortion = profile.distortionSensitivity * (1f - w)
            };
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
