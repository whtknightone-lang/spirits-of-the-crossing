// SpiritsCrossing — MartialWorldTypes.cs
// Data types for the Martial World layer of Spirits of the Crossing.
//
// MARTIAL WORLD — COMBAT AS SACRED ART
//
//   This is a world where fighting is not violence — it is worship.
//   Every discipline is a conversation between the body and something older
//   than the body. Every strike, every block, every breath is a prayer.
//
// MYTHOLOGICAL ROOTS
//
//   Popol Vuh (Mayan)
//     The Hero Twins, Hunahpu and Xbalanque, defeated the Lords of Xibalba
//     in the sacred ball game. Combat as cosmic ritual. The rubber ball
//     echoing between stone walls is the oldest sound of disciplined art.
//     Chaak, the blue god of rain and sacrifice — the warrior who gives.
//     The four failed creations (mud, wood, maize) — the martial path
//     is the same: you are broken and remade until you can worship.
//
//   Bhagavad Gita (Krishna)
//     On the battlefield of Kurukshetra, Krishna teaches Arjuna:
//     to fight is duty, and duty is love. The warrior's path is a
//     spiritual path. Combat is not destruction — it is dharma.
//
// MASTERY TIERS (Mayan Cosmology)
//
//   9 levels of Xibalba (Underworld)  — facing weakness, fear, ego
//   1 Middle World (Middleworld)       — balance achieved
//   13 levels of Heaven (Upperworld)  — combat becomes pure art and worship
//   Total: 23 tiers of mastery
//
// ZONES
//
//   Ball Court of Xibalba  — The Hero Twins' arena. Rhythm, timing, flow.
//   Dojo of Still Water    — Kung Fu, Karate, hand-to-hand. Chaak's rain.
//   Shadow Garden          — Ninja arts. The darkness before the sun rose.
//   Blade Pavilion         — Fencing, sword fighting. Precision and intent.
//   Krishna's Field        — Kurukshetra. The deepest martial teaching.
//   Maize Forge            — Where warriors are unmade and remade.
//
// MARTIAL COMPANIONS (managed by WorldAnimalSpawnSystem)
//   jaguar_martial    — Martial / Tier 1 — Crunching Jaguar from Popol Vuh
//   crane             — Martial / Tier 1 — balance, patience, the crane stance
//   plumed_serpent    — Martial / Tier 2 — Quetzalcoatl, wisdom in motion
//   martial_hawk      — Martial / Tier 2 — precision, the strike
//   fox_sacred        — Martial / Tier 3 — sacred animal who found the maize
//   quetzal           — Martial / Tier 3 — feathered serpent spirit, mastery

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.MartialWorld
{
    // -------------------------------------------------------------------------
    // Mastery tiers — mapped to Mayan cosmology
    // -------------------------------------------------------------------------

    /// <summary>
    /// 23-tier mastery system based on Mayan cosmological levels.
    /// Xibalba (1–9): the underworld — you face your weakness.
    /// Middleworld (10): balance — the turning point.
    /// Heaven (11–23): combat becomes worship.
    /// </summary>
    public enum MasteryRealm
    {
        Xibalba,     // tiers 1–9
        Middleworld,  // tier 10
        Upperworld,  // tiers 11–23
    }

    [Serializable]
    public class MasteryTierState
    {
        public int currentTier = 1;  // 1–23

        public MasteryRealm CurrentRealm =>
            currentTier <= 9  ? MasteryRealm.Xibalba :
            currentTier == 10 ? MasteryRealm.Middleworld :
                                MasteryRealm.Upperworld;

        /// <summary>Progress within current cosmological realm (0–1).</summary>
        public float RealmProgress =>
            CurrentRealm switch
            {
                MasteryRealm.Xibalba    => (currentTier - 1) / 8f,
                MasteryRealm.Middleworld => 1f,
                MasteryRealm.Upperworld => (currentTier - 11) / 12f,
                _                       => 0f,
            };

        /// <summary>
        /// Attempt to advance tier. Returns true if advancement occurred.
        /// Requires sustained mastery score above threshold for the current tier.
        /// </summary>
        public bool TryAdvance(float masteryScore)
        {
            if (currentTier >= 23) return false;
            float threshold = 0.30f + (currentTier * 0.025f); // harder to advance at higher tiers
            if (masteryScore >= threshold)
            {
                currentTier++;
                return true;
            }
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Popol Vuh creation phases — the warrior's remaking in the Maize Forge
    // -------------------------------------------------------------------------
    public enum CreationPhase
    {
        Mud,     // first attempt — falls apart, no form
        Wood,    // second attempt — has form but no soul, no memory
        Maize,   // final form — body and spirit unified
    }

    // -------------------------------------------------------------------------
    // Ball Court — the Hero Twins' arena (Popol Vuh)
    // -------------------------------------------------------------------------
    [Serializable]
    public class BallCourtRecord
    {
        public string courtId;
        public string courtName;           // "Court of Hunahpu", "Lower Court of Xibalba"

        [Header("Structure")]
        public float  courtLengthMeters;   // length of the stone alley
        public int    skullRackCount;      // carved skull racks along the walls
        public bool   hasRubberBall;       // the sacred rubber ball is present
        public bool   lordsWatch;          // the Lords of Xibalba observe (higher difficulty)

        [Header("Resonance")]
        public float  rhythmThreshold = 0.45f;    // flow + spin to play the game
        public float  victoryThreshold = 0.70f;   // sustained mastery to defeat the lords
        public float  sustainDurationSeconds = 20f;
        public string mythTrigger = "martial";
        public string victoryMythTrigger = "elder";
        public VibrationalField frozenField = new VibrationalField();

        [Header("Mastery")]
        public int    minMasteryTier = 1;
        public float  masteryReward = 0.12f;       // how much mastery score advances on victory
    }

    // -------------------------------------------------------------------------
    // Dojo of Still Water — Kung Fu, Karate, hand-to-hand. Chaak's rain.
    // -------------------------------------------------------------------------
    [Serializable]
    public class DojoRecord
    {
        public string dojoId;
        public string dojoName;            // "Dojo of Still Water", "Rain Temple"

        [Header("Structure")]
        public string discipline;          // "kung_fu", "karate", "hand_to_hand"
        public bool   chaakRainFalls;      // Chaak's perpetual rain — blue god's tears
        public float  rainIntensity;       // 0–1, drives blue band amplification

        [Header("Resonance")]
        public float  discoveryThreshold = 0.40f;
        public float  innerMasteryThreshold = 0.65f;
        public string mythTrigger = "martial";
        public VibrationalField frozenField = new VibrationalField();

        [Header("Chaak's Gift")]
        [Tooltip("When Chaak's rain falls, blue band is amplified by this per tick.")]
        public float  blueAmplification = 0.03f;
        [Tooltip("Calm-under-rain bonus — if player maintains calm while rain intensifies.")]
        public float  calmBonusThreshold = 0.55f;

        [Header("Mastery")]
        public int    minMasteryTier = 1;
        public float  masteryReward = 0.08f;
    }

    // -------------------------------------------------------------------------
    // Shadow Garden — Ninja arts. The darkness before the sun.
    // -------------------------------------------------------------------------
    [Serializable]
    public class ShadowGardenRecord
    {
        public string gardenId;
        public string gardenName;          // "Garden of No Moon", "Obsidian Path"

        [Header("Structure")]
        public float  darknessLevel;       // 0–1, how deep the shadow is
        public bool   hasSilverWater;      // moonlit reflective pools
        public bool   hasBlackStone;       // obsidian walkways

        [Header("Resonance")]
        [Tooltip("Shadow arts reward low distortion + high flow — moving without disturbance.")]
        public float  discoveryThreshold = 0.45f;
        public float  maxDistortionAllowed = 0.20f;  // must stay below this
        public float  minFlowRequired = 0.40f;        // must maintain this movement flow
        public string mythTrigger = "martial";
        public VibrationalField frozenField = new VibrationalField();

        [Header("Mastery")]
        public int    minMasteryTier = 3;
        public float  masteryReward = 0.10f;
    }

    // -------------------------------------------------------------------------
    // Blade Pavilion — Fencing, sword fighting. Precision.
    // -------------------------------------------------------------------------
    [Serializable]
    public class BladePavilionRecord
    {
        public string pavilionId;
        public string pavilionName;        // "Pavilion of Crossed Steel", "Wind Edge"

        [Header("Structure")]
        public string bladeStyle;          // "fencing", "katana", "broadsword", "dual_blade"
        public bool   hasWindCurrents;     // open-air, wind affects blade paths
        public int    trainingDummyCount;

        [Header("Resonance")]
        [Tooltip("Blade work rewards spin stability and will — precise, intentional movement.")]
        public float  discoveryThreshold = 0.45f;
        public float  precisionThreshold = 0.65f;    // spinStability required for inner mastery
        public string mythTrigger = "martial";
        public VibrationalField frozenField = new VibrationalField();

        [Header("Mastery")]
        public int    minMasteryTier = 2;
        public float  masteryReward = 0.10f;
    }

    // -------------------------------------------------------------------------
    // Krishna's Field (Kurukshetra) — the deepest martial teaching
    // -------------------------------------------------------------------------
    [Serializable]
    public class KrishnaFieldRecord
    {
        public string fieldId;
        public string fieldName;           // "Kurukshetra", "The Golden Field"

        [Header("Structure")]
        public bool   isEndlessField;      // feels infinite — golden grass, open sky
        public bool   krishnaPresent;      // the teacher appears when sourceAlignment is high

        [Header("Resonance")]
        [Tooltip("Krishna's teaching requires sourceAlignment + courage (spin + calm).")]
        public float  discoveryThreshold = 0.55f;
        public float  teachingThreshold = 0.75f;     // sourceAlignment needed for Krishna to speak
        public string discoveryMythTrigger = "martial";
        public string teachingMythTrigger = "source";  // this is the deepest — touches Source
        public VibrationalField frozenField = new VibrationalField();

        [Header("Mastery")]
        public int    minMasteryTier = 10;  // requires Middleworld or above
        public float  masteryReward = 0.15f;

        [Header("Whisper")]
        [TextArea(2, 4)]
        public List<string> krishnaWhispers = new List<string>();
        // "You grieve for those who should not be grieved for."
        // "The soul is neither born, nor does it die."
        // "Fight, Arjuna. Not because you want to. Because it is yours to do."
    }

    // -------------------------------------------------------------------------
    // Maize Forge — where warriors are unmade and remade (Popol Vuh creation)
    // -------------------------------------------------------------------------
    [Serializable]
    public class MaizeForgeRecord
    {
        public string forgeId;
        public string forgeName;           // "The Maize Forge", "Crucible of Four Creations"

        [Header("Structure")]
        public CreationPhase currentPhase;
        public bool   hasMudPit;           // first creation — falls apart
        public bool   hasWoodFrame;        // second creation — no soul
        public bool   hasMaizePaste;       // final creation — body and spirit

        [Header("Resonance")]
        [Tooltip("Each creation phase has escalating thresholds.")]
        public float  mudPhaseThreshold = 0.35f;
        public float  woodPhaseThreshold = 0.50f;
        public float  maizePhaseThreshold = 0.70f;
        public float  sustainDurationSeconds = 25f;
        public string phaseMythTrigger = "martial";
        public string completionMythTrigger = "elder";
        public VibrationalField frozenField = new VibrationalField();

        [Header("Mastery")]
        public int    minMasteryTier = 5;
        public float  masteryReward = 0.18f;  // highest reward — you survived all three creations
    }

    // -------------------------------------------------------------------------
    // A martial world zone — one area of the MartialCrossing planet
    // -------------------------------------------------------------------------
    [Serializable]
    public class MartialWorldZone
    {
        public string zoneId;
        public string zoneName;
        public string element = "Martial";
        public string martialTier;         // "ball_court", "dojo", "shadow", "blade", "field", "forge"

        public List<BallCourtRecord>       ballCourts     = new List<BallCourtRecord>();
        public List<DojoRecord>            dojos          = new List<DojoRecord>();
        public List<ShadowGardenRecord>    shadowGardens  = new List<ShadowGardenRecord>();
        public List<BladePavilionRecord>   bladePavilions = new List<BladePavilionRecord>();
        public List<KrishnaFieldRecord>    krishnaFields  = new List<KrishnaFieldRecord>();
        public List<MaizeForgeRecord>      maizeForges    = new List<MaizeForgeRecord>();
    }

    // -------------------------------------------------------------------------
    // Root data container — loaded from martial_world_data.json
    // -------------------------------------------------------------------------
    [Serializable]
    public class MartialWorldData
    {
        public List<MartialWorldZone> zones = new List<MartialWorldZone>();

        public MartialWorldZone GetZone(string id)
        {
            foreach (var z in zones)
                if (z.zoneId == id) return z;
            return null;
        }
    }
}
