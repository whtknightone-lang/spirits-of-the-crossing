// SpiritsCrossing — PortalSiteActivationSystem.cs
// The world listens. Portals are not menus — they are places that respond
// to resonance. This system evaluates every portal site near the player
// and decides whether the portal window opens, closes, or commits.
//
// HIGH-RESONANCE sites (energy wells, mountaintops) open when the player's
// vibrational field blazes in the site's target bands.
//
// LOW-RESONANCE sites (caves, ruin entrances) open when the player quiets
// down — when distortion fades and stillness rises. You have to listen
// to hear them.
//
// COMPANION BOND reduces activation thresholds when the companion's element
// matches the site. A deeply bonded fire companion makes energy wells
// easier to open. The companion is helping.
//
// AI/NPC CONTRIBUTION: NPC spirits near a site add their resonance to the
// evaluation. Cooperative presence matters.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Companions;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Autonomous;
using SpiritsCrossing.Lifecycle;

namespace SpiritsCrossing.Runtime
{
    public class PortalSiteActivationSystem : MonoBehaviour
    {
        public static PortalSiteActivationSystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Evaluation")]
        [Tooltip("How often (seconds) the system evaluates portal sites.")]
        public float evaluationInterval = 0.5f;

        [Tooltip("World-space radius within which portal sites are evaluated.")]
        public float activationRadius = 40f;

        [Header("Activation Smoothing")]
        [Tooltip("How fast portal activation grows when conditions are met.")]
        [Range(0.1f, 4f)] public float activationGrowthSpeed = 1.2f;

        [Tooltip("How fast portal activation decays when conditions fade.")]
        [Range(0.1f, 4f)] public float activationDecaySpeed = 0.6f;

        [Header("AI/NPC Contribution")]
        [Tooltip("Radius to search for NPC spirits that contribute resonance to a site.")]
        public float npcContributionRadius = 20f;

        [Tooltip("Weight of NPC resonance contribution (0–1). 1 = NPCs count as much as the player.")]
        [Range(0f, 1f)] public float npcContributionWeight = 0.35f;

        [Header("Limits")]
        public int maxSimultaneousActivations = 3;

        [Header("Debug")]
        public bool logActivations = true;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        /// <summary>Fires every evaluation tick while a site is activating. For visual/audio.</summary>
        public event Action<string, float> OnPortalSiteActivating;  // siteId, activationAmount

        /// <summary>Fires once when a site reaches full commit.</summary>
        public event Action<string, PortalDecision> OnPortalSiteCommitted; // siteId, decision

        /// <summary>Fires when a site's activation decays back to zero.</summary>
        public event Action<string> OnPortalSiteDeactivated; // siteId

        /// <summary>Fires the first time a player comes near a site.</summary>
        public event Action<PortalSiteDefinition> OnPortalSiteDiscovered;

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------
        private readonly List<PortalSiteDefinition> _sites = new List<PortalSiteDefinition>();
        private readonly Dictionary<string, PortalSiteState> _siteStates =
            new Dictionary<string, PortalSiteState>();

        private int _activeCommitCount;
        private float _evalTimer;
        private Transform _playerTransform;

        // -------------------------------------------------------------------------
        // Registration — called by realm controllers, world generators, etc.
        // -------------------------------------------------------------------------
        public void RegisterSite(PortalSiteDefinition site)
        {
            if (site == null || string.IsNullOrEmpty(site.siteId)) return;

            // Avoid duplicates
            foreach (var s in _sites)
                if (s.siteId == site.siteId) return;

            _sites.Add(site);
            _siteStates[site.siteId] = new PortalSiteState { siteId = site.siteId };

            if (logActivations)
                Debug.Log($"[PortalSiteActivation] Registered site: {site.siteId} " +
                          $"({site.siteType}) on {site.planetId}");
        }

        public void RegisterSites(IEnumerable<PortalSiteDefinition> sites)
        {
            foreach (var s in sites) RegisterSite(s);
        }

        public void UnregisterSite(string siteId)
        {
            for (int i = _sites.Count - 1; i >= 0; i--)
            {
                if (_sites[i].siteId == siteId)
                {
                    _sites.RemoveAt(i);
                    _siteStates.Remove(siteId);
                    return;
                }
            }
        }

        public void ClearAllSites()
        {
            _sites.Clear();
            _siteStates.Clear();
            _activeCommitCount = 0;
        }

