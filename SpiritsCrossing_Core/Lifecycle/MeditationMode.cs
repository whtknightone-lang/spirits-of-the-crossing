// SpiritsCrossing — MeditationMode.cs
// A thin intentional layer over the existing resonance system.
// When the player meditates deliberately, their calm and breath signals
// are gently amplified, giving clearer feedback and faster response from
// companions, spirits, and the Source.
//
// The whole system already responds to calm + breath organically.
// MeditationMode makes that intention explicit and rewarded.
//
// Entry (any of):
//   VR   — both grips held + head bowed for 3 seconds
//   Flat — 'M' key held, or programmatic Enter()
//
// While meditating:
//   calm signal ×1.30, breathCoherence ×1.20 (fed into BreathMovementInterpreter)
//   sourceAlignment gently lifts toward current coherence
//   VibrationalResonanceSystem receives the amplified field — companions respond
//   If sourceConnectionLevel >= 0.4 and depth > 0.5: Source Drop-In available
//   Events fired: OnMeditationEntered, OnMeditationDeepened, OnMeditationExited
//
// Wiring:
//   Add to Bootstrap (VRBootstrapInstaller creates it).
//   BreathMovementInterpreter receives amplified values via UpdateCalm / UpdateSourceAlignment.

using System;
using UnityEngine;
using V243.SandstoneCave;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Memory;
using SpiritsCrossing.VR;

namespace SpiritsCrossing.Lifecycle
{
    public class MeditationMode : MonoBehaviour
    {
        public static MeditationMode Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Entry")]
        [Tooltip("Seconds both grips must be held + head bowed before meditation enters (VR).")]
        public float vrEntryHoldSeconds = 3.0f;

        [Header("Signal Amplification")]
        [Range(1f, 2f)] public float calmAmplification          = 1.30f;
        [Range(1f, 2f)] public float breathCoherenceAmplification = 1.20f;

        [Tooltip("How quickly sourceAlignment lifts toward coherence while meditating.")]
        [Range(0f, 1f)] public float sourceRiseRate = 0.04f;

        [Header("Depth")]
        [Tooltip("Meditation depth grows per second while calm > 0.6.")]
        public float depthAccumRate = 0.012f;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action       OnMeditationEntered;
        public event Action<float> OnMeditationDeepened; // depth 0-1
        public event Action       OnMeditationExited;
        public event Action       OnSourceDropInAvailable; // depth > 0.5 + scl >= 0.4

        // -------------------------------------------------------------------------
        // Public state
        // -------------------------------------------------------------------------
        public bool  IsMeditating { get; private set; }
        public float Depth        { get; private set; }  // 0-1 accumulated this session
        public bool  DropInAvailable =>
            IsMeditating && Depth >= 0.5f &&
            (ResonanceMemorySystem.Instance?.SourceConnectionLevel ?? 0f) >= 0.40f;

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private BreathMovementInterpreter _interpreter;
        private float  _vrEntryTimer;
        private bool   _dropInFired;
        private float  _prevDepth;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            _interpreter = FindObjectOfType<BreathMovementInterpreter>();
        }

        private void Update()
        {
            if (_interpreter == null)
                _interpreter = FindObjectOfType<BreathMovementInterpreter>();

            HandleVREntry();
            HandleFlatEntry();

            if (IsMeditating)
            {
                ApplyAmplification();
                AccumulateDepth();
                CheckDropInAvailable();
            }
        }

        // -------------------------------------------------------------------------
        // VR Entry — both grips + head bowed for vrEntryHoldSeconds
        // -------------------------------------------------------------------------
        private void HandleVREntry()
        {
            var vr = VRInputAdapter.Instance;
            if (vr == null || !vr.IsVRActive) return;

            bool gripsHeld   = vr.IsMeditating;   // both grips held
            bool headBowed   = vr.Gestures.HeadBowed >= 0.5f;
            bool inCondition = gripsHeld && headBowed;

            if (inCondition && !IsMeditating)
            {
                _vrEntryTimer += Time.deltaTime;
                if (_vrEntryTimer >= vrEntryHoldSeconds) Enter();
            }
            else if (!inCondition)
            {
                _vrEntryTimer = 0f;
                if (IsMeditating && !gripsHeld) Exit();
            }
        }

