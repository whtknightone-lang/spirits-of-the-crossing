// SpiritsCrossing — HardwarePhysicalInputReader.cs
// Real-hardware biometric reader for human players.
//
// Sensor sources (each independently optional — falls back to last known value):
//
//   BREATH    — Unity Microphone API. Computes RMS amplitude from mic buffer
//               at ~20 Hz. BreathCycleTracker derives coherence and rate.
//
//   HEART RATE — Two paths, selectable at runtime:
//               [BLE]    IHeartRateSource stub — plug in your BLE plugin here
//                        (e.g. Polar H10 via NativeWebSocket or platform SDK)
//               [Serial] Arduino/serial-port HR sensor via System.IO.Ports
//
//   MOVEMENT  — Three paths, in priority order:
//               [XR]     XR controller velocity + angular velocity
//               [Mobile] Input.acceleration (iOS/Android IMU)
//               [KB/GP]  Keyboard WASD / gamepad left stick as movement proxy
//
// Wiring: Add HardwarePhysicalInputReader to a manager object alongside
//         PhysicalInputBridge. Toggle enableMic / enableHeartRate / enableMotion
//         from the Inspector or at runtime.

using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using SpiritsCrossing.VR;
#if UNITY_STANDALONE || UNITY_EDITOR
using System.IO.Ports;
#endif

namespace SpiritsCrossing.BiometricInput
{
    // =========================================================================
    // Heart rate source abstraction — swap implementations without changing the reader
    // =========================================================================
    public interface IHeartRateSource
    {
        float CurrentBPM { get; }
        float CurrentHRV { get; }   // normalised 0–1
        bool  IsConnected { get; }
        void  Connect();
        void  Disconnect();
    }

    /// <summary>
    /// Stub for BLE heart rate monitors (Polar H10, Apple Watch, etc.).
    /// Replace the body of Connect() / Disconnect() with your BLE plugin calls.
    /// BLE plugins: https://github.com/adrenak/univoice, or platform-native SDK.
    /// </summary>
    public class BLEHeartRateSource : IHeartRateSource
    {
        public float CurrentBPM   { get; private set; } = 70f;
        public float CurrentHRV   { get; private set; } = 0.3f;
        public bool  IsConnected  { get; private set; }

        private float _lastUpdate;

        public void Connect()
        {
            // ---------------------------------------------------------------
            // PLUG IN YOUR BLE PLUGIN HERE
            // Example (pseudo-code):
            //   BLEManager.Instance.ScanForDevice("Polar H10", OnConnected);
            //   BLEManager.Instance.OnHRNotification += OnHRData;
            // ---------------------------------------------------------------
            Debug.Log("[BLEHeartRateSource] BLE stub: not connected. Implement Connect() with your BLE plugin.");
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        /// <summary>Call this from your BLE notification callback with live data.</summary>
        public void OnHRData(float bpm, float rrIntervalMs)
        {
            CurrentBPM   = Mathf.Clamp(bpm, 30f, 220f);
            // HRV from RR interval variability (simplified RMSSD approximation)
            float rmssd  = Mathf.Abs(rrIntervalMs - (60000f / CurrentBPM));
            CurrentHRV   = Mathf.Clamp01(rmssd / 100f);
            IsConnected  = true;
            _lastUpdate  = Time.time;
        }
    }

#if UNITY_STANDALONE || UNITY_EDITOR
    /// <summary>
    /// Serial port heart rate source — for Arduino-based sensors.
    /// Arduino sketch should output "<BPM>\n" at ~1 Hz, e.g.: Serial.println(bpm);
    /// </summary>
    public class SerialHeartRateSource : IHeartRateSource
    {
        public float CurrentBPM  { get; private set; } = 70f;
        public float CurrentHRV  { get; private set; } = 0.3f;
        public bool  IsConnected { get; private set; }

        private SerialPort _port;
        private readonly string _portName;
        private readonly int    _baudRate;

        public SerialHeartRateSource(string portName = "COM3", int baudRate = 9600)
        {
            _portName = portName;
            _baudRate = baudRate;
        }

        public void Connect()
        {
            try
            {
                _port = new SerialPort(_portName, _baudRate) { ReadTimeout = 50 };
                _port.Open();
                IsConnected = true;
                Debug.Log($"[SerialHR] Connected to {_portName} @ {_baudRate} baud.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SerialHR] Could not open {_portName}: {e.Message}");
            }
        }

        public void Disconnect()
        {
            _port?.Close();
            IsConnected = false;
        }

