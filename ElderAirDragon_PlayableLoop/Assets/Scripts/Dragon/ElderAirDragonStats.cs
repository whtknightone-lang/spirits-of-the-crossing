
using UnityEngine;

namespace ArtificialUniverse.Dragon
{
    public class ElderAirDragonStats : MonoBehaviour
    {
        [Header("Core Resources")]
        [Range(0f, 1f)] public float windCharge = 0.5f;
        [Range(0f, 1f)] public float harmony = 0.75f;
        [Range(0f, 1f)] public float integrity = 1.0f;
        [Range(0f, 1f)] public float resonance = 0.5f;

        [Header("Movement")]
        public float cruiseSpeed = 12f;
        public float boostSpeed = 22f;
        public float turnSpeed = 90f;
        public float riseSpeed = 10f;
        public float diveSpeed = 18f;

        [Header("Regeneration")]
        public float harmonyRegenRate = 0.08f;
        public float windChargeRegenRate = 0.06f;

        public void TickPassive(float deltaTime, bool glidingCalmly, float motionAmount)
        {
            if (glidingCalmly)
            {
                harmony = Mathf.Clamp01(harmony + harmonyRegenRate * 1.5f * deltaTime);
                resonance = Mathf.Clamp01(resonance + 0.08f * deltaTime);
            }
            else
            {
                harmony = Mathf.Clamp01(harmony + harmonyRegenRate * 0.35f * deltaTime);
            }

            windCharge = Mathf.Clamp01(windCharge + windChargeRegenRate * (0.5f + motionAmount) * deltaTime);
            integrity = Mathf.Clamp01(integrity + 0.015f * deltaTime);
        }

        public bool Spend(float windCost, float harmonyCost, float integrityCost = 0f)
        {
            if (windCharge < windCost || harmony < harmonyCost || integrity < integrityCost)
                return false;

            windCharge -= windCost;
            harmony -= harmonyCost;
            integrity -= integrityCost;
            return true;
        }

        public void ApplyStrain(float amount)
        {
            integrity = Mathf.Clamp01(integrity - amount);
            harmony = Mathf.Clamp01(harmony - amount * 0.5f);
        }

        public void ApplyResonance(float amount)
        {
            resonance = Mathf.Clamp01(resonance + amount);
        }
    }
}
