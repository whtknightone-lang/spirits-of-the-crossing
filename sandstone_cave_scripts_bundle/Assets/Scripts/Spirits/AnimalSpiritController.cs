using UnityEngine;
using Upsilon.CEF;

namespace Upsilon.Spirits
{
    public class AnimalSpiritController : MonoBehaviour
    {
        [SerializeField] private Transform animalBody;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.8f, 0f);
        [SerializeField] private float hoverAmplitude = 0.2f;
        [SerializeField] private float hoverSpeed = 1.5f;
        [SerializeField] private float appearThreshold = 0.58f;
        [SerializeField] private GameObject visualsRoot;
        [SerializeField] private Light spiritLight;

        private CEFSharedState state;

        private void Start()
        {
            if (CEFStateBus.Instance != null)
                CEFStateBus.Instance.OnStateUpdated += HandleStateUpdated;
        }

        private void OnDestroy()
        {
            if (CEFStateBus.Instance != null)
                CEFStateBus.Instance.OnStateUpdated -= HandleStateUpdated;
        }

        private void HandleStateUpdated(CEFSharedState s)
        {
            state = s;
        }

        private void Update()
        {
            if (animalBody != null)
            {
                Vector3 p = animalBody.position + offset;
                p.y += Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude;
                transform.position = p;
            }

            float visible = 0f;
            if (state != null)
                visible = Mathf.Clamp01((state.sourceAlignment + state.identity) * 0.5f);

            bool shouldShow = visible >= appearThreshold;

            if (visualsRoot != null)
                visualsRoot.SetActive(shouldShow);

            if (spiritLight != null)
                spiritLight.intensity = shouldShow ? Mathf.Lerp(0.75f, 3f, visible) : 0f;
        }
    }
}
