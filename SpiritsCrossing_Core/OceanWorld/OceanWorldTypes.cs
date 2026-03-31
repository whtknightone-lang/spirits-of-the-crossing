// SpiritsCrossing — OceanWorldTypes.cs
// Data types for the ocean world environment system.
// Mirrors ForestWorldTypes pattern: zones contain discoverable features
// that activate myths and modify the ambient vibrational field.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.OceanWorld
{
    // -------------------------------------------------------------------------
    // Root data — loaded from ocean_world_data.json
    // -------------------------------------------------------------------------
    [Serializable]
    public class OceanWorldData
    {
        public List<OceanZone> zones = new List<OceanZone>();

        public OceanZone GetZone(string zoneId)
        {
            foreach (var z in zones) if (z.zoneId == zoneId) return z;
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Zone — a named area of the ocean (shallow reef, mid-water, deep trench)
    // -------------------------------------------------------------------------
    [Serializable]
    public class OceanZone
    {
        public string zoneId;
        public string zoneName;
        public string depthTier;   // "shallows" | "midwater" | "deep" | "abyss"
        public string element;     // "Water" (always, but allows blending)

        public List<CoralGardenRecord>     coralGardens    = new List<CoralGardenRecord>();
        public List<TidalCaveRecord>       tidalCaves      = new List<TidalCaveRecord>();
        public List<DeepTrenchRecord>      deepTrenches    = new List<DeepTrenchRecord>();
        public List<OceanCurrentRecord>    currents        = new List<OceanCurrentRecord>();
        public List<BioluminescentZoneRecord> bioZones     = new List<BioluminescentZoneRecord>();

        // Sea creatures that prefer this zone
        public List<string> preferredCompanionIds = new List<string>();
    }

    // -------------------------------------------------------------------------
    // Coral Garden — living structures that respond to player harmony
    // -------------------------------------------------------------------------
    [Serializable]
    public class CoralGardenRecord
    {
        public string gardenId;
        public string coralType;     // "brain", "fan", "staghorn", "table"
        public VibrationalField frozenField;
        public float discoveryThreshold = 0.50f;
        public string mythTrigger;
        public float healthLevel;     // 0–1, affects visual vibrancy
        [Range(0f, 1f)] public float harmonyBonus;  // passive boost when near healthy coral
    }

    // -------------------------------------------------------------------------
    // Tidal Cave — hidden chambers revealed at low harmony thresholds
    // -------------------------------------------------------------------------
    [Serializable]
    public class TidalCaveRecord
    {
        public string caveId;
        public string caveName;
        public VibrationalField frozenField;
        public float outerDiscoveryThreshold = 0.45f;
        public float innerDiscoveryThreshold = 0.65f;
        public string outerMythTrigger;
        public string innerMythTrigger;
        public bool   hasAirPocket;         // safe breathing zone inside
        public bool   hasCrystalFormation;  // visual reward at depth
        public List<TidalCaveChamber> chambers = new List<TidalCaveChamber>();
    }

    [Serializable]
    public class TidalCaveChamber
    {
        public string chamberId;
        public string chamberName;
        public VibrationalField chamberField;
        public float discoveryThreshold;
        public string mythTrigger;
        public float echoIntensity;   // how strongly sound reverberates
    }

    // -------------------------------------------------------------------------
    // Deep Trench — the ocean's most challenging environments
    // -------------------------------------------------------------------------
    [Serializable]
    public class DeepTrenchRecord
    {
        public string trenchId;
        public string trenchName;
        public float  depthMetres;
        public VibrationalField frozenField;
        public float  discoveryThreshold = 0.70f;  // deepest = hardest
        public string mythTrigger;
        public bool   hasHydrothermalVent;
        public bool   hasAbyssalCreatures;
        [Range(0f, 1f)] public float pressureIntensity;  // ambient environmental stress
    }

    // -------------------------------------------------------------------------
    // Ocean Current — linear flow features (like blessed rivers in ForestWorld)
    // -------------------------------------------------------------------------
    [Serializable]
    public class OceanCurrentRecord
    {
        public string currentId;
        public string currentName;
        public string flowDirection;    // "north", "east", "downward", "circular"
        public float  adjacencyRadius = 15f;
        public float  blueAmplification   = 0.008f;  // passive blue band boost
        public float  indigoAmplification = 0.006f;  // passive indigo band boost
        public string mythTrigger;
        public bool   hasWhaleRoute;     // large creatures follow this current
        public string whaleRouteMythTrigger;
    }

    // -------------------------------------------------------------------------
    // Bioluminescent Zone — light-emitting areas that respond to presence
    // -------------------------------------------------------------------------
    [Serializable]
    public class BioluminescentZoneRecord
    {
        public string bioZoneId;
        public string bioZoneName;
        public string glowColor;        // "blue", "green", "violet", "white"
        public VibrationalField frozenField;
        public float  activationThreshold = 0.35f;  // low threshold — bioluminescence is welcoming
        public string mythTrigger;
        [Range(0f, 1f)] public float glowIntensity;
        public bool   respondsToMovement;   // lights up when the player moves through
        public bool   respondsToStillness;  // lights up when the player is still
    }
}
