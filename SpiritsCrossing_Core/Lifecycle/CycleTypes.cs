// SpiritsCrossing — CycleTypes.cs
// Data types for the Birth / Source Drop-In / Rebirth cosmological lifecycle.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.Lifecycle
{
    // -------------------------------------------------------------------------
    // The three phases of a player's cosmological cycle
    // -------------------------------------------------------------------------
    public enum PlayerCyclePhase
    {
        Born,       // Active in the world — cave, realms, companions, cosmos growth
        InSource,   // Released into the Source — communing with Elder Dragons, observing cosmos
        Rebirth,    // Emerging from the Source — transition back into Born with carried gifts
    }

    // -------------------------------------------------------------------------
    // Gifts earned through communion — applied at Rebirth
    // Multiple gifts can coexist.
    // -------------------------------------------------------------------------
    public enum RebirthGift
    {
        SourceBoost,         // sourceConnectionLevel += 0.10 (always given)
        ElementShift,        // dominant element shifts toward communed dragon's element
        CompanionDeepening,  // one companion permanently starts at higher harmony baseline
        MythAwakening,       // a deep myth (elder/ruin/source) is permanently reinforced
        AncientMemory,       // VibrationalField carries a lasting imprint from the dragon
    }

    // -------------------------------------------------------------------------
    // Persistent lifecycle state — lives inside UniverseState
    // -------------------------------------------------------------------------
    [Serializable]
    public class LifecycleState
    {
        public PlayerCyclePhase currentPhase = PlayerCyclePhase.Born;
        public int              cycleCount   = 0;    // number of completed full cycles

        // Source Drop-In record
        public string sourceDropInUtc;
        public float  dropInSourceLevel;             // scl when drop-in happened

        // Communion record (filled during InSource)
        public string communedDragonElement;         // "Fire"|"Water"|"Earth"|"Air"
        public float  communionDepth;                // 0-1, how deeply bonded with the dragon
        public string deepenedCompanionAnimalId;     // which animal companion was deepened

        // Rebirth record
        public string rebirthUtc;
        public List<string> rebirthGiftKeys = new List<string>(); // RebirthGift enum names

        // Ancient Memory — a lasting vibrational field imprint from the communed dragon
        // Blended into the player's field each session at cycle 2+
        public VibrationalField ancientMemoryField;
        public float            ancientMemoryStrength; // 0-1, grows with cycleCount

        public bool IsInSource   => currentPhase == PlayerCyclePhase.InSource;
        public bool IsReborn     => currentPhase == PlayerCyclePhase.Rebirth;
        public bool CanDropIn(float sourceConnectionLevel) =>
            currentPhase == PlayerCyclePhase.Born && sourceConnectionLevel >= 0.40f;

        /// <summary>Ancient memory blend alpha — how much the memory field influences each session.</summary>
        public float MemoryBlendAlpha() => Mathf.Clamp01(ancientMemoryStrength * 0.25f);
    }

    // -------------------------------------------------------------------------
    // A vibrational message left by a player on a planet or sent to another player.
    // Encodes pure vibration — no words.
    // -------------------------------------------------------------------------
    [Serializable]
    public class VibrationalMessage
    {
        public string senderId;          // player id or "npc_[archetypeId]"
        public string senderName;

        // The sender's vibrational field at the moment of sending
        public VibrationalField senderField = new VibrationalField();

        public string planetId;          // where the message is anchored
        public string animalCarrierId;   // which companion animal carries it (optional)
        public string sentUtc;

        [Range(0f, 1f)] public float sourceIntensity; // how much source alignment was present
        public bool    isFromSource;     // true if sent while player was InSource

        // Color encoding of the dominant band (used by visual/particle systems)
        // Populated by VibrationalMessenger.EncodeFieldToColor()
        public float encodedR, encodedG, encodedB, encodedA;

        public Color EncodedColor() => new Color(encodedR, encodedG, encodedB, encodedA);

        public static VibrationalMessage Empty() =>
            new VibrationalMessage { sentUtc = DateTime.UtcNow.ToString("o") };
    }

    // -------------------------------------------------------------------------
    // A player's (or NPC's) cosmos presence — how they appear on the cosmos map
    // to other observers. Used by CosmosObserverMode.
    // -------------------------------------------------------------------------
    [Serializable]
    public class CosmosPresence
    {
        public string             playerId;
        public string             playerName;
        public VibrationalField   vibrationalField = new VibrationalField();
        public string             planetId;        // planet they are currently near
        public PlayerCyclePhase   cyclePhase;
        public string             dominantElement;
        public string             companionAnimalId; // the companion they're with right now
        public string             lastSeenUtc;

        // Visual rendering hints
        public float              glowIntensity;   // scales with sourceConnectionLevel
        public bool               isInSource;      // special ghost rendering
        public bool               isAncientMemory; // player has cycleCount >= 2

        /// <summary>The color this player appears as to observers.</summary>
        public Color PresenceColor()
        {
            // Encode dominant band to hue, source alignment to saturation, glow to brightness
            float h = DominantBandToHue(vibrationalField?.DominantBandName() ?? "green");
            float s = Mathf.Clamp01(0.4f + glowIntensity * 0.6f);
            float v = Mathf.Clamp01(0.5f + glowIntensity * 0.5f);
            float a = isInSource ? 0.55f : 0.85f;
            Color rgb = Color.HSVToRGB(h, s, v);
            return new Color(rgb.r, rgb.g, rgb.b, a);
        }

        private static float DominantBandToHue(string band) => band switch
        {
            "red"    => 0.00f,   // red hue
            "orange" => 0.07f,   // orange
            "yellow" => 0.17f,   // yellow
            "green"  => 0.33f,   // green
            "blue"   => 0.58f,   // blue
            "indigo" => 0.70f,   // indigo/purple
            "violet" => 0.82f,   // violet/magenta
            _        => 0.33f
        };

        public static CosmosPresence FromUniverseState(
            string playerId, string playerName,
            SpiritsCrossing.PlayerResponseSample resonance,
            string planetId, PlayerCyclePhase phase,
            string element, float scl)
        {
            var field = new VibrationalField(
                Mathf.Clamp01(resonance.distortionScore * 0.5f),
                Mathf.Clamp01(resonance.pairSyncScore   * 0.6f + resonance.joyScore * 0.4f),
                Mathf.Clamp01(resonance.joyScore        * 0.6f + resonance.flowScore * 0.4f),
                Mathf.Clamp01(resonance.calmScore       * 0.6f + resonance.stillnessScore * 0.4f),
                Mathf.Clamp01(resonance.flowScore       * 0.6f + resonance.spinScore * 0.4f),
                Mathf.Clamp01(resonance.pairSyncScore   * 0.5f + resonance.sourceAlignmentScore * 0.5f),
                Mathf.Clamp01(resonance.wonderScore     * 0.6f + resonance.sourceAlignmentScore * 0.4f));

            return new CosmosPresence
            {
                playerId        = playerId,
                playerName      = playerName,
                vibrationalField = field,
                planetId        = planetId,
                cyclePhase      = phase,
                dominantElement = element,
                lastSeenUtc     = DateTime.UtcNow.ToString("o"),
                glowIntensity   = scl,
                isInSource      = phase == PlayerCyclePhase.InSource,
            };
        }
    }

    // =========================================================================
    // AI Lifecycle Learning Path — data types
    // =========================================================================

    // -------------------------------------------------------------------------
    // The four cosmological waypoints in the AI learning path
    // -------------------------------------------------------------------------
    public enum CycleWaypointId { Birth, Death, Source, Rebirth }

    // -------------------------------------------------------------------------
    // Resonance snapshot captured at one waypoint
    // -------------------------------------------------------------------------
    [Serializable]
    public class CycleWaypointSnapshot
    {
        public CycleWaypointId                waypoint;
        public string                         timestampUtc;
        public SpiritsCrossing.PlayerResponseSample resonance = new SpiritsCrossing.PlayerResponseSample();
        public float                          sourceConnectionLevel;
        public float                          communionDepth;  // Source waypoint only
        public bool                           valid;           // false = not yet captured

        public void Capture(CycleWaypointId wp,
                            SpiritsCrossing.PlayerResponseSample r,
                            float scl, float communion = 0f)
        {
            waypoint              = wp;
            timestampUtc          = DateTime.UtcNow.ToString("o");
            resonance             = r;
            sourceConnectionLevel = scl;
            communionDepth        = communion;
            valid                 = true;
        }
    }

    // -------------------------------------------------------------------------
    // A resonance collapse event — key metric fell critically low during Born
    // -------------------------------------------------------------------------
    [Serializable]
    public class CollapseEvent
    {
        public string dimension;    // "calm" | "resonance"
        public float  floorReached;
        public string timestampUtc;
    }

    // -------------------------------------------------------------------------
    // Full record of one Birth → Death → Source → Rebirth cycle
    // -------------------------------------------------------------------------
    [Serializable]
    public class CycleLearningRecord
    {
        public int    cycleIndex;
        public string completedUtc;

        public CycleWaypointSnapshot birth   = new CycleWaypointSnapshot();
        public CycleWaypointSnapshot death   = new CycleWaypointSnapshot();
        public CycleWaypointSnapshot source  = new CycleWaypointSnapshot();
        public CycleWaypointSnapshot rebirth = new CycleWaypointSnapshot();

        public List<CollapseEvent> collapseEvents = new List<CollapseEvent>();

        // How much key signals rose from Birth to Source (the deepest state)
        public float CalmGrowth   => source.valid
            ? source.resonance.calmScore            - birth.resonance.calmScore            : 0f;
        public float WonderGrowth => source.valid
            ? source.resonance.wonderScore          - birth.resonance.wonderScore          : 0f;
        public float SourceGrowth => source.valid
            ? source.resonance.sourceAlignmentScore - birth.resonance.sourceAlignmentScore : 0f;
        public float OverallGrowth => (CalmGrowth + WonderGrowth + SourceGrowth) / 3f;
    }

    // -------------------------------------------------------------------------
    // Drive-weight modifier produced by AILifecycleLearningPath.
    // Consumed by SpiritBrainController to adapt spirits to cycle position.
    // -------------------------------------------------------------------------
    [Serializable]
    public class CyclePhaseModifier
    {
        public PlayerCyclePhase phase;

        // Additive shifts applied to normalised drive weights (small but felt)
        public float attackShift;
        public float fleeShift;
        public float seekShift;
        public float restShift;
        public float signalShift;
        public float exploreShift;

        // 0–1: overall AI atmosphere responsiveness for this phase
        public float atmosphereIntensity;
    }
}
