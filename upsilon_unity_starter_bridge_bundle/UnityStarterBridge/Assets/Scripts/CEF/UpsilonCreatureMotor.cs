using UnityEngine;

namespace Upsilon.CEF
{
    /// <summary>
    /// Optional simple motion hook for AI/animals. Feed it the same state the skin uses.
    /// Use for ears, tails, wing bones, idle sway, or body rhythm.
    /// </summary>
    public class UpsilonCreatureMotor : MonoBehaviour
    {
        [SerializeField] private string entityId = "animal_1";
        [SerializeField] private UpsilonSkinProfile skinProfile;
        [SerializeField] private Transform swayRoot;
        [SerializeField] private Vector3 swayAxis = new Vector3(0f, 1f, 0f);
        [SerializeField] private float swayDegrees = 10f;
        [SerializeField] private float secondaryMotionDegrees = 18f;

        private CEFEntityState _state;
        private float _time;

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
            if (_state == null || swayRoot == null || skinProfile == null) return;
            _time += Time.deltaTime;
            float rhythm = 0.5f + _state.coherence * 0.8f + _state.biometrics.resp * 0.6f;
            float sway = Mathf.Sin(_time * rhythm * 2f) * swayDegrees * skinProfile.bodySwayGain;
            float appendage = Mathf.Sin(_time * (rhythm * 3.1f + 0.4f)) * secondaryMotionDegrees * skinProfile.appendageSecondaryMotionGain * _state.novelty;
            swayRoot.localRotation = Quaternion.AngleAxis(sway + appendage, swayAxis.normalized);
        }

        private void HandleStateUpdated(CEFEntityState state)
        {
            if (state != null && state.entityId == entityId) _state = state;
        }
    }
}
