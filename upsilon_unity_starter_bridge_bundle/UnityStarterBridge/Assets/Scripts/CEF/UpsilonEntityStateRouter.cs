using UnityEngine;

namespace Upsilon.CEF
{
    public class UpsilonEntityStateRouter : MonoBehaviour
    {
        [SerializeField] private string entityId = "player";
        [SerializeField] private UpsilonSkinController skinController;

        private void Reset()
        {
            skinController = GetComponent<UpsilonSkinController>();
        }

        private void OnEnable()
        {
            UpsilonStateBus.StateUpdated += HandleStateUpdated;
        }

        private void OnDisable()
        {
            UpsilonStateBus.StateUpdated -= HandleStateUpdated;
        }

        private void HandleStateUpdated(CEFEntityState state)
        {
            if (state == null || state.entityId != entityId) return;
            skinController?.ApplyState(state);
        }
    }
}
