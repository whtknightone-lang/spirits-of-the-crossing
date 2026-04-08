using UnityEngine;

namespace SpiritsCrossing.Vibration
{
    /// <summary>
    /// Thin body wrapper around UpsilonSphereAI.
    /// Handles movement, emitter sensing, and chamber interaction.
    /// </summary>
    [RequireComponent(typeof(UpsilonSphereAI))]
    public class EmbodiedAgentController : MonoBehaviour
    {
        [Header("Movement")]
        [Min(0.1f)] public float moveSpeed = 1.8f;
        [Min(0.1f)] public float turnSpeed = 4f;
        [Min(0.1f)] public float wanderRadius = 10f;
        [Range(0f, 1f)] public float centerBias = 0.18f;
        public Vector3 arenaCenter = Vector3.zero;

        [Header("Sensing")]
        [Range(0f, 1f)] public float emitterSenseMultiplier = 1.0f;
        [Range(0f, 1f)] public float chamberSenseMultiplier = 1.0f;

        [Header("Visual")]
        public Transform visualRoot;
        [Min(0.1f)] public float dyingScale = 0.35f;
        [Min(0.1f)] public float respawnScale = 0.60f;

        private UpsilonSphereAI _sphere;
        private RiverSpherePhase _previousPhase;
        private Vector3 _wanderTarget;
        private float _retargetTimer;

        private void Awake()
        {
            _sphere = GetComponent<UpsilonSphereAI>();
            if (visualRoot == null) visualRoot = transform;
        }

        private void OnEnable()
        {
            if (SourceBrain.Instance != null)
                SourceBrain.Instance.RegisterAgent(_sphere);

            _previousPhase = _sphere != null ? _sphere.CurrentPhase : RiverSpherePhase.Born;
            PickNewWanderTarget();
        }

        private void OnDisable()
        {
            if (SourceBrain.Instance != null)
                SourceBrain.Instance.UnregisterAgent(_sphere);
        }

        private void Update()
        {
            if (_sphere == null) return;

            SenseEmitters();
            HandlePhaseChange();
            UpdateBodyScale();

            if (_sphere.CurrentPhase == RiverSpherePhase.Born)
                MoveDuringLife();
            else
                HoldDuringTransition();
        }

        private void SenseEmitters()
        {
            SourceBrain source = SourceBrain.Instance;
            if (source == null) return;

            var emitters = source.Emitters;
            for (int i = 0; i < emitters.Count; i++)
            {
                WorldFieldEmitter emitter = emitters[i];
                if (emitter == null) continue;

                float influence = emitter.EvaluateInfluence(transform.position) * emitterSenseMultiplier;
                if (influence <= 0f) continue;
                _sphere.Sense(emitter.CurrentField(), influence);
            }

            if (_sphere.CurrentPhase == RiverSpherePhase.Respawning && source.rebirthChamber != null)
                _sphere.Sense(source.rebirthChamber.ChamberField(), source.rebirthChamber.chamberSenseStrength * chamberSenseMultiplier);
        }

        private void HandlePhaseChange()
        {
            if (_sphere.CurrentPhase == _previousPhase) return;

            SourceBrain source = SourceBrain.Instance;
            RebirthChamber chamber = source != null ? source.rebirthChamber : null;

            if (_sphere.CurrentPhase == RiverSpherePhase.Respawning && chamber != null)
                chamber.Receive(this);
            else if (_sphere.CurrentPhase == RiverSpherePhase.Born && chamber != null)
                chamber.Release(this);

            _previousPhase = _sphere.CurrentPhase;
        }

        private void MoveDuringLife()
        {
            _retargetTimer -= Time.deltaTime;
            if (_retargetTimer <= 0f || Vector3.Distance(transform.position, _wanderTarget) < 1f)
                PickNewWanderTarget();

            Vector3 desired = (_wanderTarget - transform.position).normalized;
            desired += BestEmitterDirection() * 0.85f;

            Vector3 toCenter = arenaCenter - transform.position;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude > 0.001f)
                desired += toCenter.normalized * centerBias;

            desired.y = 0f;
            if (desired.sqrMagnitude < 0.0001f) return;

            desired.Normalize();
            float speed = moveSpeed * DriveSpeedMultiplier(_sphere.DominantDrive);
            transform.position += desired * speed * Time.deltaTime;
            transform.forward = Vector3.Slerp(transform.forward, desired, turnSpeed * Time.deltaTime);
        }

        private void HoldDuringTransition()
        {
            SourceBrain source = SourceBrain.Instance;
            if (source != null && source.rebirthChamber != null && _sphere.CurrentPhase == RiverSpherePhase.Respawning)
                source.rebirthChamber.Hold(this);
        }

        private Vector3 BestEmitterDirection()
        {
            SourceBrain source = SourceBrain.Instance;
            if (source == null) return Vector3.zero;

            float best = 0f;
            Vector3 bestDir = Vector3.zero;
            var emitters = source.Emitters;

            for (int i = 0; i < emitters.Count; i++)
            {
                WorldFieldEmitter emitter = emitters[i];
                if (emitter == null) continue;

                Vector3 delta = emitter.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.001f) continue;

                float influence = emitter.EvaluateInfluence(transform.position);
                if (influence <= 0f) continue;

                float score = influence * _sphere.HarmonyWith(emitter.CurrentField());
                if (score > best)
                {
                    best = score;
                    bestDir = delta.normalized;
                }
            }

            return bestDir;
        }

        private void UpdateBodyScale()
        {
            float target = 1f;
            if (_sphere.CurrentPhase == RiverSpherePhase.Dying) target = dyingScale;
            if (_sphere.CurrentPhase == RiverSpherePhase.Respawning) target = respawnScale;
            visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, Vector3.one * target, Time.deltaTime * 6f);
        }

        private void PickNewWanderTarget()
        {
            Vector2 circle = Random.insideUnitCircle * wanderRadius;
            _wanderTarget = arenaCenter + new Vector3(circle.x, 0f, circle.y);
            _retargetTimer = Random.Range(2.5f, 5.5f);
        }

        private static float DriveSpeedMultiplier(SpiritDriveMode mode)
        {
            switch (mode)
            {
                case SpiritDriveMode.Attack:  return 1.25f;
                case SpiritDriveMode.Flee:    return 1.35f;
                case SpiritDriveMode.Seek:    return 1.10f;
                case SpiritDriveMode.Explore: return 1.20f;
                case SpiritDriveMode.Signal:  return 0.95f;
                default:                      return 0.75f;
            }
        }
    }
}
