// SpiritsCrossing — TerrainResonanceSystem.cs
// The living terrain resonance engine. Applies to every world in the cosmos.
//
// WHAT IT DOES
//
//   Every second, this system scans the player's proximity to terrain regions
//   on the current planet. For each region within influence range:
//
//     1. Computes PULL — how much the player's current vibrational field
//        harmonizes with the terrain's signature. Higher pull = the landscape
//        feels alive, inviting, resonant. Used by UI (compass, glow, ambient sound).
//
//     2. Applies INTRODUCTION BANDS — when inside a terrain region, secondary
//        resonance bands are subtly amplified. The terrain teaches the player
//        new bands through the safety of bands they already carry.
//
//     3. Fires DISCOVERY EVENTS — first time a player resonates with a terrain
//        region above threshold, it's "discovered" and myth is activated.
//
//     4. Computes NPC TERRAIN AFFINITY — for each NPC in the autonomous system,
//        determines which terrain regions they're naturally drawn to based on
//        their spectral drift. Used to place NPCs in meaningful locations.
//
// PROGRESSION
//
//   Time spent in harmonious terrain builds resonance naturally.
//   Time spent in terrain that introduces NEW bands builds growth faster.
//   The system doesn't force movement — it creates natural pull through physics.
//   A player who follows the pull finds themselves introduced to the full
//   spectrum of the cosmos, one terrain at a time.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Autonomous;

namespace SpiritsCrossing.World
{
    public class TerrainResonanceSystem : MonoBehaviour
    {
        public static TerrainResonanceSystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // ---------------------------------------------------------------------
        // Inspector
        // ---------------------------------------------------------------------
        [Header("Scan")]
        public float scanInterval = 1.0f;

        [Header("Introduction")]
        [Tooltip("Multiplier on introduction band boost amount. Lower = more subtle teaching.")]
        [Range(0.1f, 3f)] public float introductionStrength = 1.0f;

        [Tooltip("Minimum pull score for introduction bands to activate. " +
                 "The player must somewhat resonate with the terrain before it teaches.")]
        [Range(0f, 0.5f)] public float introductionMinPull = 0.25f;

        [Header("Discovery")]
        [Tooltip("Pull score threshold for terrain discovery.")]
        [Range(0.2f, 0.8f)] public float discoveryThreshold = 0.40f;

        [Header("Ambient")]
        [Tooltip("How much terrain field blends into the world ambient field.")]
        [Range(0f, 0.5f)] public float terrainAmbientBlend = 0.15f;

        [Header("Debug")]
        public bool logDiscoveries      = true;
        public bool logIntroductions    = false;
        public bool logPullScoresEveryN = false;

        // ---------------------------------------------------------------------
        // Events
        // ---------------------------------------------------------------------
        public event Action<TerrainRegion, float>          OnTerrainDiscovered;       // region, pullScore
        public event Action<TerrainRegion, float>          OnTerrainPullChanged;      // region, newPull
        public event Action<TerrainType, string, float>    OnIntroductionBandApplied; // type, bandName, amount
        public event Action<TerrainRegion>                 OnPlayerEnteredRegion;
        public event Action<TerrainRegion>                 OnPlayerExitedRegion;

        // ---------------------------------------------------------------------
        // Public state
        // ---------------------------------------------------------------------

        /// <summary>All terrain regions on the current planet.</summary>
        public IReadOnlyList<TerrainRegion> ActiveRegions => _activeRegions;

        /// <summary>The terrain region the player is currently inside (highest pull), or null.</summary>
        public TerrainRegion CurrentPlayerRegion { get; private set; }

        /// <summary>Pull scores for all active regions, computed last scan.</summary>
        public IReadOnlyList<TerrainPullResult> PullResults => _pullResults;

        /// <summary>The terrain type the player is most drawn to right now.</summary>
        public TerrainType? StrongestPull { get; private set; }

        // ---------------------------------------------------------------------
        // Internal
        // ---------------------------------------------------------------------
        private readonly List<TerrainRegion>     _activeRegions  = new();
        private readonly List<TerrainPullResult> _pullResults    = new();
        private readonly HashSet<string>         _discoveredIds  = new();
        private readonly Dictionary<string, VibrationalField> _cachedFields = new();

        private Transform _playerTransform;
        private float     _scanTimer;
        private string    _prevRegionId;

        // ---------------------------------------------------------------------
        // Planet entry/exit
        // ---------------------------------------------------------------------

