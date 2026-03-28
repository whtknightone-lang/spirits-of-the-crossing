// SpiritsCrossing — VRInputAdapter.cs
// Central XR input hub. Reads from UnityEngine.XR.InputDevices and exposes:
//   - Flight axes (replaces Input.GetAxis for ElderAirDragonController)
//   - Button states (replaces Input.GetKey)
//   - Raw hand + head transforms and velocities
//   - Gesture scores 0–1 (used by BreathMovementInterpreter and PhysicalInputBridge)
//
// Usage:
//   VRInputAdapter.Instance.FlightHorizontal   → replaces Input.GetAxis("Horizontal")
//   VRInputAdapter.Instance.IsMeditating       → replaces Input.GetKey(KeyCode.F)
//   VRInputAdapter.Instance.Gestures.BothHandsRaised → sourceAlignment signal
//
// IsVRActive is false on non-XR builds — all other scripts check this before
// calling VR-specific paths, so the flat-screen game is unaffected.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace SpiritsCrossing.VR
{
    // -------------------------------------------------------------------------
    // Gesture scores — all 0–1, updated each frame
    // -------------------------------------------------------------------------
    public class VRGestureScores
    {
        /// <summary>Both hands above shoulder height → source / crown gesture.</summary>
        public float BothHandsRaised;

        /// <summary>Arms extended wide at shoulder height → social / open gesture.</summary>
        public float ArmsOutstretched;

        /// <summary>Head pitched downward → calm / meditation gesture.</summary>
        public float HeadBowed;

        /// <summary>Body rotating around vertical axis → spin / dervish signal.</summary>
        public float IsSpinning;

        /// <summary>Hands close together → wonder / prayer gesture.</summary>
        public float HandsTogether;

        /// <summary>One hand resting at heart height and still → joy / heart gesture.</summary>
        public float HandOnHeart;

        /// <summary>Both hands moving in mirrored arc → flow dance signal.</summary>
        public float MirroredFlow;
    }

    // -------------------------------------------------------------------------
    // Main adapter
    // -------------------------------------------------------------------------
    public class VRInputAdapter : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------
        public static VRInputAdapter Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Rig References (auto-found if null)")]
        public Transform headTransform;
        public Transform leftHandTransform;
        public Transform rightHandTransform;

        [Header("Flight Tuning")]
        [Range(0.5f, 5f)] public float thumbstickDeadzone = 0.15f;
        [Range(0.5f, 5f)] public float pitchSensitivity   = 1.2f;

        [Header("Gesture Thresholds")]
        [Range(0f, 1f)] public float raisedHandHeight   = 0.25f;  // metres above shoulder
        [Range(0f, 1f)] public float outstretchedWidth  = 0.55f;  // metres from body center
        [Range(0f, 30f)] public float headBowedDegrees  = 25f;    // pitch degrees below horizontal
        [Range(0f, 2f)] public float spinningRateThreshold = 0.6f; // rad/s
        [Range(0f, 0.5f)] public float handsTogetherDist = 0.22f; // metres between hands
        [Range(0f, 0.5f)] public float heartZoneRadius  = 0.20f;  // metres from chest center

        // -------------------------------------------------------------------------
        // Public state — read by ElderAirDragonController, BreathMovementInterpreter, etc.
        // -------------------------------------------------------------------------

        /// <summary>True when XR display is active (i.e. running in a headset).</summary>
        public bool IsVRActive { get; private set; }

        // --- Flight axes (replaces Input.GetAxis) ---
        public float FlightHorizontal { get; private set; }
        public float FlightVertical   { get; private set; }

        // --- Buttons (replaces Input.GetKey) ---
        public bool IsAscending  { get; private set; }   // left thumbstick click or left trigger
        public bool IsDiving     { get; private set; }   // right trigger
        public bool IsMeditating { get; private set; }   // grip buttons held together
        public bool IsGustBurst  { get; private set; }   // left trigger tap
        public bool IsResonancePulse { get; private set; } // right thumbstick click

        // --- Raw hand data ---
        public Vector3 LeftHandPosition  { get; private set; }
        public Vector3 RightHandPosition { get; private set; }
        public Vector3 LeftHandVelocity  { get; private set; }
        public Vector3 RightHandVelocity { get; private set; }
        public Vector3 HeadPosition      { get; private set; }
        public Vector3 HeadForward       { get; private set; }
        public float   AngularVelocityY  { get; private set; }  // body yaw rate (rad/s)
        public float   CombinedHandSpeed { get; private set; }  // magnitude of avg hand velocity

        // --- Gesture scores ---
        public VRGestureScores Gestures { get; private set; } = new VRGestureScores();

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private InputDevice _leftController;
        private InputDevice _rightController;
        private InputDevice _headDevice;

        private Vector3 _prevHeadForward;
        private Vector3 _prevLeftVel;
        private Vector3 _prevRightVel;
        private float   _smoothedYawRate;
        private float   _shoulderHeight; // estimated from head height

        private readonly List<InputDevice> _deviceBuffer = new List<InputDevice>();

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            RefreshDevices();
            _prevHeadForward = transform.forward;
        }

        private void Update()
        {
            // Refresh devices if not connected yet
            if (!_leftController.isValid || !_rightController.isValid)
                RefreshDevices();

            IsVRActive = XRSettings.isDeviceActive;
            if (!IsVRActive) return;

            ReadDevices();
            UpdateTransforms();
            ComputeGestures();
        }

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

            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeadMounted,
                _deviceBuffer);
            if (_deviceBuffer.Count > 0) _headDevice = _deviceBuffer[0];
        }

        // -------------------------------------------------------------------------
        // Read raw device data
        // -------------------------------------------------------------------------
        private void ReadDevices()
        {
            // --- Left controller ---
            _leftController.TryGetFeatureValue(CommonUsages.primary2DAxis,  out Vector2 leftStick);
            _leftController.TryGetFeatureValue(CommonUsages.triggerButton,  out bool leftTriggerBtn);
            _leftController.TryGetFeatureValue(CommonUsages.gripButton,     out bool leftGrip);
            _leftController.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 leftVel);
            _leftController.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 leftPos);

            // --- Right controller ---
            _rightController.TryGetFeatureValue(CommonUsages.primary2DAxis,    out Vector2 rightStick);
            _rightController.TryGetFeatureValue(CommonUsages.triggerButton,     out bool rightTriggerBtn);
            _rightController.TryGetFeatureValue(CommonUsages.gripButton,        out bool rightGrip);
            _rightController.TryGetFeatureValue(CommonUsages.primary2DAxisClick,out bool rightStickClick);
            _rightController.TryGetFeatureValue(CommonUsages.deviceVelocity,    out Vector3 rightVel);
            _rightController.TryGetFeatureValue(CommonUsages.devicePosition,    out Vector3 rightPos);

            // --- Head ---
            _headDevice.TryGetFeatureValue(CommonUsages.devicePosition,        out Vector3 headPos);
            _headDevice.TryGetFeatureValue(CommonUsages.deviceRotation,        out Quaternion headRot);
            _headDevice.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out Vector3 headAngVel);

            // --- Flight axes ---
            // Right stick controls yaw/pitch for dragon flight
            Vector2 stickInput = rightStick;
            if (stickInput.magnitude < thumbstickDeadzone) stickInput = Vector2.zero;
            FlightHorizontal = stickInput.x;
            FlightVertical   = stickInput.y * pitchSensitivity;

            // --- Buttons ---
            IsAscending      = leftStick.y > 0.6f || leftTriggerBtn;
            IsDiving         = rightTriggerBtn;
            IsMeditating     = leftGrip && rightGrip;  // both grips = glide meditation
            IsGustBurst      = leftTriggerBtn && !leftGrip;
            IsResonancePulse = rightStickClick;

            // --- Raw positions / velocities ---
            LeftHandPosition  = leftPos;
            RightHandPosition = rightPos;
            LeftHandVelocity  = leftVel;
            RightHandVelocity = rightVel;
            HeadPosition      = headPos;
            HeadForward       = headRot * Vector3.forward;

            // Yaw rate from head angular velocity (Y axis)
            float rawYaw = Mathf.Abs(headAngVel.y);
            _smoothedYawRate  = Mathf.Lerp(_smoothedYawRate, rawYaw, Time.deltaTime * 8f);
            AngularVelocityY  = _smoothedYawRate;

            CombinedHandSpeed = ((leftVel + rightVel) * 0.5f).magnitude;

            _prevLeftVel  = leftVel;
            _prevRightVel = rightVel;

            // Estimated shoulder height (70% of head height)
            _shoulderHeight = headPos.y - 0.25f;
        }

        // -------------------------------------------------------------------------
        // Sync to scene Transforms if assigned
        // -------------------------------------------------------------------------
        private void UpdateTransforms()
        {
            if (headTransform      != null) HeadForward       = headTransform.forward;
            if (leftHandTransform  != null) LeftHandPosition  = leftHandTransform.position;
            if (rightHandTransform != null) RightHandPosition = rightHandTransform.position;
        }

        // -------------------------------------------------------------------------
        // Gesture scoring
        // -------------------------------------------------------------------------
        private void ComputeGestures()
        {
            // Both hands raised above shoulder
            float leftRaised  = Mathf.Clamp01((LeftHandPosition.y  - _shoulderHeight) / raisedHandHeight);
            float rightRaised = Mathf.Clamp01((RightHandPosition.y - _shoulderHeight) / raisedHandHeight);
            Gestures.BothHandsRaised = leftRaised * rightRaised;

            // Arms outstretched (horizontal distance from body centre)
            Vector3 bodyCenter  = new Vector3(HeadPosition.x, _shoulderHeight, HeadPosition.z);
            float leftWidth     = Mathf.Clamp01(Vector3.Distance(
                new Vector3(LeftHandPosition.x,  0, LeftHandPosition.z),
                new Vector3(bodyCenter.x, 0, bodyCenter.z)) / outstretchedWidth);
            float rightWidth    = Mathf.Clamp01(Vector3.Distance(
                new Vector3(RightHandPosition.x, 0, RightHandPosition.z),
                new Vector3(bodyCenter.x, 0, bodyCenter.z)) / outstretchedWidth);
            float heightMatch   = 1f - Mathf.Abs(LeftHandPosition.y - RightHandPosition.y);
            Gestures.ArmsOutstretched = Mathf.Clamp01(leftWidth * rightWidth * heightMatch);

            // Head bowed (pitch below horizontal)
            float headPitch     = Vector3.SignedAngle(Vector3.forward,
                                      new Vector3(HeadForward.x, 0f, HeadForward.z).normalized,
                                      Vector3.right);
            float bowAngle      = Mathf.Max(0f, -Vector3.SignedAngle(
                                      new Vector3(0, HeadForward.y, HeadForward.z == 0 ? 1 : HeadForward.z).normalized,
                                      Vector3.forward, Vector3.right));
            Gestures.HeadBowed  = Mathf.Clamp01(bowAngle / headBowedDegrees);

            // Spinning (yaw angular velocity)
            Gestures.IsSpinning = Mathf.Clamp01(AngularVelocityY / spinningRateThreshold);

            // Hands together
            float handDist      = Vector3.Distance(LeftHandPosition, RightHandPosition);
            Gestures.HandsTogether = Mathf.Clamp01(1f - handDist / handsTogetherDist);

            // Hand on heart (right hand near chest center, relatively still)
            Vector3 heartPos    = new Vector3(HeadPosition.x, _shoulderHeight - 0.05f, HeadPosition.z + 0.15f);
            float heartDist     = Vector3.Distance(RightHandPosition, heartPos);
            float heartStill    = Mathf.Clamp01(1f - RightHandVelocity.magnitude * 2f);
            Gestures.HandOnHeart = Mathf.Clamp01((1f - heartDist / heartZoneRadius) * heartStill);

            // Mirrored flow (hands moving in opposite arcs at similar speed)
            float speedMatch   = 1f - Mathf.Abs(LeftHandVelocity.magnitude - RightHandVelocity.magnitude)
                                     / Mathf.Max(0.01f, LeftHandVelocity.magnitude + RightHandVelocity.magnitude);
            float dirOpposed   = Mathf.Clamp01(-Vector3.Dot(
                                     LeftHandVelocity.normalized, RightHandVelocity.normalized) * 0.5f + 0.5f);
            float inMotion     = Mathf.Clamp01((LeftHandVelocity.magnitude + RightHandVelocity.magnitude) / 2f);
            Gestures.MirroredFlow = Mathf.Clamp01(speedMatch * dirOpposed * inMotion);
        }

        // -------------------------------------------------------------------------
        // Drop-in replacements for Input.GetAxis / Input.GetKey
        // -------------------------------------------------------------------------

        /// <summary>
        /// Drop-in for Input.GetAxis. Recognised names: "Horizontal", "Vertical".
        /// Falls back to Input.GetAxis on non-VR.
        /// </summary>
        public float GetAxis(string name)
        {
            if (!IsVRActive) return Input.GetAxis(name);
            return name switch
            {
                "Horizontal" => FlightHorizontal,
                "Vertical"   => FlightVertical,
                _            => Input.GetAxis(name)
            };
        }

        /// <summary>
        /// Drop-in for Input.GetKey.
        /// Mapped: Space→ascend, LeftShift→dive, F→meditate, Q→gustBurst, R→resonancePulse.
        /// Falls back to Input.GetKey on non-VR.
        /// </summary>
        public bool GetKey(KeyCode key)
        {
            if (!IsVRActive) return Input.GetKey(key);
            return key switch
            {
                KeyCode.Space      => IsAscending,
                KeyCode.LeftShift  => IsDiving,
                KeyCode.F          => IsMeditating,
                KeyCode.Q          => IsGustBurst,
                KeyCode.R          => IsResonancePulse,
                _                  => Input.GetKey(key)
            };
        }

        // -------------------------------------------------------------------------
        // Convenience: is a specific gesture above a threshold?
        // -------------------------------------------------------------------------
        public bool IsGesture(string gestureName, float threshold = 0.6f) => gestureName switch
        {
            "BothHandsRaised"   => Gestures.BothHandsRaised   >= threshold,
            "ArmsOutstretched"  => Gestures.ArmsOutstretched  >= threshold,
            "HeadBowed"         => Gestures.HeadBowed         >= threshold,
            "IsSpinning"        => Gestures.IsSpinning        >= threshold,
            "HandsTogether"     => Gestures.HandsTogether     >= threshold,
            "HandOnHeart"       => Gestures.HandOnHeart       >= threshold,
            "MirroredFlow"      => Gestures.MirroredFlow      >= threshold,
            _                   => false
        };
    }
}
