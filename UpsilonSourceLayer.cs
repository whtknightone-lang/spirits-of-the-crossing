using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.SourceWorld
{
    [Serializable]
    public class UpsilonNodeDefinition
    {
        public string nodeId;
        public string band;
        public float naturalFrequency = 0.35f;
        [Range(0f, 2f)] public float sourceWeight = 1f;
        [Range(0f, 2f)] public float aiWeight = 1f;
    }

    [Serializable]
    public class UpsilonNodeState
    {
        public float phase;
        public float amplitude;
        public float coherence;
        public float target;
    }

    [Serializable]
    public class UpsilonSourceSignal
    {
        [Range(0f, 1f)] public float sourceDrive;
        [Range(0f, 1f)] public float aiDrive;
        [Range(0f, 1f)] public float unifiedIntent;
        [Range(0f, 1f)] public float coherence;
        [Range(0f, 1f)] public float invitation;
        [Range(0f, 1f)] public float emergence;
        [Range(0f, 1f)] public float stillness;
        public string currentMode = "idle";
        public bool isActive;
    }

    /// <summary>
    /// A 7-band Upsilon layer that sits above SourceWorld discovery logic and turns
    /// player/world resonance into a continuous AI/source drive signal.
    ///
    /// Intention:
    /// - stillness bands (green/blue/indigo/violet) feed Source presence
    /// - active bands (red/orange/yellow) feed AI emergence/action
    /// - node coherence becomes the bridge between them
    ///
    /// This is designed to bolt onto SourceWorldSpawner without replacing the existing myth,
    /// pool, gate, or well systems.
    /// </summary>
    public class UpsilonSourceLayer : MonoBehaviour
    {
        public static UpsilonSourceLayer Instance { get; private set; }

        [Header("Activation")]
        public bool runOnlyInSourceWorld = true;
        public bool emitIntoVibrationSystem = true;

        [Header("Node Dynamics")]
        public float coupling = 1.15f;
        public float damping = 0.22f;
        public float phaseSpeed = 1.0f;
        public float amplitudeResponse = 3.0f;
        public float discoveryPulseDecay = 0.45f;

        [Header("Emission")]
        public float sourceEmissionGain = 0.05f;
        public float aiEmissionGain = 0.05f;
        public float bridgeEmissionGain = 0.04f;

        [Header("Nodes")]
        public List<UpsilonNodeDefinition> nodes = new();

        public UpsilonSourceSignal CurrentSignal { get; private set; } = new UpsilonSourceSignal();
        public event Action<UpsilonSourceSignal> OnSignalUpdated;

        private readonly List<UpsilonNodeState> _states = new();
        private SourceWorldSpawner _spawner;
        private SourceWorldZone _zone;
        private bool _active;
        private float _discoveryPulse;
        private string _lastPulseTag = "";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (nodes == null || nodes.Count == 0)
                BuildDefaultNodes();

            RebuildState();
        }

        private void BuildDefaultNodes()
        {
            nodes = new List<UpsilonNodeDefinition>
            {
                new UpsilonNodeDefinition { nodeId = "u_red",    band = "red",    naturalFrequency = 0.92f, sourceWeight = 0.30f, aiWeight = 1.20f },
                new UpsilonNodeDefinition { nodeId = "u_orange", band = "orange", naturalFrequency = 0.82f, sourceWeight = 0.35f, aiWeight = 1.15f },
                new UpsilonNodeDefinition { nodeId = "u_yellow", band = "yellow", naturalFrequency = 0.72f, sourceWeight = 0.45f, aiWeight = 1.00f },
                new UpsilonNodeDefinition { nodeId = "u_green",  band = "green",  naturalFrequency = 0.56f, sourceWeight = 1.10f, aiWeight = 0.55f },
                new UpsilonNodeDefinition { nodeId = "u_blue",   band = "blue",   naturalFrequency = 0.48f, sourceWeight = 1.15f, aiWeight = 0.45f },
                new UpsilonNodeDefinition { nodeId = "u_indigo", band = "indigo", naturalFrequency = 0.40f, sourceWeight = 1.20f, aiWeight = 0.35f },
                new UpsilonNodeDefinition { nodeId = "u_violet", band = "violet", naturalFrequency = 0.34f, sourceWeight = 1.25f, aiWeight = 0.25f },
            };
        }

        private void RebuildState()
        {
            _states.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                _states.Add(new UpsilonNodeState
                {
                    phase = (Mathf.PI * 2f * i) / Mathf.Max(1, nodes.Count),
                    amplitude = 0f,
                    coherence = 0f,
                    target = 0f,
                });
            }
        }

        public void EnterSourceZone(SourceWorldZone zone, SourceWorldSpawner spawner)
        {
            _zone = zone;
            _spawner = spawner;
            _active = zone != null;
            CurrentSignal.isActive = _active;
        }

        public void ExitSourceZone()
        {
            _active = false;
            _zone = null;
            CurrentSignal = new UpsilonSourceSignal { isActive = false, currentMode = "idle" };
            ZeroStates();
            OnSignalUpdated?.Invoke(CurrentSignal);
        }

        public void RegisterDiscoveryPulse(float intensity, string tag)
        {
            _discoveryPulse = Mathf.Clamp01(_discoveryPulse + intensity);
            _lastPulseTag = tag ?? "discovery";
        }

        public void TickLayer(float dt, SourceWorldSpawner spawner)
        {
            if (!_active || (runOnlyInSourceWorld && (spawner == null || string.IsNullOrEmpty(spawner.ActiveZoneId))))
                return;

            _spawner = spawner ?? _spawner;
            if (_spawner == null || nodes.Count == 0 || _states.Count != nodes.Count)
                return;

            var brain = FindObjectOfType<SpiritsCrossing.SpiritAI.SpiritBrainOrchestrator>();
            var player = brain?.CurrentPlayerState;
            float calm = Mathf.Clamp01(player?.calm ?? 0f);
            float sourceAlignment = Mathf.Clamp01(player?.sourceAlignment ?? 0f);
            float breathCoherence = Mathf.Clamp01(player?.breathCoherence ?? 0f);
            float wonder = Mathf.Clamp01(player?.wonder ?? 0f);

            float myth = Mathf.Clamp01(SpiritsCrossing.SourceWorld.UniverseStateManager.Instance?.Current?.mythState.environmentalIntensity ?? 0f);
            float completion = Mathf.Clamp01(_spawner.ZoneCompletion01());
            float gateProgress = Mathf.Clamp01(_spawner.AverageGateProgress01());
            float discoveryBias = Mathf.Clamp01(_spawner.SourceDiscoveryBias());

            float redTarget = Mathf.Clamp01(0.40f * wonder + 0.35f * gateProgress + 0.25f * _discoveryPulse);
            float orangeTarget = Mathf.Clamp01(0.40f * wonder + 0.30f * completion + 0.30f * _discoveryPulse);
            float yellowTarget = Mathf.Clamp01(0.45f * wonder + 0.30f * breathCoherence + 0.25f * completion);
            float greenTarget = Mathf.Clamp01(0.55f * calm + 0.25f * breathCoherence + 0.20f * completion);
            float blueTarget = Mathf.Clamp01(0.50f * breathCoherence + 0.25f * calm + 0.25f * myth);
            float indigoTarget = Mathf.Clamp01(0.55f * sourceAlignment + 0.20f * breathCoherence + 0.25f * myth);
            float violetTarget = Mathf.Clamp01(0.50f * myth + 0.25f * sourceAlignment + 0.25f * discoveryBias);

            float sourceTarget = Mathf.Clamp01((greenTarget + blueTarget + indigoTarget + violetTarget) * 0.25f);
            float aiTarget = Mathf.Clamp01((redTarget + orangeTarget + yellowTarget) / 3f);
            float bridgeTarget = Mathf.Clamp01(0.50f * sourceTarget + 0.50f * aiTarget);

            for (int i = 0; i < nodes.Count; i++)
            {
                var def = nodes[i];
                var state = _states[i];

                state.target = def.band switch
                {
                    "red" => redTarget,
                    "orange" => orangeTarget,
                    "yellow" => yellowTarget,
                    "green" => greenTarget,
                    "blue" => blueTarget,
                    "indigo" => indigoTarget,
                    "violet" => violetTarget,
                    _ => bridgeTarget,
                };

                float leftPhase = _states[(i - 1 + _states.Count) % _states.Count].phase;
                float rightPhase = _states[(i + 1) % _states.Count].phase;
                float couplingTerm = Mathf.Sin(leftPhase - state.phase) + Mathf.Sin(rightPhase - state.phase);
                float drive = Mathf.Lerp(sourceTarget, aiTarget, def.aiWeight / Mathf.Max(0.001f, def.aiWeight + def.sourceWeight));

                state.phase += dt * phaseSpeed * (def.naturalFrequency + coupling * couplingTerm + 0.35f * drive);
                state.amplitude = Mathf.MoveTowards(state.amplitude, state.target, amplitudeResponse * dt);
                state.amplitude = Mathf.Clamp01(state.amplitude - damping * dt + Mathf.Max(0f, _discoveryPulse * 0.15f * dt));

                float localPhaseAverage = (leftPhase + rightPhase) * 0.5f;
                state.coherence = Mathf.Clamp01(0.5f + 0.5f * Mathf.Cos(state.phase - localPhaseAverage));
            }

            _discoveryPulse = Mathf.MoveTowards(_discoveryPulse, 0f, discoveryPulseDecay * dt);

            float sourceDrive = 0f;
            float aiDrive = 0f;
            float coherence = 0f;
            float stillness = 0f;
            float emergence = 0f;

            for (int i = 0; i < nodes.Count; i++)
            {
                float energy = _states[i].amplitude * _states[i].coherence;
                sourceDrive += energy * nodes[i].sourceWeight;
                aiDrive += energy * nodes[i].aiWeight;
                coherence += _states[i].coherence;

                if (nodes[i].band == "green" || nodes[i].band == "blue" || nodes[i].band == "indigo" || nodes[i].band == "violet")
                    stillness += energy;
                else
                    emergence += energy;
            }

            float sourceWeightSum = 0f;
            float aiWeightSum = 0f;
            foreach (var n in nodes)
            {
                sourceWeightSum += n.sourceWeight;
                aiWeightSum += n.aiWeight;
            }

            sourceDrive = Mathf.Clamp01(sourceDrive / Mathf.Max(0.001f, sourceWeightSum));
            aiDrive = Mathf.Clamp01(aiDrive / Mathf.Max(0.001f, aiWeightSum));
            coherence = Mathf.Clamp01(coherence / Mathf.Max(1, nodes.Count));
            stillness = Mathf.Clamp01(stillness / 4f);
            emergence = Mathf.Clamp01(emergence / 3f);

            CurrentSignal.sourceDrive = sourceDrive;
            CurrentSignal.aiDrive = aiDrive;
            CurrentSignal.unifiedIntent = Mathf.Clamp01(0.5f * (sourceDrive + aiDrive) * (0.65f + 0.35f * coherence));
            CurrentSignal.coherence = coherence;
            CurrentSignal.invitation = Mathf.Clamp01(0.65f * stillness + 0.35f * sourceDrive);
            CurrentSignal.emergence = emergence;
            CurrentSignal.stillness = stillness;
            CurrentSignal.isActive = true;
            CurrentSignal.currentMode = DetermineMode(stillness, emergence, coherence, _lastPulseTag);

            if (emitIntoVibrationSystem)
                EmitTransientBoosts(dt);

            OnSignalUpdated?.Invoke(CurrentSignal);
        }

        private void EmitTransientBoosts(float dt)
        {
            var resonance = VibrationalResonanceSystem.Instance;
            if (resonance == null) return;

            resonance.ApplyTransientBoost("green",  CurrentSignal.stillness * sourceEmissionGain * dt);
            resonance.ApplyTransientBoost("blue",   CurrentSignal.coherence * bridgeEmissionGain * dt);
            resonance.ApplyTransientBoost("indigo", CurrentSignal.sourceDrive * sourceEmissionGain * dt);
            resonance.ApplyTransientBoost("violet", CurrentSignal.invitation * sourceEmissionGain * dt);

            resonance.ApplyTransientBoost("red",    CurrentSignal.aiDrive * aiEmissionGain * dt);
            resonance.ApplyTransientBoost("orange", CurrentSignal.emergence * aiEmissionGain * dt);
            resonance.ApplyTransientBoost("yellow", CurrentSignal.unifiedIntent * bridgeEmissionGain * dt);
        }

        private string DetermineMode(float stillness, float emergence, float coherence, string pulseTag)
        {
            if (!string.IsNullOrEmpty(pulseTag) && _discoveryPulse > 0.20f)
                return pulseTag;
            if (coherence > 0.72f && stillness > emergence)
                return "source_guidance";
            if (coherence > 0.72f && emergence >= stillness)
                return "ai_manifestation";
            if (stillness > 0.55f)
                return "deep_listening";
            if (emergence > 0.55f)
                return "agent_waking";
            return "balancing";
        }

        private void ZeroStates()
        {
            foreach (var state in _states)
            {
                state.phase = 0f;
                state.amplitude = 0f;
                state.coherence = 0f;
                state.target = 0f;
            }
        }
    }
}