        /// <summary>
        /// Called by WorldSystem.EnterActiveWorld to load terrain regions for a planet.
        /// </summary>
        public void LoadTerrainForPlanet(List<TerrainRegion> regions)
        {
            _activeRegions.Clear();
            _pullResults.Clear();
            _cachedFields.Clear();
            CurrentPlayerRegion = null;
            _prevRegionId = null;
            StrongestPull = null;

            if (regions == null) return;

            foreach (var r in regions)
            {
                _activeRegions.Add(r);
                _cachedFields[r.regionId] = r.EffectiveField();
            }

            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            Debug.Log($"[TerrainResonanceSystem] Loaded {_activeRegions.Count} terrain regions.");
        }

        /// <summary>Called when leaving a planet.</summary>
        public void UnloadTerrain()
        {
            _activeRegions.Clear();
            _pullResults.Clear();
            _cachedFields.Clear();
            CurrentPlayerRegion = null;
            _prevRegionId = null;
            StrongestPull = null;
            _playerTransform = null;
        }

        // ---------------------------------------------------------------------
        // Update
        // ---------------------------------------------------------------------
        private void Update()
        {
            if (_activeRegions.Count == 0) return;

            _scanTimer += Time.deltaTime;
            if (_scanTimer < scanInterval) return;
            _scanTimer = 0f;

            ScanPlayerTerrain();
            ApplyIntroductionBands();
        }

        // ---------------------------------------------------------------------
        // Player terrain scan
        // ---------------------------------------------------------------------
        private void ScanPlayerTerrain()
        {
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return;

            if (_playerTransform == null)
                _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            Vector3 playerPos = _playerTransform != null ? _playerTransform.position : Vector3.zero;

            _pullResults.Clear();
            float bestPull = 0f;
            TerrainRegion bestRegion = null;

            foreach (var region in _activeRegions)
            {
                if (!_cachedFields.TryGetValue(region.regionId, out var terrainField))
                    continue;

                float dist = Vector3.Distance(playerPos, region.worldPosition);
                float harmony = playerField.WeightedHarmony(terrainField) * region.fieldStrength;

                var result = new TerrainPullResult(
                    region.regionId, region.terrainType, harmony, dist, region.radius);

                _pullResults.Add(result);

                float effectivePull = result.EffectivePull;
                if (effectivePull > bestPull)
                {
                    bestPull = effectivePull;
                    bestRegion = region;
                }

                // Fire pull changed events for significant changes
                OnTerrainPullChanged?.Invoke(region, effectivePull);

                // Discovery — first time player resonates with this terrain
                if (!_discoveredIds.Contains(region.regionId) && harmony >= discoveryThreshold)
                {
                    _discoveredIds.Add(region.regionId);

                    // Activate myth
                    if (!string.IsNullOrEmpty(region.mythTrigger))
                    {
                        var myth = UniverseStateManager.Instance?.Current?.mythState;
                        myth?.Activate(region.mythTrigger, "terrain_discovery", 0.45f);
                    }

                    OnTerrainDiscovered?.Invoke(region, harmony);
                    if (logDiscoveries)
                        Debug.Log($"[TerrainResonanceSystem] Terrain discovered: {region.regionName} " +
                                  $"({region.terrainType}) pull={harmony:F2}");
                }
            }

            // Track region entry/exit
            string bestId = bestRegion?.regionId;
            if (bestId != _prevRegionId)
            {
                if (_prevRegionId != null)
                {
                    var prev = GetRegion(_prevRegionId);
                    if (prev != null) OnPlayerExitedRegion?.Invoke(prev);
                }

                if (bestRegion != null && bestPull > introductionMinPull)
                {
                    OnPlayerEnteredRegion?.Invoke(bestRegion);
                    if (logDiscoveries)
                        Debug.Log($"[TerrainResonanceSystem] Entered: {bestRegion.regionName} " +
                                  $"({bestRegion.terrainType}) pull={bestPull:F2}");
                }

                _prevRegionId = bestId;
            }

            CurrentPlayerRegion = (bestRegion != null && bestPull > introductionMinPull)
                                  ? bestRegion : null;
            StrongestPull = bestRegion?.terrainType;
        }

