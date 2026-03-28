// SpiritsCrossing — ResonanceMemorySystem.cs
// The game's intelligence layer that learns the player and lets the world respond.
//
// What it does:
//   LEARN    — After every session, integrates the player's resonance sample into
//              their growing signature, updates personal bests, computes growth trend.
//
//   CLASSIFY — Detects which element and spirit archetype the player most resembles
//              using SpiritProfileLoader.ScorePlayerForPlanet() on their signature.
//
//   RESPOND  — Emits a WorldResponseIntensity (0–1) that scales cave and realm
//              atmosphere, portal brightness, and companion bond growth.
//
//   REMEMBER — Fires events when the player exceeds their personal best in any
//              dimension, so the world can react to genuine growth moments.
//
// The Source response: sourceConnectionLevel grows as the player deepens their
// practice across sessions. When it rises through thresholds, the game's world
// visibly opens — the Source remembers and acknowledges the player.
//
// Setup: Add to Bootstrap scene. GameBootstrapper / VRBootstrapInstaller
//        creates it automatically.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.SpiritAI;
using SpiritsCrossing.Companions;
using SpiritsCrossing.Memory;

namespace SpiritsCrossing.Memory
{
    public class ResonanceMemorySystem : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------
        public static ResonanceMemorySystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Learning")]
        [Range(0.05f, 0.50f)]
        [Tooltip("EMA alpha for signature update. Higher = faster learning, less memory.")]
        public float signatureAlpha = 0.20f;

        [Header("Source Connection Thresholds")]
        [Tooltip("sourceConnectionLevel values that trigger world response escalations.")]
        public float[] sourceResponseThresholds = { 0.20f, 0.40f, 0.60f, 0.80f, 0.95f };

        [Header("Personal Best")]
        [Tooltip("Minimum excess above baseline to fire OnPersonalBestExceeded.")]
        [Range(0.01f, 0.20f)] public float personalBestMargin = 0.05f;

        [Header("Debug")]
        public bool logSessionSummary = true;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        /// <summary>Player exceeded their personal best in a resonance dimension.</summary>
        public event Action<string, float> OnPersonalBestExceeded;  // dim, newBest

        /// <summary>Source connection level crossed a threshold (0-4 index).</summary>
        public event Action<int, float>    OnSourceThresholdReached; // thresholdIndex, level

        /// <summary>Player's dominant element changed.</summary>
        public event Action<string>        OnDominantElementChanged; // "Air"|"Earth"|"Water"|"Fire"

        /// <summary>Player's closest spirit archetype changed.</summary>
        public event Action<string>        OnResonanceArchetypeChanged; // archetypeId

        // -------------------------------------------------------------------------
        // Public state (read by world systems)
        // -------------------------------------------------------------------------

        /// <summary>0–1 scale for cave/realm atmosphere intensity and portal brightness.</summary>
        public float WorldResponseIntensity =>
            UniverseStateManager.Instance?.Current.learningState.WorldResponseIntensity() ?? 0f;

        /// <summary>Current source connection level (read-only convenience).</summary>
        public float SourceConnectionLevel =>
            UniverseStateManager.Instance?.Current.learningState.sourceConnectionLevel ?? 0f;

        /// <summary>The player's learned resonance signature.</summary>
        public PlayerResponseSample Signature =>
            UniverseStateManager.Instance?.Current.learningState.signature;

        /// <summary>How much the player's LIVE state exceeds their learned signature.</summary>
        public float SourceAlignmentDelta
        {
            get
            {
                var learning = UniverseStateManager.Instance?.Current.learningState;
                var orch     = _orchestrator ?? FindObjectOfType<SpiritBrainOrchestrator>();
                if (learning == null || orch == null) return 0f;
                return learning.ExcessAboveBaseline("sourceAlignment",
                    orch.CurrentPlayerState?.sourceAlignment ?? 0f);
            }
        }

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private SpiritBrainOrchestrator _orchestrator;
        private float                   _lastSourceLevel;
        private string                  _lastElement;
        private string                  _lastArchetype;
        private float                   _liveUpdateTimer;
        private const float             LIVE_INTERVAL = 2.0f; // seconds between live scans

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void OnEnable()
        {
            if (UniverseStateManager.Instance != null)
                UniverseStateManager.Instance.OnSessionApplied += OnSessionApplied;
        }

        private void OnDisable()
        {
            if (UniverseStateManager.Instance != null)
                UniverseStateManager.Instance.OnSessionApplied -= OnSessionApplied;
        }

