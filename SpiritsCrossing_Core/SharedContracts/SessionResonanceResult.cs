// SpiritsCrossing — SessionResonanceResult.cs
// Output contract emitted by CaveSessionController.OnSessionComplete.
// The ritual scene produces exactly one of these; it never mutates cosmos state directly.

using System;
using System.Collections.Generic;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing
{
    [Serializable]
    public class SessionResonanceResult
    {
        // --- Resonance snapshot at session end ---
        public PlayerResponseSample resonanceSample = new PlayerResponseSample();

        // --- Planet affinity outcome ---
        public string currentAffinityPlanet;
        public string achievableAffinityPlanet;
        public float  currentAffinityScore;
        public float  achievableAffinityScore;

        // --- Portal outcome ---
        public bool   portalUnlocked;

        // --- Myth triggers raised during this session ---
        // Keys matched by MythInterpreter rules (e.g. "source", "forest", "storm")
        public List<string> mythTriggerKeys = new List<string>();

        // --- Emotional spectrum snapshot ---
        public EmotionalResonanceSpectrum emotionalSpectrum;
        public float  emotionalDepth;         // Depth property at session end
        public float  emotionalCoherence;     // Coherence property at session end
        public bool   reachedFullSpectrum;    // All 5 bands above threshold simultaneously
        public int    highestDepthLevel = -1; // Highest depth threshold crossed (0–3)
        public string emotionalAffinityPlanet; // Planet best matching emotional shape

        // --- Session metadata ---
        public float  sessionDurationSeconds;
        public int    chakraBandsCompleted;   // 0–7; 7 means crown hold reached
        public string utcTimestamp;

        public static SessionResonanceResult Empty() =>
            new SessionResonanceResult { utcTimestamp = DateTime.UtcNow.ToString("o") };
    }
}
