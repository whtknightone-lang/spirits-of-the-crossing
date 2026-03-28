// SpiritsCrossing — CompanionBondSystem.cs
// Manages bonds between the player and all 26 elemental animal companions.
//
// Each frame:
//   1. Reads current PlayerResonanceState from SpiritBrainOrchestrator
//   2. Scores every companion via weighted resonance dot product
//   3. Grows bond for companions whose score exceeds bondThreshold
//   4. Fires events on bond tier changes
//   5. Reinforces MythState when companions are at Bonded/Companion tier
//
// Persistence: bond states live in UniverseState.companions (auto-saved)
//
// Setup: Add to Bootstrap scene alongside GameBootstrapper.
//        Loads companion_registry.json from StreamingAssets automatically.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SpiritsCrossing.SpiritAI;

namespace SpiritsCrossing.Companions
{
    public class CompanionBondSystem : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------
        public static CompanionBondSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Config")]
        public string registryFileName    = "companion_registry.json";

        [Tooltip("Bond grow rate multiplier. 1.0 = default pacing.")]
        [Range(0.1f, 5f)] public float bondGrowthRate  = 1.0f;

        [Tooltip("Bond decay per second when score is below threshold.")]
        [Range(0f, 0.01f)] public float bondDecayRate  = 0.0005f;

        [Tooltip("How often (seconds) the bond scoring loop runs.")]
        public float updateInterval = 0.5f;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<string, CompanionBondTier> OnBondTierChanged;   // animalId, newTier
        public event Action<string>                    OnCompanionFullyBonded; // animalId
        public event Action<string>                    OnActiveCompanionChanged; // animalId (or "")