        // -------------------------------------------------------------------------
        // Player transform — set by the scene controller or player spawn system
        // -------------------------------------------------------------------------
        public void SetPlayerTransform(Transform player) => _playerTransform = player;

        // -------------------------------------------------------------------------
        // Update loop
        // -------------------------------------------------------------------------
        private void Update()
        {
            _evalTimer += Time.deltaTime;
            if (_evalTimer < evaluationInterval) return;
            _evalTimer = 0f;

            if (_playerTransform == null) return;

            EvaluateAllSites();
        }

        // -------------------------------------------------------------------------
        // Core evaluation
        // -------------------------------------------------------------------------
        private void EvaluateAllSites()
        {
            var field = VibrationalResonanceSystem.Instance?.PlayerField;
            if (field == null) return;

            Vector3 playerPos = _playerTransform.position;
            var companion = GetActiveCompanionProfile();
            float companionBond = GetActiveCompanionBondLevel();
            int cycleCount = LifecycleSystem.Instance?.CycleCount ?? 0;

            foreach (var site in _sites)
            {
                if (!_siteStates.TryGetValue(site.siteId, out var state)) continue;
                if (state.isCommitted) continue;

                float dist = Vector3.Distance(playerPos, site.worldPosition);

                // --- Discovery ---
                if (!state.isDiscovered && dist <= activationRadius)
                {
                    state.isDiscovered = true;
                    state.firstDiscoveredUtc = DateTime.UtcNow.ToString("o");
                    RecordDiscovery(site);
                    OnPortalSiteDiscovered?.Invoke(site);

                    if (logActivations)
                        Debug.Log($"[PortalSiteActivation] Discovered: {site.displayName} — " +
                                  $"{site.arrivalStory}");
                }

                // --- Out of range: decay ---
                if (dist > activationRadius)
                {
                    if (state.activationAmount > 0f)
                    {
                        state.activationAmount = Mathf.Max(0f,
                            state.activationAmount - activationDecaySpeed * evaluationInterval);
                        state.dwellTime = 0f;

                        if (state.activationAmount <= 0.001f)
                        {
                            state.activationAmount = 0f;
                            OnPortalSiteDeactivated?.Invoke(site.siteId);
                        }
                    }
                    continue;
                }

                // --- Prerequisite checks ---
                if (cycleCount < site.minCycleCount) continue;
                if (!MeetsCompanionRequirement(site, companion)) continue;

                // --- Score the site ---
                float score = ScoreSite(site, field);

                // Companion bond modifier — matching element lowers effective threshold
                float thresholdReduction = 0f;
                if (companion != null && companion.element == site.element)
                    thresholdReduction = site.companionBondModifier * companionBond;

                // NPC contribution
                float npcBonus = ScoreNpcContribution(site);
                score += npcBonus * npcContributionWeight;

                score = Mathf.Clamp01(score);

                float effectiveActivationThreshold = Mathf.Max(0.05f,
                    site.activationThreshold - thresholdReduction);
                float effectiveCommitThreshold = Mathf.Max(0.15f,
                    site.commitThreshold - thresholdReduction);

                // --- Activation growth or decay ---
                if (score >= effectiveActivationThreshold)
                {
                    float growth = activationGrowthSpeed * evaluationInterval *
                                   (score / Mathf.Max(0.01f, effectiveActivationThreshold));
                    state.activationAmount = Mathf.Clamp01(state.activationAmount + growth);
                }
                else
                {
                    state.activationAmount = Mathf.Max(0f,
                        state.activationAmount - activationDecaySpeed * evaluationInterval);
                    state.dwellTime = Mathf.Max(0f, state.dwellTime - evaluationInterval * 0.5f);
                }

                // --- Emit activating event ---
                if (state.activationAmount > 0.01f)
                    OnPortalSiteActivating?.Invoke(site.siteId, state.activationAmount);

                // --- Commit dwell ---
                if (score >= effectiveCommitThreshold &&
                    state.activationAmount >= 0.80f)
                {
                    state.dwellTime += evaluationInterval;
                    if (state.dwellTime >= site.commitDwellTime &&
                        _activeCommitCount < maxSimultaneousActivations)
                    {
                        CommitSite(site, state, score);
                    }
                }

                // --- Full decay → deactivated ---
                if (state.activationAmount <= 0.001f)
                {
                    state.activationAmount = 0f;
                    OnPortalSiteDeactivated?.Invoke(site.siteId);
                }
            }
        }

