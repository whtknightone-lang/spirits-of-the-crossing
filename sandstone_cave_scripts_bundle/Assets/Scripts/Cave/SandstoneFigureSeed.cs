using UnityEngine;
using Upsilon.CEF;

namespace Upsilon.Cave
{
    public class SandstoneFigureSeed : MonoBehaviour
    {
        [Header("Seed")]
        [SerializeField] private Transform statueRoot;
        [SerializeField] private Renderer statueRenderer;
        [SerializeField] private float activationThreshold = 0.45f;
        [SerializeField] private float materializeSpeed = 1.5f;

        [Header("Spawn")]
        [SerializeField] private GameObject liveEntityPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private bool autoSpawnOnce = true;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock block;
        private CEFSharedState state;
        private bool spawned;
        private float materializeAmount;

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

            bool shouldWake = state.identity >= activationThreshold || state.coherence >= activationThreshold;
            float target = shouldWake ? 1f : 0f;
            materializeAmount = Mathf.MoveTowards(materializeAmount, target, materializeSpeed * Time.deltaTime);

            if (statueRoot != null)
            {
                float scale = Mathf.Lerp(0.92f, 1.03f, materializeAmount);
                statueRoot.localScale = Vector3.one * scale;
            }

            if (statueRenderer != null)
            {
                statueRenderer.GetPropertyBlock(block);
                Color c = Color.Lerp(new Color(0.15f, 0.1f, 0.05f), new Color(1f, 0.75f, 0.45f), materializeAmount);
                block.SetColor(EmissionColor, c * (0.1f + 1.6f * materializeAmount));
                statueRenderer.SetPropertyBlock(block);
            }

            if (autoSpawnOnce && !spawned && materializeAmount >= 0.98f)
            {
                SpawnEntity();
            }
        }

        public void SpawnEntity()
        {
            if (spawned || liveEntityPrefab == null || spawnPoint == null) return;

            Instantiate(liveEntityPrefab, spawnPoint.position, spawnPoint.rotation);
            spawned = true;
        }
    }
}