        private void Update()
        {
            _liveUpdateTimer += Time.deltaTime;
            if (_liveUpdateTimer < LIVE_INTERVAL) return;
            _liveUpdateTimer = 0f;

            CheckPersonalBestExceedance();
        }

        // -------------------------------------------------------------------------
        // Session integration — called after every cave ritual ends
        // -------------------------------------------------------------------------
        private void OnSessionApplied(SessionResonanceResult result)
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            var learning = universe.learningState;
            learning.IntegrateSession(result.resonanceSample, signatureAlpha);

            // Detect dominant element and archetype
            string newElement   = DetectDominantElement(learning);
            string newArchetype = DetectResonanceArchetype(learning);

            if (newElement != learning.dominantElement)
            {
                learning.dominantElement = newElement;
                OnDominantElementChanged?.Invoke(newElement);
            }

            if (newArchetype != learning.resonanceArchetype)
            {
                learning.resonanceArchetype = newArchetype;
                OnResonanceArchetypeChanged?.Invoke(newArchetype);
            }

            // Check source connection threshold crossings
            CheckSourceThresholds(learning);

            // Apply companion bond multiplier to CompanionBondSystem
            if (CompanionBondSystem.Instance != null)
                CompanionBondSystem.Instance.bondGrowthRate = learning.CompanionBondMultiplier();

