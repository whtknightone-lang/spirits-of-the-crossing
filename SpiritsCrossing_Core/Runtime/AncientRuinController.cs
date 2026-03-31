// SpiritsCrossing — AncientRuinController.cs
// Ancient ruins are the quiet places. They exist beneath the surface of every
// world, older than the current cycle, carrying memory from previous rebirths.
//
// Ruins respond to stillness. The player's distortion is dampened while inside.
// The resonance evaluation favors calm, indigo, violet — the deep bands.
//
// WHAT'S INSIDE:
//   Vibrational Echoes — imprints left by previous rebirth cycles. They appear
//     as shimmering fields of color. Walking through one blends the ancient
//     memory into the player's current resonance temporarily.
//
//   Companion Evolution Fragments — physical tokens of deep bond. If the
//     player's companion is at Bonded tier or higher, they can find a fragment
//     that permanently boosts that companion's bond by 0.15. One fragment per
//     ruin per companion.
//
//   Myth Shards — crystallized myth that, when touched, permanently reinforces
//     a myth key. Each shard can only be activated once.
//
//   The ruins don't talk. They hum. The player has to slow down to hear them.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Companions;
using SpiritsCrossing.Lifecycle;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.Runtime
{
    // -------------------------------------------------------------------------
    // Fragment — a companion evolution token found in a ruin
    // -------------------------------------------------------------------------
    [Serializable]
    public class CompanionFragment
    {
        public string fragmentId;
        public string requiredElement;    // companion element that benefits from this fragment
        public float  bondBoost = 0.15f;  // permanent bond level increase
        public Vector3 worldPosition;
        public string description;        // e.g. "A warm stone that pulses like a heartbeat."
    }

    // -------------------------------------------------------------------------
    // Myth shard — a crystallized myth key found in a ruin
    // -------------------------------------------------------------------------
    [Serializable]
    public class MythShard
    {
        public string shardId;
        public string mythKey;             // which myth to reinforce
        public float  reinforceStrength;   // 0–1
        public Vector3 worldPosition;
        public string description;         // e.g. "A violet crystal that hums with the memory of sky."
    }

    // =========================================================================
    public class AncientRuinController : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<string> OnRuinComplete;           // ruinId
        public event Action<CompanionFragment> OnFragmentCollected;
        public event Action<MythShard>         OnMythShardActivated;

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Ruin Definition")]
        public string ruinId;

        [Header("Fragments and Shards")]
        public List<CompanionFragment> fragments = new List<CompanionFragment>();
        public List<MythShard>         mythShards = new List<MythShard>();

        [Header("Distortion Dampening")]
        [Tooltip("How much to reduce the player's distortion while inside the ruin (multiplier).")]
        [Range(0f, 1f)] public float distortionDampen = 0.15f;

        [Header("Echo")]
        [Tooltip("Blend alpha for ancient memory echo applied while in the ruin.")]
        [Range(0f, 0.5f)] public float echoBlendAlpha = 0.12f;

        [Header("Interaction")]
        [Tooltip("Radius within which the player can collect a fragment or touch a shard.")]
        public float interactionRadius = 3f;

        [Header("Debug")]
        public bool logInteractions = true;

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------
        private bool _isActive;
        private float _originalDistortion;
        private AncientRuinRecord _record;
        private Transform _playerTransform;

        // -------------------------------------------------------------------------
        // Begin — called by PortalTransitionController after the ruin scene loads
        // -------------------------------------------------------------------------
        public void BeginRuin(string id)
        {
            ruinId = id;
            _isActive = true;

            // Get or create persistence record
            _record = GetOrCreateRecord();

            // Cache player transform
            _playerTransform = PortalSiteActivationSystem.Instance != null
                ? null // will be set via SetPlayerTransform or found in scene
                : null;

            // Dampen distortion — ruins are quiet places
            ApplyDistortionDampening();

            // Apply ancient memory echo if the player has one
            ApplyAncientEcho();

            if (logInteractions)
                Debug.Log($"[AncientRuinController] Entered ruin: {ruinId}. " +
                          $"Fragments={fragments.Count} Shards={mythShards.Count}");
        }

        public void SetPlayerTransform(Transform player) => _playerTransform = player;

        // -------------------------------------------------------------------------
        // Update — check for nearby interactable objects
        // -------------------------------------------------------------------------
        private void Update()
        {
            if (!_isActive || _playerTransform == null) return;

            Vector3 playerPos = _playerTransform.position;

            // Check fragments
            foreach (var frag in fragments)
            {
                if (_record.collectedFragmentIds.Contains(frag.fragmentId)) continue;
                if (Vector3.Distance(playerPos, frag.worldPosition) <= interactionRadius)
                    TryCollectFragment(frag);
            }

            // Check myth shards
            foreach (var shard in mythShards)
            {
                if (_record.activatedMythShardIds.Contains(shard.shardId)) continue;
                if (Vector3.Distance(playerPos, shard.worldPosition) <= interactionRadius)
                    TryActivateShard(shard);
            }
        }

        // -------------------------------------------------------------------------
        // Fragment collection
        // -------------------------------------------------------------------------
        private void TryCollectFragment(CompanionFragment frag)
        {
            // Requires companion at Bonded tier or higher with matching element
            var activeId = UniverseStateManager.Instance?.Current?.activeCompanionId;
            if (string.IsNullOrEmpty(activeId)) return;

            var profile = CompanionBondSystem.Instance?.GetProfile(activeId);
            if (profile == null) return;
            if (profile.element != frag.requiredElement) return;

            var tier = CompanionBondSystem.Instance.GetBondTier(activeId);
            if (tier < CompanionBondTier.Bonded) return;

            // Collect: permanently boost companion bond
            var bond = UniverseStateManager.Instance?.Current?.GetOrCreateBond(activeId);
            if (bond == null) return;

            bond.bondLevel = Mathf.Clamp01(bond.bondLevel + frag.bondBoost);
            _record.collectedFragmentIds.Add(frag.fragmentId);
            UniverseStateManager.Instance?.Save();

            OnFragmentCollected?.Invoke(frag);

            if (logInteractions)
                Debug.Log($"[AncientRuinController] Fragment collected: {frag.fragmentId} — " +
                          $"{profile.displayName} bond → {bond.bondLevel:F2}. " +
                          $"{frag.description}");
        }

        // -------------------------------------------------------------------------
        // Myth shard activation
        // -------------------------------------------------------------------------
        private void TryActivateShard(MythShard shard)
        {
            var myth = UniverseStateManager.Instance?.Current?.mythState;
            if (myth == null) return;

            myth.Activate(shard.mythKey, "ancient_ruin", shard.reinforceStrength);
            _record.activatedMythShardIds.Add(shard.shardId);
            UniverseStateManager.Instance?.Save();

            OnMythShardActivated?.Invoke(shard);

            if (logInteractions)
                Debug.Log($"[AncientRuinController] Myth shard activated: {shard.shardId} — " +
                          $"myth '{shard.mythKey}' reinforced at {shard.reinforceStrength:F2}. " +
                          $"{shard.description}");
        }

        // -------------------------------------------------------------------------
        // Distortion dampening — ruins are quiet, distortion fades
        // -------------------------------------------------------------------------
        private void ApplyDistortionDampening()
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            _originalDistortion = universe.persistentResonance.distortionScore;
            universe.persistentResonance.distortionScore *= distortionDampen;

            if (logInteractions)
                Debug.Log($"[AncientRuinController] Distortion dampened: " +
                          $"{_originalDistortion:F3} → " +
                          $"{universe.persistentResonance.distortionScore:F3}");
        }

        private void RestoreDistortion()
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            // Restore most of the original distortion, but keep a small reduction
            // as a lasting effect of having been in the ruin
            universe.persistentResonance.distortionScore =
                Mathf.Lerp(universe.persistentResonance.distortionScore,
                           _originalDistortion, 0.85f);
        }

        // -------------------------------------------------------------------------
        // Ancient memory echo — the ruin reflects past cycles
        // -------------------------------------------------------------------------
        private void ApplyAncientEcho()
        {
            var lc = LifecycleSystem.Instance?.GetLifecycleState();
            if (lc?.ancientMemoryField == null || lc.ancientMemoryStrength <= 0f) return;

            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            // Blend the ancient memory into current resonance gently
            float alpha = echoBlendAlpha * lc.ancientMemoryStrength;
            universe.persistentResonance.calmScore = Mathf.Lerp(
                universe.persistentResonance.calmScore,
                lc.ancientMemoryField.green, alpha);
            universe.persistentResonance.wonderScore = Mathf.Lerp(
                universe.persistentResonance.wonderScore,
                lc.ancientMemoryField.violet, alpha);
            universe.persistentResonance.sourceAlignmentScore = Mathf.Lerp(
                universe.persistentResonance.sourceAlignmentScore,
                lc.ancientMemoryField.SourceAlignment(), alpha);

            if (logInteractions)
                Debug.Log($"[AncientRuinController] Ancient echo applied. " +
                          $"Memory strength={lc.ancientMemoryStrength:F2} alpha={alpha:F3}");
        }

        // -------------------------------------------------------------------------
        // Exit — called by player interaction or auto-complete
        // -------------------------------------------------------------------------
        public void ExitRuin()
        {
            if (!_isActive) return;
            _isActive = false;

            RestoreDistortion();

            _record.hasEntered = true;
            if (string.IsNullOrEmpty(_record.firstEnteredUtc))
                _record.firstEnteredUtc = DateTime.UtcNow.ToString("o");

            UniverseStateManager.Instance?.Save();
            OnRuinComplete?.Invoke(ruinId);

            if (logInteractions)
                Debug.Log($"[AncientRuinController] Exited ruin: {ruinId}. " +
                          $"Fragments collected={_record.collectedFragmentIds.Count} " +
                          $"Shards activated={_record.activatedMythShardIds.Count}");
        }

        // -------------------------------------------------------------------------
        // Persistence
        // -------------------------------------------------------------------------
        private AncientRuinRecord GetOrCreateRecord()
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null)
                return new AncientRuinRecord { ruinId = ruinId };

            var existing = universe.GetAncientRuinRecord(ruinId);
            if (existing != null) return existing;

            var record = new AncientRuinRecord
            {
                ruinId   = ruinId,
                planetId = "", // will be filled by the site definition
            };
            universe.AddAncientRuinRecord(record);
            return record;
        }

        // -------------------------------------------------------------------------
        // Public queries
        // -------------------------------------------------------------------------
        public bool IsFragmentCollected(string fragmentId) =>
            _record?.collectedFragmentIds.Contains(fragmentId) ?? false;

        public bool IsShardActivated(string shardId) =>
            _record?.activatedMythShardIds.Contains(shardId) ?? false;

        public int RemainingFragments()
        {
            if (_record == null) return fragments.Count;
            return fragments.Count - _record.collectedFragmentIds.Count;
        }

        public int RemainingShards()
        {
            if (_record == null) return mythShards.Count;
            return mythShards.Count - _record.activatedMythShardIds.Count;
        }
    }
}