        // ---------------------------------------------------------------------
        // Introduction bands — the terrain's teaching mechanism
        // ---------------------------------------------------------------------
        private void ApplyIntroductionBands()
        {
            if (CurrentPlayerRegion == null) return;

            var resonance = VibrationalResonanceSystem.Instance;
            if (resonance == null) return;

            // Find the pull score for the current region
            float currentPull = 0f;
            foreach (var pr in _pullResults)
                if (pr.regionId == CurrentPlayerRegion.regionId)
                    { currentPull = pr.EffectivePull; break; }

            if (currentPull < introductionMinPull) return;

            // Apply introduction bands scaled by pull and strength
            var introBands = TerrainSignature.IntroductionBands(CurrentPlayerRegion.terrainType);
            foreach (var (band, amp) in introBands)
            {
                float amount = amp * introductionStrength * currentPull * scanInterval;
                resonance.ApplyTransientBoost(band, amount);

                if (logIntroductions)
                    OnIntroductionBandApplied?.Invoke(CurrentPlayerRegion.terrainType, band, amount);
            }
        }

        // ---------------------------------------------------------------------
        // NPC terrain affinity — for the autonomous system
        // ---------------------------------------------------------------------

        /// <summary>
        /// Compute terrain pull scores for an NPC based on their spectral drift.
        /// Returns a float[13] array (one per TerrainType) of pull scores (0–1).
        /// Used by NpcEvolutionSystem to determine where NPCs naturally migrate.
        /// </summary>
        public float[] ComputeNpcTerrainAffinity(float[] spectralDrift)
        {
            float[] affinity = new float[TerrainTypeUtil.Count];
            if (spectralDrift == null || spectralDrift.Length < 7) return affinity;

            // Build a temporary vibrational field from NPC spectral drift
            var npcField = new VibrationalField(
                Mathf.Clamp01(0.5f + spectralDrift[0]),  // red
                Mathf.Clamp01(0.5f + spectralDrift[1]),  // orange
                Mathf.Clamp01(0.5f + spectralDrift[2]),  // yellow
                Mathf.Clamp01(0.5f + spectralDrift[3]),  // green
                Mathf.Clamp01(0.5f + spectralDrift[4]),  // blue
                Mathf.Clamp01(0.5f + spectralDrift[5]),  // indigo
                Mathf.Clamp01(0.5f + spectralDrift[6])   // violet
            );

            for (int i = 0; i < TerrainTypeUtil.Count; i++)
            {
                var terrainField = TerrainSignature.ForType((TerrainType)i);
                affinity[i] = npcField.WeightedHarmony(terrainField);
            }

            return affinity;
        }

        /// <summary>
        /// Get the terrain type an NPC is most drawn to based on their spectral drift.
        /// </summary>
        public TerrainType GetNpcPreferredTerrain(float[] spectralDrift)
        {
            var affinity = ComputeNpcTerrainAffinity(spectralDrift);
            int best = 0;
            for (int i = 1; i < affinity.Length; i++)
                if (affinity[i] > affinity[best]) best = i;
            return (TerrainType)best;
        }

        // ---------------------------------------------------------------------
        // Ambient field contribution
        // ---------------------------------------------------------------------

        /// <summary>
        /// Blend all nearby terrain fields into an ambient field.
        /// Called by WorldSystem.RebuildAmbientField to include terrain.
        /// </summary>
        public void BlendIntoAmbient(VibrationalField ambient)
        {
            if (_pullResults.Count == 0 || ambient == null) return;

            foreach (var pr in _pullResults)
            {
                if (pr.EffectivePull < 0.1f) continue;
                if (!_cachedFields.TryGetValue(pr.regionId, out var field)) continue;

                float weight = terrainAmbientBlend * pr.EffectivePull;
                ambient.LerpToward(field, Mathf.Clamp01(weight));
            }
        }

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        public TerrainRegion GetRegion(string regionId)
        {
            foreach (var r in _activeRegions)
                if (r.regionId == regionId) return r;
            return null;
        }

        public bool IsDiscovered(string regionId) => _discoveredIds.Contains(regionId);

        public int DiscoveredCount => _discoveredIds.Count;

        /// <summary>Get all terrain regions of a specific type on the current planet.</summary>
        public List<TerrainRegion> GetRegionsByType(TerrainType type)
        {
            var result = new List<TerrainRegion>();
            foreach (var r in _activeRegions)
                if (r.terrainType == type) result.Add(r);
            return result;
        }

        /// <summary>
        /// Get the highest pull terrain region of each type.
        /// Used for UI — "the nearest cave is pulling at 0.7, the nearest mountain at 0.4..."
        /// </summary>
        public Dictionary<TerrainType, TerrainPullResult> GetBestPullByType()
        {
            var result = new Dictionary<TerrainType, TerrainPullResult>();
            foreach (var pr in _pullResults)
            {
                if (!result.TryGetValue(pr.terrainType, out var existing) ||
                    pr.EffectivePull > existing.EffectivePull)
                {
                    result[pr.terrainType] = pr;
                }
            }
            return result;
        }
    }
}
