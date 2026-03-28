// SpiritsCrossing — SourceCommunionSystem.cs
// Active only during the InSource lifecycle phase.
// Narrows the vibrational resonance field to the 4 Elder Dragons only.
// Communion depth accumulates when the player's field harmonises with a dragon.
// The deepest communion selects the primary dragon for rebirth gifts.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Companions;
using SpiritsCrossing.Memory;

namespace SpiritsCrossing.Lifecycle
{
    public class SourceCommunionSystem : MonoBehaviour
    {
        public static SourceCommunionSystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Communion Tuning")]
        [Tooltip("Harmony score above which communion depth accumulates.")]
        [Range(0f, 1f)] public float communionHarmonyThreshold = 0.65f;
        [Tooltip("Rate at which depth accumulates per second above threshold.")]
        public float communionAccumRate = 0.008f;
        [Tooltip("Minimum seconds in Source before rebirth is allowed.")]
        public float minSourceTime = 30f;

        [Header("Debug")]
        public bool logCommunionProgress;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        /// <summary>Fired at depth milestones 0.25, 0.50, 0.75 — used by haptics.</summary>
        public event Action<string, float> OnCommunionDepthMilestone; // dragonElement, depth
        /// <summary>Fired when the primary dragon changes.</summary>
        public event Action<string>        OnPrimaryDragonChanged;    // dragonElement

        // -------------------------------------------------------------------------
        // Public state
        // -------------------------------------------------------------------------
        public bool   IsActive         => LifecycleSystem.Instance?.IsInSource ?? false;
        public bool   ReadyToRebirth   => IsActive && _timeInSource >= minSourceTime && MaxDepth >= 0.25f;
        public float  MaxDepth         { get; private set; }
        public string PrimaryDragonElement { get; private set; }

        public IReadOnlyDictionary<string, float> CommunionDepths => _depth;

        // -------------------------------------------------------------------------
        // Elder Dragon archetype → element mapping
        // -------------------------------------------------------------------------
        private static readonly Dictionary<string, string> DRAGON_ELEMENTS = new()
        {
            ["EarthDragon"]   = "Earth",
            ["FireDragon"]    = "Fire",
            ["WaterDragon"]   = "Water",
            ["ElderAirDragon"]= "Air",
        };

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private readonly Dictionary<string, VibrationalField> _dragonFields = new();
        private readonly Dictionary<string, float>            _depth        = new();
        private readonly float[]                              _milestones   = { 0.25f, 0.50f, 0.75f };
        private readonly Dictionary<string, int>              _milestoneIdx = new();

        private float  _timeInSource;
        private string _prevPrimaryDragon;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return new WaitUntil(() => SpiritProfileLoader.Instance?.IsLoaded ?? false);
            LoadDragonFields();

            // Subscribe to lifecycle events
            if (LifecycleSystem.Instance != null)
                LifecycleSystem.Instance.OnPhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (LifecycleSystem.Instance != null)
                LifecycleSystem.Instance.OnPhaseChanged -= OnPhaseChanged;
        }

        private void Update()
        {
            if (!IsActive) return;
            _timeInSource += Time.deltaTime;
            UpdateCommunion();
        }

        // -------------------------------------------------------------------------
        // Load Elder Dragon vibrational fields from spirit profiles
        // -------------------------------------------------------------------------
        private void LoadDragonFields()
        {
            var loader = SpiritProfileLoader.Instance;
            if (loader == null) return;

            foreach (var kvp in DRAGON_ELEMENTS)
            {
                var profile = loader.GetProfile(kvp.Key);
                if (profile == null)
                {
                    _dragonFields[kvp.Key] = VibrationalField.NaturalAffinity(kvp.Value);
                    continue;
                }
                var sig = profile.spectralSignature;
                _dragonFields[kvp.Key] = new VibrationalField(
                    sig.red, sig.orange, sig.yellow, sig.green, sig.blue, sig.indigo, sig.violet);
            }

            // Initialise depth and milestone tracking
            foreach (var id in DRAGON_ELEMENTS.Keys)
            {
                _depth[id]        = 0f;
                _milestoneIdx[id] = 0;
            }

            Debug.Log($"[SourceCommunionSystem] Loaded {_dragonFields.Count} Elder Dragon fields.");
        }

