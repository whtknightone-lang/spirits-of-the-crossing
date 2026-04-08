using UnityEngine;

namespace Upsilon.CEF
{
    [CreateAssetMenu(menuName = "Upsilon/Skin Profile", fileName = "UpsilonSkinProfile")]
    public class UpsilonSkinProfile : ScriptableObject
    {
        [Header("Entity Identity")]
        public string profileName = "Player";
        public float identityGuard = 1.0f;
        public float noveltyGain = 1.0f;
        public float fractureSensitivity = 1.0f;

        [Header("Visual Response")]
        public float emissionBase = 0.4f;
        public float emissionCreativeEdge = 1.0f;
        public float pulseSpeedBase = 0.8f;
        public float pulseSpeedNovelty = 1.5f;
        public float distortionBase = 0.05f;
        public float distortionFracture = 0.2f;

        [Header("Body / Motion Hooks")]
        public float bodySwayGain = 1.0f;
        public float gaitVarianceGain = 1.0f;
        public float appendageSecondaryMotionGain = 1.0f;

        [Header("Haptics")]
        public float hapticBase = 0.1f;
        public float hapticDesireGain = 0.4f;
        public float hapticFractureGain = 0.7f;
    }
}
