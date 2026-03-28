// SpiritsCrossing — VRHapticsController.cs
// Wires haptic feedback to every meaningful game event.
// Replaces VRHapticsStub.cs (V129) with a fully connected system.
//
// Event → Haptic pattern:
//
//   Spirit awakened      → short bilateral pulse, intensity scales with score
//   Spirit reaches disk  → low sustained rumble on both hands
//   Spirit dimmed        → brief fade-out on left hand
//   Portal unlocked      → strong bilateral double-pulse (celebration)
//   Chakra transition    → single side-alternating tap (left = even bands, right = odd)
//   Crown hold entered   → slow continuous bilateral resonance pulse
//   Session complete     → long triple-pulse bilateral (completion)
//   Realm complete       → intensity proportional to celebration score
//   Wonder gesture       → gentle bilateral shimmer (both hands together)
//   Source gesture       → deep low rumble on both hands (hands raised)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using SpiritsCrossing.SpiritAI;
using SpiritsCrossing.Lifecycle;
using V243.SandstoneCave;

namespace SpiritsCrossing.VR
{
    public class VRHapticsController : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Intensity Scaling")]
        [Range(0f, 1f)] public float globalIntensityScale = 0.85f;

        [Header("Pattern Tuning")]
        public float awakeningPulseDuration  = 0.12f;
        public float diskArrivalDuration     = 0.6f;
        public float portalUnlockDuration    = 0.18f;
        public float chakraTapDuration       = 0.08f;
        public float crownHoldPulsePeriod    = 1.4f;   // seconds between pulses
        public float sessionCompleteDuration = 0.25f;
        public float realmCompleteDuration   = 0.30f;
        public float gesturePulseDuration    = 0.10f;

        [Header("References (auto-found if null)")]
        public SpiritBrainOrchestrator spiritOrchestrator;
        public CaveSessionController   caveSession;

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private InputDevice _leftController;
        private InputDevice _rightController;
        private bool        _crownHoldActive;
        private Coroutine   _crownPulseCoroutine;

