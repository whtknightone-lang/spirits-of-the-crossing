using UnityEngine;

namespace V243.SandstoneCave
{
    public class CaveVisualPulseController : MonoBehaviour
    {
        public Renderer[] sculptureGlowRenderers;
        public Light[] accentLights;
        private MaterialPropertyBlock block;

        [Header("Chakra Colors")]
        public Color rootColor = new Color(1f, 0.2f, 0.15f);
        public Color sacralColor = new Color(1f, 0.45f, 0.1f);
        public Color solarColor = new Color(1f, 0.85f, 0.15f);
        public Color heartColor = new Color(0.35f, 1f, 0.45f);
        public Color throatColor = new Color(0.2f, 0.75f, 1f);
        public Color thirdEyeColor = new Color(0.45f, 0.35f, 1f);
        public Color crownColor = new Color(0.85f, 0.65f, 1f);

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        public void ApplyChakra(ChakraBand band, float progress, bool isHolding)
        {
            Color c = GetColor(band);
            float pulse = isHolding ? 1.25f : Mathf.Lerp(0.25f, 1f, progress);

            foreach (Renderer r in sculptureGlowRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", c * pulse);
                r.SetPropertyBlock(block);
            }

            foreach (Light lightRef in accentLights)
            {
                if (lightRef == null) continue;
                lightRef.color = c;
                lightRef.intensity = Mathf.Lerp(0.5f, 2.25f, pulse / 1.25f);
            }
        }

        public Color GetColor(ChakraBand band)
        {
            switch (band)
            {
                case ChakraBand.Root: return rootColor;
                case ChakraBand.Sacral: return sacralColor;
                case ChakraBand.Solar: return solarColor;
                case ChakraBand.Heart: return heartColor;
                case ChakraBand.Throat: return throatColor;
                case ChakraBand.ThirdEye: return thirdEyeColor;
                case ChakraBand.Crown: return crownColor;
                default: return Color.white;
            }
        }
    }
}
