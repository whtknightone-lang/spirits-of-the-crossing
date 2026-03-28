using System;
using UnityEngine;

namespace V243.SandstoneCave
{
    public enum SculptureMode
    {
        SeatedMeditation,
        FlowDance,
        Dervish,
        CoupledDance,
        DragonManifest   // Elder dragon spirit sculptures
    }

    public enum DragonElement
    {
        Air,
        Fire,
        Water,
        Earth
    }

    public enum ChakraBand
    {
        Root,
        Sacral,
        Solar,
        Heart,
        Throat,
        ThirdEye,
        Crown
    }

    public enum SpiritArchetype
    {
        // Humanoid spirit archetypes
        Seated,
        FlowDancer,
        Dervish,
        PairA,
        PairB,
        // Elder dragon spirit archetypes — each corresponds to an elemental realm
        EarthDragon,
        FireDragon,
        WaterDragon,
        ElderAirDragon
    }

    [Serializable]
    public class SculptureResonanceProfile
    {
        public string sculptureId = "sculpture";
        public SculptureMode mode = SculptureMode.SeatedMeditation;
        [Range(0f, 1f)] public float calmBias = 0.5f;
        [Range(0f, 1f)] public float joyBias = 0.5f;
        [Range(0f, 1f)] public float wonderBias = 0.5f;
        [Range(0f, 1f)] public float socialCouplingBias = 0.25f;
        [Range(0f, 1f)] public float upwardLiftBias = 0.5f;
        [Range(0f, 1f)] public float distortionSensitivity = 0.25f;
    }

    [Serializable]
    public class ChakraState
    {
        public ChakraBand activeBand = ChakraBand.Root;
        [Range(0f, 1f)] public float progress01 = 0f;
        public bool isHolding = false;
        public float holdTimer = 0f;
    }

    [Serializable]
    public class CavePlayerResonanceState
    {
        [Range(0f, 1f)] public float breathCoherence = 0f;
        [Range(0f, 1f)] public float movementFlow = 0f;
        [Range(0f, 1f)] public float spinStability = 0f;
        [Range(0f, 1f)] public float socialSync = 0f;
        [Range(0f, 1f)] public float calm = 0f;
        [Range(0f, 1f)] public float joy = 0f;
        [Range(0f, 1f)] public float wonder = 0f;
        [Range(0f, 1f)] public float distortion = 0f;
        [Range(0f, 1f)] public float sourceAlignment = 0f;

        public void LerpToward(CavePlayerResonanceState target, float t)
        {
            breathCoherence = Mathf.Lerp(breathCoherence, target.breathCoherence, t);
            movementFlow = Mathf.Lerp(movementFlow, target.movementFlow, t);
            spinStability = Mathf.Lerp(spinStability, target.spinStability, t);
            socialSync = Mathf.Lerp(socialSync, target.socialSync, t);
            calm = Mathf.Lerp(calm, target.calm, t);
            joy = Mathf.Lerp(joy, target.joy, t);
            wonder = Mathf.Lerp(wonder, target.wonder, t);
            distortion = Mathf.Lerp(distortion, target.distortion, t);
            sourceAlignment = Mathf.Lerp(sourceAlignment, target.sourceAlignment, t);
        }
    }

    [Serializable]
    public class PlanetVibrationProfile
    {
        public string planetId = "planet";
        [Range(0f, 2f)] public float groundingWeight = 1f;
        [Range(0f, 2f)] public float flowWeight = 1f;
        [Range(0f, 2f)] public float willWeight = 1f;
        [Range(0f, 2f)] public float heartWeight = 1f;
        [Range(0f, 2f)] public float expressionWeight = 1f;
        [Range(0f, 2f)] public float visionWeight = 1f;
        [Range(0f, 2f)] public float crownWeight = 1f;
        [Range(0f, 2f)] public float socialWeight = 1f;
        [Range(0f, 2f)] public float rotationWeight = 1f;
        [Range(0f, 2f)] public float solitudeWeight = 1f;
    }

    [Serializable]
    public class PlayerResponseSample
    {
        [Range(0f, 1f)] public float stillnessScore;
        [Range(0f, 1f)] public float flowScore;
        [Range(0f, 1f)] public float spinScore;
        [Range(0f, 1f)] public float pairSyncScore;
        [Range(0f, 1f)] public float calmScore;
        [Range(0f, 1f)] public float joyScore;
        [Range(0f, 1f)] public float wonderScore;
        [Range(0f, 1f)] public float distortionScore;
        [Range(0f, 1f)] public float sourceAlignmentScore;

        public static PlayerResponseSample FromState(CavePlayerResonanceState state)
        {
            return new PlayerResponseSample
            {
                stillnessScore = state.breathCoherence * state.calm,
                flowScore = state.movementFlow,
                spinScore = state.spinStability,
                pairSyncScore = state.socialSync,
                calmScore = state.calm,
                joyScore = state.joy,
                wonderScore = state.wonder,
                distortionScore = state.distortion,
                sourceAlignmentScore = state.sourceAlignment
            };
        }
    }
}