        private readonly List<InputDevice> _deviceBuffer = new List<InputDevice>();

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            RefreshDevices();
            WireEvents();
        }

        private void OnDestroy() => UnwireEvents();

        // -------------------------------------------------------------------------
        // Device discovery
        // -------------------------------------------------------------------------
        private void RefreshDevices()
        {
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller,
                _deviceBuffer);
            if (_deviceBuffer.Count > 0) _leftController = _deviceBuffer[0];

            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
                _deviceBuffer);
            if (_deviceBuffer.Count > 0) _rightController = _deviceBuffer[0];
        }

        // -------------------------------------------------------------------------
        // Event wiring
        // -------------------------------------------------------------------------
        private void WireEvents()
        {
            // Spirit orchestrator
            if (spiritOrchestrator == null)
                spiritOrchestrator = FindObjectOfType<SpiritBrainOrchestrator>();

            if (spiritOrchestrator != null)
            {
                spiritOrchestrator.OnSpiritAwakened    += OnSpiritAwakened;
                spiritOrchestrator.OnSpiritReachesDisk += OnSpiritReachesDisk;
                spiritOrchestrator.OnSpiritDimmed      += OnSpiritDimmed;
            }

            // Cave session
            if (caveSession == null)
                caveSession = FindObjectOfType<CaveSessionController>();

            if (caveSession != null)
            {
                caveSession.OnChakraChanged += OnChakraChanged;
                caveSession.OnSessionComplete += OnSessionComplete;
            }

            // Universe events
            if (UniverseStateManager.Instance != null)
                UniverseStateManager.Instance.OnRealmOutcomeApplied += OnRealmComplete;

            // Lifecycle events
            if (LifecycleSystem.Instance != null)
            {
                LifecycleSystem.Instance.OnSourceDropIn    += OnSourceDropIn;
                LifecycleSystem.Instance.OnRebirthComplete += OnRebirthComplete;
            }
            if (SourceCommunionSystem.Instance != null)
                SourceCommunionSystem.Instance.OnCommunionDepthMilestone += OnCommunionMilestone;

            // Portal unlock
            var portal = FindObjectOfType<PortalUnlockController>();
            // PortalUnlockController doesn't have an event yet — we poll it in Update.
        }

        private void UnwireEvents()
        {
            if (spiritOrchestrator != null)
            {
                spiritOrchestrator.OnSpiritAwakened    -= OnSpiritAwakened;
                spiritOrchestrator.OnSpiritReachesDisk -= OnSpiritReachesDisk;
                spiritOrchestrator.OnSpiritDimmed      -= OnSpiritDimmed;
            }
            if (caveSession != null)
            {
                caveSession.OnChakraChanged   -= OnChakraChanged;
                caveSession.OnSessionComplete -= OnSessionComplete;
            }
            if (UniverseStateManager.Instance != null)
                UniverseStateManager.Instance.OnRealmOutcomeApplied -= OnRealmComplete;

            if (LifecycleSystem.Instance != null)
            {
                LifecycleSystem.Instance.OnSourceDropIn    -= OnSourceDropIn;
                LifecycleSystem.Instance.OnRebirthComplete -= OnRebirthComplete;
            }
            if (SourceCommunionSystem.Instance != null)
                SourceCommunionSystem.Instance.OnCommunionDepthMilestone -= OnCommunionMilestone;
        }

        // -------------------------------------------------------------------------
        // Portal poll (no event on PortalUnlockController yet)
        // -------------------------------------------------------------------------
        private bool _lastPortalState;

        private void Update()
        {
            if (!_leftController.isValid || !_rightController.isValid)
                RefreshDevices();

            // Poll portal unlock state
            var portal = FindObjectOfType<PortalUnlockController>();
            if (portal != null && portal.portalUnlocked && !_lastPortalState)
                OnPortalUnlocked();
            _lastPortalState = portal != null && portal.portalUnlocked;

            // Poll VR gesture scores for ambient haptics
            var vr = VRInputAdapter.Instance;
            if (vr != null && vr.IsVRActive)
                TickGestureHaptics(vr);
        }

        // -------------------------------------------------------------------------
        // Game event handlers
        // -------------------------------------------------------------------------

        private void OnSpiritAwakened(SpiritBrainController spirit, float score)
        {
            // Scale intensity with how strongly the spirit matched
            float intensity = Mathf.Clamp01(score * 1.2f) * globalIntensityScale;
            StartCoroutine(BilateralPulse(intensity, awakeningPulseDuration));
            Debug.Log($"[VRHapticsController] Spirit awakened: {spirit.archetypeId} " +
                      $"intensity={intensity:F2}");
        }

        private void OnSpiritReachesDisk(SpiritBrainController spirit)
        {
            // Sustained low rumble — spirit has fully arrived at center
            StartCoroutine(BilateralSustained(0.35f * globalIntensityScale,
                                               diskArrivalDuration));
        }

        private void OnSpiritDimmed(SpiritBrainController spirit)
        {
            // Fade-out on left hand only — spirit withdrawing
            StartCoroutine(SingleHandFade(_leftController, 0.25f * globalIntensityScale,
                                          0.20f));
        }

        private void OnPortalUnlocked()
        {
            // Double-pulse celebration
            StartCoroutine(DoublePulse(0.85f * globalIntensityScale,
                                        portalUnlockDuration, 0.10f));
            Debug.Log("[VRHapticsController] Portal unlocked — double pulse.");
        }

        private void OnChakraChanged(ChakraState state)
        {
            // Alternate sides by chakra band index (even=left, odd=right)
            // Crown hold triggers continuous pulse loop
            bool isLeft = ((int)state.activeBand % 2 == 0);

            if (state.activeBand == ChakraBand.Crown && state.isHolding)
            {
                if (!_crownHoldActive)
                {
                    _crownHoldActive = true;
                    _crownPulseCoroutine = StartCoroutine(CrownHoldLoop());
                }
            }
            else
            {
                if (_crownHoldActive)
                {
                    _crownHoldActive = false;
                    if (_crownPulseCoroutine != null)
                        StopCoroutine(_crownPulseCoroutine);
                }
                // Chakra transition tap
                InputDevice device = isLeft ? _leftController : _rightController;
                StartCoroutine(SingleHandPulse(device, 0.5f * globalIntensityScale,
                                                chakraTapDuration));
            }
        }

        private void OnSessionComplete(SessionResonanceResult result)
        {
            _crownHoldActive = false;
            if (_crownPulseCoroutine != null) StopCoroutine(_crownPulseCoroutine);

            float intensity = result.portalUnlocked
                ? 0.90f * globalIntensityScale
                : 0.60f * globalIntensityScale;
            StartCoroutine(TriplePulse(intensity, sessionCompleteDuration, 0.12f));
        }

        private void OnRealmComplete(RealmOutcome outcome)
        {
            float intensity = Mathf.Clamp01(outcome.celebration * 1.1f) * globalIntensityScale;
            StartCoroutine(BilateralPulse(intensity, realmCompleteDuration));
        }

        // ---- Source Drop-In: 3-second deep bilateral rumble ----
        private void OnSourceDropIn()
        {
            StartCoroutine(BilateralSustained(0.40f * globalIntensityScale, 3.0f));
            Debug.Log("[VRHapticsController] Source Drop-In rumble.");
        }

        // ---- Dragon Communion milestone: element-specific pulse ----
        private void OnCommunionMilestone(string element, float depth)
        {
            switch (element)
            {
                case "Fire":  // right hand strong sharp pulse
                    StartCoroutine(SingleHandPulse(_rightController, 0.70f * globalIntensityScale, 0.15f));
                    break;
                case "Water": // left hand slow gentle pulse
                    StartCoroutine(SingleHandPulse(_leftController,  0.40f * globalIntensityScale, 0.35f));
                    break;
                case "Earth": // deep bilateral low sustained
                    StartCoroutine(BilateralSustained(0.35f * globalIntensityScale, 0.50f));
                    break;
                case "Air":   // quick alternating tap
                    StartCoroutine(SingleHandPulse(_leftController,  0.45f * globalIntensityScale, 0.10f));
                    StartCoroutine(SingleHandPulse(_rightController, 0.45f * globalIntensityScale, 0.10f));
                    break;
            }
            Debug.Log($"[VRHapticsController] Communion milestone: {element} depth={depth:F2}");
        }

        // ---- Rebirth: ascending triple pulse 0.5 / 0.7 / 0.9 ----
        private void OnRebirthComplete(System.Collections.Generic.List<string> gifts)
        {
            StartCoroutine(AscendingTriplePulse());
            Debug.Log("[VRHapticsController] Rebirth ascending triple pulse.");
        }

        private System.Collections.IEnumerator AscendingTriplePulse()
        {
            float[] intensities = { 0.50f, 0.70f, 0.90f };
            foreach (float i in intensities)
            {
                yield return BilateralPulse(i * globalIntensityScale, 0.30f);
                yield return new WaitForSeconds(0.15f);
            }
        }

        // -------------------------------------------------------------------------
        // Gesture-driven ambient haptics
        // -------------------------------------------------------------------------
        private float _lastWonderPulse;
        private float _lastSourcePulse;

        private void TickGestureHaptics(VRInputAdapter vr)
        {
            var g = vr.Gestures;

            // Wonder (hands together) → gentle bilateral shimmer
            if (g.HandsTogether >= 0.75f && Time.time - _lastWonderPulse > 0.5f)
            {
                float i = g.HandsTogether * 0.4f * globalIntensityScale;
                StartCoroutine(BilateralPulse(i, gesturePulseDuration));
                _lastWonderPulse = Time.time;
            }

            // Source (both hands raised) → deep bilateral rumble
            if (g.BothHandsRaised >= 0.70f && Time.time - _lastSourcePulse > 0.8f)
            {
                float i = g.BothHandsRaised * 0.55f * globalIntensityScale;
                StartCoroutine(BilateralSustained(i, gesturePulseDuration * 2f));
                _lastSourcePulse = Time.time;
            }
        }

        // -------------------------------------------------------------------------
        // Haptic coroutines
        // -------------------------------------------------------------------------

        private IEnumerator BilateralPulse(float intensity, float duration)
        {
            Pulse(_leftController,  intensity, duration);
            Pulse(_rightController, intensity, duration);
            yield return new WaitForSeconds(duration);
        }

        private IEnumerator BilateralSustained(float intensity, float duration)
        {
            float end = Time.time + duration;
            while (Time.time < end)
            {
                Pulse(_leftController,  intensity, 0.05f);
                Pulse(_rightController, intensity, 0.05f);
                yield return new WaitForSeconds(0.06f);
            }
        }

        private IEnumerator DoublePulse(float intensity, float duration, float gap)
        {
            yield return BilateralPulse(intensity, duration);
            yield return new WaitForSeconds(gap);
            yield return BilateralPulse(intensity * 0.75f, duration);
        }

        private IEnumerator TriplePulse(float intensity, float duration, float gap)
        {
            yield return BilateralPulse(intensity,        duration);
            yield return new WaitForSeconds(gap);
            yield return BilateralPulse(intensity * 0.8f, duration);
            yield return new WaitForSeconds(gap);
            yield return BilateralPulse(intensity * 0.6f, duration);
        }

        private IEnumerator SingleHandPulse(InputDevice device, float intensity, float duration)
        {
            Pulse(device, intensity, duration);
            yield return new WaitForSeconds(duration);
        }

        private IEnumerator SingleHandFade(InputDevice device, float intensity, float duration)
        {
            float end = Time.time + duration;
            while (Time.time < end)
            {
                float t = 1f - (Time.time - (end - duration)) / duration;
                Pulse(device, intensity * t, 0.04f);
                yield return new WaitForSeconds(0.05f);
            }
        }

        private IEnumerator CrownHoldLoop()
        {
            while (_crownHoldActive)
            {
                // Slow alternating bilateral pulse — crown resonance
                Pulse(_leftController,  0.30f * globalIntensityScale, 0.25f);
                yield return new WaitForSeconds(crownHoldPulsePeriod * 0.5f);
                if (!_crownHoldActive) break;
                Pulse(_rightController, 0.30f * globalIntensityScale, 0.25f);
                yield return new WaitForSeconds(crownHoldPulsePeriod * 0.5f);
            }
        }

        // -------------------------------------------------------------------------
        // Core haptic send
        // -------------------------------------------------------------------------
        private static void Pulse(InputDevice device, float amplitude, float duration)
        {
            if (!device.isValid) return;
            device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
        }
    }
}
