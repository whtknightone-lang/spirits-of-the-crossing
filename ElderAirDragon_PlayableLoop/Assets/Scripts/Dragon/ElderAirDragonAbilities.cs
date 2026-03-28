
using UnityEngine;

namespace ArtificialUniverse.Dragon
{
    [RequireComponent(typeof(ElderAirDragonStats))]
    [RequireComponent(typeof(ElderAirDragonController))]
    public class ElderAirDragonAbilities : MonoBehaviour
    {
        private ElderAirDragonStats stats;
        private ElderAirDragonController controller;

        [Header("Ability Tuning")]
        public float gustRadius = 8f;
        public float resonanceRadius = 14f;
        public float spiralLift = 10f;

        private float gustCooldown;
        private float spiralCooldown;
        private float resonanceCooldown;

        private void Awake()
        {
            stats = GetComponent<ElderAirDragonStats>();
            controller = GetComponent<ElderAirDragonController>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            gustCooldown -= dt;
            spiralCooldown -= dt;
            resonanceCooldown -= dt;

            if (Input.GetKeyDown(KeyCode.Q))
                TryGustBurst();

            if (Input.GetKeyDown(KeyCode.E))
                TrySpiralAscent();

            if (Input.GetKeyDown(KeyCode.R))
                TryResonancePulse();
        }

        private void TryGustBurst()
        {
            if (gustCooldown > 0f) return;
            if (!stats.Spend(0.18f, 0.05f)) return;

            stats.ApplyResonance(0.08f);
            gustCooldown = 2f;

            Collider[] hits = Physics.OverlapSphere(transform.position, gustRadius);
            foreach (var hit in hits)
            {
                Rigidbody rb = hit.attachedRigidbody;
                if (rb != null)
                {
                    Vector3 dir = (hit.transform.position - transform.position).normalized;
                    rb.AddForce(dir * 12f, ForceMode.Impulse);
                }
            }
        }

        private void TrySpiralAscent()
        {
            if (spiralCooldown > 0f) return;
            if (!stats.Spend(0.12f, 0.08f, 0.03f)) return;

            transform.position += Vector3.up * spiralLift;
            stats.ApplyResonance(0.12f);
            spiralCooldown = 4f;
        }

        private void TryResonancePulse()
        {
            if (resonanceCooldown > 0f) return;
            if (!stats.Spend(0.20f, 0.10f, 0.05f)) return;

            stats.ApplyResonance(0.18f);
            resonanceCooldown = 5f;

            Collider[] hits = Physics.OverlapSphere(transform.position, resonanceRadius);
            foreach (var hit in hits)
            {
                var targetStats = hit.GetComponent<ElderAirDragonStats>();
                if (targetStats != null && targetStats != stats)
                {
                    targetStats.ApplyResonance(0.1f);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, gustRadius);

            Gizmos.color = new Color(0.9f, 0.9f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, resonanceRadius);
        }
    }
}
