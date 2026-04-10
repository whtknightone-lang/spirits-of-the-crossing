using System;
using System.Collections.Generic;
using UnityEngine;

namespace UpsilonPath.Sphere
{
    public enum UpsilonQuestionNode
    {
        What,
        Why,
        When,
        Where,
        Who,
        WhatIf,
        ForWhatReason
    }

    [Serializable]
    public struct UpsilonFrequencyBand
    {
        public float baseFrequency;
        public float amplitude;
        public float phase;
        public float coherence;

        public float Sample(float time)
        {
            return amplitude * Mathf.Sin((time * baseFrequency) + phase) * Mathf.Clamp01(coherence);
        }
    }

    [Serializable]
    public class UpsilonNodeState
    {
        public UpsilonQuestionNode nodeType;
        [Range(0f, 1f)] public float activation = 0.5f;
        [Range(0f, 1f)] public float clarity = 0.5f;
        [Range(0f, 1f)] public float openness = 0.5f;
        [Range(0f, 1f)] public float distortion = 0.0f;
        [Range(0f, 1f)] public float memoryCharge = 0.0f;
        [Range(0f, 1f)] public float sourceAffinity = 0.5f;
        public Color nodeColor = Color.white;
        public UpsilonFrequencyBand band;
        public List<int> linkedNodeIndices = new List<int>();

        public float EffectiveSignal(float time)
        {
            float wave = band.Sample(time);
            float signal = activation * clarity * openness * Mathf.Lerp(1f, sourceAffinity, 0.5f);
            float noise = Mathf.Clamp01(distortion);
            return Mathf.Clamp(signal + wave - noise, -1f, 1f);
        }

        public void Normalize()
        {
            activation = Mathf.Clamp01(activation);
            clarity = Mathf.Clamp01(clarity);
            openness = Mathf.Clamp01(openness);
            distortion = Mathf.Clamp01(distortion);
            memoryCharge = Mathf.Clamp01(memoryCharge);
            sourceAffinity = Mathf.Clamp01(sourceAffinity);
            band.coherence = Mathf.Clamp01(band.coherence);
        }
    }

    public static class UpsilonNodeFactory
    {
        public static UpsilonNodeState CreateDefault(UpsilonQuestionNode nodeType)
        {
            int idx = (int)nodeType;
            return new UpsilonNodeState
            {
                nodeType = nodeType,
                activation = 0.5f,
                clarity = 0.5f,
                openness = 0.5f,
                distortion = 0.1f,
                memoryCharge = 0f,
                sourceAffinity = 0.6f,
                nodeColor = Color.HSVToRGB(idx / 7f, 0.75f, 1f),
                band = new UpsilonFrequencyBand
                {
                    baseFrequency = 0.25f + (idx * 0.08f),
                    amplitude = 0.15f,
                    phase = idx * 0.5f,
                    coherence = 0.7f
                },
                linkedNodeIndices = BuildDefaultLinks(idx)
            };
        }

        private static List<int> BuildDefaultLinks(int idx)
        {
            return new List<int>
            {
                (idx + 1) % 7,
                (idx + 6) % 7,
                (idx + 3) % 7
            };
        }
    }
}
