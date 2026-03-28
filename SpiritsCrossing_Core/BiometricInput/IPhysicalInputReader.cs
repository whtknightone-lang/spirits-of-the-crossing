// SpiritsCrossing — IPhysicalInputReader.cs + SimulatedPhysicalInputReader.cs
// IPhysicalInputReader: contract for all sensor sources (hardware or simulated).
// SimulatedPhysicalInputReader: archetype-driven AI player simulation loaded from
// biometric_profiles.json. Used for testing, NPC bodies, and offline development.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SpiritsCrossing.BiometricInput
{
    // =========================================================================
    // Interface
    // =========================================================================
    public interface IPhysicalInputReader
    {
        /// <summary>Latest raw sensor signals.</summary>
        RawBiometricSignals CurrentSignals { get; }

        /// <summary>True when at least one active sensor is returning data.</summary>
        bool IsConnected { get; }

        void StartReading();
        void StopReading();
    }

    // =========================================================================
    // Simulated reader — archetype-driven, loaded from biometric_profiles.json
    // =========================================================================
    [Serializable]
    internal class BiometricProfileEntry
    {
        public string archetypeId;

        // Flat signal parameters (extracted from JSON)
        public float breath_rate_hz      = 0.25f;
        public float breath_amplitude    = 0.70f;
        public float breath_regularity   = 0.80f;
        public float hr_bpm_mean         = 72f;
        public float hr_bpm_std          = 4f;
        public float movement_speed_mean = 0.3f;
        public float movement_jerk_mean  = 0.1f;
        public float rotation_rate_mean  = 0.15f;
        public float breath_hold_prob    = 0.03f;
    }

    public class SimulatedPhysicalInputReader : MonoBehaviour, IPhysicalInputReader
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Archetype")]
        [Tooltip("Matches an archetypeId in biometric_profiles.json.")]
        public string archetypeId = "Seated";

        [Header("Config")]
        public string biometricProfilesFile = "biometric_profiles.json";

        [Tooltip("Update rate in seconds (0 = every frame).")]
        public float updateInterval = 0.05f;

        [Tooltip("If set, the spirit brain's current mode modulates signal intensity.")]
        public SpiritAI.SpiritBrainController linkedBrain;

        // -------------------------------------------------------------------------
        // IPhysicalInputReader
        // -------------------------------------------------------------------------
        public RawBiometricSignals CurrentSignals { get; private set; } = RawBiometricSignals.Default();
        public bool IsConnected { get; private set; }

        public void StartReading()
        {
            IsConnected = true;
            _reading    = true;
        }

        public void StopReading()
        {
            _reading    = false;
            IsConnected = false;
        }

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private BiometricProfileEntry _profile;
        private BreathCycleTracker    _breathTracker = new BreathCycleTracker();
        private bool                  _reading;
        private float                 _timer;
        private float                 _phase;        // breath phase accumulator
        private float                 _phaseNoise;   // accumulated irregularity
        private float                 _hrCurrent;
        private float                 _holdTimer;
        private bool                  _inHold;
        private static System.Random  _rng = new System.Random(42);

        private void Awake()
        {
            StartCoroutine(LoadProfile());
        }

        private void OnEnable()  => StartReading();
        private void OnDisable() => StopReading();

        private IEnumerator LoadProfile()
        {
            yield return null;
            string path = Path.Combine(Application.streamingAssetsPath, biometricProfilesFile);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SimulatedPhysicalInputReader] {biometricProfilesFile} not found. " +
                                 "Using default profile. Copy biometric_profiles.json to StreamingAssets/.");
                _profile = DefaultProfile();
            }
            else
            {
                // biometric_profiles.json is a top-level dict by archetypeId.
                // We extract the sub-object for our archetype.
                string json     = File.ReadAllText(path);
                string subJson  = ExtractSubObject(json, archetypeId);
                if (subJson != null)
                {
                    // Flatten signalParameters sub-object into our entry class
                    string paramsJson = ExtractSubObject(subJson, "signalParameters");
                    _profile = paramsJson != null
                        ? JsonUtility.FromJson<BiometricProfileEntry>(paramsJson)
                        : DefaultProfile();
                    _profile.archetypeId = archetypeId;
                }
                else
                {
                    Debug.LogWarning($"[SimulatedPhysicalInputReader] Archetype '{archetypeId}' not found.");
                    _profile = DefaultProfile();
                }
            }

            _hrCurrent = _profile.hr_bpm_mean;
            IsConnected = true;
            Debug.Log($"[SimulatedPhysicalInputReader] Loaded profile for '{archetypeId}'. " +
                      $"BreathHz={_profile.breath_rate_hz:F2} HR={_profile.hr_bpm_mean:F0}");
        }

        private void Update()
        {
            if (!_reading || _profile == null) return;
            _timer += Time.deltaTime;
            if (updateInterval > 0f && _timer < updateInterval) return;
            float dt = _timer;
            _timer = 0f;
            Tick(dt);
        }

        private void Tick(float dt)
        {
            // --- Breath ---
            float noiseLevel  = (1f - _profile.breath_regularity) * 0.15f;
            _phaseNoise      += ((float)(_rng.NextDouble() * 2 - 1)) * noiseLevel * dt;
            _phase           += _profile.breath_rate_hz * Mathf.PI * 2f * dt;
            float rawBreath   = Mathf.Sin(_phase + _phaseNoise);
            float breathAmp   = Mathf.Clamp01((rawBreath + 1f) * 0.5f) * _profile.breath_amplitude;

            // Breath hold injection
            bool isHold = false;
            if (!_inHold && (float)_rng.NextDouble() < _profile.breath_hold_prob * dt)
            {
                _inHold    = true;
                _holdTimer = 2f + (float)_rng.NextDouble() * 2f;
            }
            if (_inHold)
            {
                _holdTimer -= dt;
                isHold      = true;
                breathAmp   = Mathf.Lerp(breathAmp, 0.6f, 0.3f); // flatten
                if (_holdTimer <= 0f) _inHold = false;
            }

            _breathTracker.Feed(breathAmp, Time.time);

            // --- Heart rate (AR1) ---
            float hrNoise = ((float)(_rng.NextDouble() * 2 - 1)) * _profile.hr_bpm_std * 0.3f;
            _hrCurrent    = 0.97f * _hrCurrent + 0.03f * _profile.hr_bpm_mean + hrNoise;
            _hrCurrent    = Mathf.Clamp(_hrCurrent, 40f, 200f);

            // HRV: rolling std approximation
            float hrv = Mathf.Clamp01(_profile.hr_bpm_std / 20f);

            // --- Movement ---
            // Modulate by linked brain's current drive mode
            float speedMult = 1f, jerkMult = 1f, rotMult = 1f;
            if (linkedBrain != null)
            {
                speedMult = linkedBrain.CurrentMode switch
                {
                    SpiritAI.SpiritDriveMode.Explore => 1.4f,
                    SpiritAI.SpiritDriveMode.Attack  => 1.3f,
                    SpiritAI.SpiritDriveMode.Flee    => 1.5f,
                    SpiritAI.SpiritDriveMode.Rest    => 0.3f,
                    SpiritAI.SpiritDriveMode.Signal  => 0.7f,
                    _                                => 1.0f
                };
                jerkMult = linkedBrain.CurrentMode switch
                {
                    SpiritAI.SpiritDriveMode.Explore => 1.6f,
                    SpiritAI.SpiritDriveMode.Attack  => 1.8f,
                    SpiritAI.SpiritDriveMode.Rest    => 0.2f,
                    _                                => 1.0f
                };
            }

            float speed = Mathf.Clamp01(_profile.movement_speed_mean * speedMult +
                          ((float)(_rng.NextDouble() * 2 - 1)) * 0.05f);
            float jerk  = Mathf.Clamp01(_profile.movement_jerk_mean  * jerkMult  +
                          (float)_rng.NextDouble() * 0.05f);
            float rot   = Mathf.Clamp01(_profile.rotation_rate_mean  * rotMult   +
                          (float)_rng.NextDouble() * 0.05f);

            CurrentSignals = new RawBiometricSignals
            {
                breathAmplitude      = breathAmp,
                breathRateHz         = _profile.breath_rate_hz,
                isBreathHold         = isHold,
                heartRateBPM         = _hrCurrent,
                heartRateVariability = hrv,
                acceleration         = new Vector3(
                    ((float)(_rng.NextDouble() * 2 - 1)) * speed * 9.8f,
                    ((float)(_rng.NextDouble() * 2 - 1)) * speed * 3f,
                    ((float)(_rng.NextDouble() * 2 - 1)) * speed * 4f),
                movementSpeed  = speed,
                movementJerk   = jerk,
                rotationRate   = rot,
                isStill        = speed < 0.08f,
                pairSyncScore  = 0f, // populated externally for paired players
                sampleTimestamp = Time.time,
                isConnected    = true,
            };
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------
        private static BiometricProfileEntry DefaultProfile() => new BiometricProfileEntry
        {
            archetypeId          = "Default",
            breath_rate_hz       = 0.25f,
            breath_amplitude     = 0.70f,
            breath_regularity    = 0.80f,
            hr_bpm_mean          = 72f,
            hr_bpm_std           = 4f,
            movement_speed_mean  = 0.3f,
            movement_jerk_mean   = 0.1f,
            rotation_rate_mean   = 0.15f,
            breath_hold_prob     = 0.03f,
        };

        // Minimal JSON sub-object extractor (avoids Newtonsoft dependency)
        private static string ExtractSubObject(string json, string key)
        {
            string marker = $"\"{key}\"";
            int keyIdx = json.IndexOf(marker, StringComparison.Ordinal);
            if (keyIdx < 0) return null;
            int objStart = json.IndexOf('{', keyIdx + marker.Length);
            if (objStart < 0) return null;
            int depth = 0, objEnd = objStart;
            for (int j = objStart; j < json.Length; j++)
            {
                if (json[j] == '{') depth++;
                else if (json[j] == '}') { depth--; if (depth == 0) { objEnd = j; break; } }
            }
            return json.Substring(objStart, objEnd - objStart + 1);
        }
    }
}