        // -------------------------------------------------------------------------
        // Communion accumulation each frame
        // -------------------------------------------------------------------------
        private void UpdateCommunion()
        {
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return;

            float bestDepth  = 0f;
            string bestDragon = null;

            foreach (var kvp in _dragonFields)
            {
                string id   = kvp.Key;
                float  harm = playerField.WeightedHarmony(kvp.Value);

                if (harm >= communionHarmonyThreshold)
                {
                    float gain = communionAccumRate * (harm - communionHarmonyThreshold) /
                                 (1f - communionHarmonyThreshold) * Time.deltaTime;
                    _depth[id] = Mathf.Clamp01(_depth[id] + gain);
                }

                // Check milestones
                int idx = _milestoneIdx.TryGetValue(id, out int m) ? m : 0;
                while (idx < _milestones.Length && _depth[id] >= _milestones[idx])
                {
                    OnCommunionDepthMilestone?.Invoke(DRAGON_ELEMENTS[id], _depth[id]);
                    if (logCommunionProgress)
                        Debug.Log($"[SourceCommunionSystem] {id} milestone: {_milestones[idx]:F2}");
                    idx++;
                }
                _milestoneIdx[id] = idx;

                if (_depth[id] > bestDepth) { bestDepth = _depth[id]; bestDragon = id; }
            }

            MaxDepth = bestDepth;

            if (bestDragon != _prevPrimaryDragon)
            {
                _prevPrimaryDragon  = bestDragon;
                PrimaryDragonElement = bestDragon != null ? DRAGON_ELEMENTS[bestDragon] : null;
                if (PrimaryDragonElement != null)
                    OnPrimaryDragonChanged?.Invoke(PrimaryDragonElement);
            }
        }

        // -------------------------------------------------------------------------
        // Phase change — reset when entering Source
        // -------------------------------------------------------------------------
        private void OnPhaseChanged(PlayerCyclePhase phase)
        {
            if (phase == PlayerCyclePhase.InSource)
            {
                _timeInSource = 0f;
                foreach (var id in DRAGON_ELEMENTS.Keys)
                {
                    _depth[id]        = 0f;
                    _milestoneIdx[id] = 0;
                }
                MaxDepth            = 0f;
                PrimaryDragonElement = null;
                _prevPrimaryDragon  = null;
                Debug.Log("[SourceCommunionSystem] Communion reset for new Source entry.");
            }
        }

        // -------------------------------------------------------------------------
        // Trigger rebirth — called by UI or gesture
        // -------------------------------------------------------------------------
        public void TriggerRebirth()
        {
            if (!ReadyToRebirth)
            {
                Debug.Log($"[SourceCommunionSystem] Not ready (depth={MaxDepth:F2} time={_timeInSource:F0}s).");
                return;
            }

            // Find deepened companion from primary dragon's element
            string element      = PrimaryDragonElement ?? "Air";
            string dragonId     = _prevPrimaryDragon;
            string companionId  = FindBestCompanionForElement(element);

            // Build ancient memory field from the primary dragon
            VibrationalField dragonField = dragonId != null && _dragonFields.TryGetValue(dragonId, out var df)
                ? df : VibrationalField.NaturalAffinity(element);

            LifecycleSystem.Instance?.InitiateRebirth(
                element, MaxDepth, companionId, dragonField);
        }

        private string FindBestCompanionForElement(string element)
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return null;

            var loader = CompanionBondSystem.Instance;
            if (loader == null) return null;

            string best = null; float bestLevel = 0f;
            foreach (var profile in loader.AllProfiles)
            {
                if (profile.element != element) continue;
                var bond = universe.GetOrCreateBond(profile.animalId);
                if (bond.bondLevel > bestLevel) { bestLevel = bond.bondLevel; best = profile.animalId; }
            }
            return best;
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------
        public float GetDepth(string dragonArchetypeId)
            => _depth.TryGetValue(dragonArchetypeId, out float d) ? d : 0f;

        public float GetDepthForElement(string element)
        {
            foreach (var kvp in DRAGON_ELEMENTS)
                if (kvp.Value == element && _depth.TryGetValue(kvp.Key, out float d))
                    return d;
            return 0f;
        }
    }
}
