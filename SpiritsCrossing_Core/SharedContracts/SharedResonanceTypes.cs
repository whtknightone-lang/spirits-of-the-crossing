// SpiritsCrossing — SharedResonanceTypes.cs
// Canonical runtime contracts for player resonance state.
// All scene namespaces (V243.SandstoneCave, V243.Portals, Resonance.Core)
// should migrate to these shared types instead of defining their own.

using System;
using UnityEngine;

namespace SpiritsCrossing
{
    // -------------------------------------------------------------------------
    // Live per-frame resonance state. Owned by BreathMovementInterpreter and
    // passed down to cave/portal/realm systems each tick.
    // -------------------------------------------------------------------------
    [Serializable]
    public class PlayerResonanceState
    {
        [Range(0f, 1f)] public float breathCoherence;
        [Range(0f, 1f)] public float movementFlow;
        [Range(0f, 1f)] public float spinStability;
        [Range(0f, 1f)] public float socialSync;
        [Range(0f, 1f)] public float calm;
        [Range(0f, 1f)] public float joy;
        [Range(0f, 1f)] public float wonder;
        [Range(0f, 1f)] public float distortion;
        [Range(0f, 1f)] public float sourceAlignment;

        public void LerpToward(PlayerResonanceState target, float t)
        {
            breathCoherence  = Mathf.Lerp(breathCoherence,  target.breathCoherence,  t);
            movementFlow     = Mathf.Lerp(movementFlow,     target.movementFlow,     t);
            spinStability    = Mathf.Lerp(spinStability,    target.spinStability,    t);
            socialSync       = Mathf.Lerp(socialSync,       target.socialSync,       t);
            calm             = Mathf.Lerp(calm,             target.calm,             t);
            joy              = Mathf.Lerp(joy,              target.joy,              t);
            wonder           = Mathf.Lerp(wonder,           target.wonder,           t);
            distortion       = Mathf.Lerp(distortion,       target.distortion,       t);
            sourceAlignment  = Mathf.Lerp(sourceAlignment,  target.sourceAlignment,  t);
        }

        /// <summary>Snapshot the live state into a persistent sample.</summary>
        public PlayerResponseSample ToSample()
        {
            return new PlayerResponseSample
            {
                stillnessScore      = breathCoherence * calm,
                flowScore           = movementFlow,
                spinScore           = spinStability,
                pairSyncScore       = socialSync,
                calmScore           = calm,
                joyScore            = joy,
                wonderScore         = wonder,
                distortionScore     = distortion,
                sourceAlignmentScore = sourceAlignment
            };
        }
    }

    // -------------------------------------------------------------------------
    // Snapshot of player resonance taken at session or checkpoint boundaries.
    // Used by affinity scoring, portal scoring, myth activation, and persistence.
    // -------------------------------------------------------------------------
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

        /// <summary>Blend this sample with another over time (EMA-style).</summary>
        public void BlendIn(PlayerResponseSample other, float alpha)
        {
            stillnessScore       = Mathf.Lerp(stillnessScore,       other.stillnessScore,       alpha);
            flowScore            = Mathf.Lerp(flowScore,            other.flowScore,            alpha);
            spinScore            = Mathf.Lerp(spinScore,            other.spinScore,            alpha);
            pairSyncScore        = Mathf.Lerp(pairSyncScore,        other.pairSyncScore,        alpha);
            calmScore            = Mathf.Lerp(calmScore,            other.calmScore,            alpha);
            joyScore             = Mathf.Lerp(joyScore,             other.joyScore,             alpha);
            wonderScore          = Mathf.Lerp(wonderScore,          other.wonderScore,          alpha);
            distortionScore      = Mathf.Lerp(distortionScore,      other.distortionScore,      alpha);
            sourceAlignmentScore = Mathf.Lerp(sourceAlignmentScore, other.sourceAlignmentScore, alpha);
        }

        /// <summary>Dominant quality by score (used for myth keying).</summary>
        public string DominantQuality()
        {
            float best = 0f;
            string key = "none";
            Check("stillness", stillnessScore);
            Check("flow",      flowScore);
            Check("spin",      spinScore);
            Check("social",    pairSyncScore);
            Check("calm",      calmScore);
            Check("joy",       joyScore);
            Check("wonder",    wonderScore);
            Check("source",    sourceAlignmentScore);
            return key;

            void Check(string k, float v) { if (v > best) { best = v; key = k; } }
        }
    }

    // -------------------------------------------------------------------------
    // Persistent long-form player identity. Accumulates across sessions.
    // Owned by UniverseStateManager.
    // -------------------------------------------------------------------------
    [Serializable]
    public class PlayerIdentityProfile
    {
        [Range(0f, 1f)] public float coherence       = 0.55f;
        [Range(0f, 1f)] public float trust           = 0.50f;
        [Range(0f, 1f)] public float joy             = 0.50f;
        [Range(0f, 1f)] public float courage         = 0.50f;
        [Range(0f, 1f)] public float openness        = 0.50f;
        [Range(0f, 1f)] public float shadowLoad      = 0.35f;
        [Range(0f, 1f)] public float desireIntensity = 0.60f;

        /// <summary>Integrate a session sample into the long-term identity (slow EMA).</summary>
        public void IntegrateSession(PlayerResponseSample sample, float alpha = 0.08f)
        {
            coherence  = Mathf.Clamp01(Mathf.Lerp(coherence,  sample.stillnessScore + sample.calmScore * 0.5f, alpha));
            joy        = Mathf.Clamp01(Mathf.Lerp(joy,        sample.joyScore,                                  alpha));
            trust      = Mathf.Clamp01(Mathf.Lerp(trust,      sample.sourceAlignmentScore,                      alpha));
            courage    = Mathf.Clamp01(Mathf.Lerp(courage,    sample.spinScore,                                  alpha));
            openness   = Mathf.Clamp01(Mathf.Lerp(openness,   sample.wonderScore,                               alpha));
            shadowLoad = Mathf.Clamp01(Mathf.Lerp(shadowLoad, sample.distortionScore,                           alpha));
        }

        public float WorthinessSignal() =>
            Mathf.Clamp01(coherence * 0.30f + trust * 0.25f + courage * 0.20f + joy * 0.25f);

        public float LearningReadiness() =>
            Mathf.Clamp01(courage * 0.35f + openness * 0.35f + (1f - shadowLoad) * 0.30f);
    }
}