            if (logSessionSummary)
                LogSummary(learning, result);
        }

        // -------------------------------------------------------------------------
        // Element detection — which of the 4 elements does the player's signature align with?
        // Uses SpiritProfileLoader to score against planet preferred archetypes.
        // -------------------------------------------------------------------------
        private string DetectDominantElement(ResonanceLearningState learning)
        {
            var loader = SpiritProfileLoader.Instance;
            if (loader == null || !loader.IsLoaded)
                return FallbackElement(learning.signature);

            var scores = new Dictionary<string, float>
            {
                ["Air"]   = loader.ScorePlayerForPlanet("SkySpiral",  learning.signature),
                ["Earth"] = loader.ScorePlayerForPlanet("ForestHeart",learning.signature),
                ["Water"] = loader.ScorePlayerForPlanet("WaterFlow",  learning.signature),
                ["Fire"]  = loader.ScorePlayerForPlanet("DarkContrast",learning.signature),
            };

            string best = "Air"; float bestScore = 0f;
            foreach (var kv in scores)
                if (kv.Value > bestScore) { bestScore = kv.Value; best = kv.Key; }

            return best;
        }

        private string FallbackElement(PlayerResponseSample s)
        {
            // Simple heuristic when loader isn't ready
            float air   = s.spinScore           * 0.5f + s.wonderScore          * 0.5f;
            float earth = s.stillnessScore      * 0.5f + s.sourceAlignmentScore * 0.5f;
            float water = s.flowScore           * 0.5f + s.pairSyncScore        * 0.5f;
            float fire  = s.spinScore           * 0.5f + (1f - s.calmScore)     * 0.5f;

            if (earth >= air && earth >= water && earth >= fire) return "Earth";
            if (water >= air && water >= fire)                   return "Water";
            if (fire  > air)                                     return "Fire";
            return "Air";
        }

        // -------------------------------------------------------------------------
        // Archetype detection — which spirit does the player most resemble?
        // -------------------------------------------------------------------------
        private string DetectResonanceArchetype(ResonanceLearningState learning)
        {
            var loader = SpiritProfileLoader.Instance;
            if (loader == null || !loader.IsLoaded) return "Seated";

            string best = "Seated"; float bestScore = 0f;
            foreach (var profile in loader.Profiles.spirits)
            {
                float score = profile.driveWeights.rest    * learning.signature.calmScore            +
                              profile.driveWeights.seek    * learning.signature.joyScore             +
                              profile.driveWeights.signal  * learning.signature.pairSyncScore        +
                              profile.driveWeights.explore * learning.signature.wonderScore          +
                              profile.driveWeights.flee    * (1f - learning.signature.distortionScore) +
                              profile.driveWeights.attack  * learning.signature.spinScore;
                if (score > bestScore) { bestScore = score; best = profile.archetypeId; }
            }
            return best;
        }

        // -------------------------------------------------------------------------
        // Source threshold crossings
        // -------------------------------------------------------------------------
        private void CheckSourceThresholds(ResonanceLearningState learning)
        {
            float level = learning.sourceConnectionLevel;
            for (int i = 0; i < sourceResponseThresholds.Length; i++)
            {
                float t = sourceResponseThresholds[i];
                if (level >= t && _lastSourceLevel < t)
                {
                    OnSourceThresholdReached?.Invoke(i, level);
                    Debug.Log($"[ResonanceMemorySystem] Source threshold {i} reached: {level:F3}");

                    // The world responds at each threshold:
                    // Activate source myth with escalating strength
                    var myth = UniverseStateManager.Instance?.Current.mythState;
                    myth?.Activate("source", "memory", 0.3f + i * 0.15f);

                    // Elder myth at higher thresholds
                    if (i >= 3) myth?.Activate("elder", "memory", level);
                }
            }
            _lastSourceLevel = level;
        }

        // -------------------------------------------------------------------------
        // Live personal best monitoring
        // -------------------------------------------------------------------------
        private void CheckPersonalBestExceedance()
        {
            var learning = UniverseStateManager.Instance?.Current.learningState;
            if (_orchestrator == null) _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();
            if (learning == null || _orchestrator?.CurrentPlayerState == null) return;

            var p = _orchestrator.CurrentPlayerState;
            CheckDim("calm",            p.calm,            learning.personalBests.calm);
            CheckDim("joy",             p.joy,             learning.personalBests.joy);
            CheckDim("wonder",          p.wonder,          learning.personalBests.wonder);
            CheckDim("sourceAlignment", p.sourceAlignment, learning.personalBests.sourceAlignment);
            CheckDim("movementFlow",    p.movementFlow,    learning.personalBests.movementFlow);
            CheckDim("spinStability",   p.spinStability,   learning.personalBests.spinStability);
        }

        private void CheckDim(string dim, float currentValue, float currentBest)
        {
            if (currentValue > currentBest + personalBestMargin)
            {
                OnPersonalBestExceeded?.Invoke(dim, currentValue);
                // Update the live best in UniverseState
                var learning = UniverseStateManager.Instance?.Current.learningState;
                if (learning != null)
                {
                    switch (dim)
                    {
                        case "calm":            learning.personalBests.calm            = currentValue; break;
                        case "joy":             learning.personalBests.joy             = currentValue; break;
                        case "wonder":          learning.personalBests.wonder          = currentValue; break;
                        case "sourceAlignment": learning.personalBests.sourceAlignment = currentValue; break;
                        case "movementFlow":    learning.personalBests.movementFlow    = currentValue; break;
                        case "spinStability":   learning.personalBests.spinStability   = currentValue; break;
                    }
                }
            }
        }

        // -------------------------------------------------------------------------
        // Debug log
        // -------------------------------------------------------------------------
        private void LogSummary(ResonanceLearningState l, SessionResonanceResult r)
        {
            Debug.Log(
                $"[ResonanceMemorySystem] Session #{l.sessionsAnalyzed} integrated.\n" +
                $"  Source connection: {l.sourceConnectionLevel:F3}  Trend: {l.growthTrend:+0.00;-0.00}\n" +
                $"  Element: {l.dominantElement}  Archetype: {l.resonanceArchetype}\n" +
                $"  Signature: calm={l.signature.calmScore:F2} joy={l.signature.joyScore:F2} " +
                $"source={l.signature.sourceAlignmentScore:F2} wonder={l.signature.wonderScore:F2}\n" +
                $"  WorldResponse: {l.WorldResponseIntensity():F3}  " +
                $"CompanionBondMult: {l.CompanionBondMultiplier():F2}x");
        }

        // -------------------------------------------------------------------------
        // Public API for world systems
        // -------------------------------------------------------------------------

        /// <summary>
        /// Is the player currently performing above their learned baseline?
        /// Used by cave/realm audio and visual systems to detect "flow state".
        /// </summary>
        public bool IsInFlowState()
        {
            return SourceAlignmentDelta >= 0.08f;
        }

        /// <summary>
        /// Personalized greeting string for UI when player returns.
        /// Maps source connection level and archetype to a message tone.
        /// </summary>
        public string GetReturnGreeting()
        {
            var learning = UniverseStateManager.Instance?.Current.learningState;
            if (learning == null || learning.sessionsAnalyzed == 0)
                return "The cave is open.";

            float sc = learning.sourceConnectionLevel;
            string element = learning.dominantElement ?? "Air";

            if (sc >= 0.80f) return $"The Source remembers you. The {element} path deepens.";
            if (sc >= 0.60f) return $"Your {element} resonance is growing stronger.";
            if (sc >= 0.40f) return $"The {element} spirits know your presence.";
            if (sc >= 0.20f) return $"Your pattern is forming. Walk the {element} path.";
            return "The journey begins with breath.";
        }
    }
}
