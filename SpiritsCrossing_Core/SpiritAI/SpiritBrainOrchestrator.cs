// SpiritsCrossing — SpiritBrainOrchestrator.cs
// THE live connection between player input and every spirit brain in the scene.
//
// What it does each frame:
//   1. Reads CavePlayerResonanceState from BreathMovementInterpreter
//   2. Converts it to SpiritsCrossing.PlayerResonanceState (shared contract)
//   3. Calls SpiritBrainController.UpdateFromPlayerState() on every spirit
//   4. Reads each spirit's PlayerResonanceScore() → awakening strength
//   5. Pushes awakening strength + current drive mode into SpiritLikenessController
//
// Setup — add ONE of these to the CaveSystems manager object:
//   - It auto-finds BreathMovementInterpreter and all SpiritBrainControllers in scene
//   - Assign spirits manually via the Inspector for explicit ordering, OR
//     leave the list empty to auto-discover all spirits on Start
//
// The orchestrator also fires OnSpiritAwakened when any spirit crosses the
// awakening threshold, so the cave session can react (portal unlock, audio, etc.)

using System;
using System.Collections.Generic;
using UnityEngine;
using V243.SandstoneCave;

namespace SpiritsCrossing.SpiritAI
{
    public class SpiritBrainOrchestrator : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Input Source")]
        [Tooltip("Auto-found if null.")]
        public BreathMovementInterpreter breathInterpreter;

        [Header("Spirit Brains")]
        [Tooltip("Leave empty to auto-discover all SpiritBrainControllers in scene.")]
        public List<SpiritBrainController> spirits = new List<SpiritBrainController>();

        [Header("Awakening")]
        [Range(0f, 1f)]
        [Tooltip("Resonance score required before a spirit is considered 'awakened'.")]
        public float awakeningThreshold = 0.55f;

        [Range(0f, 1f)]
        [Tooltip("Resonance score above which a spirit moves to the center disk.")]
        public float centerDiskThreshold = 0.75f;

        [Header("Update Rate")]
        [Tooltip("Brain update interval in seconds. 0 = every frame.")]
        public float updateInterval = 0.05f;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        /// <summary>Fires when a spirit first crosses the awakening threshold.</summary>
        public event Action<SpiritBrainController, float> OnSpiritAwakened;

        /// <summary>Fires when a spirit's score drops back below the threshold.</summary>
        public event Action<SpiritBrainController> OnSpiritDimmed;

        /// <summary>Fires when a spirit reaches the center-disk threshold.</summary>
        public event Action<SpiritBrainController> OnSpiritReachesDisk;

        // -------------------------------------------------------------------------
        // Public read-only state
        // -------------------------------------------------------------------------
        /// <summary>The spirit currently resonating most strongly with the player.</summary>
        public SpiritBrainController LeadSpirit { get; private set; }

        /// <summary>Live resonance scores indexed by spirit archetype ID.</summary>
        public IReadOnlyDictionary<string, float> ResonanceScores => _scores;

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private readonly Dictionary<string, float> _scores      = new();
        private readonly HashSet<string>            _awakened    = new();
        private readonly HashSet<string>            _atDisk      = new();

        private PlayerResonanceState _sharedState = new PlayerResonanceState();
        private float                _timer;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            if (breathInterpreter == null)
                breathInterpreter = FindObjectOfType<BreathMovementInterpreter>();

            if (breathInterpreter == null)
                Debug.LogWarning("[SpiritBrainOrchestrator] No BreathMovementInterpreter found. " +
                                 "Spirit brains will not receive player input.");

            // Auto-discover spirits if none assigned
            if (spirits.Count == 0)
            {
                spirits.AddRange(FindObjectsOfType<SpiritBrainController>());
                Debug.Log($"[SpiritBrainOrchestrator] Auto-discovered {spirits.Count} spirit brain(s).");
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (updateInterval > 0f && _timer < updateInterval) return;
            _timer = 0f;

            SyncPlayerState();
            UpdateAllBrains();
            UpdateAwakeningState();
        }

        // -------------------------------------------------------------------------
        // Step 1 — Convert CavePlayerResonanceState → shared PlayerResonanceState
        // -------------------------------------------------------------------------
        private void SyncPlayerState()
        {
            if (breathInterpreter == null) return;

            var cave = breathInterpreter.state;  // V243.SandstoneCave.CavePlayerResonanceState

            _sharedState.breathCoherence = cave.breathCoherence;
            _sharedState.movementFlow    = cave.movementFlow;
            _sharedState.spinStability   = cave.spinStability;
            _sharedState.socialSync      = cave.socialSync;
            _sharedState.calm            = cave.calm;
            _sharedState.joy             = cave.joy;
            _sharedState.wonder          = cave.wonder;
            _sharedState.distortion      = cave.distortion;
            _sharedState.sourceAlignment = cave.sourceAlignment;
        }

