using UnityEngine;

namespace V243.SandstoneCave
{
    public class PortalUnlockController : MonoBehaviour
    {
        public bool portalUnlocked;
        public GameObject portalVisual;
        public Light portalLight;
        public float requiredHoldSeconds = 240f;
        public float maxDistortion = 0.25f;
        public float requiredCalmOrJoy = 0.6f;

        public void Evaluate(CavePlayerResonanceState state, ChakraState chakra)
        {
            if (portalUnlocked)
            {
                return;
            }

            if (chakra.activeBand == ChakraBand.Crown &&
                chakra.isHolding &&
                chakra.holdTimer > requiredHoldSeconds &&
                state.distortion < maxDistortion &&
                (state.calm > requiredCalmOrJoy || state.joy > requiredCalmOrJoy))
            {
                UnlockPortal();
            }
        }

        public void UnlockPortal()
        {
            portalUnlocked = true;
            if (portalVisual != null)
            {
                portalVisual.SetActive(true);
            }
            if (portalLight != null)
            {
                portalLight.enabled = true;
                portalLight.intensity = 4f;
            }
        }
    }
}
