// SpiritsCrossing — ResonanceLearningState.cs
// Persistent learning data that accumulates across all sessions.
// Owned by UniverseState. Updated by ResonanceMemorySystem after each session.
//
// This is how the game remembers and learns the player:
//   - signature:           who the player IS (rolling average of all sessions)
//   - personalBests:       what the player has ACHIEVED per dimension
//   - sourceConnectionLevel: how deeply the player has connected to the Source
//   - dominantElement:     which elemental path the player naturally walks
//   - resonanceArchetype:  which spirit archetype the player most resembles
//   - growthTrend:         is the player growing, stable, or oscillating?

using System;
using UnityEngine;

namespace SpiritsCrossing.Memory
{
    // -------------------------------------------------------------------------
    // Per-dimension personal bests — the player's peak in each resonance quality
    // -------------------------------------------------------------------------
    [Serializable]
    public class PersonalBests
    {
        [Range(0f, 1f)] public float calm;
        [Range(0f, 1f)] public float joy;
        [Range(0f, 1f)] public float wonder;
        [Range(0f, 1f)] public float socialSync;
        [Range(0f, 1f)] public float movementFlow;
        [Range(0f, 1f)] public float spinStability;
        [Range(0f, 1f)] public float sourceAlignment;
        [Range(0f, 1f)] public float breathCoherence;
        [Range(0f, 1f)] public float overallCoherence;   // best single-session average

        public float Get(string dim) => dim switch
        {
            "calm"            => calm,
            "joy"             => joy,
            "wonder"          => wonder,
            "socialSync"      => socialSync,
            "movementFlow"    => movementFlow,
            "spinStability"   => spinStability,
            "sourceAlignment" => sourceAlignment,
            "breathCoherence" => breathCoherence,
            _                 => 0f
        };

        public bool Update(PlayerResponseSample s)
        {
            bool improved = false;
            if (s.calmScore            > calm)            { calm            = s.calmScore;            improved = true; }
            if (s.joyScore             > joy)             { joy             = s.joyScore;             improved = true; }
            if (s.wonderScore          > wonder)          { wonder          = s.wonderScore;          improved = true; }
            if (s.pairSyncScore        > socialSync)      { socialSync      = s.pairSyncScore;        improved = true; }
            if (s.flowScore            > movementFlow)    { movementFlow    = s.flowScore;            improved = true; }
            if (s.spinScore            > spinStability)   { spinStability   = s.spinScore;            improved = true; }
            if (s.sourceAlignmentScore > sourceAlignment) { sourceAlignment = s.sourceAlignmentScore; improved = true; }
            if (s.stillnessScore       > breathCoherence) { breathCoherence = s.stillnessScore;       improved = true; }

            float overall = (s.calmScore + s.joyScore + s.wonderScore + s.sourceAlignmentScore +
                             s.flowScore + s.spinScore + s.pairSyncScore + s.stillnessScore) / 8f;
            if (overall > overallCoherence) { overallCoherence = overall; improved = true; }
            return improved;
        }
    }

    // -------------------------------------------------------------------------
    // Rolling session quality tracker — last N sessions for trend analysis
    // -------------------------------------------------------------------------
    [Serializable]
    public class SessionQualityWindow
    {
        public float[] sourceAlignmentHistory = new float[10];
        public float[] overallHistory         = new float[10];
        public int     writeIndex;
        public int     count;

        public void Record(PlayerResponseSample s)
        {
            float overall = (s.calmScore + s.joyScore + s.wonderScore +
                             s.sourceAlignmentScore + s.flowScore) / 5f;
            sourceAlignmentHistory[writeIndex % 10] = s.sourceAlignmentScore;
            overallHistory        [writeIndex % 10] = overall;
            writeIndex++;
            count = Mathf.Min(count + 1, 10);
        }

