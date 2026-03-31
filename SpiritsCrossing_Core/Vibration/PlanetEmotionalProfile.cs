// SpiritsCrossing — PlanetEmotionalProfile.cs
// Each planet has a characteristic emotional resonance profile that shapes
// how the player's emotional spectrum is modulated while on that world.
//
// The planet doesn't FORCE emotions — it ENCOURAGES them.
// ForestHeart nurtures peace and stillness.
// DarkContrast ignites fierce joy and source intensity.
// SourceVeil deepens source connection and peace.
//
// Modulation is additive-multiplicative: the planet gently amplifies
// bands that match its character and slightly dampens bands that don't,
// without ever zeroing anything out. The player always retains their
// own emotional shape — the planet just colors it.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SpiritsCrossing.Vibration
{
    // -------------------------------------------------------------------------
    // Per-band weight: how strongly the planet resonates with each emotional band
    // -------------------------------------------------------------------------
    [Serializable]
    public class EmotionalWeights
    {
        [Range(0f, 2f)] public float stillness = 1f;
        [Range(0f, 2f)] public float peace     = 1f;
        [Range(0f, 2f)] public float joy       = 1f;
        [Range(0f, 2f)] public float love      = 1f;
        [Range(0f, 2f)] public float source    = 1f;

        public float GetWeight(int bandIndex) => bandIndex switch
        {
            0 => stillness, 1 => peace, 2 => joy, 3 => love, 4 => source, _ => 1f
        };
    }

    // -------------------------------------------------------------------------
    // Single planet profile
    // -------------------------------------------------------------------------
    [Serializable]
    public class PlanetEmotionalProfile
    {
        public string           planetId;
        public string           element;
        public string           description;
        public EmotionalWeights weights = new EmotionalWeights();

        [Tooltip("How intensely this planet modulates the player's spectrum. " +
                 "0 = no effect (neutral), 1 = full modulation.")]
        [Range(0f, 1f)] public float modulationStrength = 0.5f;

        [Tooltip("Modulation style: 'gentle' lerps softly, 'intense' pushes harder.")]
        public string modulationStyle = "gentle";

        /// <summary>
        /// Modulate a player's emotional spectrum based on this planet's character.
        /// Returns a NEW spectrum — does not modify the input.
        ///
        /// Algorithm:
        ///   For each band, the planet's weight acts as a multiplier:
        ///     weight > 1.0 → amplifies that band
        ///     weight < 1.0 → dampens that band
        ///     weight = 1.0 → no change
        ///
        ///   The modulationStrength controls how much the planet's influence
        ///   blends with the player's raw spectrum:
        ///     result = lerp(raw, modulated, modulationStrength)
        ///
        ///   "gentle" style uses sqrt(modulationStrength) for a softer curve.
        ///   "intense" style uses modulationStrength² for a snappier push.
        /// </summary>
        public EmotionalResonanceSpectrum ModulateSpectrum(EmotionalResonanceSpectrum raw)
        {
            float strength = modulationStyle switch
            {
                "gentle"  => Mathf.Sqrt(modulationStrength),
                "intense" => modulationStrength * modulationStrength,
                _         => modulationStrength
            };

            var modulated = new EmotionalResonanceSpectrum();
            for (int i = 0; i < EmotionalResonanceSpectrum.BandCount; i++)
            {
                float rawBand  = raw.GetBand(i);
                float weight   = weights.GetWeight(i);

                // Multiplicative modulation — planet amplifies/dampens
                float pushed = Mathf.Clamp01(rawBand * weight);

                // Blend between raw and modulated based on planet strength
                float result = Mathf.Lerp(rawBand, pushed, strength);
                modulated.SetBand(i, result);
            }

            return modulated;
        }

        /// <summary>
        /// How well the player's emotional spectrum aligns with this planet's character.
        /// Weighted dot product of the player's bands against the planet's weights.
        /// Returns 0–1: 0 = no alignment, 1 = perfect alignment.
        /// </summary>
        public float AlignmentScore(EmotionalResonanceSpectrum spectrum)
        {
            float dot   = 0f;
            float total = 0f;
            for (int i = 0; i < EmotionalResonanceSpectrum.BandCount; i++)
            {
                float w = weights.GetWeight(i);
                dot   += spectrum.GetBand(i) * w;
                total += w;
            }
            return total > 0f ? Mathf.Clamp01(dot / total) : 0f;
        }
    }

    // -------------------------------------------------------------------------
    // Collection (matches planet_emotional_profiles.json)
    // -------------------------------------------------------------------------
    [Serializable]
    public class PlanetEmotionalProfileCollection
    {
        public List<PlanetEmotionalProfile> profiles = new List<PlanetEmotionalProfile>();

        public PlanetEmotionalProfile GetProfile(string planetId)
        {
            foreach (var p in profiles)
                if (p.planetId == planetId) return p;
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Loader utility
    // -------------------------------------------------------------------------
    public static class PlanetEmotionalProfileLoader
    {
        private static PlanetEmotionalProfileCollection _cached;

        public static PlanetEmotionalProfileCollection Load(
            string fileName = "planet_emotional_profiles.json")
        {
            if (_cached != null) return _cached;

            string path = Path.Combine(Application.streamingAssetsPath, fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[PlanetEmotionalProfile] {fileName} not found in StreamingAssets.");
                return new PlanetEmotionalProfileCollection();
            }

            try
            {
                string json = File.ReadAllText(path);
                _cached = JsonUtility.FromJson<PlanetEmotionalProfileCollection>(json);
                Debug.Log($"[PlanetEmotionalProfile] Loaded {_cached.profiles.Count} planet emotional profiles.");
                return _cached;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlanetEmotionalProfile] Load error: {e.Message}");
                return new PlanetEmotionalProfileCollection();
            }
        }

        public static void ClearCache() => _cached = null;
    }
}
