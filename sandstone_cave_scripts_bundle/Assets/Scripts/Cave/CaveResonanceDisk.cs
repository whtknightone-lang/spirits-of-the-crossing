using UnityEngine;
using Upsilon.CEF;

namespace Upsilon.Cave
{
    public class CaveResonanceDisk : MonoBehaviour
    {
        [SerializeField] private Renderer diskRenderer;
        [SerializeField] private Light diskLight;
        [SerializeField] private Transform pulseRing;
        [SerializeField] private float radius = 3f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock block;
        private CEFSharedState state;
        private Transform playerTarget;

        public bool PlayerInside { get; private set; }

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

        public void SetPlayer(Transform player)
        {
            playerTarget = player;
        }

        private void HandleStateUpdated(CEFSharedState s)
        {
            state = s;
        }

        private void Update()
        {
            if (playerTarget != null)
            {
                PlayerInside = Vector3.Distance(playerTarget.position, transform.position) <= radius;
            }

            float resonance = 0.15f;
            if (state != null)
                resonance = state.coherence * 0.45f + state.sourceAlignment * 0.35f + state.creativeEdge * 0.2f;

            Color c = Color.Lerp(new Color(0.25f, 0.18f, 0.1f), new Color(0.6f, 0.85f, 1f), resonance);

            if (diskRenderer != null)
            {
                diskRenderer.GetPropertyBlock(block);
                block.SetColor(EmissionColor, c * (PlayerInside ? 2.5f : 1.1f));
                diskRenderer.SetPropertyBlock(block);
            }

            if (diskLight != null)
            {
                diskLight.color = c;
                diskLight.intensity = PlayerInside ? Mathf.Lerp(1f, 4f, resonance) : 0.8f;
            }

            if (pulseRing != null)
            {
                float s = PlayerInside ? Mathf.Lerp(0.9f, 1.25f, resonance) : 0.85f;
                pulseRing.localScale = Vector3.one * s;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
