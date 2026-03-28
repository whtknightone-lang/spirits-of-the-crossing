// SpiritsCrossing — ForestWorldTypes.cs
// Data types for the Forest World layer of Spirits of the Crossing.
//
// ELEMENTS OF THE FOREST WORLD
//
//   Dryads            — People-like figures living inside ancient trees.
//                       Faces and forms visible through bark at sufficient
//                       resonance. They are the spirit of place — they do not
//                       move. They watch. They have always been watching.
//
//   Overgrown Stone Rings — Ancient ceremonial circles swallowed by the forest.
//                           Stones lean, sink, crack — but the field they hold
//                           is intact beneath the moss. The ring still knows
//                           what it was built for.
//
//   Temple Ruins      — Angkor Wat-scale structures. Root-threaded, vine-hung,
//                       reclaimed by enormous trees whose roots have grown
//                       through walls and embraced columns. Many chambers,
//                       each with its own resonance signature.
//
//   Blessed Rivers    — Water that carries the forest's living field. Following
//                       a blessed river amplifies green/heart and violet/source
//                       bands gradually over time. The water remembers.
//
//   Forest Animals    — Parrot, Forest Hawk, Elder White Stag. See companion
//                       registry. They are part of this world; they appear near
//                       its features.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.ForestWorld
{
    // -------------------------------------------------------------------------
    // Dryad visibility state — how much of the dryad can be perceived
    // -------------------------------------------------------------------------
    public enum DryadVisibility
    {
        Hidden      = 0,   // resonance < 0.35 — nothing visible, you feel watched
        Impression  = 1,   // resonance 0.35–0.55 — faint figure in the grain of the bark
        Emerging    = 2,   // resonance 0.55–0.75 — face clearly visible, watching
        Present     = 3,   // resonance > 0.75 — full form visible, may gesture or speak
    }

    // -------------------------------------------------------------------------
    // A dryad bound to a single ancient tree
    // -------------------------------------------------------------------------
    [Serializable]
    public class DryadRecord
    {
        public string dryadId;
        public string treeId;           // links to scene object tag or ID
        public string forestZone;       // which part of the forest world

        [Header("Personality")]
        public string temperament;      // "patient", "grieving", "joyful", "fierce", "ancient"
        public string mythFragment;     // myth key activated when the dryad becomes Present

        [Header("Resonance")]
        public float impressionThreshold = 0.35f;  // when the impression first appears
        public float presenceThreshold   = 0.75f;  // when they are fully Present
        public VibrationalField resonanceAffinity = new VibrationalField(); // what field they respond to

        [Header("Whisper")]
        public List<string> whisperLines = new List<string>();
        // Each line is delivered once, in order, as resonance builds.
        // They do not repeat. The dryad remembers what it has said.

        [Header("Behaviour")]
        public bool canGesture = true;   // at Present tier: nod, raise hand, point toward ruins
        public bool bindsToRuin;         // true if this dryad is bound to a nearby stone ring/temple
        public string boundRuinId;

        // Derived
        public DryadVisibility VisibilityForHarmony(float harmony)
        {
            if (harmony >= presenceThreshold)  return DryadVisibility.Present;
            if (harmony >= 0.55f)              return DryadVisibility.Emerging;
            if (harmony >= impressionThreshold) return DryadVisibility.Impression;
            return DryadVisibility.Hidden;
        }
    }

    // -------------------------------------------------------------------------
    // Overgrown stone ring — ancient ceremonial circle
    // -------------------------------------------------------------------------
    [Serializable]
    public class StoneRingRecord
    {
        public string ringId;
        public string forestZone;

        [Header("Structure")]
        public int    stoneCount;           // number of standing/fallen stones (5–13)
        public float  ringRadiusMeters;     // world-space radius
        public float  overgrowthLevel;      // 0 = freshly exposed, 1 = nearly buried
        // overgrowthLevel drives: moss density, vine coverage, stone lean angles
        // At 1.0: some stones are underground, visible only as moss mounds

        [Header("Field")]
        public VibrationalField frozenField = new VibrationalField();
        public float  discoveryThreshold = 0.60f;  // harmony needed to feel the ring activate
        public string mythTrigger;
        public string era;                  // "The Root Age", "Before the First Fire", etc.

        [Header("Dryads")]
        public List<string> nearbyDryadIds = new List<string>();
        // Dryads associated with this ring are older and harder to see.
        // Their presenceThreshold += 0.10 when the ring is undiscovered.

        [Header("Ambient")]
        public bool  hasAltarStone;         // central flat stone, used for offerings
        public float altarResonanceBonus = 0.08f; // standing at altar boosts discovery harmony
        public string centerMythTrigger;    // activated when player stands at altar stone
    }

    // -------------------------------------------------------------------------
    // Temple chamber — one room/corridor within an Angkor-like structure
    // -------------------------------------------------------------------------
    [Serializable]
    public class TempleChamber
    {
        public string chamberId;
        public string chamberName;          // "The Root Hall", "Chamber of Still Water", etc.
        public float  discoveryThreshold;
        public string mythTrigger;
        public VibrationalField chamberField = new VibrationalField();

        [Header("Overgrowth")]
        public float rootDensity;           // 0–1: how many tree roots thread through walls
        public float vineCoverage;          // 0–1: vine density on walls and columns
        public bool  hasOpenRoof;           // roots have pushed through — light streams in

        [Header("Dryads")]
        public List<string> chamberDryadIds = new List<string>();
        // Temple dryads live in root-wrapped columns, not trees.
        // Their gestures point toward deeper chambers.
    }

    // -------------------------------------------------------------------------
    // Angkor Wat-scale temple ruin
    // -------------------------------------------------------------------------
    [Serializable]
    public class TempleRecord
    {
        public string templeId;
        public string forestZone;
        public string templeStyle;         // "angkor", "stepped", "tower-complex", "causeway"

        [Header("Structure")]
        public List<TempleChamber> chambers = new List<TempleChamber>();
        public float  overallRootDensity;
        public bool   hasCauseway;         // long stone path leading to the main gate
        public bool   hasMoat;             // moat, now filled with still black water + lotus

        [Header("Field")]
        public VibrationalField templeField = new VibrationalField();
        public float  outerDiscoveryThreshold = 0.45f;  // to perceive the temple exists
        public float  innerDiscoveryThreshold = 0.70f;  // to enter the inner sanctum
        public string outerMythTrigger;
        public string innerMythTrigger;

        [Header("Animals")]
        // These animals are naturally drawn to this temple.
        // Forest Hawks nest in the towers. Parrots roost in the causeway trees.
        // The Elder White Stag may appear at dawn in the inner courtyard.
        public bool  hawkNestPresent   = true;
        public bool  parrotRoostPresent = true;
        public bool  stagAppearsAtDawn  = false; // only in sanctum-level temples

        // The total field of the temple is the blend of all unlocked chambers
        public VibrationalField ActiveField(List<string> unlockedChamberIds)
        {
            if (chambers.Count == 0) return templeField;
            var blend = new VibrationalField(
                templeField.red,    templeField.orange, templeField.yellow,
                templeField.green,  templeField.blue,   templeField.indigo,
                templeField.violet);
            float w = 0.20f;
            foreach (var c in chambers)
                if (unlockedChamberIds.Contains(c.chamberId))
                    blend.LerpToward(c.chamberField, w);
            blend.Clamp01();
            return blend;
        }
    }

    // -------------------------------------------------------------------------
    // Blessed river segment
    // -------------------------------------------------------------------------
    [Serializable]
    public class BlessedRiverRecord
    {
        public string riverId;
        public string riverName;            // "The Green Thread", "Source Vein", etc.
        public string forestZone;

        [Header("Path")]
        public int    segmentCount;         // number of waypoint segments
        public float  totalLengthMeters;
        public bool   hasSourcePool;        // river origin — strongest resonance point
        public bool   hasSeaExit;           // where it leaves the forest world

        [Header("Resonance")]
        // Blessed rivers amplify green (heart) and violet (source) primarily.
        // The player gains resonance passively just from walking alongside.
        public float  greenAmplification  = 0.04f;   // per-second bonus while adjacent
        public float  violetAmplification = 0.025f;  // per-second bonus while adjacent
        public float  adjacencyRadius     = 6f;       // metres from riverbank

        [Header("Animals")]
        // River birds, frogs, and the Elder White Stag drinks from source pools.
        public bool  stagDrinksAtSource = false;      // true on sanctum-grade rivers

        [Header("Myth")]
        public string mythTrigger;           // activated when player first touches the water
        public string sourcePoolMythTrigger; // activated at the origin pool only
        public VibrationalField riverField = new VibrationalField(); // the water's own field
    }

    // -------------------------------------------------------------------------
    // Forest zone — one coherent section of the forest world
    // Each zone has its own mix of features and ambient field.
    // -------------------------------------------------------------------------
    [Serializable]
    public class ForestZone
    {
        public string zoneId;
        public string zoneName;            // "The Deep Wood", "The Sunken Rings", etc.
        public string element;             // dominant element of this zone
        public VibrationalField ambientField = new VibrationalField();

        public List<DryadRecord>     dryads      = new List<DryadRecord>();
        public List<StoneRingRecord> stoneRings  = new List<StoneRingRecord>();
        public List<TempleRecord>    temples     = new List<TempleRecord>();
        public List<BlessedRiverRecord> rivers   = new List<BlessedRiverRecord>();

        // Companions that prefer this zone
        // (resolved against CompanionRegistry.preferredPlanet)
        public List<string> preferredCompanionIds = new List<string>();
    }

    // -------------------------------------------------------------------------
    // Root collection for the full forest world
    // -------------------------------------------------------------------------
    [Serializable]
    public class ForestWorldData
    {
        public string worldId;
        public string worldName;           // "The Forest of the Crossing"
        public List<ForestZone> zones = new List<ForestZone>();
        public string generatedAt;

        public ForestZone GetZone(string zoneId)
        {
            foreach (var z in zones) if (z.zoneId == zoneId) return z;
            return null;
        }

        public DryadRecord GetDryad(string dryadId)
        {
            foreach (var z in zones)
                foreach (var d in z.dryads)
                    if (d.dryadId == dryadId) return d;
            return null;
        }

        public StoneRingRecord GetRing(string ringId)
        {
            foreach (var z in zones)
                foreach (var r in z.stoneRings)
                    if (r.ringId == ringId) return r;
            return null;
        }

        public TempleRecord GetTemple(string templeId)
        {
            foreach (var z in zones)
                foreach (var t in z.temples)
                    if (t.templeId == templeId) return t;
            return null;
        }
    }
}