        /// <summary>Trend: positive = improving, negative = declining, 0 = stable.</summary>
        public float ComputeTrend()
        {
            if (count < 3) return 0f;
            // Compare first half vs second half of window
            float firstHalf = 0f, secondHalf = 0f;
            int halfCount = count / 2;
            for (int i = 0; i < halfCount; i++)          firstHalf  += overallHistory[i];
            for (int i = halfCount; i < count; i++)       secondHalf += overallHistory[i];
            firstHalf  /= halfCount;
            secondHalf /= Mathf.Max(1, count - halfCount);
            return Mathf.Clamp(secondHalf - firstHalf, -1f, 1f);
        }

        public float MeanSourceAlignment()
        {
            if (count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < count; i++) sum += sourceAlignmentHistory[i];
            return sum / count;
        }
    }

    // -------------------------------------------------------------------------
    // Full learning state — the game's memory of who the player is
    // -------------------------------------------------------------------------
    [Serializable]
    public class ResonanceLearningState
    {
        // ---- Signature (who the player IS) ----
        // EMA-updated after every session. Represents the player's natural baseline.
        public PlayerResponseSample signature = new PlayerResponseSample();

        // ---- Personal bests (what the player has ACHIEVED) ----
        public PersonalBests personalBests = new PersonalBests();

        // ---- Source connection (deepens across the whole journey) ----
        // Grows when sourceAlignment and breathCoherence are consistently high.
        // The fundamental measure of the player's relationship with the Source.
        [Range(0f, 1f)] public float sourceConnectionLevel;

        // ---- Classification ----
        public string dominantElement;    // "Air" | "Earth" | "Water" | "Fire"
        public string resonanceArchetype; // spirit archetype id e.g. "Seated"

        // ---- Momentum ----
        public float growthTrend;         // -1 to 1
        public int   sessionsAnalyzed;
        public string lastUpdatedUtc;

        // ---- Trend window ----
        public SessionQualityWindow sessionWindow = new SessionQualityWindow();

        // -------------------------------------------------------------------------
        // Integration — called by ResonanceMemorySystem after each session
        // -------------------------------------------------------------------------
        public void IntegrateSession(PlayerResponseSample s, float emaAlpha = 0.20f)
        {
            // Update signature via EMA (recent sessions weight more)
            signature.BlendIn(s, emaAlpha);

            // Update personal bests
            personalBests.Update(s);

            // Update source connection level — grows from sustained source alignment
            float sourceSignal = s.sourceAlignmentScore * 0.6f + s.stillnessScore * 0.4f;
            sourceConnectionLevel = Mathf.Clamp01(
                sourceConnectionLevel + (sourceSignal - sourceConnectionLevel) * 0.12f);

            // Update trend window
            sessionWindow.Record(s);
            growthTrend = sessionWindow.ComputeTrend();

            sessionsAnalyzed++;
            lastUpdatedUtc = DateTime.UtcNow.ToString("o");
        }

        // -------------------------------------------------------------------------
        // Queries
        // -------------------------------------------------------------------------

        /// <summary>How far above baseline is the player in a given dimension right now?</summary>
        public float ExcessAboveBaseline(string dim, float currentValue)
        {
            return Mathf.Max(0f, currentValue - GetSignatureDim(dim));
        }

        public float GetSignatureDim(string dim) => dim switch
        {
            "calm"            => signature.calmScore,
            "joy"             => signature.joyScore,
            "wonder"          => signature.wonderScore,
            "socialSync"      => signature.pairSyncScore,
            "movementFlow"    => signature.flowScore,
            "spinStability"   => signature.spinScore,
            "sourceAlignment" => signature.sourceAlignmentScore,
            "breathCoherence" => signature.stillnessScore,
            _                 => 0f
        };

        /// <summary>
        /// Companion bond threshold modifier: players with strong source connection
        /// bond slightly more easily (the world opens to them).
        /// </summary>
        public float CompanionBondMultiplier() =>
            1f + sourceConnectionLevel * 0.4f;

        /// <summary>
        /// Cave/realm atmosphere intensity modifier based on source connection
        /// and growth trend.
        /// </summary>
        public float WorldResponseIntensity() =>
            Mathf.Clamp01(sourceConnectionLevel * 0.7f + Mathf.Max(0f, growthTrend) * 0.3f);
    }
}
