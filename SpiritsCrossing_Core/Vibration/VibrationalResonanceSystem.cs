// SpiritsCrossing — VibrationalResonanceSystem.cs
// The continuous organic resonance matching engine.
//
// Every frame this system:
//   1. Builds the player's current 7-band vibrational field from live physiology
//   2. Computes harmony between the player and every registered animal/spirit
//   3. Smoothly tracks harmony over time (configurable lag) — no snapping
//   4. Tracks harmony velocity (rising/falling) so animals can sense momentum
//   5. Broadcasts the player's field so portals, caves, and realms can respond
//
// There are NO rules, NO triggers, NO boolean conditions.
// Every animal responds to a single continuous float: its current harmony with
// the player's vibrational field. That float drives everything — distance,
// speed, animation blend, sound volume, particle density.
//
// Natural thresholds emerge from the physics:
//   > 0.85  "resonance lock" — deep alignment, companion state
//   > 0.65  "open field"    — animal engages, performs
//   > 0.45  "curiosity"     — animal orients, approaches slowly
//   > 0.25  "awareness"     — animal notices, watches
//   < 0.25  "distance"      — animal moves away naturally
//
// These aren't programmed rules — they're the natural consequence of the
// cosine harmony function reaching different regions of its domain.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.SpiritAI;
using SpiritsCrossing.Companions;

namespace SpiritsCrossing.Vibration
{
    public class VibrationalResonanceSystem : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------
        public static VibrationalResonanceSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Field Smoothing")]
        [Tooltip("How quickly the player field tracks their resonance state. " +
                 "Lower = more inertia = more organic feel.")]
        [Range(0.5f, 20f)] public float fieldSmoothSpeed = 3.0f;

        [Tooltip("How quickly per-animal harmony scores smooth. " +
                 "Lower = animals respond more slowly and naturally.")]
        [Range(0.5f, 10f)] public float harmonySmoothSpeed = 2.0f;

        [Header("Natural Thresholds (read-only — emerge from cosine physics)")]
        [Range(0f, 1f)] public float lockThreshold     = 0.85f; // resonance lock
        [Range(0f, 1f)] public float openThreshold     = 0.65f; // animal opens / performs
        [Range(0f, 1f)] public float curiousThreshold  = 0.45f; // animal turns / approaches
        [Range(0f, 1f)] public float awareThreshold    = 0.25f; // animal notices / watches
        // Below awareThreshold = natural distance

        [Header("Debug")]
        public bool showDebugEverySecond;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        /// <summary>Player's dominant vibrational band changed.</summary>
        public event Action<string>        OnPlayerDominantBandChanged;  // bandName

        /// <summary>Animal harmony changed significantly (> 0.02 delta).</summary>
        public event Action<string, float> OnHarmonyChanged;             // animalId, harmony

        /// <summary>Animal crossed into resonance lock (> lockThreshold).</summary>
        public event Action<string>        OnResonanceLock;              // animalId

        /// <summary>Animal dropped below aware threshold (released from all engagement).</summary>
        public event Action<string>        OnResonanceRelease;           // animalId

        // -------------------------------------------------------------------------
        // Public state
        // -------------------------------------------------------------------------

        /// <summary>The player's current live vibrational field.</summary>
        public VibrationalField PlayerField { get; private set; } = new VibrationalField();

        /// <summary>Live smoothed harmony scores per animal. Range 0–1.</summary>
        public IReadOnlyDictionary<string, float> HarmonyScores => _harmony;

        /// <summary>Harmony velocity per animal (positive=rising, negative=falling).</summary>
        public IReadOnlyDictionary<string, float> HarmonyVelocity => _velocity;

        /// <summary>
        /// The animal currently most harmonically aligned with the player.
        /// </summary>
        public string HighestHarmonyAnimal { get; private set; }
        public float  HighestHarmonyScore  { get; private set; }

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private readonly Dictionary<string, VibrationalField> _animalFields  = new();
        private readonly Dictionary<string, float>            _harmony        = new();
        private readonly Dictionary<string, float>            _prevHarmony    = new();
        private readonly Dictionary<string, float>            _velocity       = new();
        private readonly HashSet<string>                      _locked         = new();

        private SpiritBrainOrchestrator _orchestrator;
        private string                  _prevDominantBand;
        private float                   _debugTimer;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return null; // let loaders complete
            _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();

