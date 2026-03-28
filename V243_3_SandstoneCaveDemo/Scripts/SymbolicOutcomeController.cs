using UnityEngine;
using UnityEngine.Events;

namespace V243.SandstoneCave
{
    public class SymbolicOutcomeController : MonoBehaviour
    {
        [Header("References")]
        public CaveSessionController sessionController;
        public PlanetAffinityInterpreter planetAffinityInterpreter;
        public PlanetSigilProjector planetSigilProjector;
        public AdaptiveCaveAudioController adaptiveAudioController;
        public Light chamberRevealLight;
        public ParticleSystem revealParticles;

        [Header("Events")]
        public UnityEvent<string> onPlanetCurrentRevealed;
        public UnityEvent<string> onPlanetAchievableRevealed;

        [Header("Runtime")]
        public bool resultShown;

        private void Update()
        {
            if (resultShown || sessionController == null || !sessionController.sessionComplete)
            {
                return;
            }

            RevealResult();
        }

        public void RevealResult()
        {
            resultShown = true;

            if (planetAffinityInterpreter != null)
            {
                string planetId = planetAffinityInterpreter.achievableAffinityPlanet;
                if (string.IsNullOrWhiteSpace(planetId))
                {
                    planetId = planetAffinityInterpreter.currentAffinityPlanet;
                }

                if (planetSigilProjector != null)
                {
                    planetSigilProjector.ProjectPlanet(planetId);
                }

                onPlanetCurrentRevealed?.Invoke(planetAffinityInterpreter.currentAffinityPlanet);
                onPlanetAchievableRevealed?.Invoke(planetAffinityInterpreter.achievableAffinityPlanet);
            }

            if (adaptiveAudioController != null)
            {
                adaptiveAudioController.ForcePlanetReveal();
            }

            if (chamberRevealLight != null)
            {
                chamberRevealLight.enabled = true;
                chamberRevealLight.intensity = 5f;
            }

            if (revealParticles != null)
            {
                revealParticles.Play();
            }
        }

        public void ResetResult()
        {
            resultShown = false;
            if (planetSigilProjector != null)
            {
                planetSigilProjector.ClearAll();
            }
        }
    }
}