        // -------------------------------------------------------------------------
        // Flat Entry — 'M' key
        // -------------------------------------------------------------------------
        private void HandleFlatEntry()
        {
            if (VRInputAdapter.Instance?.IsVRActive ?? false) return;
            if (Input.GetKeyDown(KeyCode.M)) { if (IsMeditating) Exit(); else Enter(); }
        }

        // -------------------------------------------------------------------------
        // Enter / Exit
        // -------------------------------------------------------------------------
        public void Enter()
        {
            if (IsMeditating) return;
            IsMeditating = true;
            Depth        = 0f;
            _dropInFired = false;
            OnMeditationEntered?.Invoke();
            Debug.Log("[MeditationMode] Entered. calm×1.3, breathCoherence×1.2 active.");
        }

        public void Exit()
        {
            if (!IsMeditating) return;
            IsMeditating = false;
            OnMeditationExited?.Invoke();
            Debug.Log($"[MeditationMode] Exited. Final depth={Depth:F3}");
        }

        // -------------------------------------------------------------------------
        // Amplify calm + breathCoherence in the interpreter
        // -------------------------------------------------------------------------
        private void ApplyAmplification()
        {
            if (_interpreter == null) return;

            var state = _interpreter.state;

            // Amplified values — clamped to 1.0
            float amplifiedCalm      = Mathf.Clamp01(state.calm            * calmAmplification);
            float amplifiedCoherence = Mathf.Clamp01(state.breathCoherence * breathCoherenceAmplification);

            // Source alignment gently rises toward the coherence level
            float targetSource = Mathf.Max(state.sourceAlignment,
                amplifiedCoherence * 0.5f + amplifiedCalm * 0.3f);
            float newSource = Mathf.MoveTowards(state.sourceAlignment, targetSource,
                sourceRiseRate * Time.deltaTime);

            // Push amplified values back into the interpreter
            _interpreter.UpdateCalm(amplifiedCalm);
            _interpreter.UpdateBreath(amplifiedCoherence);
            _interpreter.UpdateSourceAlignment(newSource);
        }

        // -------------------------------------------------------------------------
        // Accumulate depth while in deep calm
        // -------------------------------------------------------------------------
        private void AccumulateDepth()
        {
            if (_interpreter == null) return;
            float calm = _interpreter.state.calm;

            if (calm >= 0.60f)
            {
                float gain  = depthAccumRate * (calm - 0.60f) / 0.40f * Time.deltaTime;
                Depth = Mathf.Clamp01(Depth + gain);

                // Fire deepened every 0.1 increment
                if (Depth - _prevDepth >= 0.10f)
                {
                    OnMeditationDeepened?.Invoke(Depth);
                    _prevDepth = Mathf.Floor(Depth * 10f) / 10f;
                    Debug.Log($"[MeditationMode] Depth={Depth:F2}");
                }
            }
        }

        // -------------------------------------------------------------------------
        // Check Source Drop-In availability
        // -------------------------------------------------------------------------
        private void CheckDropInAvailable()
        {
            if (!_dropInFired && DropInAvailable)
            {
                _dropInFired = true;
                OnSourceDropInAvailable?.Invoke();
                Debug.Log("[MeditationMode] Source Drop-In now available.");
            }
        }

        // -------------------------------------------------------------------------
        // Read-only status for UI
        // -------------------------------------------------------------------------
        public string GetStatusString()
        {
            if (!IsMeditating) return "Not meditating";
            if (Depth < 0.25f)  return "Settling...";
            if (Depth < 0.50f)  return "Deepening";
            if (Depth < 0.75f)  return "In resonance";
            if (DropInAvailable) return "Source available";
            return "Deep communion";
        }
    }
}