            // Load animal vibrational fields from SpiritProfileLoader
            yield return new WaitUntil(() => SpiritProfileLoader.Instance?.IsLoaded ?? false);
            LoadAnimalFields();
        }

        private void Update()
        {
            UpdatePlayerField();
            UpdateHarmonyScores();
            FireEvents();

            if (showDebugEverySecond)
            {
                _debugTimer += Time.deltaTime;
                if (_debugTimer >= 1f)
                {
                    _debugTimer = 0f;
                    Debug.Log($"[VibrationalResonanceSystem] Player: {PlayerField} " +
                              $"Dominant={PlayerField.DominantBandName()} " +
                              $"TopAnimal={HighestHarmonyAnimal} ({HighestHarmonyScore:F3})");
                }
            }
        }

        // -------------------------------------------------------------------------
        // Load animal fields from spirit profiles
        // -------------------------------------------------------------------------
        private void LoadAnimalFields()
        {
            var loader = SpiritProfileLoader.Instance;
            if (loader == null || !loader.IsLoaded) return;

            foreach (var profile in loader.Profiles.spirits)
            {
                var sig = profile.spectralSignature;
                _animalFields[profile.archetypeId] = new VibrationalField(
                    sig.red, sig.orange, sig.yellow, sig.green, sig.blue, sig.indigo, sig.violet);
            }

            // Also register companion animals from CompanionBondSystem
            if (CompanionBondSystem.Instance != null)
            {
                foreach (var companion in CompanionBondSystem.Instance.AllProfiles)
                {
                    if (_animalFields.ContainsKey(companion.animalId)) continue;
                    // Build from element affinity if no spectral profile
                    _animalFields[companion.animalId] = VibrationalField.NaturalAffinity(companion.element);
                }
            }

            Debug.Log($"[VibrationalResonanceSystem] Loaded {_animalFields.Count} animal fields.");
        }

        // -------------------------------------------------------------------------
        // Build player vibrational field from live resonance state
        // -------------------------------------------------------------------------
        private void UpdatePlayerField()
        {
            if (_orchestrator == null)
                _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();

            var state = _orchestrator?.CurrentPlayerState;
            if (state == null) return;

            var target = VibrationalField.FromResonanceState(state);
            PlayerField.LerpToward(target, Time.deltaTime * fieldSmoothSpeed);
        }

        // -------------------------------------------------------------------------
        // Compute harmony scores for all registered animals
        // -------------------------------------------------------------------------
        private void UpdateHarmonyScores()
        {
            float bestScore    = 0f;
            string bestAnimal  = null;

            foreach (var kvp in _animalFields)
            {
                string id          = kvp.Key;
                VibrationalField af = kvp.Value;

                // Weighted harmony: animal's characteristic bands weighted more heavily
                float rawHarmony = PlayerField.WeightedHarmony(af);

                // Smooth toward raw (natural inertia)
                if (!_harmony.TryGetValue(id, out float prev)) prev = 0.5f;
                float smoothed = Mathf.Lerp(prev, rawHarmony, Time.deltaTime * harmonySmoothSpeed);
                _prevHarmony[id] = prev;
                _harmony[id]     = smoothed;
                _velocity[id]    = (smoothed - prev) / Mathf.Max(0.001f, Time.deltaTime);

                if (smoothed > bestScore) { bestScore = smoothed; bestAnimal = id; }
            }

            HighestHarmonyAnimal = bestAnimal;
            HighestHarmonyScore  = bestScore;
        }

        // -------------------------------------------------------------------------
        // Fire events based on harmony transitions
        // -------------------------------------------------------------------------
        private void FireEvents()
        {
            // Player dominant band change
            string dominant = PlayerField.DominantBandName();
            if (dominant != _prevDominantBand)
            {
                _prevDominantBand = dominant;
                OnPlayerDominantBandChanged?.Invoke(dominant);
            }

            // Per-animal events
            foreach (var kvp in _harmony)
            {
                string id     = kvp.Key;
                float  h      = kvp.Value;
                float  prev   = _prevHarmony.TryGetValue(id, out float p) ? p : 0.5f;

                // Significant change event
                if (Mathf.Abs(h - prev) > 0.02f)
                    OnHarmonyChanged?.Invoke(id, h);

                // Resonance lock
                bool isLocked = _locked.Contains(id);
                if (!isLocked && h >= lockThreshold)
                {
                    _locked.Add(id);
                    OnResonanceLock?.Invoke(id);
                }
                else if (isLocked && h < lockThreshold - 0.05f) // hysteresis
                {
                    _locked.Remove(id);
                }

                // Release
                if (prev >= awareThreshold && h < awareThreshold)
                    OnResonanceRelease?.Invoke(id);
            }
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Get the smoothed harmony score for a specific animal (0–1).</summary>
        public float GetHarmony(string animalId)
            => _harmony.TryGetValue(animalId, out float h) ? h : 0.5f;

        /// <summary>Get the harmony velocity for a specific animal (positive=rising).</summary>
        public float GetHarmonyVelocity(string animalId)
            => _velocity.TryGetValue(animalId, out float v) ? v : 0f;

        /// <summary>Is this animal currently in resonance lock with the player?</summary>
        public bool IsLocked(string animalId) => _locked.Contains(animalId);

        /// <summary>
        /// Get natural engagement distance for an animal based on current harmony.
        /// range: (maxDist, minDist) — no discrete tiers, fully continuous.
        /// </summary>
        public float NaturalDistance(string animalId, float minDist, float maxDist)
            => Mathf.Lerp(maxDist, minDist, GetHarmony(animalId));

        /// <summary>
        /// Get natural movement speed for an animal based on harmony.
        /// Animals move faster when harmony is high (drawn in), slower when distant.
        /// </summary>
        public float NaturalSpeed(string animalId, float maxSpeed)
            => GetHarmony(animalId) * maxSpeed;

        /// <summary>
        /// Register an animal field manually (e.g. for a unique NPC not in the profiles).
        /// </summary>
        public void RegisterAnimalField(string animalId, VibrationalField field)
        {
            _animalFields[animalId] = field;
        }

        /// <summary>
        /// Register using element affinity (for animals without a spectral profile).
        /// </summary>
        public void RegisterByElement(string animalId, string element)
        {
            _animalFields[animalId] = VibrationalField.NaturalAffinity(element);
        }

        /// <summary>
        /// Return all animals whose current harmony is above a given threshold.
        /// Sorted by harmony descending.
        /// </summary>
        public List<(string animalId, float harmony)> GetAnimalsAboveHarmony(float threshold)
        {
            var result = new List<(string, float)>();
            foreach (var kvp in _harmony)
                if (kvp.Value >= threshold) result.Add((kvp.Key, kvp.Value));
            result.Sort((a, b) => b.harmony.CompareTo(a.harmony));
            return result;
        }
    }
}
