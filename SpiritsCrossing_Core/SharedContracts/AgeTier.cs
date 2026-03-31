// SpiritsCrossing — AgeTier.cs
// Age-appropriate experience tiers. Each tier defines how the myth engine,
// companions, realms, and AI learning systems behave. Younger tiers get a
// complete, alive world — not a reduced one — just tuned to a gentler frequency.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpiritsCrossing
{
    // -------------------------------------------------------------------------
    // Tier enum — stored in UniverseState, set at profile creation
    // -------------------------------------------------------------------------
    public enum AgeTier
    {
        Seedling = 0,   // roughly 6–9:  companion-led, gentle, solo only
        Explorer = 1,   // roughly 10–13: full world, dampened intensity
        Voyager  = 2,   // 14+: full experience
    }

    // -------------------------------------------------------------------------
    // Young AI learning configuration — how the game's AI spirits learn and
    // grow alongside the player. Younger tiers get more guidance, faster
    // positive reinforcement, and a safer exploration envelope.
    // -------------------------------------------------------------------------
    [Serializable]
    public class YoungAILearningConfig
    {
        /// <summary>How fast spirit AI adapts to the player (higher = faster learning).</summary>
        [Range(0.5f, 3.0f)] public float learningRate = 1.0f;

        /// <summary>Bonus to wonder/exploration drives in spirit brains.</summary>
        [Range(0f, 1f)] public float curiosityBoost = 0f;

        /// <summary>How much the AI guides vs. follows. 0 = fully autonomous, 1 = fully guided.</summary>
        [Range(0f, 1f)] public float guidanceLevel = 0f;

        /// <summary>Limits how far NPC spirits wander from the player (world units). 0 = unlimited.</summary>
        public float safeExplorationRadius = 0f;

        /// <summary>Positive reinforcement multiplier — how strongly the AI celebrates player actions.</summary>
        [Range(1f, 3f)] public float encouragementMultiplier = 1.0f;

        /// <summary>Whether the AI narrates what it is learning (teaching the player about the system).</summary>
        public bool narrateLearning = false;

        /// <summary>Whether the AI offers gentle hints when the player seems stuck.</summary>
        public bool offerHints = false;

        /// <summary>Maximum distortion the AI will express (caps NPC distortion output).</summary>
        [Range(0f, 1f)] public float maxAIDistortion = 1.0f;
    }

    // -------------------------------------------------------------------------
    // Per-tier profile — every system reads this to adapt its behavior.
    // Created via AgeTierProfile.ForTier() static factory.
    // -------------------------------------------------------------------------
    [Serializable]
    public class AgeTierProfile
    {
        public AgeTier tier;
        public string  storyVariant;   // "seedling" | "explorer" | "voyager"

        // --- Myth filtering ---
        public List<string> blockedMythKeys  = new List<string>();
        public List<string> seedlingOnlyMyths = new List<string>();
        [Range(0f, 1f)] public float strengthCap = 1.0f;

        // --- Companion tuning ---
        [Range(0.5f, 3f)] public float companionBondMultiplier = 1.0f;
        public string tricksterMode = "full";   // "play" | "gentle" | "full"

        // --- World tuning ---
        [Range(0f, 1f)] public float contrastDampen = 1.0f;
        public bool soloOnly = false;

        // --- Realm name softening (key = original realmId, value = age-appropriate name) ---
        public List<RealmNameOverride> realmNameOverrides = new List<RealmNameOverride>();

        // --- Young AI learning ---
        public YoungAILearningConfig aiLearning = new YoungAILearningConfig();

        // -------------------------------------------------------------------------
        // Factory
        // -------------------------------------------------------------------------
        public static AgeTierProfile ForTier(AgeTier tier)
        {
            switch (tier)
            {
                case AgeTier.Seedling: return BuildSeedling();
                case AgeTier.Explorer: return BuildExplorer();
                default:               return BuildVoyager();
            }
        }

        // =====================================================================
        // SEEDLING — companion-led, gentle, solo, young AI fully guided
        // =====================================================================
        private static AgeTierProfile BuildSeedling()
        {
            return new AgeTierProfile
            {
                tier          = AgeTier.Seedling,
                storyVariant  = "seedling",
                strengthCap   = 0.60f,
                blockedMythKeys = new List<string>
                {
                    "storm", "fire", "machine", "rebirth"
                },
                seedlingOnlyMyths = new List<string>
                {
                    "wonder", "friendship", "garden", "starlight",
                    "discovery", "insight", "exploration"
                },
                companionBondMultiplier = 2.0f,
                tricksterMode           = "play",
                contrastDampen          = 0.20f,
                soloOnly                = true,
                realmNameOverrides = new List<RealmNameOverride>
                {
                    new RealmNameOverride { originalRealmId = "FireRealm",    displayName = "The Hearth",      worldName = "HearthWorld" },
                    new RealmNameOverride { originalRealmId = "MachineRealm", displayName = "The Workshop",    worldName = "WorkshopWorld" },
                    new RealmNameOverride { originalRealmId = "SourceRealm",  displayName = "The Dreamspring", worldName = "DreamspringWorld" },
                    new RealmNameOverride { originalRealmId = "OceanRealm",   displayName = "The Tide Pool",   worldName = "TidePoolWorld" },
                },
                aiLearning = new YoungAILearningConfig
                {
                    learningRate            = 2.5f,
                    curiosityBoost          = 0.40f,
                    guidanceLevel           = 0.85f,
                    safeExplorationRadius   = 30f,
                    encouragementMultiplier = 2.5f,
                    narrateLearning         = true,
                    offerHints              = true,
                    maxAIDistortion         = 0.15f,
                }
            };
        }

        // =====================================================================
        // EXPLORER — full world, dampened intensity, AI learns alongside
        // =====================================================================
        private static AgeTierProfile BuildExplorer()
        {
            return new AgeTierProfile
            {
                tier          = AgeTier.Explorer,
                storyVariant  = "explorer",
                strengthCap   = 0.80f,
                blockedMythKeys = new List<string>(), // nothing blocked
                seedlingOnlyMyths = new List<string>
                {
                    "discovery", "insight", "exploration"
                },
                companionBondMultiplier = 1.3f,
                tricksterMode           = "gentle",
                contrastDampen          = 0.60f,
                soloOnly                = false,
                realmNameOverrides      = new List<RealmNameOverride>(),
                aiLearning = new YoungAILearningConfig
                {
                    learningRate            = 1.6f,
                    curiosityBoost          = 0.20f,
                    guidanceLevel           = 0.40f,
                    safeExplorationRadius   = 60f,
                    encouragementMultiplier = 1.6f,
                    narrateLearning         = true,
                    offerHints              = false,
                    maxAIDistortion         = 0.50f,
                }
            };
        }

        // =====================================================================
        // VOYAGER — full experience, AI fully autonomous
        // =====================================================================
        private static AgeTierProfile BuildVoyager()
        {
            return new AgeTierProfile
            {
                tier          = AgeTier.Voyager,
                storyVariant  = "voyager",
                strengthCap   = 1.0f,
                blockedMythKeys   = new List<string>(),
                seedlingOnlyMyths = new List<string>(),
                companionBondMultiplier = 1.0f,
                tricksterMode           = "full",
                contrastDampen          = 1.0f,
                soloOnly                = false,
                realmNameOverrides      = new List<RealmNameOverride>(),
                aiLearning = new YoungAILearningConfig
                {
                    learningRate            = 1.0f,
                    curiosityBoost          = 0f,
                    guidanceLevel           = 0f,
                    safeExplorationRadius   = 0f,
                    encouragementMultiplier = 1.0f,
                    narrateLearning         = false,
                    offerHints              = false,
                    maxAIDistortion         = 1.0f,
                }
            };
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------
        public bool IsMythBlocked(string mythKey) => blockedMythKeys.Contains(mythKey);

        public bool IsSeedlingOnlyMyth(string mythKey) => seedlingOnlyMyths.Contains(mythKey);

        public string GetRealmDisplayName(string originalRealmId)
        {
            foreach (var o in realmNameOverrides)
                if (o.originalRealmId == originalRealmId) return o.displayName;
            return originalRealmId;
        }

        public string GetRealmWorldName(string originalRealmId)
        {
            foreach (var o in realmNameOverrides)
                if (o.originalRealmId == originalRealmId) return o.worldName;
            return null; // no override — use original
        }
    }

    // -------------------------------------------------------------------------
    // Realm name override entry (for JsonUtility serialization)
    // -------------------------------------------------------------------------
    [Serializable]
    public class RealmNameOverride
    {
        public string originalRealmId;
        public string displayName;
        public string worldName;
    }
}
