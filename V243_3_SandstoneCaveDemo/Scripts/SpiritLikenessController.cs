using UnityEngine;

namespace V243.SandstoneCave
{
    public class SpiritLikenessController : MonoBehaviour
    {
        public SpiritArchetype archetype;
        public Transform wallAnchor;
        public Transform centerDiskTarget;
        public Animator animator;
        public float moveSpeed = 1.5f;
        public float awakenThreshold = 0.55f;
        public float centerDwellTime = 12f;

        [Header("Runtime")]
        public bool awakened;
        public bool atCenter;
        public float centerTimer;

        public PlayerResponseTracker responseTracker;

        private void Reset()
        {
            wallAnchor = transform;
        }

        private void Update()
        {
            if (responseTracker == null || centerDiskTarget == null || wallAnchor == null)
            {
                return;
            }

            float activation = GetActivationScore(responseTracker.LiveState);
            if (!awakened && activation >= awakenThreshold)
            {
                awakened = true;
                TriggerAnimation("Awaken");
            }

            if (!awakened)
            {
                return;
            }

            if (!atCenter)
            {
                MoveTowards(centerDiskTarget.position);
                if (Vector3.Distance(transform.position, centerDiskTarget.position) < 0.1f)
                {
                    atCenter = true;
                    centerTimer = 0f;
                    TriggerCenterPerformance();
                }
            }
            else
            {
                centerTimer += Time.deltaTime;
                if (centerTimer >= centerDwellTime)
                {
                    MoveTowards(wallAnchor.position);
                    if (Vector3.Distance(transform.position, wallAnchor.position) < 0.1f)
                    {
                        atCenter = false;
                        awakened = false;
                        TriggerAnimation("Rest");
                    }
                }
            }
        }

        private float GetActivationScore(CavePlayerResonanceState state)
        {
            switch (archetype)
            {
                // ---- Humanoid spirits ----
                case SpiritArchetype.Seated:
                    return state.breathCoherence * 0.5f + state.calm * 0.5f;
                case SpiritArchetype.FlowDancer:
                    return state.movementFlow * 0.6f + state.joy * 0.4f;
                case SpiritArchetype.Dervish:
                    return state.spinStability * 0.65f + state.wonder * 0.35f;
                case SpiritArchetype.PairA:
                case SpiritArchetype.PairB:
                    return state.socialSync * 0.7f + state.joy * 0.3f;

                // ---- Elder Dragon spirits ----
                // Earth Dragon awakens through deep stillness and source alignment
                case SpiritArchetype.EarthDragon:
                    return state.calm * 0.50f + state.breathCoherence * 0.30f +
                           state.sourceAlignment * 0.20f;

                // Fire Dragon awakens through spin intensity and distortion (meeting the fire)
                case SpiritArchetype.FireDragon:
                    return state.spinStability * 0.45f + state.distortion * 0.35f +
                           state.movementFlow * 0.20f;

                // Water Dragon awakens through flowing movement and social sync
                case SpiritArchetype.WaterDragon:
                    return state.movementFlow * 0.50f + state.socialSync * 0.30f +
                           state.breathCoherence * 0.20f;

                // Elder Air Dragon awakens through wonder and source alignment
                case SpiritArchetype.ElderAirDragon:
                    return state.wonder * 0.45f + state.sourceAlignment * 0.35f +
                           state.spinStability * 0.20f;

                default:
                    return 0f;
            }
        }

        private void MoveTowards(Vector3 target)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            transform.LookAt(new Vector3(target.x, transform.position.y, target.z));
        }

        private void TriggerCenterPerformance()
        {
            switch (archetype)
            {
                case SpiritArchetype.Seated:
                    TriggerAnimation("Sit");
                    break;
                case SpiritArchetype.FlowDancer:
                    TriggerAnimation("Dance");
                    break;
                case SpiritArchetype.Dervish:
                    TriggerAnimation("Twirl");
                    break;
                case SpiritArchetype.PairA:
                case SpiritArchetype.PairB:
                    TriggerAnimation("PairFlow");
                    break;
            }
        }

        private void TriggerAnimation(string triggerName)
        {
            if (animator != null)
            {
                animator.SetTrigger(triggerName);
            }
        }
    }
}
