using UnityEngine;

namespace UpsilonPath.Sphere
{
    public class UpsilonPerceptionBridge : MonoBehaviour
    {
        public UpsilonSphere sphere;
        public UpsilonResonanceMemory resonanceMemory;

        [Header("Perception Output")]
        [Range(0f, 1f)] public float clarity;
        [Range(0f, 1f)] public float distortion;
        [Range(0f, 1f)] public float expectationBias = 0.5f;
        [Range(0f, 1f)] public float stationShiftReadiness;
        public UpsilonQuestionNode dominantNode;

        [Header("External Bridge")]
        [Tooltip("Optional tuning loop object from the game layer.")]
        public MonoBehaviour targetPerceptionLoop;

        private void Reset()
        {
            sphere = GetComponent<UpsilonSphere>();
            resonanceMemory = GetComponent<UpsilonResonanceMemory>();
        }

        private void Awake()
        {
            if (sphere == null)
                sphere = GetComponent<UpsilonSphere>();
            if (resonanceMemory == null)
                resonanceMemory = GetComponent<UpsilonResonanceMemory>();
        }

        private void Update()
        {
            if (sphere == null)
                return;

            clarity = Mathf.Clamp01(sphere.perceptionReadiness * sphere.sourceReception);
            distortion = Mathf.Clamp01(sphere.resonancePressure + sphere.fieldNoise * 0.5f);
            dominantNode = ResolveDominantNode();
            stationShiftReadiness = Mathf.Clamp01((clarity + expectationBias) * 0.5f - distortion * 0.25f);

            PushToOptionalTarget();
        }

        private UpsilonQuestionNode ResolveDominantNode()
        {
            if (resonanceMemory != null && resonanceMemory.traces.Count > 0)
            {
                return resonanceMemory.traces[resonanceMemory.traces.Count - 1].dominantNode;
            }

            UpsilonSphereSnapshot snapshot = sphere.CreateSnapshot();
            if (snapshot.nodes == null || snapshot.nodes.Count == 0)
                return UpsilonQuestionNode.What;

            int best = 0;
            float score = float.MinValue;
            for (int i = 0; i < snapshot.nodes.Count; i++)
            {
                float local = snapshot.nodes[i].activation + snapshot.nodes[i].clarity - snapshot.nodes[i].distortion;
                if (local > score)
                {
                    score = local;
                    best = i;
                }
            }

            return snapshot.nodes[best].nodeType;
        }

        private void PushToOptionalTarget()
        {
            if (targetPerceptionLoop == null)
                return;

            targetPerceptionLoop.SendMessage("ApplySpherePerception", this, SendMessageOptions.DontRequireReceiver);
        }
    }
}
