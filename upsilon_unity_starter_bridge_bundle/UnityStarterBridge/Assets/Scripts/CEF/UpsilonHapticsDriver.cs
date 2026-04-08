using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace Upsilon.CEF
{
    /// <summary>
    /// Lightweight haptics driver. Attach to the bridge object and assign controller devices if desired.
    /// This is intentionally simple and can be replaced by XR Interaction Toolkit or device-specific code.
    /// </summary>
    public class UpsilonHapticsDriver : MonoBehaviour
    {
        [SerializeField] private string sourceEntityId = "player";
        [SerializeField] private UpsilonSkinProfile playerProfile;
        [SerializeField] private XRController leftController;
        [SerializeField] private XRController rightController;
        [SerializeField] private float pulseInterval = 0.1f;

        private CEFEntityState _state;
        private float _timer;

        private void OnEnable()
        {
            UpsilonStateBus.StateUpdated += HandleStateUpdated;
        }

        private void OnDisable()
        {
            UpsilonStateBus.StateUpdated -= HandleStateUpdated;
        }

        private void Update()
        {
            if (_state == null || playerProfile == null) return;
            _timer += Time.deltaTime;
            if (_timer < pulseInterval) return;
            _timer = 0f;

            float amplitude = playerProfile.hapticBase +
                              _state.desireTension * playerProfile.hapticDesireGain +
                              _state.fractureRisk * playerProfile.hapticFractureGain;
            amplitude = Mathf.Clamp01(amplitude);
            float duration = Mathf.Lerp(0.02f, 0.12f, _state.creativeEdge);
            float frequency = Mathf.Lerp(0.15f, 0.8f, _state.novelty);

            SendHaptic(leftController, amplitude, duration, frequency);
            SendHaptic(rightController, amplitude * 0.9f, duration, frequency);
        }

        private void HandleStateUpdated(CEFEntityState state)
        {
            if (state != null && state.entityId == sourceEntityId) _state = state;
        }

        private static void SendHaptic(XRController controller, float amplitude, float duration, float frequency)
        {
            if (controller == null || controller.device == null) return;
            if (controller.device is IHaptics haptics)
            {
                haptics.PauseHaptics();
                haptics.ResumeHaptics();
                haptics.SendHapticImpulse(0, amplitude, duration);
            }
        }
    }
}