        /// <summary>Poll once per frame from HardwarePhysicalInputReader.Update().</summary>
        public void Poll()
        {
            if (_port == null || !_port.IsOpen) return;
            try
            {
                string line = _port.ReadLine().Trim();
                if (float.TryParse(line, out float bpm))
                    CurrentBPM = Mathf.Clamp(bpm, 30f, 220f);
            }
            catch (TimeoutException) { /* no data this frame */ }
        }
    }
#endif

    // =========================================================================
    // Hardware reader MonoBehaviour
    // =========================================================================
    public class HardwarePhysicalInputReader : MonoBehaviour, IPhysicalInputReader
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Breath — Microphone")]
        public bool   enableMic         = true;
        [Tooltip("Leave empty to use the default system microphone.")]
        public string micDeviceName     = "";
        public int    micSampleRate     = 16000;
        public float  rmsWindowSeconds  = 0.1f;   // RMS window for amplitude
        [Range(0f, 0.1f)] public float stillnessThreshold = 0.02f;

        [Header("Heart Rate")]
        public bool   enableHeartRate   = true;
        public enum   HRSourceType { BLE, Serial }
        public HRSourceType hrSource    = HRSourceType.BLE;
        public string serialPortName    = "COM3";
        public int    serialBaudRate    = 9600;

        [Header("Movement")]
        public bool   enableMotion      = true;
        public enum   MotionSourceType  { XR, Accelerometer, KeyboardGamepad }
        public MotionSourceType motionSource = MotionSourceType.Accelerometer;
        [Tooltip("Max expected acceleration magnitude for normalisation (m/s²).")]
        public float  maxAcceleration   = 20f;

        [Header("Debug")]
        public bool   logSensorStatus   = true;

        // -------------------------------------------------------------------------
        // IPhysicalInputReader
        // -------------------------------------------------------------------------
        public RawBiometricSignals CurrentSignals { get; private set; } = RawBiometricSignals.Default();
        public bool IsConnected { get; private set; }

        public void StartReading()
        {
            IsConnected = true;
            if (enableMic)        StartMic();
            if (enableHeartRate)  StartHR();
            if (enableMotion)     InitMotion();
        }

        public void StopReading()
        {
            StopMic();
            _hrSource?.Disconnect();
            IsConnected = false;
        }

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private AudioClip         _micClip;
        private bool              _micActive;
        private BreathCycleTracker _breathTracker = new BreathCycleTracker();
        private float             _breathAmp;

        private IHeartRateSource  _hrSource;

        private Vector3           _prevAccel;
        private Vector3           _prevXRVelocity;
        private float             _speed;
        private float             _jerk;
        private float             _rotRate;
        private float             _prevRotMag;
        private InputDevice       _xrRightHand;
        private InputDevice       _xrHead;
        private readonly List<InputDevice> _xrDeviceBuffer = new List<InputDevice>();

        private void Start()  => StartReading();
        private void OnDestroy() => StopReading();

        private void Update()
        {
            float breathAmp = _micActive ? SampleMicRMS() : CurrentSignals.breathAmplitude;
            _breathTracker.Feed(breathAmp, Time.time);
            _breathAmp = breathAmp;

#if UNITY_STANDALONE || UNITY_EDITOR
            if (_hrSource is SerialHeartRateSource serial) serial.Poll();
#endif
            UpdateMovement();
            BuildSignals();
        }

        // -------------------------------------------------------------------------
        // Breath — Microphone
        // -------------------------------------------------------------------------
        private void StartMic()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[HardwarePhysicalInputReader] No microphone found.");
                return;
            }
            string device = string.IsNullOrEmpty(micDeviceName) ? null : micDeviceName;
            _micClip   = Microphone.Start(device, true, 1, micSampleRate);
            _micActive = true;
            if (logSensorStatus)
                Debug.Log($"[HardwarePhysicalInputReader] Microphone started: {device ?? "default"}");
        }

        private void StopMic()
        {
            if (_micActive) Microphone.End(micDeviceName);
            _micActive = false;
        }

        private float SampleMicRMS()
        {
            if (_micClip == null) return 0f;
            int windowSamples = Mathf.Max(1, (int)(rmsWindowSeconds * micSampleRate));
            int micPos        = Microphone.GetPosition(null);
            int start         = micPos - windowSamples;
            if (start < 0) return CurrentSignals.breathAmplitude; // not enough data yet

            float[] samples = new float[windowSamples];
            _micClip.GetData(samples, start);

            float rms = 0f;
            foreach (float s in samples) rms += s * s;
            rms = Mathf.Sqrt(rms / windowSamples);

            // Map mic RMS to breath amplitude (rough calibration; tune per environment)
            return Mathf.Clamp01(rms * 12f);
        }

        // -------------------------------------------------------------------------
        // Heart Rate
        // -------------------------------------------------------------------------
        private void StartHR()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            if (hrSource == HRSourceType.Serial)
            {
                var s = new SerialHeartRateSource(serialPortName, serialBaudRate);
                s.Connect();
                _hrSource = s;
                return;
            }
