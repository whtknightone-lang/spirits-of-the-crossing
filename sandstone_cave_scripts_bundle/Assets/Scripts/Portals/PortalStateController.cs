using UnityEngine;
using Upsilon.CEF;
using Upsilon.Cave;

namespace Upsilon.Portals
{
    public class PortalStateController : MonoBehaviour
    {
        [SerializeField] private CaveResonanceDisk resonanceDisk;
        [SerializeField] private Transform portalRing;
        [SerializeField] private Renderer portalRenderer;
        [SerializeField] private Light portalLight;
        [SerializeField] private ParticleSystem portalMist;

        [Header("Thresholds")]
        [SerializeField] private float coherenceThreshold = 0.58f;
        [SerializeField] private float identityThreshold = 0.55f;
        [SerializeField] private float fractureMaximum = 0.35f;
        [SerializeField] private float sourceThreshold = 0.52f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock block;
        private CEFSharedState state;

        public bool IsStable { get; private set; }

        private void Start()
        {
            block = new MaterialPropertyBlock();

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
            if (state == null) return;

            IsStable = resonanceDisk != null &&
                       resonanceDisk.PlayerInside &&
                       state.coherence >= coherenceThreshold &&
                       state.identity >= identityThreshold &&
                       state.fractureRisk <= fractureMaximum &&
                       state.sourceAlignment >= sourceThreshold;

            float ringScale = Mathf.Lerp(0.85f, 1.25f, state.creativeEdge);
            if (portalRing != null)
            {
                portalRing.localScale = Vector3.one * ringScale;
                portalRing.Rotate(Vector3.up, (IsStable ? 22f : 7f) * Time.deltaTime, Space.Self);
            }

            if (portalRenderer != null)
            {
                portalRenderer.GetPropertyBlock(block);
                Color c = IsStable
                    ? Color.Lerp(new Color(0.4f, 0.7f, 1f), new Color(0.8f, 0.6f, 1f), state.creativeEdge)
                    : new Color(0.18f, 0.12f, 0.08f);

                block.SetColor(EmissionColor, c * (IsStable ? 3.5f : 0.35f));
                portalRenderer.SetPropertyBlock(block);
            }

            if (portalLight != null)
            {
                portalLight.color = IsStable ? new Color(0.75f, 0.9f, 1f) : new Color(0.35f, 0.25f, 0.15f);
                portalLight.intensity = IsStable ? 5f : 0.5f;
            }

            if (portalMist != null)
            {
                var emission = portalMist.emission;
                emission.rateOverTime = IsStable ? 35f : 5f;

                if (!portalMist.isPlaying) portalMist.Play();
            }
        }
    }
}
