using UnityEngine;

namespace Upsilon.Cave
{
    public class FigureToEntityMorph : MonoBehaviour
    {
        [SerializeField] private GameObject statueObject;
        [SerializeField] private GameObject liveEntityObject;
        [SerializeField] private float morphDuration = 2.5f;
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private bool morphing;
        private float timer;

        public void BeginMorph()
        {
            morphing = true;
            timer = 0f;

            if (liveEntityObject != null)
                liveEntityObject.SetActive(true);
        }

        private void Update()
        {
            if (!morphing) return;

            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / morphDuration);
            float k = curve.Evaluate(t);

            if (statueObject != null)
            {
                statueObject.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.2f, k);
            }

            if (liveEntityObject != null)
            {
                liveEntityObject.transform.localScale = Vector3.Lerp(Vector3.one * 0.2f, Vector3.one, k);
            }

            if (t >= 1f)
            {
                morphing = false;
                if (statueObject != null) statueObject.SetActive(false);
            }
        }
    }
}
