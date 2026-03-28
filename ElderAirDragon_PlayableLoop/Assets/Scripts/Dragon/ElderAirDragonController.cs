
using UnityEngine;
using SpiritsCrossing.VR;

namespace ArtificialUniverse.Dragon
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(ElderAirDragonStats))]
    public class ElderAirDragonController : MonoBehaviour
    {
        private CharacterController controller;
        private ElderAirDragonStats stats;

        private Vector3 velocity;
        private bool isMeditating;
        private float lastMotionAmount;

        public float MotionAmount => lastMotionAmount;
        public bool IsMeditating => isMeditating;
        public Vector3 CurrentVelocity => velocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            stats = GetComponent<ElderAirDragonStats>();
        }

        private void Update()
        {
            HandleMovement(Time.deltaTime);
        }

        private void HandleMovement(float dt)
        {
            var vr = VRInputAdapter.Instance;
            float yaw; float pitch; bool ascend; bool dive;

            if (vr != null && vr.IsVRActive)
            {
                yaw          = vr.FlightHorizontal;
                pitch        = vr.FlightVertical + Mathf.Clamp(vr.HeadForward.y * 0.5f, -0.5f, 0.5f);
                ascend       = vr.IsAscending;
                dive         = vr.IsDiving;
                isMeditating = vr.IsMeditating;
            }
            else
            {
                yaw          = Input.GetAxis("Horizontal");
                pitch        = Input.GetAxis("Vertical");
                ascend       = Input.GetKey(KeyCode.Space);
                dive         = Input.GetKey(KeyCode.LeftShift);
                isMeditating = Input.GetKey(KeyCode.F);
            }

            transform.Rotate(Vector3.up,    yaw   * stats.turnSpeed         * dt, Space.World);
            transform.Rotate(Vector3.right, -pitch * stats.turnSpeed * 0.6f * dt, Space.Self);

            float speed = isMeditating ? stats.cruiseSpeed * 0.45f : stats.cruiseSpeed;
            if (dive) speed = stats.boostSpeed;

            Vector3 forward  = transform.forward * speed;
            Vector3 vertical = Vector3.zero;
            if (ascend) vertical += Vector3.up   * stats.riseSpeed;
            if (dive)   vertical += Vector3.down  * stats.diveSpeed * 0.35f;

            velocity = forward + vertical;
            if (isMeditating) velocity *= 0.6f;

            controller.Move(velocity * dt);
            lastMotionAmount = Mathf.Clamp01(
                new Vector3(velocity.x, 0f, velocity.z).magnitude / Mathf.Max(1f, stats.boostSpeed));
            stats.TickPassive(dt, isMeditating, lastMotionAmount);
        }
    }
}
