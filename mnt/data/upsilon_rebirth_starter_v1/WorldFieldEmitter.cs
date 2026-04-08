using UnityEngine;

namespace SpiritsCrossing.Vibration
{
    /// <summary>
    /// Simple environmental resonance zone.
    /// Agents do not need colliders or triggers; they sample this zone by distance.
    /// </summary>
    public class WorldFieldEmitter : MonoBehaviour
    {
        public enum FieldPreset
        {
            Custom,
            Grove,
            Flame,
            Shrine,
            River,
            Companion
        }

        [Header("Field Shape")]
        public FieldPreset preset = FieldPreset.Grove;
        public bool usePresetBands = true;
        [Min(0.1f)] public float radius = 8f;
        [Range(0f, 1f)] public float strength = 0.70f;
        [Range(0f, 2f)] public float pulseSpeed = 0.35f;
        [Range(0f, 0.25f)] public float pulseDepth = 0.10f;

        [Header("Custom Bands")]
        [Range(0f, 1f)] public float red = 0.10f;
        [Range(0f, 1f)] public float orange = 0.10f;
        [Range(0f, 1f)] public float yellow = 0.20f;
        [Range(0f, 1f)] public float green = 0.60f;
        [Range(0f, 1f)] public float blue = 0.55f;
        [Range(0f, 1f)] public float indigo = 0.25f;
        [Range(0f, 1f)] public float violet = 0.20f;

        private void OnEnable()
        {
            if (SourceBrain.Instance != null)
                SourceBrain.Instance.RegisterEmitter(this);
        }

        private void OnDisable()
        {
            if (SourceBrain.Instance != null)
                SourceBrain.Instance.UnregisterEmitter(this);
        }

        public float EvaluateInfluence(Vector3 point)
        {
            float distance = Vector3.Distance(transform.position, point);
            if (distance >= radius) return 0f;

            float falloff = 1f - (distance / radius);
            return strength * falloff;
        }

        public VibrationalField CurrentField()
        {
            Vector4 bandsA = usePresetBands ? PresetA(preset) : new Vector4(red, orange, yellow, green);
            Vector3 bandsB = usePresetBands ? PresetB(preset) : new Vector3(blue, indigo, violet);

            float pulse = 1f + pulseDepth * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f);

            return new VibrationalField(
                Mathf.Clamp01(bandsA.x * pulse),
                Mathf.Clamp01(bandsA.y * pulse),
                Mathf.Clamp01(bandsA.z * pulse),
                Mathf.Clamp01(bandsA.w * pulse),
                Mathf.Clamp01(bandsB.x * pulse),
                Mathf.Clamp01(bandsB.y * pulse),
                Mathf.Clamp01(bandsB.z * pulse));
        }

        [ContextMenu("Apply Preset Now")]
        public void ApplyPresetNow()
        {
            Vector4 a = PresetA(preset);
            Vector3 b = PresetB(preset);
            red = a.x; orange = a.y; yellow = a.z; green = a.w;
            blue = b.x; indigo = b.y; violet = b.z;
        }

        private static Vector4 PresetA(FieldPreset value)
        {
            switch (value)
            {
                case FieldPreset.Grove:     return new Vector4(0.08f, 0.10f, 0.25f, 0.75f);
                case FieldPreset.Flame:     return new Vector4(0.85f, 0.72f, 0.28f, 0.18f);
                case FieldPreset.Shrine:    return new Vector4(0.10f, 0.16f, 0.20f, 0.35f);
                case FieldPreset.River:     return new Vector4(0.06f, 0.12f, 0.18f, 0.50f);
                case FieldPreset.Companion: return new Vector4(0.18f, 0.28f, 0.24f, 0.44f);
                default:                    return new Vector4(0.10f, 0.10f, 0.20f, 0.60f);
            }
        }

        private static Vector3 PresetB(FieldPreset value)
        {
            switch (value)
            {
                case FieldPreset.Grove:     return new Vector3(0.62f, 0.22f, 0.18f);
                case FieldPreset.Flame:     return new Vector3(0.08f, 0.05f, 0.06f);
                case FieldPreset.Shrine:    return new Vector3(0.42f, 0.72f, 0.88f);
                case FieldPreset.River:     return new Vector3(0.82f, 0.48f, 0.34f);
                case FieldPreset.Companion: return new Vector3(0.34f, 0.42f, 0.58f);
                default:                    return new Vector3(0.55f, 0.25f, 0.20f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.25f);
            Gizmos.DrawSphere(transform.position, radius);
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
