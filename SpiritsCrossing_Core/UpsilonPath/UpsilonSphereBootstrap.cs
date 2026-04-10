using UnityEngine;

namespace UpsilonPath.Sphere
{
    public class UpsilonSphereBootstrap : MonoBehaviour
    {
        public bool autoCreateOnThisObject = true;
        public bool initializeDefaultNodes = true;
        public bool logSetup = true;

        [Header("Optional Links")]
        public MonoBehaviour perceptionLoopTarget;

        private void Awake()
        {
            if (!autoCreateOnThisObject)
                return;

            UpsilonSphere sphere = GetOrAdd<UpsilonSphere>();
            UpsilonResonanceMemory memory = GetOrAdd<UpsilonResonanceMemory>();
            UpsilonPerceptionBridge bridge = GetOrAdd<UpsilonPerceptionBridge>();

            if (initializeDefaultNodes && (sphere.nodes == null || sphere.nodes.Count == 0))
            {
                sphere.InitializeDefaultNodes();
                sphere.RecomputeSphere(Time.time);
            }

            memory.sphere = sphere;
            bridge.sphere = sphere;
            bridge.resonanceMemory = memory;
            bridge.targetPerceptionLoop = perceptionLoopTarget;

            if (logSetup)
            {
                Debug.Log($"[UpsilonSphereBootstrap] Ready on '{gameObject.name}' with {sphere.nodes.Count} nodes.", this);
            }
        }

        private T GetOrAdd<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component == null)
                component = gameObject.AddComponent<T>();
            return component;
        }
    }
}
