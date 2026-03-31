// SpiritsCrossing — CompanionBehaviorController.cs
// Place on each companion prefab.
// Drives behavior from a single continuous float: vibrational harmony.
//
// There is no state machine. Distance, speed, look-at weight, and animation
// blends all derive continuously from harmony between the player's live
// vibrational field and this animal's natural field.
//
//   harmony 0.0  → animal at max distance, wandering, ignoring player
//   harmony 0.5  → neutral — animal is aware, neither drawn nor repelled
//   harmony 1.0  → animal drawn to minimum distance, fully present
//
// The feel is organic because harmony changes smoothly with physiology —
// a player growing calmer is gently accompanied by the snake drawing closer,
// not because a rule fired but because the fields aligned.
//
// NPC companions: if linkedSpiritBrain is set, the animal's orientation
// also mirrors the NPC's drive mode.

using System;
using UnityEngine;
using SpiritsCrossing.SpiritAI;
using SpiritsCrossing.VR;
using SpiritsCrossing.Vibration;

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

        [Header("Distance Range (harmony drives continuously between these)")]
        public float maxDistance = 12f;   // harmony = 0
        public float minDistance =  1.2f; // harmony = 1

        [Header("Movement")]
        public float maxMoveSpeed   = 4f;
        public float wanderRadius   = 5f;
        [Range(0f, 10f)] public float wanderInterval = 4f;

        [Header("Look-at")]
        [Tooltip("How much the animal faces the player. 0=never, 1=always.")]
        public float lookAtWeight = 1f;   // scales with harmony automatically

        [Header("NPC Link")]
        [Tooltip("Set for NPC companions. If null, companion tracks player only.")]
        public SpiritBrainController linkedSpiritBrain;

        [Header("Runtime — read-only")]
        [SerializeField] private float  _currentHarmony;    // 0-1, continuous
        [SerializeField] private float  _harmonyVelocity;   // positive=rising
        [SerializeField] private float  _targetDistance;
        [SerializeField] private string _dominantBandMatch; // which band is driving the bond

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        /// <summary>Fires whenever harmony crosses a natural threshold.</summary>
        public event Action<float> OnHarmonyChanged;  // new harmony value

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private CompanionProfile      _profile;
        private Vector3               _wanderTarget;
        private float                 _wanderTimer;
        private Animator              _animator;
        private float                 _prevHarmony;
        private CompanionRuleAction   _overrideAction;
        private float                 _overrideTimer;  // >0 = override active
        private bool                  _hasOverride;
        private UpsilonNodeBrain      _upsilonNode;    // living oscillator (optional)

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            _animator    = GetComponentInChildren<Animator>();
            _upsilonNode = GetComponent<UpsilonNodeBrain>();

            if (playerTransform == null)
                playerTransform = Camera.main?.transform;

            if (wanderCenter == null)
                wanderCenter = transform;

            PickNewWanderTarget();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            if (VibrationalResonanceSystem.Instance != null)
                VibrationalResonanceSystem.Instance.OnResonanceLock -= OnResonanceLock;
        }

        private void SubscribeToEvents()
        {
            if (VibrationalResonanceSystem.Instance != null)
                VibrationalResonanceSystem.Instance.OnResonanceLock += OnResonanceLock;
        }

        private void Update()
        {
            if (_profile == null && CompanionBondSystem.Instance != null)
                _profile = CompanionBondSystem.Instance.GetProfile(animalId);
            if (_profile == null) return;

            // Read live harmony from the vibrational system
            var vrs = VibrationalResonanceSystem.Instance;
            _currentHarmony  = vrs?.GetHarmony(animalId)          ?? 0.5f;
            _harmonyVelocity = vrs?.GetHarmonyVelocity(animalId)  ?? 0f;

            if (Mathf.Abs(_currentHarmony - _prevHarmony) > 0.02f)
            {
                OnHarmonyChanged?.Invoke(_currentHarmony);
                _prevHarmony = _currentHarmony;
            }

            TickContinuousBehavior();
        }

        // -------------------------------------------------------------------------
        // Behavior override from CompanionAssignmentManager rule engine
        // -------------------------------------------------------------------------
        public void SetBehaviorOverride(CompanionRuleAction action, float duration)
        {
            _overrideAction = action;
            _overrideTimer  = duration;
            _hasOverride    = true;
        }

        public void ClearBehaviorOverride() { _hasOverride = false; _overrideTimer = 0f; }

        // -------------------------------------------------------------------------
        // Continuous vibrational behavior — no state machine, one flow
        // -------------------------------------------------------------------------
        private void TickContinuousBehavior()
        {
            // Rule override takes priority over harmony-based movement
            if (_hasOverride)
            {
                _overrideTimer -= Time.deltaTime;
                if (_overrideTimer <= 0f) { _hasOverride = false; }
                else { ApplyOverride(_overrideAction); return; }
            }

            float h = _currentHarmony;

            // --- Distance: continuously lerps between max and min ---
            _targetDistance = Mathf.Lerp(maxDistance, minDistance, h);

            // --- Speed: faster when drawn in, slower when distant ---
            float speed = h * maxMoveSpeed;

            // --- Movement target ---
            if (playerTransform != null && h > 0.25f)
            {
                // Drawn toward player orbit at harmony-derived distance
                Vector3 target = OffsetFromPlayer(_targetDistance);
                MoveToward(target, speed);
            }
            else
            {
                // Wander when harmony is low
                MoveToward(_wanderTarget, speed * 0.4f);
            }

            // --- Look-at: scales with harmony ---
            if (playerTransform != null && h > 0.30f)
            {
                float lookWeight = Mathf.Clamp01((h - 0.30f) / 0.50f) * lookAtWeight;
                if (lookWeight > 0.1f) LookAt(playerTransform.position);
            }

            // --- Animation: continuous floats, no trigger snaps ---
            if (_animator != null)
            {
                _animator.SetFloat("Harmony",         _currentHarmony);
                _animator.SetFloat("HarmonyVelocity", _harmonyVelocity);

                // Element performance blend — rises above open threshold
                float performBlend = Mathf.Clamp01((h - 0.65f) / 0.20f);
                _animator.SetFloat("ElementPerform",  performBlend);

                // Resonance lock pulse — full presence
                _animator.SetFloat("ResonanceLock", h >= 0.85f ? 1f : 0f);

                // Discovery: animal is encountering something new — curiosity anim
                if (_upsilonNode != null)
                    _animator.SetFloat("DiscoveryLevel", _upsilonNode.DiscoveryLevel);
            }

            // --- NPC drive mirroring (still organic) ---
            if (linkedSpiritBrain != null && h > 0.40f)
                MirrorNPCDrive(linkedSpiritBrain.CurrentMode);

            // --- Wander tick ---
            _wanderTimer += Time.deltaTime;
            if (_wanderTimer >= wanderInterval)
            {
                _wanderTimer = 0f;
                PickNewWanderTarget();
            }
        }

        // -------------------------------------------------------------------------
        // Override execution
        // -------------------------------------------------------------------------
        private void ApplyOverride(CompanionRuleAction action)
        {
            if (playerTransform == null) return;
            switch (action)
            {
                case CompanionRuleAction.Approach:
                    MoveToward(OffsetFromPlayer(minDistance), maxMoveSpeed);
                    LookAt(playerTransform.position);
                    break;
                case CompanionRuleAction.Retreat:
                    MoveToward(OffsetFromPlayer(maxDistance * 0.85f), maxMoveSpeed * 0.6f);
                    break;
                case CompanionRuleAction.Alert:
                    LookAt(playerTransform.position);
                    if (_animator != null) _animator.SetTrigger("Alert");
                    break;
                case CompanionRuleAction.Perform:
                    LookAt(playerTransform.position);
                    if (_animator != null) _animator.SetFloat("ElementPerform", 1f);
                    break;
                case CompanionRuleAction.Bond:
                    // Temporarily boost bond growth via CompanionBondSystem
                    if (CompanionBondSystem.Instance != null)
                        CompanionBondSystem.Instance.bondGrowthRate = Mathf.Min(5f,
                            CompanionBondSystem.Instance.bondGrowthRate + 0.5f);
                    MoveToward(OffsetFromPlayer(minDistance * 1.5f), maxMoveSpeed);
                    break;
                case CompanionRuleAction.Guard:
                    // Position between player and wanderTarget
                    Vector3 guardPos = (playerTransform.position + _wanderTarget) * 0.5f;
                    MoveToward(guardPos, maxMoveSpeed);
                    LookAt(_wanderTarget);
                    break;
                case CompanionRuleAction.Follow:
                    MoveToward(OffsetFromPlayer(minDistance), maxMoveSpeed * 1.1f);
                    LookAt(playerTransform.position);
                    break;
                case CompanionRuleAction.Release:
                    MoveToward(_wanderTarget, maxMoveSpeed * 0.3f);
                    break;
            }
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
        private void OnResonanceLock(string id)
        {
            if (id != animalId) return;
            Debug.Log($"[CompanionBehaviorController] {_profile?.displayName} — resonance lock!");

            // VR haptic pulse on resonance lock
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
        public CompanionProfile Profile        => _profile;
        public float            CurrentHarmony => _currentHarmony;
        public float            HarmonyVelocity => _harmonyVelocity;
    }
}