        // -------------------------------------------------------------------------
        // Public registry access
        // -------------------------------------------------------------------------
        public bool IsLoaded { get; private set; }
        public IReadOnlyList<CompanionProfile> AllProfiles => _profiles;

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private List<CompanionProfile>           _profiles   = new List<CompanionProfile>();
        private Dictionary<string, CompanionProfile> _index  = new Dictionary<string, CompanionProfile>();
        private Dictionary<string, CompanionBondTier> _prevTiers = new Dictionary<string, CompanionBondTier>();
        private SpiritBrainOrchestrator          _orchestrator;
        private float                            _timer;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return LoadRegistry();
            _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();
        }

        private void Update()
        {
            if (!IsLoaded) return;
            _timer += Time.deltaTime;
            if (_timer < updateInterval) return;
            _timer = 0f;

            var state = GetPlayerState();
            if (state != null) TickBonds(state);
        }

        // -------------------------------------------------------------------------
        // Registry loading
        // -------------------------------------------------------------------------
        private IEnumerator LoadRegistry()
        {
            yield return null;
            string path = Path.Combine(Application.streamingAssetsPath, registryFileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CompanionBondSystem] {registryFileName} not found. " +
                                 "Copy companion_registry.json to Assets/StreamingAssets/.");
                yield break;
            }

            try
            {
                string json = File.ReadAllText(path);
                var registry = JsonUtility.FromJson<CompanionRegistry>(json);
                if (registry == null) { Debug.LogError("[CompanionBondSystem] Failed to parse registry."); yield break; }

                _profiles = registry.companions;
                _index.Clear();
                foreach (var p in _profiles)
                {
                    _index[p.animalId] = p;
                    _prevTiers[p.animalId] = CompanionBondTier.Distant;
                }
                IsLoaded = true;
                Debug.Log($"[CompanionBondSystem] Loaded {_profiles.Count} companion profiles.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CompanionBondSystem] Registry load error: {e.Message}");
            }
        }

        // -------------------------------------------------------------------------
        // Bond scoring and growth
        // -------------------------------------------------------------------------
        private void TickBonds(PlayerResonanceState playerState)
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            var myth = universe.mythState;

            foreach (var profile in _profiles)
            {
                float score = profile.resonanceWeights.Score(playerState);
                var   bond  = universe.GetOrCreateBond(profile.animalId);

                if (score >= profile.bondThreshold)
                {
                    // Grow bond — slower for higher tier companions
                    float tierMultiplier = 1f / profile.tier;
                    float growth = score * bondGrowthRate * tierMultiplier * updateInterval * 0.015f;
                    bond.bondLevel = Mathf.Clamp01(bond.bondLevel + growth);
                    bond.lastSeenUtc = DateTime.UtcNow.ToString("o");
                }
                else
                {
                    // Decay slowly when player resonance doesn't match
                    bond.bondLevel = Mathf.Max(0f, bond.bondLevel - bondDecayRate * updateInterval);
                }

                // Check tier transitions
                var newTier = profile.TierForBondLevel(bond.bondLevel);
                if (!_prevTiers.TryGetValue(profile.animalId, out var prevTier) || newTier != prevTier)
                {
                    _prevTiers[profile.animalId] = newTier;
                    OnBondTierChanged?.Invoke(profile.animalId, newTier);

                    if (newTier == CompanionBondTier.Companion)
                    {
                        OnCompanionFullyBonded?.Invoke(profile.animalId);
                        Debug.Log($"[CompanionBondSystem] Full bond: {profile.displayName} ({profile.element})");
                    }

                    // Reinforce myth when bond reaches Bonded tier
                    if (newTier >= CompanionBondTier.Bonded && !string.IsNullOrEmpty(profile.mythTrigger))
                        myth.Activate(profile.mythTrigger, "companion",
                                      Mathf.Clamp01(bond.bondLevel * 0.8f));
                }

                // Continuous myth pulse at Companion tier
                if (newTier == CompanionBondTier.Companion && !string.IsNullOrEmpty(profile.mythTrigger))
                    myth.Activate(profile.mythTrigger, "companion", bond.bondLevel * 0.5f);
            }
        }

        // -------------------------------------------------------------------------
        // Player state source (prefers SpiritBrainOrchestrator, falls back to default)
        // -------------------------------------------------------------------------
        private PlayerResonanceState GetPlayerState()
        {
            if (_orchestrator == null)
                _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();

            return _orchestrator?.CurrentPlayerState;
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        public CompanionProfile GetProfile(string animalId)
        {
            _index.TryGetValue(animalId, out var p);
            return p;
        }

        public float GetBondLevel(string animalId)
        {
            return UniverseStateManager.Instance?.Current.GetOrCreateBond(animalId)?.bondLevel ?? 0f;
        }

        public CompanionBondTier GetBondTier(string animalId)
        {
            var bond    = UniverseStateManager.Instance?.Current.GetOrCreateBond(animalId);
            var profile = GetProfile(animalId);
            if (bond == null || profile == null) return CompanionBondTier.Distant;
            return profile.TierForBondLevel(bond.bondLevel);
        }

        /// <summary>Score the current player resonance against a specific companion.</summary>
        public float ScoreForCompanion(string animalId)
        {
            var profile = GetProfile(animalId);
            var state   = GetPlayerState();
            if (profile == null || state == null) return 0f;
            return profile.resonanceWeights.Score(state);
        }

        /// <summary>
        /// Return companions that match the player's current resonance above threshold,
        /// ordered by score descending. Used by UI and spawn systems.
        /// </summary>
        public List<(CompanionProfile profile, float score)> GetNearbyCompanions(float minScore = 0f)
        {
            var state  = GetPlayerState();
            var result = new List<(CompanionProfile, float)>();
            if (state == null) return result;

            foreach (var p in _profiles)
            {
                float score = p.resonanceWeights.Score(state);
                if (score >= Mathf.Max(minScore, p.bondThreshold * 0.5f))
                    result.Add((p, score));
            }
            result.Sort((a, b) => b.score.CompareTo(a.score));
            return result;
        }

        /// <summary>
        /// Get default companions for an NPC archetype (used by CompanionBehaviorController).
        /// </summary>
        public List<string> GetNpcDefaultCompanions(string archetypeId)
        {
            // Load from registry file (cached)
            string path = Path.Combine(Application.streamingAssetsPath, registryFileName);
            if (!File.Exists(path)) return new List<string>();
            try
            {
                string json     = File.ReadAllText(path);
                var    registry = JsonUtility.FromJson<CompanionRegistry>(json);
                foreach (var e in registry.npcDefaultCompanions)
                    if (e.archetypeId == archetypeId) return e.animalIds;
            }
            catch { /* ignore */ }
            return new List<string>();
        }

        /// <summary>Manually set a companion as the active one accompanying the player.</summary>
        public void SetActiveCompanion(string animalId)
        {
            if (UniverseStateManager.Instance == null) return;
            UniverseStateManager.Instance.Current.SetActiveCompanion(animalId);
            UniverseStateManager.Instance.Save();
            OnActiveCompanionChanged?.Invoke(animalId);
            Debug.Log($"[CompanionBondSystem] Active companion: {GetProfile(animalId)?.displayName ?? animalId}");
        }

        /// <summary>Get the highest-bonded companion across all elements.</summary>
        public CompanionProfile GetStrongestBond()
        {
            CompanionProfile best      = null;
            float            bestLevel = 0f;
            foreach (var p in _profiles)
            {
                float level = GetBondLevel(p.animalId);
                if (level > bestLevel) { bestLevel = level; best = p; }
            }
            return best;
        }
    }
}