        // -------------------------------------------------------------------------
        // Scoring — evaluates resonance against site band thresholds
        // -------------------------------------------------------------------------
        private float ScoreSite(PortalSiteDefinition site, VibrationalField field)
        {
            if (site.bandThresholds == null || site.bandThresholds.Count == 0)
                return 0f;

            float totalScore = 0f;
            float totalWeight = 0f;

            foreach (var band in site.bandThresholds)
            {
                float bandValue = GetBandValue(field, band.bandName);

                float bandScore;
                if (site.activationMode == PortalActivationMode.HighResonance)
                {
                    // High mode: score rises as band value exceeds min threshold
                    bandScore = bandValue >= band.min
                        ? Mathf.InverseLerp(band.min, band.max, bandValue)
                        : 0f;
                }
                else
                {
                    // Low mode: score rises as band value DROPS BELOW max threshold
                    // Stillness activates these portals — less is more
                    bandScore = bandValue <= band.max
                        ? Mathf.InverseLerp(band.max, band.min, bandValue)
                        : 0f;
                }

                totalScore += bandScore * band.weight;
                totalWeight += band.weight;
            }

            return totalWeight > 0f ? Mathf.Clamp01(totalScore / totalWeight) : 0f;
        }

        private float GetBandValue(VibrationalField f, string bandName) => bandName switch
        {
            "red"    => f.red,
            "orange" => f.orange,
            "yellow" => f.yellow,
            "green"  => f.green,
            "blue"   => f.blue,
            "indigo" => f.indigo,
            "violet" => f.violet,
            _        => 0f
        };

        // -------------------------------------------------------------------------
        // NPC resonance contribution
        // -------------------------------------------------------------------------
        private float ScoreNpcContribution(PortalSiteDefinition site)
        {
            // Find NPC spirits near the site and average their resonance
            // in the site's target bands. This means cooperative AI presence
            // helps open portals the player can't open alone.
            var npcSystem = NpcEvolutionSystem.Instance;
            if (npcSystem == null) return 0f;

            float totalNpcScore = 0f;
            int npcCount = 0;

            var nearbyNpcs = npcSystem.GetNpcsNearPosition(site.worldPosition, npcContributionRadius);
            if (nearbyNpcs == null) return 0f;

            foreach (var npc in nearbyNpcs)
            {
                if (npc.vibrationalField == null) continue;
                float npcScore = ScoreSite(site, npc.vibrationalField);
                totalNpcScore += npcScore;
                npcCount++;
            }

            return npcCount > 0 ? totalNpcScore / npcCount : 0f;
        }

        // -------------------------------------------------------------------------
        // Commit — portal is fully activated, player can enter
        // -------------------------------------------------------------------------
        private void CommitSite(PortalSiteDefinition site, PortalSiteState state, float score)
        {
            state.isCommitted = true;
            state.activationAmount = 1f;
            _activeCommitCount++;

            // Build portal decision
            var slot = new PortalSlot
            {
                portalId     = site.siteId,
                displayName  = site.displayName,
                score        = score,
                revealAmount = 1f,
                stability    = score,
                isChallenge  = false,
            };

            var decision = new PortalDecision();
            decision.destinationType = site.destinationType;
            decision.targetPlanetId  = site.targetPlanetId;
            decision.Commit(slot, site.targetRealmId ?? "");

            // Record in universe state
            RecordActivation(site);

            // Myth activation — discovering and activating portals is always mythic
            var myth = UniverseStateManager.Instance?.Current?.mythState;
            myth?.Activate("portal_awakening", "portal_site", 0.50f);

            if (site.destinationType == PortalDestinationType.ElderDragonEncounter)
                myth?.Activate("elder", "portal_convergence", 0.70f);

            // Register as dynamic portal in PortalRevealSystem so transition controller can pick it up
            var portalState = new PortalState
            {
                portalId     = site.siteId,
                displayName  = site.displayName,
                revealAmount = 1f,
                score        = score,
                dwellTime    = state.dwellTime,
                isCommitted  = true,
                portalTier   = (int)site.destinationType,
                siteType     = site.siteType,
            };
            PortalRevealSystem.Instance?.RegisterDynamicPortal(portalState);

            OnPortalSiteCommitted?.Invoke(site.siteId, decision);

            if (logActivations)
            {
                Debug.Log($"[PortalSiteActivation] COMMITTED: {site.displayName} " +
                          $"({site.siteType} → {site.destinationType}) score={score:F3}");

                if (site.destinationType == PortalDestinationType.WorldTravel)
                    Debug.Log($"[PortalSiteActivation] World travel → {site.targetPlanetId}");
                else if (site.destinationType == PortalDestinationType.AncientRuin)
                    Debug.Log($"[PortalSiteActivation] Ancient ruin → {site.targetRuinId}");
                else if (site.destinationType == PortalDestinationType.ElderDragonEncounter)
                    Debug.Log($"[PortalSiteActivation] Elder dragon → {site.targetDragonElement}");
            }
        }

