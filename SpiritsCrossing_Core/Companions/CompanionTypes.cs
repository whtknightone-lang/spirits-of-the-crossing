// SpiritsCrossing — CompanionTypes.cs
// Data types for the companion animal system.
// All structures mirror companion_registry.json for clean serialization.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpiritsCrossing.Companions
{
    // -------------------------------------------------------------------------
    // Bond progression tier
    // -------------------------------------------------------------------------
    public enum CompanionBondTier
    {
        Distant    = 0,   // bond < 0.25  — watches from afar, rarely approaches
        Curious    = 1,   // bond 0.25–0.50 — follows, responds to player state
        Bonded     = 2,   // bond 0.50–0.75 — close, performs elemental behaviors
        Companion  = 3,   // bond > 0.75    — fully integrated, myth reinforcement
    }

    // -------------------------------------------------------------------------
    // Resonance weight vector — mirrors PlayerResonanceState dimensions
    // -------------------------------------------------------------------------
    [Serializable]
    public class CompanionResonanceWeights
    {
        [Range(0f, 1f)] public float calm;
        [Range(0f, 1f)] public float joy;
        [Range(0f, 1f)] public float wonder;
        [Range(0f, 1f)] public float socialSync;
        [Range(0f, 1f)] public float movementFlow;
        [Range(0f, 1f)] public float spinStability;
        [Range(0f, 1f)] public float sourceAlignment;
        [Range(0f, 1f)] public float breathCoherence;

        /// <summary>Weighted dot product against a player resonance state.</summary>
        public float Score(PlayerResonanceState p)
        {
            float dot = calm            * p.calm            +
                        joy             * p.joy             +
                        wonder          * p.wonder          +
                        socialSync      * p.socialSync      +
                        movementFlow    * p.movementFlow    +
                        spinStability   * p.spinStability   +
                        sourceAlignment * p.sourceAlignment +
                        breathCoherence * p.breathCoherence;

            float total = calm + joy + wonder + socialSync + movementFlow +
                          spinStability + sourceAlignment + breathCoherence;

            return total > 0f ? Mathf.Clamp01(dot / total) : 0f;
        }

        /// <summary>Overload for PlayerResponseSample (session snapshots).</summary>
        public float Score(PlayerResponseSample s)
        {
            float dot = calm            * s.calmScore            +
                        joy             * s.joyScore             +
                        wonder          * s.wonderScore          +
                        socialSync      * s.pairSyncScore        +
                        movementFlow    * s.flowScore            +
                        spinStability   * s.spinScore            +
                        sourceAlignment * s.sourceAlignmentScore +
                        breathCoherence * s.stillnessScore;

            float total = calm + joy + wonder + socialSync + movementFlow +
                          spinStability + sourceAlignment + breathCoherence;

            return total > 0f ? Mathf.Clamp01(dot / total) : 0f;
        }
    }

    // -------------------------------------------------------------------------
    // Static companion profile (loaded from companion_registry.json)
    // -------------------------------------------------------------------------
    [Serializable]
    public class CompanionProfile
    {
        public string animalId;
        public string displayName;
        public string element;       // "Air" | "Earth" | "Water" | "Fire"
        public int    tier;          // 1 | 2 | 3
        public string behaviorMode;  // e.g. "wise", "playful", "fierce-wise"
        public string description;
        public CompanionResonanceWeights resonanceWeights = new CompanionResonanceWeights();
        public float  bondThreshold;
        public string mythTrigger;
        public string preferredPlanet;
        public string npcArchetype;

        public CompanionBondTier TierForBondLevel(float bondLevel)
        {
            if (bondLevel >= 0.75f) return CompanionBondTier.Companion;
            if (bondLevel >= 0.50f) return CompanionBondTier.Bonded;
            if (bondLevel >= 0.25f) return CompanionBondTier.Curious;
            return CompanionBondTier.Distant;
        }
    }

    // -------------------------------------------------------------------------
    // Persistent bond state (serialized into UniverseState)
    // -------------------------------------------------------------------------
    [Serializable]
    public class CompanionBondState
    {
        public string animalId;
        [Range(0f, 1f)] public float bondLevel;
        public int    encounterCount;
        public bool   isActive;          // currently accompanying the player
        public string lastSeenUtc;

        public CompanionBondTier Tier => bondLevel >= 0.75f ? CompanionBondTier.Companion
                                       : bondLevel >= 0.50f ? CompanionBondTier.Bonded
                                       : bondLevel >= 0.25f ? CompanionBondTier.Curious
                                                             : CompanionBondTier.Distant;
    }

    // -------------------------------------------------------------------------
    // Root collection (matches companion_registry.json top-level object)
    // -------------------------------------------------------------------------
    [Serializable]
    public class CompanionRegistry
    {
        public List<CompanionProfile>            companions           = new List<CompanionProfile>();
        // NPC default companions: archetype name → list of animalIds
        // Stored as flat list of pairs for JsonUtility compatibility
        public List<NpcCompanionEntry>           npcDefaultCompanions = new List<NpcCompanionEntry>();
    }

    [Serializable]
    public class NpcCompanionEntry
    {
        public string            archetypeId;
        public List<string>      animalIds = new List<string>();
    }
}
