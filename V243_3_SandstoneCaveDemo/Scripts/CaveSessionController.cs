using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing;
using SpiritsCrossing.SpiritAI;
using SpiritsCrossing.RUE;

namespace V243.SandstoneCave
{
    public class CaveSessionController : MonoBehaviour
    {
        [Header("Session Timing")]
        public float totalSessionLength = 720f;
        public float crownHoldStart = 420f;
        public bool autoStart = true;

        [Header("State")]
        public ChakraState chakraState = new ChakraState();
        public float sessionTimer;
        public bool sessionRunning;
        public bool sessionComplete;

        [Header("References")]
        public CaveVisualPulseController visualPulseController;
        public PortalUnlockController portalUnlockController;
        public PlanetAffinityInterpreter planetAffinityInterpreter;
        public PlayerResponseTracker playerResponseTracker;

        public delegate void ChakraChanged(ChakraState state);
        public event ChakraChanged OnChakraChanged;

        // Emitted when the session ends. Consumed by GameBootstrapper → UniverseStateManager.
        public event Action<SessionResonanceResult> OnSessionComplete;

        // Spirit awakenings accumulated during a session — promoted to myth trigger keys.
        private readonly List<string>    _mythTriggerAccumulator = new List<string>();
        private          SpiritBrainOrchestrator _orchestrator;

        // RUE Bridge — tracks the last awakened archetype for player resonance injection.
        private string _activeArchetype = null;

        private void Start()
        {
            if (autoStart)
            {
                StartSession();
            }
        }

        private void Update()
        {
            if (!sessionRunning || sessionComplete)
            {
                return;
            }

            sessionTimer += Time.deltaTime;

            if (sessionTimer < crownHoldStart)
            {
                UpdateChakraProgression(sessionTimer);
            }
            else
            {
                EnterCrownHold();
            }

            if (visualPulseController != null)
            {
                visualPulseController.ApplyChakra(chakraState.activeBand, chakraState.progress01, chakraState.isHolding);
            }

            OnChakraChanged?.Invoke(chakraState);

            if (portalUnlockController != null && playerResponseTracker != null)
            {
                portalUnlockController.Evaluate(playerResponseTracker.LiveState, chakraState);
            }

            if (sessionTimer >= totalSessionLength)
            {
                CompleteSession();
            }

            // ── RUE Bridge: push live player state into the simulation every frame ──
            PushResonanceToRUE();
        }

        public void StartSession()
        {
            sessionTimer = 0f;
            sessionRunning = true;
            sessionComplete = false;
            chakraState = new ChakraState();

            _mythTriggerAccumulator.Clear();

            // Subscribe to spirit awakening events for the duration of this session.
            // Unsubscribe first so re-starting a session never double-registers the handler.
            if (_orchestrator == null)
                _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();
            if (_orchestrator != null)
            {
                _orchestrator.OnSpiritAwakened -= HandleSpiritAwakened; // remove stale subscription if any
                _orchestrator.OnSpiritAwakened += HandleSpiritAwakened;
            }
        }

        public void StopSession()
        {
            sessionRunning = false;
            if (_orchestrator != null)
                _orchestrator.OnSpiritAwakened -= HandleSpiritAwakened;
        }

        private void CompleteSession()
        {
            sessionComplete = true;
            sessionRunning = false;

            // Stop listening to spirit awakenings — session is now sealed.
            if (_orchestrator != null)
                _orchestrator.OnSpiritAwakened -= HandleSpiritAwakened;

            if (playerResponseTracker != null && planetAffinityInterpreter != null)
            {
                planetAffinityInterpreter.currentSample = playerResponseTracker.BuildSample();
                planetAffinityInterpreter.EvaluateAffinities();
            }

            OnSessionComplete?.Invoke(BuildSessionResult());
        }