        // -------------------------------------------------------------------------
        // Companion helpers
        // -------------------------------------------------------------------------
        private CompanionProfile GetActiveCompanionProfile()
        {
            var activeId = UniverseStateManager.Instance?.Current?.activeCompanionId;
            if (string.IsNullOrEmpty(activeId)) return null;
            return CompanionBondSystem.Instance?.GetProfile(activeId);
        }

        private float GetActiveCompanionBondLevel()
        {
            var activeId = UniverseStateManager.Instance?.Current?.activeCompanionId;
            if (string.IsNullOrEmpty(activeId)) return 0f;
            return CompanionBondSystem.Instance?.GetBondLevel(activeId) ?? 0f;
        }

        private bool MeetsCompanionRequirement(PortalSiteDefinition site, CompanionProfile companion)
        {
            if (site.minCompanionBondTier <= 0) return true; // no requirement
            if (companion == null) return false;

            var tier = CompanionBondSystem.Instance?.GetBondTier(companion.animalId)
                       ?? CompanionBondTier.Distant;
            return (int)tier >= site.minCompanionBondTier;
        }

        // -------------------------------------------------------------------------
        // Persistence helpers
        // -------------------------------------------------------------------------
        private void RecordDiscovery(PortalSiteDefinition site)
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            // Ensure discoveredPortalSites list exists and add record
            var record = new DiscoveredPortalSiteRecord
            {
                siteId             = site.siteId,
                planetId           = site.planetId,
                firstDiscoveredUtc = DateTime.UtcNow.ToString("o"),
            };
            universe.AddDiscoveredPortalSite(record);
            UniverseStateManager.Instance?.Save();
        }

        private void RecordActivation(PortalSiteDefinition site)
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            var record = universe.GetDiscoveredPortalSite(site.siteId);
            if (record != null)
            {
                record.timesActivated++;
                UniverseStateManager.Instance?.Save();
            }
        }

        // -------------------------------------------------------------------------
        // Public queries
        // -------------------------------------------------------------------------
        public PortalSiteState GetSiteState(string siteId)
        {
            _siteStates.TryGetValue(siteId, out var state);
            return state;
        }

        public PortalSiteDefinition GetSiteDefinition(string siteId)
        {
            foreach (var s in _sites) if (s.siteId == siteId) return s;
            return null;
        }

        public List<PortalSiteDefinition> GetDiscoveredSites()
        {
            var result = new List<PortalSiteDefinition>();
            foreach (var s in _sites)
            {
                if (_siteStates.TryGetValue(s.siteId, out var state) && state.isDiscovered)
                    result.Add(s);
            }
            return result;
        }

        public List<PortalSiteDefinition> GetActivatingSites()
        {
            var result = new List<PortalSiteDefinition>();
            foreach (var s in _sites)
            {
                if (_siteStates.TryGetValue(s.siteId, out var state) &&
                    state.activationAmount > 0.01f && !state.isCommitted)
                    result.Add(s);
            }
            return result;
        }

        /// <summary>
        /// Reset a committed site so it can be activated again (e.g. after returning
        /// from a world-travel or ruin visit).
        /// </summary>
        public void ResetSiteCommit(string siteId)
        {
            if (_siteStates.TryGetValue(siteId, out var state))
            {
                state.isCommitted = false;
                state.activationAmount = 0f;
                state.dwellTime = 0f;
                _activeCommitCount = Mathf.Max(0, _activeCommitCount - 1);
            }
            PortalRevealSystem.Instance?.RemoveDynamicPortal(siteId);
        }
    }
}
