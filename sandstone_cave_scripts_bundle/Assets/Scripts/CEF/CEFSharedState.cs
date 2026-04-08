using System;
using UnityEngine;

namespace Upsilon.CEF
{
    [Serializable]
    public class CEFSharedState
    {
        public float time;
        public float creativeEdge;
        public float coherence;
        public float novelty;
        public float identity;
        public float fractureRisk;
        public float sourceAlignment;
        public float desireTension;

        public float[] channels = new float[7];

        public bool inventionGlobe;
        public float globeStrength;

        public static CEFSharedState Default()
        {
            return new CEFSharedState
            {
                time = 0f,
                creativeEdge = 0.25f,
                coherence = 0.2f,
                novelty = 0.2f,
                identity = 0.2f,
                fractureRisk = 0.1f,
                sourceAlignment = 0.2f,
                desireTension = 0.2f,
                channels = new float[7] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f },
                inventionGlobe = false,
                globeStrength = 0f
            };
        }
    }
}
