// SpiritsCrossing — MythState.cs
// Persistent myth activation layer. Sits above scenes, below cosmos progression.
// Myths activate from ritual outcomes, realm outcomes, and history patterns.
// Once active, myths publish scalar modifiers consumed by portals, companions,
// environmental presentation, and unlock logic.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpiritsCrossing
{
    [Serializable]
    public class ActiveMyth
    {
        public string mythKey;          // e.g. "forest", "storm", "source", "elder"
        public string sourceTag;        // "ritual" | "realm" | "companion" | "history"
        [Range(0f, 1f)] public float strength;
        public string activatedAtUtc;
    }

    [Serializable]
    public class MythState
    {
        public List<ActiveMyth> activeMyths = new List<ActiveMyth>();

        // Age tier — synced from UniverseState so Activate() can self-gate
        public AgeTier ageTier = AgeTier.Voyager;

        // --- Portal bias modifiers applied by MythInterpreter ---
        [Range(0f, 1f)] public float portalBiasForest;
        [Range(0f, 1f)] public float portalBiasSky;
        [Range(0f, 1f)] public float portalBiasOcean;
        [Range(0f, 1f)] public float portalBiasFire;
        [Range(0f, 1f)] public float portalBiasMachine;
        [Range(0f, 1f)] public float portalBiasMartial;
        [Range(0f, 1f)] public float portalBiasSource;

        // --- Environmental/companion modifiers ---
        [Range(0f, 1f)] public float companionSensitivity;
        [Range(0f, 1f)] public float environmentalIntensity;
        [Range(0f, 1f)] public float ruinEchoStrength;

        // -------------------------------------------------------------------------

        public bool HasMyth(string key)
        {
            foreach (var m in activeMyths)
                if (m.mythKey == key) return true;
            return false;
        }

        public float GetStrength(string key)
        {
            foreach (var m in activeMyths)
                if (m.mythKey == key) return m.strength;
            return 0f;
        }

        /// <summary>Activate or reinforce a myth. Age-gated: blocked myths are silently skipped,
        /// seedling-only myths only activate for younger tiers, and strength is capped per tier.</summary>
        public void Activate(string key, string sourceTag, float strength)
        {
            var config = AgeTierProfile.ForTier(ageTier);

            // Block myths that are inappropriate for this age tier
            if (config.IsMythBlocked(key)) return;

            // Seedling-only myths (wonder, friendship, etc.) only activate for tiers that allow them
            if (config.tier == AgeTier.Voyager && config.IsSeedlingOnlyMyth(key)) return;

            // Cap strength to age-appropriate maximum
            strength = Mathf.Clamp01(Mathf.Min(strength, config.strengthCap));

            foreach (var m in activeMyths)
            {
                if (m.mythKey != key) continue;
                m.strength       = Mathf.Clamp01(Mathf.Max(m.strength, strength));
                m.sourceTag      = sourceTag;
                m.activatedAtUtc = DateTime.UtcNow.ToString("o");
                RebuildModifiers();
                return;
            }

            activeMyths.Add(new ActiveMyth
            {
                mythKey        = key,
                sourceTag      = sourceTag,
                strength       = Mathf.Clamp01(strength),
                activatedAtUtc = DateTime.UtcNow.ToString("o")
            });
            RebuildModifiers();
        }

        /// <summary>Decay all myth strengths by a fixed amount (call once per session end).</summary>
        public void DecayAll(float amount = 0.05f)
        {
            for (int i = activeMyths.Count - 1; i >= 0; i--)
            {
                activeMyths[i].strength = Mathf.Clamp01(activeMyths[i].strength - amount);
                if (activeMyths[i].strength <= 0f)
                    activeMyths.RemoveAt(i);
            }
            RebuildModifiers();
        }

        /// <summary>Decay a single myth by its calibrated per-session rate.</summary>
        public void DecaySpecific(string key, float amount)
        {
            for (int i = activeMyths.Count - 1; i >= 0; i--)
            {
                if (activeMyths[i].mythKey != key) continue;
                activeMyths[i].strength = Mathf.Clamp01(activeMyths[i].strength - amount);
                if (activeMyths[i].strength <= 0f)
                    activeMyths.RemoveAt(i);
                break;
            }
            RebuildModifiers();
        }

        /// <summary>Recompute scalar modifiers from current active myths.</summary>
        public void RebuildModifiers()
        {
            portalBiasForest  = GetStrength("forest")  * 0.6f;
            portalBiasSky     = GetStrength("sky")      * 0.6f;
            portalBiasOcean   = GetStrength("ocean")    * 0.6f;
            portalBiasFire    = GetStrength("fire")     * 0.6f;
            portalBiasMachine = GetStrength("machine")  * 0.6f;
            portalBiasMartial = GetStrength("martial")  * 0.6f;
            portalBiasSource  = GetStrength("source")   * 0.6f;

            companionSensitivity   = Mathf.Clamp01(GetStrength("elder") + GetStrength("source") * 0.5f);
            environmentalIntensity = Mathf.Clamp01(GetStrength("storm") + GetStrength("fire")   * 0.4f);
            ruinEchoStrength       = Mathf.Clamp01(GetStrength("ruin")  + GetStrength("elder")  * 0.3f);
        }
    }
}
