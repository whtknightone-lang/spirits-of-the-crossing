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

        private readonly List<PortalState> _portals = new List<PortalState>
        {
            new PortalState { portalId = "ForestCalm",        displayName = "Forest Calm"         },
            new PortalState { portalId = "OceanSurf",         displayName = "Ocean Surf"           },
            new PortalState { portalId = "FireMilitary",      displayName = "Fire Military"        },
            new PortalState { portalId = "AirDancing",        displayName = "Air Dancing"          },
            new PortalState { portalId = "SourceEnergyWells", displayName = "Source Energy Wells"  },
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
                if (portal.portalId == _playerNearPortalId && rawScore >= commitThreshold)
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
        // Scoring — pure resonance band math
        // -------------------------------------------------------------------------
        private float ScorePortal(string portalId, VibrationalField f) => portalId switch
        {
            "ForestCalm"        => f.green  * forestBlendGreen  + f.indigo * forestBlendIndigo,
            "OceanSurf"         => f.blue   * oceanBlendBlue    + f.green  * oceanBlendGreen,
            "FireMilitary"      => f.red    * fireBlendRed      + f.orange * fireBlendOrange,
            "AirDancing"        => f.yellow * airBlendYellow    + f.violet * airBlendViolet,
            "SourceEnergyWells" => f.violet * sourceBlendViolet + f.indigo * sourceBlendIndigo,
            _                   => 0f,
        };

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
            foreach (var p in _portals)
            {
                p.revealAmount = minReveal;
                p.score        = 0f;
                p.dwellTime    = 0f;
                p.isCommitted  = false;
            }
            _playerNearPortalId = null;
        }
    }
}
