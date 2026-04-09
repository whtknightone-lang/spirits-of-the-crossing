using System;
using System.Collections.Generic;
using UnityEngine;

namespace UpsilonPath.Sphere
{
    [Serializable]
    public class ResonanceMemoryTrace
    {
        public string label;
        public float timestamp;
        public float coherence;
        public float sourceReception;
        public float emotionalCharge;
        public UpsilonQuestionNode dominantNode;
    }

    public class UpsilonResonanceMemory : MonoBehaviour
    {
        public UpsilonSphere sphere;
        public int maxTraces = 64;
        public float captureThreshold = 0.55f;
        [Range(0f, 1f)] public float emotionalCharge = 0.5f;
        public List<ResonanceMemoryTrace> traces = new List<ResonanceMemoryTrace>();

        private void Reset()
        {
            sphere = GetComponent<UpsilonSphere>();
        }

        private void Awake()
        {
            if (sphere == null)
                sphere = GetComponent<UpsilonSphere>();
        }

        private void OnEnable()
        {
            if (sphere != null)
                sphere.OnSphereUpdated += HandleSphereUpdated;
        }

        private void OnDisable()
        {
            if (sphere != null)
                sphere.OnSphereUpdated -= HandleSphereUpdated;
        }

        private void HandleSphereUpdated(UpsilonSphereSnapshot snapshot)
        {
            if (snapshot.overallCoherence >= captureThreshold || snapshot.sourceReception >= captureThreshold)
            {
                CaptureTrace(snapshot, InferDominantNode(snapshot));
            }
        }

        public void CaptureTrace(UpsilonSphereSnapshot snapshot, UpsilonQuestionNode dominantNode, string label = "auto_trace")
        {
            traces.Add(new ResonanceMemoryTrace
            {
                label = label,
                timestamp = Time.time,
                coherence = snapshot.overallCoherence,
                sourceReception = snapshot.sourceReception,
                emotionalCharge = emotionalCharge,
                dominantNode = dominantNode
            });

            while (traces.Count > maxTraces)
            {
                traces.RemoveAt(0);
            }
        }

        public UpsilonQuestionNode InferDominantNode(UpsilonSphereSnapshot snapshot)
        {
            if (snapshot.nodes == null || snapshot.nodes.Count == 0)
                return UpsilonQuestionNode.What;

            int bestIndex = 0;
            float bestValue = float.MinValue;
            for (int i = 0; i < snapshot.nodes.Count; i++)
            {
                float score = snapshot.nodes[i].activation + snapshot.nodes[i].clarity + snapshot.nodes[i].sourceAffinity - snapshot.nodes[i].distortion;
                if (score > bestValue)
                {
                    bestValue = score;
                    bestIndex = i;
                }
            }

            return snapshot.nodes[bestIndex].nodeType;
        }

        public float GetMemoryBias(UpsilonQuestionNode nodeType)
        {
            float total = 0f;
            int count = 0;
            for (int i = 0; i < traces.Count; i++)
            {
                if (traces[i].dominantNode == nodeType)
                {
                    total += traces[i].coherence * Mathf.Lerp(0.5f, 1.5f, traces[i].emotionalCharge);
                    count++;
                }
            }

            return count == 0 ? 0f : Mathf.Clamp01(total / count);
        }
    }
}