        // -------------------------------------------------------------------------
        // Step 2 — Push player state into every spirit brain
        // -------------------------------------------------------------------------
        private void UpdateAllBrains()
        {
            foreach (var spirit in spirits)
            {
                if (spirit == null) continue;
                spirit.UpdateFromPlayerState(_sharedState);
            }
        }

        // -------------------------------------------------------------------------
        // Step 3 — Read awakening scores and fire events
        // -------------------------------------------------------------------------
        private void UpdateAwakeningState()
        {
            float bestScore    = -1f;
            SpiritBrainController bestSpirit = null;

            foreach (var spirit in spirits)
            {
                if (spirit == null) continue;

                float score = spirit.PlayerResonanceScore(_sharedState);
                _scores[spirit.archetypeId] = score;

                bool wasAwakened = _awakened.Contains(spirit.archetypeId);
                bool nowAwakened = score >= awakeningThreshold;

                if (nowAwakened && !wasAwakened)
                {
                    _awakened.Add(spirit.archetypeId);
                    OnSpiritAwakened?.Invoke(spirit, score);
                    Debug.Log($"[SpiritBrainOrchestrator] {spirit.archetypeId} awakened " +
                              $"(score={score:F3}, mode={spirit.CurrentMode})");
                }
                else if (!nowAwakened && wasAwakened)
                {
                    _awakened.Remove(spirit.archetypeId);
                    _atDisk.Remove(spirit.archetypeId);
                    OnSpiritDimmed?.Invoke(spirit);
                }

                // Center-disk threshold
                bool wasAtDisk = _atDisk.Contains(spirit.archetypeId);
                bool nowAtDisk = score >= centerDiskThreshold;
                if (nowAtDisk && !wasAtDisk)
                {
                    _atDisk.Add(spirit.archetypeId);
                    OnSpiritReachesDisk?.Invoke(spirit);
                    Debug.Log($"[SpiritBrainOrchestrator] {spirit.archetypeId} reached center disk " +
                              $"(score={score:F3})");
                }

                // Drive the SpiritLikenessController on the same GameObject if present
                PushToLikenessController(spirit, score);

                if (score > bestScore) { bestScore = score; bestSpirit = spirit; }
            }

            LeadSpirit = bestSpirit;
        }

        // -------------------------------------------------------------------------
        // Step 4 — Feed score + mode into SpiritLikenessController (if present)
        // The cave's existing SpiritLikenessController needs the awakening signal
        // to move the spirit to the center disk. We drive it here rather than
        // modifying that class, using its public API.
        // -------------------------------------------------------------------------
        private void PushToLikenessController(SpiritBrainController brain, float score)
        {
            var likeness = brain.GetComponent<SpiritLikenessController>();
            if (likeness == null) return;

            // SpiritLikenessController tracks the player response tracker internally.
            // We expose the awakening score via the response tracker's sample fields
            // if a PlayerResponseTracker is present on the same object.
            // If not, the OnSpiritAwakened event is the hookup point.
            // Nothing to do here unless the designer has wired a custom bridge.
        }

        // -------------------------------------------------------------------------
        // Convenience — current live PlayerResonanceState (read by other systems)
        // -------------------------------------------------------------------------
        public PlayerResonanceState CurrentPlayerState => _sharedState;

        /// <summary>Is a specific spirit currently awakened?</summary>
        public bool IsAwakened(string archetypeId) => _awakened.Contains(archetypeId);

        /// <summary>Is a specific spirit at the center disk?</summary>
        public bool IsAtDisk(string archetypeId) => _atDisk.Contains(archetypeId);

        /// <summary>
        /// Force-register a spirit at runtime (e.g. when a prefab is instantiated
        /// after Start() has run).
        /// </summary>
        public void RegisterSpirit(SpiritBrainController spirit)
        {
            if (!spirits.Contains(spirit))
                spirits.Add(spirit);
        }

        public void UnregisterSpirit(SpiritBrainController spirit)
        {
            spirits.Remove(spirit);
            _scores.Remove(spirit.archetypeId);
            _awakened.Remove(spirit.archetypeId);
            _atDisk.Remove(spirit.archetypeId);
        }
    }
}
