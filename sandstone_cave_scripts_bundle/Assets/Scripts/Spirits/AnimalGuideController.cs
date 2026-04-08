using UnityEngine;
using Upsilon.CEF;

namespace Upsilon.Spirits
{
    public class AnimalGuideController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform guidePointNearDisk;
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float turnSpeed = 8f;
        [SerializeField] private float followDistance = 1.8f;
        [SerializeField] private Animator animator;

        private CEFSharedState state;

        private void Start()
        {
            if (CEFStateBus.Instance != null)
                CEFStateBus.Instance.OnStateUpdated += HandleStateUpdated;
        }

        private void OnDestroy()
        {
            if (CEFStateBus.Instance != null)
                CEFStateBus.Instance.OnStateUpdated -= HandleStateUpdated;
        }

        private void HandleStateUpdated(CEFSharedState s)
        {
            state = s;
        }

        private void Update()
        {
            if (player == null || guidePointNearDisk == null) return;

            Transform target = ShouldGuideToDisk() ? guidePointNearDisk : player;
            Vector3 flatTarget = target.position;
            flatTarget.y = transform.position.y;

            Vector3 toTarget = flatTarget - transform.position;
            float dist = toTarget.magnitude;

            if (target == player && dist < followDistance)
            {
                SetMoveAmount(0.15f);
                return;
            }

            Vector3 dir = toTarget.normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * turnSpeed);
            }

            SetMoveAmount(Mathf.Clamp01(dist));
        }

        private bool ShouldGuideToDisk()
        {
            if (state == null) return true;
            return state.coherence < 0.55f || state.identity < 0.5f;
        }

        private void SetMoveAmount(float amount)
        {
            if (animator != null)
                animator.SetFloat("Move", amount);
        }
    }
}
