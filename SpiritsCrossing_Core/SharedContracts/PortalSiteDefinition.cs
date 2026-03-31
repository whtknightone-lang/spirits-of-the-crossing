// SpiritsCrossing — PortalSiteDefinition.cs
// Data types for world-discovered portal sites.
//
// Portals are not fixed UI — they are places in the world. Caves, sink wells,
// energy wells, mountaintops, clearings, convergence points. Each site type
// responds to different resonance conditions, and each can open a different
// kind of passage: realm entry, world travel, ancient ruin, or elder dragon
// encounter.
//
// ACTIVATION MODES
//   HighResonance — the portal opens when the player's resonance in the
//                   site's target bands EXCEEDS the threshold. Energy wells
//                   and mountaintops respond to intensity.
//   LowResonance  — the portal opens when resonance DROPS BELOW the threshold.
//                   Caves and ruin entrances respond to stillness, patience,
//                   the absence of noise. You have to quiet down to hear them.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpiritsCrossing
{
    // -------------------------------------------------------------------------
    // Where in the world a portal can manifest
    // -------------------------------------------------------------------------
    public enum PortalSiteType
    {
        Cave,         // darkness, stillness → earth/source portals
        SinkWell,     // low terrain where energy pools → water portals
        EnergyWell,   // high-vibration zones → fire portals
        MountainTop,  // exposed peaks → air/source portals
        Clearing,     // open natural spaces → forest portals
        Convergence,  // rare: multiple elements meet → source/elder dragon
    }

    // -------------------------------------------------------------------------
    // How resonance activates the portal
    // -------------------------------------------------------------------------
    public enum PortalActivationMode
    {
        HighResonance, // opens when player resonance exceeds thresholds
        LowResonance,  // opens when player resonance drops below thresholds
    }

    // -------------------------------------------------------------------------
    // What the portal leads to
    // -------------------------------------------------------------------------
    public enum PortalDestinationType
    {
        Realm,                // standard realm scene (Forest, Ocean, Fire, Sky, Source)
        WorldTravel,          // passage to another planet
        AncientRuin,          // hidden location within the current world
        ElderDragonEncounter, // summons an elder dragon at a convergence site
    }

    // -------------------------------------------------------------------------
    // Resonance thresholds for a single band — min/max range
    // -------------------------------------------------------------------------
    [Serializable]
    public class ResonanceBandThreshold
    {
        public string bandName;    // "red","orange","yellow","green","blue","indigo","violet"
        [Range(0f, 1f)] public float min;
        [Range(0f, 1f)] public float max;
        [Range(0f, 1f)] public float weight; // how much this band contributes to the score
    }

    // -------------------------------------------------------------------------
    // A portal site definition — describes a place in the world where a portal
    // can manifest and the conditions that activate it
    // -------------------------------------------------------------------------
    [Serializable]
    public class PortalSiteDefinition
    {
        public string            siteId;
        public string            displayName;
        public PortalSiteType    siteType;
        public string            planetId;
        public Vector3           worldPosition;

        // --- Activation ---
        public PortalActivationMode activationMode;
        public List<ResonanceBandThreshold> bandThresholds = new List<ResonanceBandThreshold>();

        [Tooltip("Overall score threshold (0–1) the site must reach to begin activating.")]
        [Range(0f, 1f)] public float activationThreshold = 0.35f;

        [Tooltip("Score threshold to fully commit the portal (player can enter).")]
        [Range(0f, 1f)] public float commitThreshold = 0.60f;

        [Tooltip("Seconds of sustained activation at commit level before portal commits.")]
        public float commitDwellTime = 5.0f;

        // --- Companion interaction ---
        [Tooltip("Element this site resonates with. Matching companion element lowers thresholds.")]
        public string element; // "Air","Earth","Fire","Water","Source"

        [Tooltip("Threshold reduction per unit of companion bond level (0–1) when elements match.")]
        [Range(0f, 0.3f)] public float companionBondModifier = 0.10f;

        [Tooltip("Minimum companion bond tier required for this site to be activatable at all.")]
        public int minCompanionBondTier = 0; // 0=Distant (no requirement), 1=Curious, 2=Bonded, 3=Companion

        // --- Destination ---
        public PortalDestinationType destinationType;
        public string targetRealmId;    // for Realm destinations
        public string targetPlanetId;   // for WorldTravel destinations
        public string targetRuinId;     // for AncientRuin destinations
        public string targetDragonElement; // for ElderDragonEncounter — which element dragon to summon

        // --- Story ---
        [TextArea(3, 6)]
        public string arrivalStory;     // mythic text when portal first shimmers into view

        // --- Lifecycle ---
        [Tooltip("Minimum rebirth cycle count required. 0 = available from the start.")]
        public int minCycleCount = 0;
    }

    // -------------------------------------------------------------------------
    // Runtime state for an active portal site — tracks activation progress
    // -------------------------------------------------------------------------
    [Serializable]
    public class PortalSiteState
    {
        public string siteId;
        public float  activationAmount; // 0–1, smooth
        public float  dwellTime;        // seconds sustained at commit level
        public bool   isCommitted;
        public bool   isDiscovered;     // player has been near this site at least once
        public string firstDiscoveredUtc;
    }

    // -------------------------------------------------------------------------
    // Persistence record for a discovered site — stored in UniverseState
    // -------------------------------------------------------------------------
    [Serializable]
    public class DiscoveredPortalSiteRecord
    {
        public string siteId;
        public string planetId;
        public string firstDiscoveredUtc;
        public int    timesActivated;
        public int    timesEntered;
    }

    // -------------------------------------------------------------------------
    // Persistence record for ancient ruin progress
    // -------------------------------------------------------------------------
    [Serializable]
    public class AncientRuinRecord
    {
        public string ruinId;
        public string planetId;
        public bool   hasEntered;
        public List<string> collectedFragmentIds = new List<string>();
        public List<string> activatedMythShardIds = new List<string>();
        public string firstEnteredUtc;
    }
}
