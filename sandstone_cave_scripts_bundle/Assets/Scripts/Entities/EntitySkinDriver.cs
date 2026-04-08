using UnityEngine;
using Upsilon.CEF;

namespace Upsilon.Entities
{
    public class EntitySkinDriver : MonoBehaviour
    {
        public enum SkinType
        {
            Player,
            AI,
            Animal,
            AnimalSpirit
        }

        [Header("Identity")]
        [SerializeField] private SkinType skinType = SkinType.Player;

        [Header("Visual Targets")]
        [SerializeField] private Renderer[] renderersToDrive;
        [SerializeField] private Light coreLight;
        [SerializeField] private Transform auraShell;
        [SerializeField] private ParticleSystem fractureFX;

        [Header("Tuning")]
        [SerializeField] private float pulseSpeed = 6f;
        [SerializeField] private float auraScaleMin = 0.9f;
        [SerializeField] private float auraScaleMax = 1.25f;
        [SerializeField] private float lightMin = 0.6f;
        [SerializeField] private float lightMax = 3.5f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private CEFStateBus bus;
        private CEFSharedState state;
        private MaterialPropertyBlock block;

        private void Start()
        {
            bus = CEFStateBus.Instance;
            block = new MaterialPropertyBlock();

            if (bus != null)
                bus.OnStateUpdated += HandleStateUpdated;
        }

        private void OnDestroy()
        {
            if (bus != null)
                bus.OnStateUpdated -= HandleStateUpdated;
        }

        private void HandleStateUpdated(CEFSharedState s)
        {
            state = s;
        }

        private void Update()
        {
            if (state == null) return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            float energy = Mathf.Lerp(0.15f, 1f, state.creativeEdge);
            float fracture = state.fractureRisk;

            Color baseColor = GetSkinColor();
            Color emission = baseColor * (0.5f + energy * 2f + pulse * 0.35f);

            for (int i = 0; i < renderersToDrive.Length; i++)
            {
                if (renderersToDrive[i] == null) continue;
                renderersToDrive[i].GetPropertyBlock(block);
                block.SetColor(EmissionColor, emission);
                renderersToDrive[i].SetPropertyBlock(block);
            }

            if (coreLight != null)
            {
                coreLight.color = baseColor;
                coreLight.intensity = Mathf.Lerp(lightMin, lightMax, state.identity * 0.7f + state.creativeEdge * 0.3f);
            }

            if (auraShell != null)
            {
                float shellScale = Mathf.Lerp(auraScaleMin, auraScaleMax, state.sourceAlignment * 0.5f + pulse * 0.5f);
                if (skinType == SkinType.AnimalSpirit)
                    shellScale *= 1.12f;
                auraShell.localScale = Vector3.one * shellScale;
            }

            if (fractureFX != null)
            {
                var emissionModule = fractureFX.emission;
                emissionModule.rateOverTime = fracture > 0.35f ? Mathf.Lerp(0f, 40f, fracture) : 0f;

                if (fracture > 0.35f && !fractureFX.isPlaying) fractureFX.Play();
                if (fracture <= 0.35f && fractureFX.isPlaying) fractureFX.Stop();
            }
        }

        private Color GetSkinColor()
        {
            switch (skinType)
            {
                case SkinType.Player:
                    return new Color(0.3f, 0.8f, 1f);
                case SkinType.AI:
                    return new Color(1f, 0.55f, 0.2f);
                case SkinType.Animal:
                    return new Color(0.45f, 1f, 0.55f);
                case SkinType.AnimalSpirit:
                    return new Color(0.85f, 0.75f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
