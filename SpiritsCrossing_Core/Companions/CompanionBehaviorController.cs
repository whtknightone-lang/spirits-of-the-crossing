// SpiritsCrossing — CompanionBehaviorController.cs
// Place on each companion prefab. Drives behavior through 4 bond tiers.
//
// Bond tiers → behavior:
//   Distant  (< 0.25)  — stays far, avoids eye contact, occasional glance
//   Curious  (0.25–0.50) — follows at distance, turns to face player, short approaches
//   Bonded   (0.50–0.75) — stays nearby, performs element-specific idle animations
//   Companion(> 0.75)  — fully present, myth reinforcement active, VR haptics on events
//
// NPC companions: if linkedSpiritBrain is set, the companion mirrors the NPC's
// drive mode (Rest→idle, Explore→wander, Signal→approach player, etc.)
//
// Wiring:
//   1. Add this component to a companion prefab.
//   2. Set animalId to match an entry in companion_registry.json.
//   3. Assign transform anchors (player, wanderCenter).
//   4. Optionally link a SpiritBrainController for NPC companions.

using System;
using UnityEngine;
using SpiritsCrossing.SpiritAI;
using SpiritsCrossing.VR;

namespace SpiritsCrossing.Companions
{
    public class CompanionBehaviorController : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Identity")]
        [Tooltip("Must match an animalId in companion_registry.json")]
        public string animalId = "raven";

        [Header("Targets")]
        public Transform playerTransform;
        public Transform wanderCenter;

        [Header("Distances")]
        public float distantDistance   = 12f;
        public float curiousDistance   =  6f;
        public float bondedDistance    =  2.5f;
        public float companionDistance =  1.2f;

        [Header("Movement")]
        public float moveSpeed          = 3f;
        public float wanderRadius       = 5f;
        [Range(0f, 10f)] public float wanderInterval = 4f;

        [Header("NPC Link")]
        [Tooltip("Set for NPC companions. If null, companion tracks player only.")]
        public SpiritBrainController linkedSpiritBrain;

        [Header("Runtime — read-only")]
        [SerializeField] private CompanionBondTier _currentTier = CompanionBondTier.Distant;
        [SerializeField] private float             _bondLevel;
        [SerializeField] private string            _behaviorMode;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<CompanionBondTier> OnTierChanged;

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private CompanionProfile   _profile;
        private CompanionBondTier  _prevTier = CompanionBondTier.Distant;
        private Vector3            _wanderTarget;
        private float              _wanderTimer;
        private Animator           _animator;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            _animator = GetComponentInChildren<Animator>();
            if (playerTransform == null)
                playerTransform = Camera.main?.transform;

            if (wanderCenter == null)
                wanderCenter = transform;

