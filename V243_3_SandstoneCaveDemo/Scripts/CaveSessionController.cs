using System;
using UnityEngine;
using SpiritsCrossing;

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
        }

        public void StartSession()
        {
            sessionTimer = 0f;
            sessionRunning = true;
            sessionComplete = false;
            chakraState = new ChakraState();
        }

        public void StopSession()
        {
            sessionRunning = false;
        }

        private void CompleteSession()
        {
            sessionComplete = true;
            sessionRunning = false;

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

            return result;
        }

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
