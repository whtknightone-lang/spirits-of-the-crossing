// SpiritsCrossing — PortalRevealSystem.cs
// Evaluates which portals are revealed based on the player's live vibrational field.
//
// PORTAL SCORING
//   Each portal maps to a dominant resonance band (or combination).
//   The player's current PlayerField band values drive portal reveal amounts (0–1).
//   Portals are never hidden completely — they exist at a minimum dim presence
//   so the player can sense them before they resonate.
//
//   ForestCalm        → green (heart) + indigo     — Earth/forest portal
//   OceanSurf         → blue + green               — Water portal
//   FireMilitary      → red + orange               — Fire portal
//   AirDancing        → yellow + violet            — Air portal (luminous freedom)
//   SourceEnergyWells → violet + indigo            — Source portal (deepest)
//
// COMMIT SYSTEM
//   Player sustains presence near a portal above commitThreshold for
//   commitDwellTime seconds → portal commits automatically.
//   PortalTransitionController subscribes to OnPortalCommitted.
//
// REVEAL AMOUNT
//   Each portal has a smooth reveal float driven by resonance.
//   0.00 = barely there, dim shimmer
//   0.40 = clearly visible, calling
//   0.70 = bright and resonant, inviting
//   1.00 = fully open, player is deeply aligned

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.Runtime
{
    [Serializable]
    public class PortalState
    {
        public string portalId;
        public string displayName;
        public float  revealAmount;     // 0–1, smooth
        public float  score;            // raw resonance score this frame
        public float  dwellTime;        // seconds player has been near this portal at commit level
        public bool   isCommitted;

        // --- Site-discovered portals ---
        public int             portalTier;  // 0 = cave base, 1+ = site-discovered (maps to PortalDestinationType)
        public PortalSiteType  siteType;    // where in the world this portal was found
        public bool            isDynamic;   // true if registered by PortalSiteActivationSystem
    }

    public class PortalRevealSystem : MonoBehaviour
    {
        public static PortalRevealSystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Reveal Smoothing")]
        [Range(0.5f, 8f)] public float revealSmoothSpeed = 2.0f;

        [Header("Minimum Reveal (portals never fully disappear)")]
        [Range(0f, 0.3f)] public float minReveal = 0.05f;

        [Header("Commit")]
        [Tooltip("Portal score above this triggers commit dwell timer.")]
        [Range(0.3f, 0.9f)] public float commitThreshold = 0.55f;
        [Tooltip("Seconds of sustained presence near portal to commit.")]
        public float commitDwellTime = 5.0f;

        [Header("Portal Band Weights (tuning)")]
        // These drive the scoring. Values are blend ratios of the 7 bands.
        public float forestBlendGreen  = 0.55f;
        public float forestBlendIndigo = 0.45f;

        public float oceanBlendBlue    = 0.55f;
        public float oceanBlendGreen   = 0.45f;

        public float fireBlendRed      = 0.55f;
        public float fireBlendOrange   = 0.45f;

        public float airBlendYellow    = 0.50f;
        public float airBlendViolet    = 0.50f;

        public float sourceBlendViolet = 0.60f;
        public float sourceBlendIndigo = 0.40f;

        public float martialBlendRed    = 0.50f;
        public float martialBlendYellow = 0.50f;

        [Header("Debug")]
        public bool logScoresEverySecond = false;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<PortalState, PortalDecision> OnPortalCommitted;
        public event Action<string, float>               OnPortalRevealChanged;  // portalId, revealAmount

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------
        public IReadOnlyList<PortalState> Portals => _portals;

        /// <summary>
        /// Portal commits are disabled until the ritual layer explicitly enables them
        /// (via EnablePortalCommits) once the ritual's SessionResonanceResult reports
        /// portalUnlocked = true. This ensures portals can only be traversed after a
        /// completed cave ritual, not mid-session or before the player has arrived.
        /// </summary>
        public bool CommitsEnabled { get; private set; }

        /// <summary>Called by GameBootstrapper when the ritual session completes with portalUnlocked = true.</summary>
        public void EnablePortalCommits()
        {
            CommitsEnabled = true;
            Debug.Log("[PortalRevealSystem] Portal commits enabled.");
        }

        private readonly List<PortalState> _portals = new List<PortalState>
        {
            new PortalState { portalId = "ForestCalm",        displayName = "Forest Calm"         },
            new PortalState { portalId = "OceanSurf",         displayName = "Ocean Surf"           },
            new PortalState { portalId = "FireMilitary",      displayName = "Fire Military"        },
            new PortalState { portalId = "AirDancing",        displayName = "Air Dancing"          },
            new PortalState { portalId = "SourceEnergyWells", displayName = "Source Energy Wells"  },
            new PortalState { portalId = "MartialDiscipline", displayName = "Martial Discipline"   },
        };

        private float  _debugTimer;
        private string _playerNearPortalId; // set by scene trigger volumes

        // -------------------------------------------------------------------------
        // Update
        // -------------------------------------------------------------------------
        private void Update()
        {
            var field = VibrationalResonanceSystem.Instance?.PlayerField;
            if (field == null) return;

            foreach (var portal in _portals)
            {
                if (portal.isCommitted) continue;

                float rawScore = ScorePortal(portal.portalId, field);
                portal.score = rawScore;

                // Smooth reveal toward target (with minimum floor)
                float targetReveal = Mathf.Max(minReveal, rawScore);
                float prevReveal   = portal.revealAmount;
                portal.revealAmount = Mathf.Lerp(portal.revealAmount, targetReveal,
                                                  Time.deltaTime * revealSmoothSpeed);

                if (Mathf.Abs(portal.revealAmount - prevReveal) > 0.005f)
                    OnPortalRevealChanged?.Invoke(portal.portalId, portal.revealAmount);

                // Commit dwell — only if player is physically near this portal
                // and the ritual layer has granted commit permission.
                if (CommitsEnabled && portal.portalId == _playerNearPortalId && rawScore >= commitThreshold)
                {
                    portal.dwellTime += Time.deltaTime;
                    if (portal.dwellTime >= commitDwellTime)
                        CommitPortal(portal);
                }
                else
                {
                    portal.dwellTime = Mathf.Max(0f, portal.dwellTime - Time.deltaTime * 0.5f);
                }
            }

            if (logScoresEverySecond)
            {
                _debugTimer += Time.deltaTime;
                if (_debugTimer >= 1f)
                {
                    _debugTimer = 0f;
                    foreach (var p in _portals)
                        Debug.Log($"[PortalRevealSystem] {p.portalId}: score={p.score:F2} " +
                                  $"reveal={p.revealAmount:F2} dwell={p.dwellTime:F1}s");
                }
            }
        }

        // -------------------------------------------------------------------------
        // Scoring — resonance band math + myth portal bias
        //
        // Myth bias is additive, capped at 0.25 so it rewards myth-earned history
        // without overriding genuine low resonance. A player who hasn't earned the
        // myth gets nothing extra; one who has earned it finds their natural portal
        // opens a little sooner and commits a little more readily.
        // -------------------------------------------------------------------------
        private const float MythBiasCap = 0.25f;

        private float ScorePortal(string portalId, VibrationalField f)
        {
            // Base vibrational band score
            float base_ = portalId switch
            {
                "ForestCalm"        => f.green  * forestBlendGreen  + f.indigo * forestBlendIndigo,
                "OceanSurf"         => f.blue   * oceanBlendBlue    + f.green  * oceanBlendGreen,
                "FireMilitary"      => f.red    * fireBlendRed      + f.orange * fireBlendOrange,
                "AirDancing"        => f.yellow * airBlendYellow    + f.violet * airBlendViolet,
                "SourceEnergyWells" => f.violet * sourceBlendViolet + f.indigo * sourceBlendIndigo,
                "MartialDiscipline" => f.red    * martialBlendRed    + f.yellow * martialBlendYellow,
                _                   => 0f,
            };

            // Myth portal bias — accumulated myth history opens portals slightly wider
            var myth = UniverseStateManager.Instance?.Current.mythState;
            if (myth == null) return base_;

            float bias = portalId switch
            {
                "ForestCalm"        => myth.portalBiasForest,
                "OceanSurf"         => myth.portalBiasOcean,
                "FireMilitary"      => myth.portalBiasFire,
                "AirDancing"        => myth.portalBiasSky,
                "SourceEnergyWells" => myth.portalBiasSource,
                "MartialDiscipline" => myth.portalBiasMartial,
                _                   => 0f,
            };

            return Mathf.Clamp01(base_ + Mathf.Min(bias, MythBiasCap));
        }

        // -------------------------------------------------------------------------
        // Commit
        // -------------------------------------------------------------------------
        private void CommitPortal(PortalState portal)
        {
            portal.isCommitted = true;
            portal.revealAmount = 1.0f;

            string realmId = PortalRealmRegistry.RealmForPortal(portal.portalId);

            var slot = new PortalSlot
            {
                portalId     = portal.portalId,
                displayName  = portal.displayName,
                score        = portal.score,
                revealAmount = 1.0f,
                stability    = portal.score,
                isChallenge  = portal.score < commitThreshold + 0.05f,
            };

            var decision = new PortalDecision();
            decision.Commit(slot, realmId);

            UniverseStateManager.Instance?.Current.lastPortalDecision.Commit(slot, realmId);
            UniverseStateManager.Instance?.Save();

            OnPortalCommitted?.Invoke(portal, decision);
            Debug.Log($"[PortalRevealSystem] Portal committed: {portal.portalId} → {realmId} " +
                      $"(score={portal.score:F2} dwell={portal.dwellTime:F1}s)");
        }

        // -------------------------------------------------------------------------
        // Scene trigger API — called by collider trigger volumes in scene
        // -------------------------------------------------------------------------
        public void PlayerEnterPortalRadius(string portalId)
        {
            _playerNearPortalId = portalId;
            Debug.Log($"[PortalRevealSystem] Player near portal: {portalId}");
        }

        public void PlayerExitPortalRadius(string portalId)
        {
            if (_playerNearPortalId == portalId)
                _playerNearPortalId = null;
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------
        public PortalState GetPortal(string portalId)
        {
            foreach (var p in _portals) if (p.portalId == portalId) return p;
            return null;
        }

        public PortalState GetHighestRevealPortal()
        {
            PortalState best = null;
            foreach (var p in _portals)
                if (best == null || p.revealAmount > best.revealAmount) best = p;
            return best;
        }

        public void ResetAllPortals()
        {
            // Remove dynamic portals entirely; reset base portals
            for (int i = _portals.Count - 1; i >= 0; i--)
            {
                if (_portals[i].isDynamic)
                {
                    _portals.RemoveAt(i);
                    continue;
                }
                _portals[i].revealAmount = minReveal;
                _portals[i].score        = 0f;
                _portals[i].dwellTime    = 0f;
                _portals[i].isCommitted  = false;
            }
            _playerNearPortalId = null;
            CommitsEnabled = false;  // require a new ritual before portals can commit again
            Debug.Log("[PortalRevealSystem] All portals reset. Commits disabled until next ritual.");
        }

        // -------------------------------------------------------------------------
        // Dynamic portal registration — used by PortalSiteActivationSystem
        // -------------------------------------------------------------------------

        /// <summary>
        /// Register a portal discovered at a world site. It joins the live portal
        /// list and participates in reveal scoring and commit flow.
        /// </summary>
        public void RegisterDynamicPortal(PortalState portal)
        {
            if (portal == null || string.IsNullOrEmpty(portal.portalId)) return;

            // Avoid duplicates
            foreach (var p in _portals)
                if (p.portalId == portal.portalId) return;

            portal.isDynamic = true;
            _portals.Add(portal);
            OnPortalRevealChanged?.Invoke(portal.portalId, portal.revealAmount);

            Debug.Log($"[PortalRevealSystem] Dynamic portal registered: {portal.portalId} " +
                      $"(tier={portal.portalTier} site={portal.siteType})");
        }

        /// <summary>
        /// Remove a dynamic portal (e.g. when the site deactivates or player leaves the area).
        /// </summary>
        public void RemoveDynamicPortal(string portalId)
        {
            for (int i = _portals.Count - 1; i >= 0; i--)
            {
                if (_portals[i].portalId == portalId && _portals[i].isDynamic)
                {
                    _portals.RemoveAt(i);
                    Debug.Log($"[PortalRevealSystem] Dynamic portal removed: {portalId}");
                    return;
                }
            }
        }

        /// <summary>Get all currently registered dynamic (site-discovered) portals.</summary>
        public List<PortalState> GetDynamicPortals()
        {
            var result = new List<PortalState>();
            foreach (var p in _portals)
                if (p.isDynamic) result.Add(p);
            return result;
        }
    }
}
