using System;
using UnityEngine;

namespace Upsilon.CEF
{
    [Serializable]
    public class BiometricsData
    {
        public float hrv;
        public float eda;
        public float emg;
        public float resp;
        public float imu;
        public float temp;
    }

    [Serializable]
    public class GlobeEvent
    {
        public string type;
        public float strength;
    }

    [Serializable]
    public class CEFPacket
    {
        public float t;
        public string entity;
        public float creative_edge;
        public float coherence;
        public float novelty;
        public float identity;
        public float fracture_risk;
        public float source_alignment;
        public float desire_tension;
        public float shadow_pressure;
        public float[] channels;
        public GlobeEvent[] globe_events;
        public BiometricsData biometrics;
    }

    [Serializable]
    public class CEFEntityState
    {
        public string entityId;
        public float timestamp;
        public float creativeEdge;
        public float coherence;
        public float novelty;
        public float identity;
        public float fractureRisk;
        public float sourceAlignment;
        public float desireTension;
        public float shadowPressure;
        public float[] channels = new float[7];
        public BiometricsData biometrics = new BiometricsData();
        public GlobeEvent[] globeEvents = Array.Empty<GlobeEvent>();

        public void Apply(CEFPacket packet)
        {
            entityId = packet.entity;
            timestamp = packet.t;
            creativeEdge = packet.creative_edge;
            coherence = packet.coherence;
            novelty = packet.novelty;
            identity = packet.identity;
            fractureRisk = packet.fracture_risk;
            sourceAlignment = packet.source_alignment;
            desireTension = packet.desire_tension;
            shadowPressure = packet.shadow_pressure;
            if (packet.channels != null && packet.channels.Length == 7)
            {
                Array.Copy(packet.channels, channels, 7);
            }
            biometrics = packet.biometrics ?? biometrics;
            globeEvents = packet.globe_events ?? Array.Empty<GlobeEvent>();
        }
    }
}
