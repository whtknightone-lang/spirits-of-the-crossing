using System;
using System.Collections.Generic;
using UnityEngine;

namespace UpsilonPath.Sphere
{
    public enum SphereOperatingMode
    {
        Resting,
        Perceiving,
        Tuning,
        Expressing,
        Remembering,
        Reorienting
    }

    [Serializable]
    public class UpsilonSphereSnapshot
    {
        public float overallCoherence;
        public float sourceReception;
        public float perceptionReadiness;
        public float resonancePressure;
        public SphereOperatingMode mode;
        public List<UpsilonNodeState> nodes = new List<UpsilonNodeState>();
    }

    public class UpsilonSphere : MonoBehaviour
    {
        [Header("Sphere Core")]
        [Range(0f, 1f)] public float sourceSignal = 0.7f;
        [Range(0f, 1f)] public float fieldNoise = 0.15f;
        [Range(0f, 1f)] public float globalMemoryBias = 0.2f;
        [Range(0f, 1f)] public float perceptionBias = 0.5f;
        public SphereOperatingMode operatingMode = SphereOperatingMode.Resting;

        [Header("Nodes")]
        public List<UpsilonNodeState> nodes = new List<UpsilonNodeState>();

        [Header("Computed")]
        [Range(-1f, 1f)] public float overallCoherence;
        [Range(0f, 1f)] public float sourceReception;
        [Range(0f, 1f)] public float perceptionReadiness;
        [Range(0f, 1f)] public float resonancePressure;

        public event Action<UpsilonSphereSnapshot> OnSphereUpdated;

        public bool AutoInitializeDefaults => nodes == null || nodes.Count == 0;

        // Reset() runs when the component is added via the Unity Editor Inspector.
        // It seeds default nodes for editor-time convenience.
        // At runtime, initialization is owned by UpsilonSphereBootstrap.
        private void Reset()
        {
            InitializeDefaultNodes();
            RecomputeSphere(Time.time);
        }

        private void Awake()
        {
            // Intentionally empty at runtime.
            // Use UpsilonSphereBootstrap (initializeDefaultNodes=true) to populate nodes,
            // or call InitializeDefaultNodes() directly if using the sphere standalone.
        }

        private void Update()
        {
            RecomputeSphere(Time.time);
        }

        public void InitializeDefaultNodes()
        {
            nodes = new List<UpsilonNodeState>();
            foreach (UpsilonQuestionNode nodeType in Enum.GetValues(typeof(UpsilonQuestionNode)))
            {
                nodes.Add(UpsilonNodeFactory.CreateDefault(nodeType));
            }
        }

        public void RecomputeSphere(float time)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            float totalSignal = 0f;
            float totalSource = 0f;
            float totalReadiness = 0f;
            float totalPressure = 0f;

            for (int i = 0; i < nodes.Count; i++)
            {
                UpsilonNodeState node = nodes[i];
                float localSignal = node.EffectiveSignal(time);
                float neighborInfluence = ComputeNeighborInfluence(i, time);

                node.activation = Mathf.Clamp01(node.activation + (neighborInfluence * 0.02f));
                node.clarity = Mathf.Clamp01(node.clarity + ((sourceSignal - node.distortion) * 0.01f));
                node.memoryCharge = Mathf.Clamp01(node.memoryCharge + globalMemoryBias * 0.0025f);
                node.Normalize();

                totalSignal += localSignal;
                totalSource += node.sourceAffinity;
                totalReadiness += node.clarity * node.openness;
                totalPressure += Mathf.Abs(localSignal - sourceSignal) + node.distortion;
            }

            overallCoherence = Mathf.Clamp(totalSignal / nodes.Count, -1f, 1f);
            sourceReception = Mathf.Clamp01((totalSource / nodes.Count) * (1f - fieldNoise * 0.5f));
            perceptionReadiness = Mathf.Clamp01((totalReadiness / nodes.Count) * Mathf.Lerp(0.5f, 1.25f, perceptionBias));
            resonancePressure = Mathf.Clamp01((totalPressure / nodes.Count) * 0.5f);

            OnSphereUpdated?.Invoke(CreateSnapshot());
        }

        public float ComputeNeighborInfluence(int nodeIndex, float time)
        {
            if (nodeIndex < 0 || nodeIndex >= nodes.Count)
                return 0f;

            UpsilonNodeState node = nodes[nodeIndex];
            float influence = 0f;
            for (int i = 0; i < node.linkedNodeIndices.Count; i++)
            {
                int linkedIndex = node.linkedNodeIndices[i];
                if (linkedIndex >= 0 && linkedIndex < nodes.Count)
                {
                    influence += nodes[linkedIndex].EffectiveSignal(time);
                }
            }

            return node.linkedNodeIndices.Count > 0 ? influence / node.linkedNodeIndices.Count : 0f;
        }

        public UpsilonSphereSnapshot CreateSnapshot()
        {
            return new UpsilonSphereSnapshot
            {
                overallCoherence = overallCoherence,
                sourceReception = sourceReception,
                perceptionReadiness = perceptionReadiness,
                resonancePressure = resonancePressure,
                mode = operatingMode,
                nodes = CloneNodes()
            };
        }

        private List<UpsilonNodeState> CloneNodes()
        {
            var clone = new List<UpsilonNodeState>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
            {
                UpsilonNodeState src = nodes[i];
                clone.Add(new UpsilonNodeState
                {
                    nodeType = src.nodeType,
                    activation = src.activation,
                    clarity = src.clarity,
                    openness = src.openness,
                    distortion = src.distortion,
                    memoryCharge = src.memoryCharge,
                    sourceAffinity = src.sourceAffinity,
                    nodeColor = src.nodeColor,
                    band = src.band,
                    linkedNodeIndices = new List<int>(src.linkedNodeIndices)
                });
            }
            return clone;
        }
    }
}