            PickNewWanderTarget();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            if (CompanionBondSystem.Instance != null)
            {
                CompanionBondSystem.Instance.OnBondTierChanged      -= OnBondTierChangedExternally;
                CompanionBondSystem.Instance.OnCompanionFullyBonded -= OnFullyBonded;
            }
        }

        private void SubscribeToEvents()
        {
            if (CompanionBondSystem.Instance == null) return;
            CompanionBondSystem.Instance.OnBondTierChanged      += OnBondTierChangedExternally;
            CompanionBondSystem.Instance.OnCompanionFullyBonded += OnFullyBonded;
        }

        private void Update()
        {
            // Refresh profile and bond state
            if (_profile == null && CompanionBondSystem.Instance != null)
                _profile = CompanionBondSystem.Instance.GetProfile(animalId);

            if (_profile == null) return;

            _bondLevel    = CompanionBondSystem.Instance?.GetBondLevel(animalId)  ?? 0f;
            _currentTier  = CompanionBondSystem.Instance?.GetBondTier(animalId)   ?? CompanionBondTier.Distant;
            _behaviorMode = _profile.behaviorMode;

            TickBehavior();
        }

        // -------------------------------------------------------------------------
        // Behavior state machine
        // -------------------------------------------------------------------------
        private void TickBehavior()
        {
            switch (_currentTier)
            {
                case CompanionBondTier.Distant:   BehaviorDistant();   break;
                case CompanionBondTier.Curious:   BehaviorCurious();   break;
                case CompanionBondTier.Bonded:    BehaviorBonded();    break;
                case CompanionBondTier.Companion: BehaviorCompanion(); break;
            }

            _wanderTimer += Time.deltaTime;
            if (_wanderTimer >= wanderInterval)
            {
                _wanderTimer = 0f;
                PickNewWanderTarget();
            }
        }

        // ---- Distant: wander near spawn, rarely look at player ----
        private void BehaviorDistant()
        {
            MoveToward(_wanderTarget, moveSpeed * 0.5f);
            SetAnimTriggerIf("Idle", true);
        }

        // ---- Curious: orbit at medium distance, faces player periodically ----
        private void BehaviorCurious()
        {
            if (playerTransform == null) return;
            Vector3 target = OffsetFromPlayer(curiousDistance);
            MoveToward(target, moveSpeed * 0.8f);
            if (UnityEngine.Random.value < 0.02f)
                LookAt(playerTransform.position);
            SetAnimTriggerIf("Alert", true);
        }

        // ---- Bonded: stays close, performs element-specific idle ----
        private void BehaviorBonded()
        {
            if (playerTransform == null) return;
            Vector3 target = OffsetFromPlayer(bondedDistance);
            MoveToward(target, moveSpeed);
            LookAt(playerTransform.position);

            // Element-specific animation
            string elemAnim = _profile?.element switch
            {
                "Air"   => "SoarIdle",
                "Earth" => "GroundIdle",
                "Water" => "FlowIdle",
                "Fire"  => "FlameIdle",
                _       => "Idle"
            };
            SetAnimTriggerIf(elemAnim, true);

            // Mirror NPC spirit brain mode if linked
            if (linkedSpiritBrain != null)
                MirrorNPCDrive(linkedSpiritBrain.CurrentMode);
        }

        // ---- Companion: fully present, myth reinforcement, VR feedback ----
        private void BehaviorCompanion()
        {
            if (playerTransform == null) return;
            Vector3 target = OffsetFromPlayer(companionDistance);
            MoveToward(target, moveSpeed * 1.2f);
            LookAt(playerTransform.position);
            SetAnimTriggerIf("CompanionIdle", true);

            // Mirror NPC spirit brain
            if (linkedSpiritBrain != null)
                MirrorNPCDrive(linkedSpiritBrain.CurrentMode);
        }

        // -------------------------------------------------------------------------
        // NPC drive mirroring
        // -------------------------------------------------------------------------
        private void MirrorNPCDrive(SpiritDriveMode driveMode)
        {
            switch (driveMode)
            {
                case SpiritDriveMode.Rest:    SetAnimTriggerIf("Idle",    true); break;
                case SpiritDriveMode.Seek:    SetAnimTriggerIf("Follow",  true); break;
                case SpiritDriveMode.Explore: SetAnimTriggerIf("Wander",  true); PickNewWanderTarget(); break;
                case SpiritDriveMode.Signal:  SetAnimTriggerIf("Signal",  true); LookAt(playerTransform?.position ?? transform.position); break;
                case SpiritDriveMode.Flee:    SetAnimTriggerIf("Retreat", true); break;
                case SpiritDriveMode.Attack:  SetAnimTriggerIf("Display", true); break;
            }
        }

        // -------------------------------------------------------------------------
        // Event handlers
        // -------------------------------------------------------------------------
        private void OnBondTierChangedExternally(string id, CompanionBondTier tier)
        {
            if (id != animalId) return;
            if (tier != _prevTier)
            {
                _prevTier = tier;
                OnTierChanged?.Invoke(tier);
                Debug.Log($"[CompanionBehaviorController] {_profile?.displayName} tier → {tier}");
            }
        }

        private void OnFullyBonded(string id)
        {
            if (id != animalId) return;
            SetAnimTriggerIf("BondComplete", true);

            // VR haptic pulse on full bond
            var vr = VRInputAdapter.Instance;
            if (vr != null && vr.IsVRActive)
            {
                var leftDev  = GetXRController(UnityEngine.XR.XRNode.LeftHand);
                var rightDev = GetXRController(UnityEngine.XR.XRNode.RightHand);
                leftDev.SendHapticImpulse(0, 0.6f, 0.3f);
                rightDev.SendHapticImpulse(0, 0.6f, 0.3f);
            }
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------
        private void MoveToward(Vector3 target, float speed)
        {
            if (Vector3.Distance(transform.position, target) < 0.2f) return;
            transform.position = Vector3.MoveTowards(
                transform.position, target, speed * Time.deltaTime);
        }

        private void LookAt(Vector3 point)
        {
            Vector3 dir = (point - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 4f);
        }

        private Vector3 OffsetFromPlayer(float distance)
        {
            if (playerTransform == null) return transform.position;
            Vector3 dir = (transform.position - playerTransform.position).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = transform.right;
            return playerTransform.position + dir * distance;
        }

        private void PickNewWanderTarget()
        {
            Vector3 center = wanderCenter?.position ?? transform.position;
            Vector2 rand   = UnityEngine.Random.insideUnitCircle * wanderRadius;
            _wanderTarget  = center + new Vector3(rand.x, 0f, rand.y);
        }

        private void SetAnimTriggerIf(string triggerName, bool condition)
        {
            if (_animator == null || !condition) return;
            _animator.SetTrigger(triggerName);
        }

        private static UnityEngine.XR.InputDevice GetXRController(UnityEngine.XR.XRNode node)
        {
            var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesAtXRNode(node, devices);
            return devices.Count > 0 ? devices[0] : default;
        }

        // Public accessors for UI / other systems
        public CompanionProfile   Profile    => _profile;
        public CompanionBondTier  CurrentTier => _currentTier;
        public float              BondLevel  => _bondLevel;
    }
}
