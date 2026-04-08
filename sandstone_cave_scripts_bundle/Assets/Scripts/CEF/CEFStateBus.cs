using System;
using UnityEngine;

namespace Upsilon.CEF
{
    public class CEFStateBus : MonoBehaviour
    {
        public static CEFStateBus Instance { get; private set; }

        public CEFSharedState CurrentState { get; private set; }

        public event Action<CEFSharedState> OnStateUpdated;

        [SerializeField] private bool useSyntheticState = true;
        [SerializeField] private float syntheticSpeed = 0.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            CurrentState = CEFSharedState.Default();
        }

        private void Update()
        {
            if (!useSyntheticState) return;

            float t = Time.time * syntheticSpeed;

            CurrentState.time = Time.time;
            CurrentState.coherence = 0.45f + 0.25f * Mathf.Sin(t * 0.7f);
            CurrentState.novelty = 0.5f + 0.2f * Mathf.Sin(t * 1.1f + 1.5f);
            CurrentState.identity = 0.4f + 0.3f * Mathf.Sin(t * 0.5f + 0.7f);
            CurrentState.fractureRisk = Mathf.Clamp01(0.2f + 0.15f * Mathf.Sin(t * 1.9f + 2f));
            CurrentState.sourceAlignment = 0.5f + 0.25f * Mathf.Sin(t * 0.6f + 0.2f);
            CurrentState.desireTension = 0.5f + 0.25f * Mathf.Sin(t * 1.3f + 2.2f);
            CurrentState.creativeEdge = ComputeCreativeEdge(CurrentState.coherence, CurrentState.novelty, CurrentState.sourceAlignment, CurrentState.desireTension, CurrentState.fractureRisk);

            for (int i = 0; i < CurrentState.channels.Length; i++)
            {
                CurrentState.channels[i] = 0.5f + 0.45f * Mathf.Sin(t * (0.8f + i * 0.07f) + i * 0.6f);
            }

            CurrentState.inventionGlobe = CurrentState.creativeEdge > 0.68f && CurrentState.coherence > 0.42f;
            CurrentState.globeStrength = CurrentState.inventionGlobe ? Mathf.InverseLerp(0.68f, 0.95f, CurrentState.creativeEdge) : 0f;

            OnStateUpdated?.Invoke(CurrentState);
        }

        public void PushState(CEFSharedState newState)
        {
            CurrentState = newState;
            OnStateUpdated?.Invoke(CurrentState);
        }

        private float ComputeCreativeEdge(float c, float n, float s, float d, float q)
        {
            float midBand = 4f * c * (1f - c);
            float score = 0.35f * midBand + 0.2f * n + 0.2f * s + 0.2f * d - 0.15f * q;
            return Mathf.Clamp01(score);
        }
    }
}