        public SessionResonanceResult BuildSessionResult()
        {
            var result = SessionResonanceResult.Empty();
            result.sessionDurationSeconds = sessionTimer;
            result.chakraBandsCompleted   = (int)chakraState.activeBand + (chakraState.isHolding ? 1 : 0);
            result.portalUnlocked         = portalUnlockController != null && portalUnlockController.portalUnlocked;

            if (playerResponseTracker != null)
            {
                var raw = playerResponseTracker.BuildSample();
                result.resonanceSample = new SpiritsCrossing.PlayerResponseSample
                {
                    stillnessScore       = raw.stillnessScore,
                    flowScore            = raw.flowScore,
                    spinScore            = raw.spinScore,
                    pairSyncScore        = raw.pairSyncScore,
                    calmScore            = raw.calmScore,
                    joyScore             = raw.joyScore,
                    wonderScore          = raw.wonderScore,
                    distortionScore      = raw.distortionScore,
                    sourceAlignmentScore = raw.sourceAlignmentScore
                };
            }

            if (planetAffinityInterpreter != null)
            {
                result.currentAffinityPlanet    = planetAffinityInterpreter.currentAffinityPlanet;
                result.achievableAffinityPlanet = planetAffinityInterpreter.achievableAffinityPlanet;
                result.currentAffinityScore     = planetAffinityInterpreter.currentAffinityScore;
                result.achievableAffinityScore  = planetAffinityInterpreter.achievableAffinityScore;
            }

            // Promote spirit awakenings captured during this session to myth trigger keys.
            foreach (var key in _mythTriggerAccumulator)
                if (!result.mythTriggerKeys.Contains(key))
                    result.mythTriggerKeys.Add(key);

            return result;
        }

        // -------------------------------------------------------------------------
        // Spirit awakening → myth key promotion
        // -------------------------------------------------------------------------
        private void HandleSpiritAwakened(SpiritBrainController brain, float score)
        {
            string mythKey = ArchetypeIdToMythKey(brain.archetypeId);
            if (!string.IsNullOrEmpty(mythKey) && !_mythTriggerAccumulator.Contains(mythKey))
            {
                _mythTriggerAccumulator.Add(mythKey);
                Debug.Log($"[CaveSessionController] Spirit awakened: {brain.archetypeId} → myth key '{mythKey}' (score={score:F2})");
            }

            // RUE Bridge: update active archetype so the simulation knows which Nahual is present.
            _activeArchetype = brain.archetypeId;
        }

        /// <summary>
        /// Maps a spirit archetype ID (string) to the canonical myth key used by MythInterpreter.
        /// Archetype IDs are the PascalCase string values of SpiritArchetype enum entries.
        /// </summary>
        // ── RUE Bridge helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Derives PlayerResonance from the live cave state and sends it to RUEBridge.
        /// Called every Update() tick while the session is running.
        ///
        /// Mapping:
        ///   sync_level      ← sourceAlignmentScore  (stillness / crown coherence)
        ///   energy          ← (flowScore + joyScore + wonderScore) scaled 0–5
        ///   archetype       ← last awakened spirit archetype
        ///   planet_affinity ← planetAffinityInterpreter.currentAffinityPlanet
        ///   breath_phase    ← chakra band progress mapped to 0–2π
        /// </summary>
        private void PushResonanceToRUE()
        {
            if (RUEBridge.Instance == null) return;

            float syncLevel  = 0f;
            float energy     = 2f;
            string affinity  = null;

            if (playerResponseTracker != null)
            {
                var s = playerResponseTracker.BuildSample();
                syncLevel = s.sourceAlignmentScore;
                energy    = Mathf.Clamp((s.flowScore + s.joyScore + s.wonderScore) * 5f / 3f, 0f, 5f);
            }

            if (planetAffinityInterpreter != null)
                affinity = planetAffinityInterpreter.currentAffinityPlanet;

            // Breath phase: smoothly cycles 0 → 2π as the player moves through each chakra band.
            float breathPhase = chakraState.progress01 * 2f * Mathf.PI;

            RUEBridge.Instance.SetPlayerResonance(
                syncLevel,
                energy,
                _activeArchetype,
                affinity,
                breathPhase
            );
        }

        private static string ArchetypeIdToMythKey(string archetypeId) => archetypeId switch
        {
            "Seated"         => "source",
            "FlowDancer"     => "ocean",
            "Dervish"        => "sky",
            "PairA"          => "social",
            "PairB"          => "social",
            "EarthDragon"    => "forest",
            "FireDragon"     => "fire",
            "WaterDragon"    => "ocean",
            "ElderAirDragon" => "elder",
            _                => string.Empty
        };

        private void UpdateChakraProgression(float t)
        {
            float bandDuration = crownHoldStart / 7f;
            int bandIndex = Mathf.Clamp((int)(t / bandDuration), 0, 6);
            chakraState.activeBand = (ChakraBand)bandIndex;
            chakraState.progress01 = (t % bandDuration) / bandDuration;
            chakraState.isHolding = false;
            chakraState.holdTimer = 0f;
        }

        private void EnterCrownHold()
        {
            chakraState.activeBand = ChakraBand.Crown;
            chakraState.progress01 = 1f;
            chakraState.isHolding = true;
            chakraState.holdTimer += Time.deltaTime;
        }
    }
}
