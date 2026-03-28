
using UnityEngine;

namespace ArtificialUniverse.UI
{
    public class DragonCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 6f, -12f);
        [SerializeField] private float followSpeed = 5f;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + target.TransformDirection(offset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
            transform.LookAt(target.position + Vector3.up * 2f);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
