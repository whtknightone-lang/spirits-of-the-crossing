using UnityEngine;

namespace Upsilon.CEF
{
    public class UpsilonSkinController : MonoBehaviour
    {
        [SerializeField] private UpsilonSkinProfile skinProfile;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private string emissionProperty = "_EmissionColor";
        [SerializeField] private string baseColorProperty = "_BaseColor";
        [SerializeField] private bool animateScale = true;

        private MaterialPropertyBlock _mpb;
        private float _pulse;
        private CEFEntityState _currentState;

        private static readonly Color[] ChannelColors = new[]
        {
            new Color(0.50f, 0.20f, 0.90f),
            new Color(0.20f, 0.45f, 1.00f),
            new Color(0.20f, 0.90f, 0.35f),
            new Color(1.00f, 0.85f, 0.15f),
            new Color(1.00f, 0.55f, 0.15f),
            new Color(0.95f, 0.20f, 0.18f),
            new Color(0.55f, 0.55f, 0.60f),
        };

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (visualRoot == null) visualRoot = transform;
        }

        private void Update()
        {
            if (_currentState == null || skinProfile == null) return;

            float pulseSpeed = skinProfile.pulseSpeedBase + _currentState.novelty * skinProfile.pulseSpeedNovelty;
            _pulse += Time.deltaTime * pulseSpeed;
            float pulseValue = 0.5f + 0.5f * Mathf.Sin(_pulse * Mathf.PI * 2f);

            float emission = skinProfile.emissionBase + _currentState.creativeEdge * skinProfile.emissionCreativeEdge;
            emission += _currentState.sourceAlignment * 0.35f;
            emission -= _currentState.shadowPressure * 0.15f;
            emission = Mathf.Max(0f, emission);

            Color upsilonColor = BlendChannelColor(_currentState.channels);
            Color emissionColor = upsilonColor * emission * (0.7f + pulseValue * 0.6f);

            foreach (var renderer in targetRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(emissionProperty, emissionColor);
                _mpb.SetColor(baseColorProperty, Color.Lerp(Color.white * 0.2f, upsilonColor, 0.8f));
                renderer.SetPropertyBlock(_mpb);
            }

            if (animateScale && visualRoot != null)
            {
                float distortion = skinProfile.distortionBase + _currentState.fractureRisk * skinProfile.distortionFracture;
                float scale = 1f + pulseValue * 0.05f + distortion * 0.1f;
                visualRoot.localScale = Vector3.one * scale;
            }
        }

        public void ApplyState(CEFEntityState state)
        {
            _currentState = state;
        }

        private static Color BlendChannelColor(float[] channels)
        {
            if (channels == null || channels.Length != 7) return Color.white;
            Color c = Color.black;
            float total = 0f;
            for (int i = 0; i < 7; i++)
            {
                c += ChannelColors[i] * channels[i];
                total += channels[i];
            }
            if (total < 0.0001f) return Color.white;
            return c / total;
        }
    }
}
