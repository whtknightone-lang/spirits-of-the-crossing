// SpiritsCrossing — PortalDecision.cs
// Normalized output of the portal scoring layer (ResonancePortalInterpreter).
// The portal system never owns persistence; it only produces and commits a PortalDecision.

using System;

namespace SpiritsCrossing
{
    [Serializable]
    public class PortalSlot
    {
        public string portalId;
        public string displayName;
        public float  score;
        public float  revealAmount;   // 0–1 visual reveal driven value
        public float  stability;      // 0–1 stability (drives animation/audio)
        public bool   isChallenge;
    }

    [Serializable]
    public class PortalDecision
    {
        public PortalSlot current;
        public PortalSlot achievable;
        public PortalSlot challenge;

        // Set when player commits to a portal (CavePortalRevealManager.CommitToCurrentPortal)
        public string committedPortalId;
        public string targetRealmId;    // portal id → realm id resolved by PortalRealmRegistry
        public bool   hasCommitted;
        public string utcTimestamp;

        // --- Site-discovered portal destinations ---
        public PortalDestinationType destinationType;  // Realm, WorldTravel, AncientRuin, ElderDragonEncounter
        public string targetPlanetId;   // for WorldTravel — which planet to travel to
        public string targetRuinId;     // for AncientRuin — which ruin interior to load
        public string targetDragonElement; // for ElderDragonEncounter — which dragon element

        public void Commit(PortalSlot slot, string realmId)
        {
            committedPortalId = slot?.portalId;
            targetRealmId     = realmId;
            hasCommitted      = true;
            utcTimestamp      = DateTime.UtcNow.ToString("o");
        }

        public static PortalDecision Empty() =>
            new PortalDecision { utcTimestamp = DateTime.UtcNow.ToString("o") };
    }

    // Maps portal IDs to realm IDs. Extend as portals and realms grow.
    public static class PortalRealmRegistry
    {
        public static string RealmForPortal(string portalId) => portalId switch
        {
            // --- Base cave portals ---
            "ForestCalm"         => "ForestRealm",
            "OceanSurf"          => "OceanRealm",
            "FireMilitary"       => "FireRealm",
            "AirDancing"         => "SkyRealm",
            "SourceEnergyWells"  => "SourceRealm",

            // --- Evolved realm portals (discovered at world sites) ---
            "DeepForestGate"     => "DeepForestRealm",
            "AbyssalOceanGate"   => "AbyssalOceanRealm",
            "InnerForgeGate"     => "InnerForgeRealm",
            "SkyCathedralGate"   => "SkyCathedralRealm",
            "SourceWellspringGate" => "SourceWellspringRealm",

            // --- Martial realm portal (sacred combat, Popol Vuh / Krishna) ---
            "MartialDiscipline"  => "MartialRealm",

            // --- Elder dragon realm portals (convergence sites, post-rebirth) ---
            "ElderAirConvergence"    => "ElderSkyRealm",
            "ElderEarthConvergence"  => "ElderForestRealm",
            "ElderFireConvergence"   => "ElderFireRealm",
            "ElderWaterConvergence"  => "ElderOceanRealm",
            "ElderSourceConvergence" => "ElderSourceRealm",

            _                    => "UnknownRealm"
        };
    }
}
