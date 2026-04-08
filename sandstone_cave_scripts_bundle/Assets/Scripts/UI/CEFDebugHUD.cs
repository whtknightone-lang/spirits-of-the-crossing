using UnityEngine;
using TMPro;
using Upsilon.CEF;
using Upsilon.Cave;
using Upsilon.Portals;

namespace Upsilon.UI
{
    public class CEFDebugHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private FirstStepsSequenceController sequence;
        [SerializeField] private PortalStateController portal;

        private void Update()
        {
            if (text == null || CEFStateBus.Instance == null) return;

            var s = CEFStateBus.Instance.CurrentState;
            string seq = sequence != null ? sequence.CurrentStage.ToString() : "n/a";
            string portalState = portal != null ? (portal.IsStable ? "STABLE" : "LOCKED") : "n/a";

            text.text =
                $"CEF-2\n" +
                $"Edge: {s.creativeEdge:F2}\n" +
                $"Coherence: {s.coherence:F2}\n" +
                $"Novelty: {s.novelty:F2}\n" +
                $"Identity: {s.identity:F2}\n" +
                $"Fracture: {s.fractureRisk:F2}\n" +
                $"Source: {s.sourceAlignment:F2}\n" +
                $"Desire: {s.desireTension:F2}\n" +
                $"Globe: {(s.inventionGlobe ? $"ON ({s.globeStrength:F2})" : "OFF")}\n" +
                $"Sequence: {seq}\n" +
                $"Portal: {portalState}";
        }
    }
}
