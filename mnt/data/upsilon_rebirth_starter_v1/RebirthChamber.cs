using System.Collections.Generic;
using UnityEngine;

namespace SpiritsCrossing.Vibration
{
    /// <summary>
    /// Small chamber that catches respawning agents and releases them back into the world.
    /// It also provides a rebirth-biased field while the agent is inside the chamber.
    /// </summary>
    public class RebirthChamber : MonoBehaviour
    {
        [Header("Spawn Layout")]
        public List<Transform> releasePoints = new List<Transform>();
        [Min(0.5f)] public float holdRadius = 1.5f;
        [Min(0.5f)] public float releaseRadius = 3f;

        [Header("Chamber Field")]
        [Range(0f, 1f)] public float chamberSenseStrength = 0.45f;
        [Range(0f, 1f)] public float red = 0.08f;
        [Range(0f, 1f)] public float orange = 0.10f;
        [Range(0f, 1f)] public float yellow = 0.16f;
        [Range(0f, 1f)] public float green = 0.42f;
        [Range(0f, 1f)] public float blue = 0.34f;
        [Range(0f, 1f)] public float indigo = 0.68f;
        [Range(0f, 1f)] public float violet = 0.84f;

        private readonly Dictionary<EmbodiedAgentController, Vector3> _heldOffsets = new Dictionary<EmbodiedAgentController, Vector3>();

        public VibrationalField ChamberField()
        {
            return new VibrationalField(red, orange, yellow, green, blue, indigo, violet);
        }

        public void Receive(EmbodiedAgentController controller)
        {
            if (controller == null) return;

            Vector3 offset = HashOffset(controller.name, holdRadius);
            _heldOffsets[controller] = offset;
            controller.transform.position = transform.position + offset;
        }

        public void Hold(EmbodiedAgentController controller)
        {
            if (controller == null) return;

            if (!_heldOffsets.ContainsKey(controller))
                _heldOffsets[controller] = HashOffset(controller.name, holdRadius);

            controller.transform.position = transform.position + _heldOffsets[controller];
        }

        public void Release(EmbodiedAgentController controller)
        {
            if (controller == null) return;

            Vector3 spawn = transform.position + Random.insideUnitSphere * releaseRadius;
            spawn.y = transform.position.y;

            if (releasePoints != null && releasePoints.Count > 0)
            {
                Transform t = releasePoints[Random.Range(0, releasePoints.Count)];
                if (t != null) spawn = t.position;
            }

            controller.transform.position = spawn;
            _heldOffsets.Remove(controller);
        }

        private static Vector3 HashOffset(string seed, float radius)
        {
            int hash = string.IsNullOrEmpty(seed) ? 1 : seed.GetHashCode();
            Random.State previous = Random.state;
            Random.InitState(hash);
            Vector2 circle = Random.insideUnitCircle * radius;
            Random.state = previous;
            return new Vector3(circle.x, 0f, circle.y);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0.5f, 1f, 0.25f);
            Gizmos.DrawSphere(transform.position, holdRadius);
            Gizmos.color = new Color(0.7f, 0.5f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, holdRadius);
        }
    }
}
