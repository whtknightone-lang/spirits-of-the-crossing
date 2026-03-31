// SpiritsCrossing — SkyWorldTypes.cs
// Data types for the sky world environment system on SkySpiral planet.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.SkyWorld
{
    [Serializable]
    public class SkyWorldData
    {
        public List<SkyZone> zones = new List<SkyZone>();

        public SkyZone GetZone(string zoneId)
        {
            foreach (var z in zones) if (z.zoneId == zoneId) return z;
            return null;
        }
    }

    [Serializable]
    public class SkyZone
    {
        public string zoneId;
        public string zoneName;
        public string altitudeTier;   // "lowsky" | "cloudlayer" | "highsky" | "stratosphere"
        public string element;        // "Air"

        public List<CloudFormationRecord>  cloudFormations  = new List<CloudFormationRecord>();
        public List<AerialRuinRecord>      aerialRuins      = new List<AerialRuinRecord>();
        public List<FloatingIslandRecord>  floatingIslands  = new List<FloatingIslandRecord>();
        public List<UpdraftRecord>         updrafts         = new List<UpdraftRecord>();
        public List<WindCurrentRecord>     windCurrents     = new List<WindCurrentRecord>();
        public List<SkyMountainRecord>     mountains        = new List<SkyMountainRecord>();

        public List<string> preferredCompanionIds = new List<string>();
    }

    // -------------------------------------------------------------------------
    // Cloud Formation — shaped by player wonder and spin
    // -------------------------------------------------------------------------
    [Serializable]
    public class CloudFormationRecord
    {
        public string formationId;
        public string formationType;    // "spiral", "anvil", "lenticular", "pillar"
        public VibrationalField frozenField;
        public float discoveryThreshold = 0.45f;
        public string mythTrigger;
        [Range(0f, 1f)] public float density;
        public bool   shapeshifts;      // cloud changes form in response to player state
    }

    // -------------------------------------------------------------------------
    // Aerial Ruin — ancient floating structures revealed by spin stability
    // -------------------------------------------------------------------------
    [Serializable]
    public class AerialRuinRecord
    {
        public string ruinId;
        public string ruinName;
        public string era;              // "wind-age", "storm-age", "star-age"
        public VibrationalField frozenField;
        public float outerDiscoveryThreshold = 0.50f;
        public float innerDiscoveryThreshold = 0.70f;
        public string outerMythTrigger;
        public string innerMythTrigger;
        public bool   hasWindOrgan;     // ancient instrument that plays in the wind
        public bool   hasStarChart;     // celestial navigation knowledge
        public List<AerialChamber> chambers = new List<AerialChamber>();
    }

    [Serializable]
    public class AerialChamber
    {
        public string chamberId;
        public string chamberName;
        public VibrationalField chamberField;
        public float discoveryThreshold;
        public string mythTrigger;
        public float windResonance;     // how strongly the chamber amplifies wind
    }

    // -------------------------------------------------------------------------
    // Floating Island — stable land masses in the sky
    // -------------------------------------------------------------------------
    [Serializable]
    public class FloatingIslandRecord
    {
        public string islandId;
        public string islandName;
        public string biome;            // "garden", "crystal", "ancient_forest", "bare_rock"
        public VibrationalField frozenField;
        public float discoveryThreshold = 0.40f;
        public string mythTrigger;
        public bool   hasNestingGround;   // sky creatures nest here
        public bool   hasAncientTree;     // connects sky to earth myth
        [Range(0f, 1f)] public float stabilityScore;  // how solid the island feels
    }

    // -------------------------------------------------------------------------
    // Updraft — vertical wind columns that amplify yellow and violet bands
    // -------------------------------------------------------------------------
    [Serializable]
    public class UpdraftRecord
    {
        public string updraftId;
        public string updraftName;
        public float  radius = 10f;
        public float  liftStrength;
        public float  yellowAmplification = 0.007f;
        public float  violetAmplification = 0.006f;
        public string mythTrigger;
        public bool   isSpiral;         // spiral updrafts reward spin stability
    }

    // -------------------------------------------------------------------------
    // Sky Mountain — the on-ramp to air play. Mountains pierce the cloud layer
    // and give grounded players and AI a physical path upward. Caves inside hold
    // resonance chambers. Energy wells at the summit launch into open sky.
    // -------------------------------------------------------------------------
    [Serializable]
    public class SkyMountainRecord
    {
        public string mountainId;
        public string mountainName;
        public string peakType;          // "spire", "plateau", "caldera", "twin_peak"
        public float  peakAltitude;      // metres above zone base
        public VibrationalField frozenField;
        public float  discoveryThreshold = 0.35f;   // mountains are visible — easy to find
        public string mythTrigger;

        // The ascent path — how the mountain teaches the player to rise
        [Range(0f, 1f)] public float ascentDifficulty;   // 0 = gentle slope, 1 = sheer face
        [Range(0f, 1f)] public float windExposure;        // how much wind the player feels climbing

        // Caves inside the mountain
        public List<MountainCaveRecord> caves = new List<MountainCaveRecord>();

        // Energy wells at or near the summit
        public List<EnergyWellRecord>   energyWells = new List<EnergyWellRecord>();

        // Summit features
        public bool   hasSummitNest;      // eagle/wind serpent nesting at the peak
        public bool   hasSummitShrine;    // ancient sky shrine — strong myth trigger
        public string summitMythTrigger;  // fires when player reaches the top
    }

    // -------------------------------------------------------------------------
    // Mountain Cave — resonance chambers inside sky mountains.
    // Sheltered from wind, these are preparation spaces where the player
    // builds breath coherence and calm before the summit launch.
    // AI spirits use caves to learn and recalibrate before ascending.
    // -------------------------------------------------------------------------
    [Serializable]
    public class MountainCaveRecord
    {
        public string caveId;
        public string caveName;
        public string caveType;          // "wind_tunnel", "crystal_grotto", "echo_chamber", "hot_spring"
        public VibrationalField frozenField;
        public float  discoveryThreshold = 0.40f;
        public string mythTrigger;

        // Resonance properties — caves amplify specific qualities
        [Range(0f, 1f)] public float breathAmplification;   // boosts breath coherence while inside
        [Range(0f, 1f)] public float calmAmplification;     // boosts calm while inside
        [Range(0f, 1f)] public float echoIntensity;         // sound reverb — the cave sings back

        // Wind tunnels connect to the outside — they teach the player about airflow
        public bool   hasWindTunnel;       // natural wind passage through the cave
        public float  windTunnelLift;      // how strongly the tunnel pushes upward

        // AI learning: caves are where young AI spirits recalibrate
        public bool   isAICalibrationPoint;  // AI spirits gather here to learn
        [Range(0f, 1f)] public float aiLearningBoost;  // multiplier on AI learning rate while in cave
    }

    // -------------------------------------------------------------------------
    // Energy Well — concentrated vertical launch points at mountain summits
    // and ridgelines. Standing in a well rapidly builds skyFlow, breathExpansion,
    // and spinLift — preparing the player for full air-based play.
    //
    // For young players (Seedling): energy wells are gentle geysers that carry
    // the player upward automatically with their companion.
    // For AI: energy wells are convergence points where multiple AI spirits
    // synchronise before ascending together.
    // -------------------------------------------------------------------------
    [Serializable]
    public class EnergyWellRecord
    {
        public string wellId;
        public string wellName;
        public string wellType;          // "geyser", "vortex", "pulse", "fountain"
        public VibrationalField frozenField;
        public float  activationThreshold = 0.30f;  // low — energy wells are welcoming
        public string mythTrigger;

        // Launch properties
        [Range(0f, 1f)] public float launchIntensity;  // how powerfully the well launches
        public float  launchAltitudeGain;              // metres of altitude gained
        public float  yellowAmplification = 0.015f;    // strong yellow boost (joy/wonder)
        public float  violetAmplification = 0.012f;    // strong violet boost (wonder/source)

        // The well charges over time — spending time near it builds the launch
        public float  chargeTimeSeconds = 8f;          // seconds to fully charge
        [Range(0f, 1f)] public float currentCharge;    // runtime state: 0–1 charge level

        // AI convergence: multiple AI spirits synchronise at wells before ascending
        public bool   isAIConvergencePoint;
        public int    aiConvergenceCapacity = 3;       // how many AI spirits can sync here

        // Seedling mode: auto-launch with companion narration
        public bool   autoLaunchForSeedling = true;
        public string seedlingNarration;               // "Hold on tight! Here we go!"
    }

    // -------------------------------------------------------------------------
    // Wind Current — horizontal flow paths (like ocean currents / forest rivers)
    // -------------------------------------------------------------------------
    [Serializable]
    public class WindCurrentRecord
    {
        public string currentId;
        public string currentName;
        public string direction;        // "east", "west", "ascending", "descending"
        public float  adjacencyRadius = 20f;
        public float  yellowAmplification = 0.005f;
        public float  violetAmplification = 0.004f;
        public string mythTrigger;
        public bool   hasCloudWhaleRoute;
        public string whaleRouteMythTrigger;
    }
}
