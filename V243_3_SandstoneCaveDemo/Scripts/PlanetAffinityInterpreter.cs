using System.Collections.Generic;
using UnityEngine;

namespace V243.SandstoneCave
{
    public class PlanetAffinityInterpreter : MonoBehaviour
    {
        public List<PlanetVibrationProfile> planetProfiles = new List<PlanetVibrationProfile>();
        public PlayerResponseSample currentSample = new PlayerResponseSample();

        [Header("Results")]
        public string currentAffinityPlanet;
        public string achievableAffinityPlanet;
        public float currentAffinityScore;
        public float achievableAffinityScore;

        public void EvaluateAffinities()
        {
            float bestCurrent = float.MinValue;
            float bestAchievable = float.MinValue;
            currentAffinityPlanet = string.Empty;
            achievableAffinityPlanet = string.Empty;

            foreach (PlanetVibrationProfile profile in planetProfiles)
            {
                float currentScore =
                    currentSample.stillnessScore * profile.groundingWeight +
                    currentSample.flowScore * profile.flowWeight +
                    currentSample.spinScore * profile.rotationWeight +
                    currentSample.pairSyncScore * profile.socialWeight +
                    currentSample.calmScore * profile.groundingWeight +
                    currentSample.joyScore * profile.heartWeight +
                    currentSample.wonderScore * profile.visionWeight +
                    currentSample.sourceAlignmentScore * profile.crownWeight -
                    currentSample.distortionScore * 0.5f;

                float achievableScore =
                    currentScore +
                    (currentSample.sourceAlignmentScore * 0.35f) +
                    (currentSample.wonderScore * 0.2f);

                if (currentScore > bestCurrent)
                {
                    bestCurrent = currentScore;
                    currentAffinityPlanet = profile.planetId;
                    currentAffinityScore = currentScore;
                }

                if (achievableScore > bestAchievable)
                {
                    bestAchievable = achievableScore;
                    achievableAffinityPlanet = profile.planetId;
                    achievableAffinityScore = achievableScore;
                }
            }
        }

        public void LoadDefaultProfiles()
        {
            planetProfiles = new List<PlanetVibrationProfile>
            {
                new PlanetVibrationProfile { planetId = "ForestHeart", flowWeight = 0.8f, heartWeight = 1.4f, socialWeight = 1.0f, groundingWeight = 0.8f },
                new PlanetVibrationProfile { planetId = "SkySpiral", rotationWeight = 1.4f, visionWeight = 1.1f, crownWeight = 1.0f, flowWeight = 0.6f },
                new PlanetVibrationProfile { planetId = "SourceVeil", groundingWeight = 0.7f, crownWeight = 1.5f, visionWeight = 1.0f, solitudeWeight = 1.1f },
                new PlanetVibrationProfile { planetId = "WaterFlow", flowWeight = 1.5f, heartWeight = 0.8f, socialWeight = 0.8f, groundingWeight = 0.4f },
                new PlanetVibrationProfile { planetId = "MachineOrder", groundingWeight = 1.2f, willWeight = 1.0f, expressionWeight = 0.6f, rotationWeight = 0.2f },
                new PlanetVibrationProfile { planetId = "DarkContrast", visionWeight = 0.7f, crownWeight = 0.4f, groundingWeight = 0.6f, flowWeight = 0.4f, socialWeight = 0.2f }
            };
        }
    }
}
