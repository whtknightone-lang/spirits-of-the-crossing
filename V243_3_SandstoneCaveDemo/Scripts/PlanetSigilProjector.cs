using System.Collections.Generic;
using UnityEngine;

namespace V243.SandstoneCave
{
    public class PlanetSigilProjector : MonoBehaviour
    {
        [System.Serializable]
        public class PlanetVisualBinding
        {
            public string planetId = "planet";
            public GameObject sigilRoot;
            public Light accentLight;
            public Color accentColor = Color.white;
            public AudioClip revealClip;
        }

        public List<PlanetVisualBinding> planetBindings = new List<PlanetVisualBinding>();
        public AudioSource audioSource;

        public string activePlanetId;

        public void ProjectPlanet(string planetId)
        {
            activePlanetId = planetId;

            foreach (PlanetVisualBinding binding in planetBindings)
            {
                bool isActive = binding.planetId == planetId;

                if (binding.sigilRoot != null)
                {
                    binding.sigilRoot.SetActive(isActive);
                }

                if (binding.accentLight != null)
                {
                    binding.accentLight.enabled = isActive;
                    binding.accentLight.color = binding.accentColor;
                    binding.accentLight.intensity = isActive ? 4f : 0f;
                }

                if (isActive && audioSource != null && binding.revealClip != null)
                {
                    audioSource.PlayOneShot(binding.revealClip);
                }
            }
        }

        public void ClearAll()
        {
            activePlanetId = string.Empty;
            foreach (PlanetVisualBinding binding in planetBindings)
            {
                if (binding.sigilRoot != null)
                {
                    binding.sigilRoot.SetActive(false);
                }

                if (binding.accentLight != null)
                {
                    binding.accentLight.enabled = false;
                    binding.accentLight.intensity = 0f;
                }
            }
        }
    }
}