#endif
            var ble = new BLEHeartRateSource();
            ble.Connect();
            _hrSource = ble;
        }

        // -------------------------------------------------------------------------
        // Movement
        // -------------------------------------------------------------------------
        private void InitMotion()
        {
            if (motionSource == MotionSourceType.XR)
                RefreshXRDevices();
        }

        private void RefreshXRDevices()
        {
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
                _xrDeviceBuffer);
            if (_xrDeviceBuffer.Count > 0) _xrRightHand = _xrDeviceBuffer[0];

            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeadMounted,
                _xrDeviceBuffer);
            if (_xrDeviceBuffer.Count > 0) _xrHead = _xrDeviceBuffer[0];
        }

        private void UpdateMovement()
        {
            Vector3 accel    = Vector3.zero;
            float   rotInput = 0f;

            switch (motionSource)
            {
                case MotionSourceType.Accelerometer:
                    accel = Input.acceleration * 9.8f;
                    break;

                case MotionSourceType.XR:
                    // Prefer VRInputAdapter if available (already reads both hands)
                    var vr = VRInputAdapter.Instance;
                    if (vr != null && vr.IsVRActive)
                    {
                        // Use combined hand velocity as the primary motion signal
                        accel    = (vr.LeftHandVelocity + vr.RightHandVelocity) * 0.5f;
                        rotInput = vr.AngularVelocityY;  // head yaw rate for spin
                    }
                    else
                    {
                        // Direct XR device read fallback
                        if (!_xrRightHand.isValid) RefreshXRDevices();
                        if (_xrRightHand.isValid)
                        {
                            _xrRightHand.TryGetFeatureValue(CommonUsages.deviceVelocity,        out Vector3 handVel);
                            _xrRightHand.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out Vector3 handAngVel);
                            accel    = handVel;
                            rotInput = handAngVel.magnitude;
                        }
                        if (_xrHead.isValid)
                        {
                            _xrHead.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out Vector3 headAngVel);
                            rotInput = Mathf.Max(rotInput, Mathf.Abs(headAngVel.y));
                        }
                    }
                    break;

                case MotionSourceType.KeyboardGamepad:
                    float h = Input.GetAxis("Horizontal");
                    float v = Input.GetAxis("Vertical");
                    accel   = new Vector3(h, 0f, v) * maxAcceleration * 0.3f;
                    break;
            }

            float mag  = accel.magnitude;
            float norm = Mathf.Clamp01(mag / Mathf.Max(0.01f, maxAcceleration));

            _jerk      = Mathf.Clamp01(Vector3.Distance(accel, _prevAccel) / Mathf.Max(0.01f, maxAcceleration));
            _speed     = Mathf.Lerp(_speed,   norm,          Time.deltaTime * 5f);
            _rotRate   = Mathf.Lerp(_rotRate, rotInput * 0.5f, Time.deltaTime * 5f);
            _prevAccel = accel;
        }

        // -------------------------------------------------------------------------
        // Build final signal struct
        // -------------------------------------------------------------------------
        private void BuildSignals()
        {
            float bpm = _hrSource != null ? _hrSource.CurrentBPM : 70f;
            float hrv = _hrSource != null ? _hrSource.CurrentHRV : 0.3f;

            CurrentSignals = new RawBiometricSignals
            {
                breathAmplitude      = _breathAmp,
                breathRateHz         = EstimateBreathRate(),
                isBreathHold         = _breathAmp > 0.45f && _breathAmp < 0.65f &&
                                       Mathf.Abs(_breathAmp - CurrentSignals.breathAmplitude) < 0.02f,
                heartRateBPM         = bpm,
                heartRateVariability = hrv,
                acceleration         = _prevAccel,
                movementSpeed        = _speed,
                movementJerk         = _jerk,
                rotationRate         = _rotRate,
                isStill              = _speed < stillnessThreshold,
                pairSyncScore        = 0f,
                sampleTimestamp      = Time.time,
                isConnected          = true,
            };
        }

        private float EstimateBreathRate()
        {
            // Estimated from coherence tracker window:
            // coherence is higher when periods are regular, so we can back-calculate
            // a rough rate from the last crossing time.
            // Simplified: return the profile default or last stable reading.
            return Mathf.Clamp(CurrentSignals.breathRateHz, 0.1f, 0.6f);
        }
    }
}
