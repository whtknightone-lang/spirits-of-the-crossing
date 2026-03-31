// SpiritsCrossing — TerrainResonance.cs
// Shared terrain types and vibrational signatures for the Cosmic Terrain System.
//
// TERRAIN AS TEACHER
//
//   Every terrain type carries an innate vibrational signature. The landscape
//   is not decoration — it is a field. Caves amplify indigo (introspection).
//   Mountains amplify violet (vision). Rivers amplify green (flow).
//
//   When a player or NPC enters a terrain region, two things happen:
//     1. RESONANCE PULL — the terrain's primary bands harmonize with the being's
//        current field. High harmony = the being feels drawn there naturally.
//     2. INTRODUCTION   — the terrain subtly amplifies secondary bands the being
//        is weak in. This is how terrain teaches: caves introduce violet through
//        the safety of indigo. Mountains introduce red through the height of violet.
//
//   Over time, beings are guided across the full spectrum of the cosmos — not by
//   rules or waypoints, but by the natural pull of resonance with the land itself.
//
// TERRAIN TYPES (13)
//
//   Cave         — indigo + violet     (introspection, depth, stillness)
//   Crag         — red + orange        (exposure, intensity, raw edge)
//   Fissure      — red + indigo        (the gap between, descent into the unknown)
//   Hill         — green + yellow      (gentle rise, openness, soft challenge)
//   Mountain     — violet + red        (vision from height, courage to climb)
//   River        — green + blue        (flow, heart, following the current)
//   Lake         — blue + green        (stillness on water, reflection, depth)
//   InlandOcean  — blue + violet       (vast inner depth, source-adjacent)
//   Valley       — green + orange      (shelter, warmth, protection)
//   Plateau      — yellow + violet     (open sky, clear seeing, freedom)
//   Ravine       — orange + indigo     (narrow passage, focused intensity)
//   Glacier      — blue + indigo       (frozen time, patience, ancient memory)
//   Caldera      — red + violet        (fire's crown, transformation at the peak)

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing
{
    // -------------------------------------------------------------------------
    // Terrain type enumeration — the bones of every world
    // -------------------------------------------------------------------------
    public enum TerrainType
    {
        Cave,         // 0
        Crag,         // 1
        Fissure,      // 2
        Hill,         // 3
        Mountain,     // 4
        River,        // 5
        Lake,         // 6
        InlandOcean,  // 7
        Valley,       // 8
        Plateau,      // 9
        Ravine,       // 10
        Glacier,      // 11
        Caldera,      // 12
    }

    public static class TerrainTypeUtil
    {
        public const int Count = 13;

        public static readonly string[] Names =
        {
            "Cave", "Crag", "Fissure", "Hill", "Mountain",
            "River", "Lake", "InlandOcean", "Valley", "Plateau",
            "Ravine", "Glacier", "Caldera",
        };
    }

    // -------------------------------------------------------------------------
    // TerrainSignature — the innate vibrational profile of each terrain type.
    // These are canonical: a cave is a cave on every planet. The planet's
    // element tints the field, but the terrain's shape is universal.
    // -------------------------------------------------------------------------
    public static class TerrainSignature
    {
        /// <summary>
        /// Return the canonical vibrational field for a terrain type.
        /// Primary bands are at 0.70, secondary at 0.40, all others at 0.15.
        /// This gives every terrain a clear character without being binary.
        /// </summary>
        public static VibrationalField ForType(TerrainType type) => type switch
        {
            //                                    red    orange yellow green  blue   indigo violet
            TerrainType.Cave        => new VibrationalField(0.15f, 0.15f, 0.15f, 0.15f, 0.30f, 0.70f, 0.65f),
            TerrainType.Crag        => new VibrationalField(0.70f, 0.60f, 0.30f, 0.15f, 0.15f, 0.15f, 0.15f),
            TerrainType.Fissure     => new VibrationalField(0.65f, 0.25f, 0.15f, 0.15f, 0.15f, 0.60f, 0.30f),
            TerrainType.Hill        => new VibrationalField(0.15f, 0.25f, 0.60f, 0.70f, 0.30f, 0.15f, 0.15f),
            TerrainType.Mountain    => new VibrationalField(0.55f, 0.25f, 0.30f, 0.15f, 0.15f, 0.30f, 0.70f),
            TerrainType.River       => new VibrationalField(0.15f, 0.15f, 0.25f, 0.65f, 0.60f, 0.25f, 0.15f),
            TerrainType.Lake        => new VibrationalField(0.15f, 0.15f, 0.15f, 0.55f, 0.70f, 0.30f, 0.25f),
            TerrainType.InlandOcean => new VibrationalField(0.15f, 0.15f, 0.15f, 0.25f, 0.70f, 0.40f, 0.60f),
            TerrainType.Valley      => new VibrationalField(0.15f, 0.55f, 0.30f, 0.70f, 0.25f, 0.15f, 0.15f),
            TerrainType.Plateau     => new VibrationalField(0.15f, 0.15f, 0.65f, 0.25f, 0.15f, 0.25f, 0.60f),
            TerrainType.Ravine      => new VibrationalField(0.30f, 0.65f, 0.25f, 0.15f, 0.15f, 0.55f, 0.15f),
            TerrainType.Glacier     => new VibrationalField(0.15f, 0.15f, 0.15f, 0.15f, 0.65f, 0.60f, 0.25f),
            TerrainType.Caldera     => new VibrationalField(0.70f, 0.40f, 0.25f, 0.15f, 0.15f, 0.25f, 0.65f),
            _                       => new VibrationalField(0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f),
        };

        /// <summary>
        /// Introduction bands — the secondary resonance bands that terrain
        /// subtly amplifies when a being is present. This is how terrain teaches:
        /// it exposes beings to bands they're weak in, through the safety of
        /// bands they're strong in.
        ///
        /// Returns (bandName, amplification) pairs. Typically 1–2 bands
        /// at low amplification (0.01–0.03 per tick).
        /// </summary>
        public static (string band, float amp)[] IntroductionBands(TerrainType type) => type switch
        {
            TerrainType.Cave        => new[] { ("violet", 0.02f), ("blue", 0.01f) },
            TerrainType.Crag        => new[] { ("yellow", 0.02f) },
            TerrainType.Fissure     => new[] { ("violet", 0.015f), ("orange", 0.01f) },
            TerrainType.Hill        => new[] { ("orange", 0.015f), ("blue", 0.01f) },
            TerrainType.Mountain    => new[] { ("red", 0.02f), ("indigo", 0.01f) },
            TerrainType.River       => new[] { ("indigo", 0.015f), ("yellow", 0.01f) },
            TerrainType.Lake        => new[] { ("violet", 0.015f), ("green", 0.01f) },
            TerrainType.InlandOcean => new[] { ("violet", 0.02f), ("indigo", 0.015f) },
            TerrainType.Valley      => new[] { ("blue", 0.015f), ("indigo", 0.01f) },
            TerrainType.Plateau     => new[] { ("red", 0.015f), ("green", 0.01f) },
            TerrainType.Ravine      => new[] { ("violet", 0.015f), ("green", 0.01f) },
            TerrainType.Glacier     => new[] { ("violet", 0.02f), ("red", 0.01f) },
            TerrainType.Caldera     => new[] { ("indigo", 0.02f), ("green", 0.01f) },
            _                       => Array.Empty<(string, float)>(),
        };
    }

    // -------------------------------------------------------------------------
    // A terrain region — one geographic area within a world.
    // Loaded from planet world data or generated procedurally.
    // -------------------------------------------------------------------------
    [Serializable]
    public class TerrainRegion
    {
        public string      regionId;
        public string      regionName;       // "Indigo Caves", "Razor Crag", "Source Lake"
        public TerrainType terrainType;
        public string      planetId;
        public Vector3     worldPosition;
        public float       radius = 30f;     // world-space radius of influence

        [Tooltip("Element tint — planet element that modifies the canonical terrain signature.")]
        public string      elementTint;      // "Air", "Earth", "Fire", "Water", "Source", "Martial"

        [Tooltip("How strongly the terrain field radiates (0–1). Larger/deeper formations radiate more.")]
        [Range(0f, 1f)]
        public float       fieldStrength = 0.60f;

        [Tooltip("Myth trigger activated when a being first resonates with this terrain.")]
        public string      mythTrigger;

        [TextArea(2, 4)]
        public string      description;

        /// <summary>
        /// Compute the terrain's effective vibrational field, tinted by the planet's element.
        /// The canonical terrain signature is blended with the element affinity.
        /// </summary>
        public VibrationalField EffectiveField()
        {
            var canonical = TerrainSignature.ForType(terrainType);
            if (string.IsNullOrEmpty(elementTint)) return canonical;

            var tint = VibrationalField.NaturalAffinity(elementTint);
            var result = new VibrationalField(
                canonical.red,    canonical.orange, canonical.yellow,
                canonical.green,  canonical.blue,   canonical.indigo,
                canonical.violet);

            // Subtle tint — 20% blend toward the planet's element
            result.LerpToward(tint, 0.20f);
            result.Clamp01();
            return result;
        }
    }

    // -------------------------------------------------------------------------
    // Result of computing terrain pull for a being
    // -------------------------------------------------------------------------
    [Serializable]
    public struct TerrainPullResult
    {
        public string      regionId;
        public TerrainType terrainType;
        public float       pullScore;        // 0–1, harmony between being and terrain
        public float       distance;         // world-space distance from being to region center
        public bool        isWithinRadius;   // within the terrain's influence radius

        /// <summary>
        /// Effective pull: harmony weighted by proximity.
        /// Full pull within the radius, fading linearly to 0 at 2x radius.
        /// </summary>
        public float EffectivePull =>
            isWithinRadius ? pullScore :
            (distance < radius * 2f) ? pullScore * (1f - (distance - radius) / radius) : 0f;

        private float radius; // set during construction
        public TerrainPullResult(string id, TerrainType type, float pull, float dist, float regionRadius)
        {
            regionId       = id;
            terrainType    = type;
            pullScore      = pull;
            distance       = dist;
            radius         = regionRadius;
            isWithinRadius = dist <= regionRadius;
        }
    }
}
