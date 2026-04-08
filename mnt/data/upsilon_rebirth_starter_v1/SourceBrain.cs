using System.Collections.Generic;
using UnityEngine;

namespace SpiritsCrossing.Vibration
{
    /// <summary>
    /// Global resonance manager for a small rebirth simulation.
    /// Keeps a lightweight Source field alive and provides a registry
    /// for agents and field emitters.
    /// </summary>
    public class SourceBrain : MonoBehaviour
    {
        public static SourceBrain Instance { get; private set; }

        [Header("Global Source Field")]
        [Range(0f, 1f)] public float red = 0.18f;
        [Range(0f, 1f)] public float orange = 0.22f;
        [Range(0f, 1f)] public float yellow = 0.28f;
        [Range(0f, 1f)] public float green = 0.52f;
        [Range(0f, 1f)] public float blue = 0.40f;
        [Range(0f, 1f)] public float indigo = 0.34f;
        [Range(0f, 1f)] public float violet = 0.48f;

        [Tooltip("How strongly Source touches all living agents every frame.")]
        [Range(0f, 1f)] public float sourceStrength = 0.18f;

        [Tooltip("Slow global pulse that keeps the world from feeling static.")]
        [Range(0f, 2f)] public float pulseSpeed = 0.25f;

        [Range(0f, 0.25f)] public float greenPulse = 0.08f;
        [Range(0f, 0.25f)] public float violetPulse = 0.06f;

        [Header("Scene References")]
        public RebirthChamber rebirthChamber;

        public VibrationalField GlobalField { get; private set; } = new VibrationalField();
        public IReadOnlyList<UpsilonSphereAI> Agents => _agents;
        public IReadOnlyList<WorldFieldEmitter> Emitters => _emitters;

        private readonly List<UpsilonSphereAI> _agents = new List<UpsilonSphereAI>();
        private readonly List<WorldFieldEmitter> _emitters = new List<WorldFieldEmitter>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildGlobalField();
        }

        private void Update()
        {
            BuildGlobalField();

            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                UpsilonSphereAI agent = _agents[i];
                if (agent == null)
                {
                    _agents.RemoveAt(i);
                    continue;
                }

                agent.Sense(GlobalField, sourceStrength);
            }
        }

        public void RegisterAgent(UpsilonSphereAI agent)
        {
            if (agent == null || _agents.Contains(agent)) return;
            _agents.Add(agent);
        }

        public void UnregisterAgent(UpsilonSphereAI agent)
        {
            if (agent == null) return;
            _agents.Remove(agent);
        }

        public void RegisterEmitter(WorldFieldEmitter emitter)
        {
            if (emitter == null || _emitters.Contains(emitter)) return;
            _emitters.Add(emitter);
        }

        public void UnregisterEmitter(WorldFieldEmitter emitter)
        {
            if (emitter == null) return;
            _emitters.Remove(emitter);
        }

        private void BuildGlobalField()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f);

            float g = Mathf.Clamp01(green + greenPulse * pulse);
            float v = Mathf.Clamp01(violet + violetPulse * (1f - pulse));

            GlobalField = new VibrationalField(
                Mathf.Clamp01(red),
                Mathf.Clamp01(orange),
                Mathf.Clamp01(yellow),
                g,
                Mathf.Clamp01(blue),
                Mathf.Clamp01(indigo),
                v);
        }
    }
}
