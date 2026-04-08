using UnityEngine;

namespace SpiritsCrossing.Vibration
{
    /// <summary>
    /// Fast scene bootstrapper.
    /// Creates a SourceBrain, a chamber, three field zones, and a population of agents.
    /// This is intentionally simple so you can edit it into your larger framework.
    /// </summary>
    public class SimulationBootstrap : MonoBehaviour
    {
        [Header("Population")]
        [Min(1)] public int agentCount = 12;
        public GameObject agentPrefab;
        [Min(2f)] public float spawnRadius = 10f;

        [Header("Auto Create")]
        public bool bootstrapOnStart = true;
        public bool createDemoEmitters = true;
        public bool usePrimitiveSphereIfNoPrefab = true;

        [Header("Scene References")]
        public SourceBrain sourceBrain;
        public RebirthChamber rebirthChamber;

        private bool _bootstrapped;

        private void Start()
        {
            if (bootstrapOnStart)
                BootstrapNow();
        }

        [ContextMenu("Bootstrap Now")]
        public void BootstrapNow()
        {
            if (_bootstrapped) return;

            EnsureCoreObjects();

            if (createDemoEmitters)
                EnsureEmitter("Emitter_Grove", new Vector3(-10f, 0f, 0f), WorldFieldEmitter.FieldPreset.Grove);
            if (createDemoEmitters)
                EnsureEmitter("Emitter_Flame", new Vector3(10f, 0f, 0f), WorldFieldEmitter.FieldPreset.Flame);
            if (createDemoEmitters)
                EnsureEmitter("Emitter_Shrine", new Vector3(0f, 0f, 10f), WorldFieldEmitter.FieldPreset.Shrine);

            for (int i = 0; i < agentCount; i++)
                SpawnAgent(i);

            _bootstrapped = true;
        }

        private void EnsureCoreObjects()
        {
            if (sourceBrain == null)
                sourceBrain = FindObjectOfType<SourceBrain>();

            if (sourceBrain == null)
            {
                GameObject go = new GameObject("SourceBrain");
                sourceBrain = go.AddComponent<SourceBrain>();
            }

            if (rebirthChamber == null)
                rebirthChamber = FindObjectOfType<RebirthChamber>();

            if (rebirthChamber == null)
            {
                GameObject go = new GameObject("RebirthChamber");
                go.transform.position = Vector3.zero;
                rebirthChamber = go.AddComponent<RebirthChamber>();
            }

            sourceBrain.rebirthChamber = rebirthChamber;
        }

        private void EnsureEmitter(string objectName, Vector3 position, WorldFieldEmitter.FieldPreset preset)
        {
            GameObject found = GameObject.Find(objectName);
            if (found != null) return;

            GameObject go = new GameObject(objectName);
            go.transform.position = position;
            WorldFieldEmitter emitter = go.AddComponent<WorldFieldEmitter>();
            emitter.preset = preset;
            emitter.usePresetBands = true;
            emitter.radius = 8f;
            emitter.strength = 0.70f;
        }

        private void SpawnAgent(int index)
        {
            GameObject instance = CreateAgentObject(index);
            instance.name = "UpsilonAgent_" + index.ToString("00");

            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            instance.transform.position = new Vector3(circle.x, 0f, circle.y);

            UpsilonSphereAI sphere = instance.GetComponent<UpsilonSphereAI>();
            if (sphere != null && string.IsNullOrWhiteSpace(sphere.entityId))
                sphere.entityId = instance.name;

            EmbodiedAgentController controller = instance.GetComponent<EmbodiedAgentController>();
            if (controller != null)
                controller.arenaCenter = Vector3.zero;
        }

        private GameObject CreateAgentObject(int index)
        {
            if (agentPrefab != null)
                return Instantiate(agentPrefab);

            if (usePrimitiveSphereIfNoPrefab)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = Vector3.one * 0.9f;
                if (go.GetComponent<Collider>() != null)
                    Destroy(go.GetComponent<Collider>());
                AddAgentComponents(go, index);
                return go;
            }

            GameObject fallback = new GameObject();
            AddAgentComponents(fallback, index);
            return fallback;
        }

        private static void AddAgentComponents(GameObject go, int index)
        {
            if (go.GetComponent<UpsilonSphereAI>() == null)
                go.AddComponent<UpsilonSphereAI>();

            if (go.GetComponent<EmbodiedAgentController>() == null)
                go.AddComponent<EmbodiedAgentController>();

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                float t = index / 12f;
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = Color.Lerp(new Color(0.45f, 0.65f, 1f), new Color(0.95f, 0.65f, 0.95f), t);
            }
        }
    }
}
