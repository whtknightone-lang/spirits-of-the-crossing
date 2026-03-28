// SpiritsCrossing — DragonStats.cs
// Shared stat block used by all four dragon realm game loops.
// Each dragon's game loop adds its own element-specific metrics on top.

using UnityEngine;

namespace SpiritsCrossing.DragonRealms
{
    public class DragonStats : MonoBehaviour
    {
        [Header("Core Resources")]
        [Range(0f, 1f)] public float harmony        = 0.70f;
        [Range(0f, 1f)] public float resonance       = 0.50f;
        [Range(0f, 1f)] public float integrity       = 1.00f;
        [Range(0f, 1f)] public float elementalCharge = 0.30f;

        [Header("Regeneration")]
        public float harmonyRegenRate       = 0.06f;
        public float elementalChargeRate    = 0.05f;

        // -------------------------------------------------------------------------
        // Passive tick — call each frame from the dragon game loop
        // -------------------------------------------------------------------------
        public void TickPassive(float deltaTime, bool isCalm, float motionAmount)
        {
            if (isCalm)
            {
                harmony         = Mathf.Clamp01(harmony         + harmonyRegenRate   * 1.8f  * deltaTime);
                resonance       = Mathf.Clamp01(resonance       + 0.06f              * deltaTime);
            }
            else
            {
                harmony         = Mathf.Clamp01(harmony         + harmonyRegenRate   * 0.3f  * deltaTime);
            }

            elementalCharge = Mathf.Clamp01(elementalCharge + elementalChargeRate  *
                                            (0.4f + motionAmount * 0.6f)          * deltaTime);
            integrity       = Mathf.Clamp01(integrity       + 0.012f * deltaTime);
        }

        // -------------------------------------------------------------------------
        // Resource spend — returns false if insufficient resources
        // -------------------------------------------------------------------------
        public bool Spend(float harmonyCost, float chargeCost, float integrityCost = 0f)
        {
            if (harmony < harmonyCost || elementalCharge < chargeCost || integrity < integrityCost)
                return false;

            harmony         -= harmonyCost;
            elementalCharge -= chargeCost;
            integrity       -= integrityCost;
            return true;
        }

        public void ApplyStrain(float amount)
        {
            integrity = Mathf.Clamp01(integrity - amount);
            harmony   = Mathf.Clamp01(harmony   - amount * 0.4f);
        }

        public void ApplyResonance(float amount)
        {
            resonance = Mathf.Clamp01(resonance + amount);
        }

        // -------------------------------------------------------------------------
        // Seed from player resonance snapshot (called by each loop's BeginRealm)
        // -------------------------------------------------------------------------
        public void SeedFromPlayer(PlayerResponseSample sample,
                                   float harmonyBase   = 0.5f,
                                   float resonanceBase = 0.3f)
        {
            harmony   = Mathf.Clamp01(harmonyBase   + sample.calmScore    * 0.3f);
            resonance = Mathf.Clamp01(resonanceBase + sample.wonderScore  * 0.4f);
            integrity = 1.0f;
        }
    }
}
