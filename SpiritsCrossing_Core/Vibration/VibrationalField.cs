// SpiritsCrossing — VibrationalField.cs
// Core vibrational field type. The fundamental unit of resonance in the game.
//
// Every entity — player, animal, spirit, planet, realm — has a 7-band
// vibrational field [red, orange, yellow, green, blue, indigo, violet].
//
// Harmony between two fields is computed as cosine phase similarity:
//   harmony = (1/7) Σ_i (cos((A_i - B_i) * π) + 1) / 2
//
// This mirrors the Kuramoto phase coupling in the brain model:
//   - 1.0  = perfect alignment  (constructive resonance)
//   - 0.5  = orthogonal         (neutral)
//   - 0.0  = perfect opposition (destructive resonance)
//
// The key property: harmony changes smoothly and continuously.
// There are no thresholds or boolean triggers — only the flowing
// degree of vibrational coherence between two fields.

using System;
using UnityEngine;
using SpiritsCrossing.SpiritAI;

namespace SpiritsCrossing.Vibration
{
    [Serializable]
    public class VibrationalField
    {
        // Band order matches RBE-1: red=0, orange=1, yellow=2, green=3, blue=4, indigo=5, violet=6
        public float red;
        public float orange;
        public float yellow;
        public float green;
        public float blue;
        public float indigo;
        public float violet;

        public VibrationalField() { }

        public VibrationalField(float r, float o, float y, float g, float b, float i, float v)
        {
            red = r; orange = o; yellow = y; green = g; blue = b; indigo = i; violet = v;
        }

        // -------------------------------------------------------------------------
        // Band access
        // -------------------------------------------------------------------------
        public float GetBand(int index) => index switch
        {
            0 => red, 1 => orange, 2 => yellow, 3 => green,
            4 => blue, 5 => indigo, 6 => violet, _ => 0f
        };

        public void SetBand(int index, float value)
        {
            switch (index)
            {
                case 0: red    = value; break; case 1: orange = value; break;
                case 2: yellow = value; break; case 3: green  = value; break;
                case 4: blue   = value; break; case 5: indigo = value; break;
                case 6: violet = value; break;
            }
        }

        public string DominantBandName()
        {
            float best = 0f; string key = "green";
            Check("red",    red);    Check("orange", orange); Check("yellow", yellow);
            Check("green",  green);  Check("blue",   blue);   Check("indigo", indigo);
            Check("violet", violet);
            return key;
            void Check(string k, float v) { if (v > best) { best = v; key = k; } }
        }

        // -------------------------------------------------------------------------
        // Harmony — cosine phase similarity (0=opposed, 0.5=neutral, 1=aligned)
        //
        //   harmony_i = (cos((a_i - b_i) * π) + 1) / 2
        //
        // Using π-scaled cosine means:
        //   identical values → cos(0)=1 → harmony=1.0
        //   opposite values  → cos(π)=-1 → harmony=0.0
        //   half-step apart  → cos(π/2)=0 → harmony=0.5
        // -------------------------------------------------------------------------
        public float Harmony(VibrationalField other)
        {
            float sum = 0f;
            for (int i = 0; i < 7; i++)
            {
                float diff  = (GetBand(i) - other.GetBand(i)) * Mathf.PI;
                sum += (Mathf.Cos(diff) + 1f) * 0.5f;
            }
            return sum / 7f;
        }

        /// <summary>
        /// Weighted harmony — bands with higher amplitude in the reference field
        /// contribute more to the score. Rewards players who are strong in the
        /// animal's characteristic frequencies.
        /// </summary>
        public float WeightedHarmony(VibrationalField other)
        {
            float weightedSum = 0f;
            float totalWeight = 0f;
            for (int i = 0; i < 7; i++)
            {
                float weight = other.GetBand(i);           // animal's characteristic band
                float diff   = (GetBand(i) - other.GetBand(i)) * Mathf.PI;
                weightedSum  += weight * (Mathf.Cos(diff) + 1f) * 0.5f;
                totalWeight  += weight;
            }
            return totalWeight > 0f ? weightedSum / totalWeight : Harmony(other);
        }

        // -------------------------------------------------------------------------
        // Coherence — Kuramoto order parameter of this field's own bands
        // r = |mean(exp(iθ))| where θ_k = π * band_k
        // High coherence = bands are internally aligned.
        // -------------------------------------------------------------------------
        public float Coherence()
        {
            float cosSum = 0f, sinSum = 0f;
            for (int i = 0; i < 7; i++)
            {
                float phase = GetBand(i) * Mathf.PI;
                cosSum += Mathf.Cos(phase);
                sinSum += Mathf.Sin(phase);
            }
            return Mathf.Sqrt(cosSum * cosSum + sinSum * sinSum) / 7f;
        }

        // -------------------------------------------------------------------------
        // Source alignment — how much this field resonates with the violet-indigo
        // (Source) region of the spectrum.
        // -------------------------------------------------------------------------
        public float SourceAlignment() =>
            Mathf.Clamp01(violet * 0.55f + indigo * 0.35f + green * 0.10f);

        // -------------------------------------------------------------------------
        // Factory methods
        // -------------------------------------------------------------------------

        /// <summary>
        /// Map a live PlayerResonanceState to a 7-band vibrational field.
        /// Each band corresponds to a physiological/emotional quality.
        /// </summary>
        public static VibrationalField FromResonanceState(PlayerResonanceState s)
        {
            return new VibrationalField(
                red:    Mathf.Clamp01(s.distortion   * 0.60f + (1f - s.calm)      * 0.40f), // heat / intensity
                orange: Mathf.Clamp01(s.socialSync   * 0.60f + s.joy              * 0.40f), // social warmth
                yellow: Mathf.Clamp01(s.joy          * 0.60f + s.movementFlow     * 0.40f), // vitality / radiance
                green:  Mathf.Clamp01(s.calm         * 0.60f + s.breathCoherence  * 0.40f), // heart / peace
                blue:   Mathf.Clamp01(s.movementFlow * 0.55f + s.spinStability    * 0.45f), // flow / motion
                indigo: Mathf.Clamp01(s.socialSync   * 0.55f + s.sourceAlignment  * 0.45f), // deep resonance
                violet: Mathf.Clamp01(s.wonder       * 0.60f + s.sourceAlignment  * 0.40f)  // mystery / source
            );
        }

        /// <summary>
        /// Build from a spirit spectral signature (loaded from spirit_profiles.json).
        /// This means every animal's vibration IS its RBE-1 equilibrium state.
        /// </summary>
        public static VibrationalField FromSpectralSignature(SpectralSignature sig)
        {
            return new VibrationalField(
                sig.red, sig.orange, sig.yellow, sig.green, sig.blue, sig.indigo, sig.violet);
        }

        /// <summary>
        /// Build from a planet spectral signature (loaded from cosmos_data.json).
        /// </summary>
        public static VibrationalField FromPlanetSpectral(Cosmos.PlanetSpectralSignature sig)
        {
            return new VibrationalField(
                sig.red, sig.orange, sig.yellow, sig.green, sig.blue, sig.indigo, sig.violet);
        }

        /// <summary>
        /// Natural elemental vibrational field — used when no specific profile is loaded.
        /// </summary>
        public static VibrationalField NaturalAffinity(string element) => element switch
        {
            "Fire"   => new VibrationalField(0.80f, 0.65f, 0.50f, 0.12f, 0.72f, 0.20f, 0.78f),
            "Earth"  => new VibrationalField(0.05f, 0.60f, 0.45f, 0.90f, 0.10f, 0.35f, 0.12f),
            "Water"  => new VibrationalField(0.08f, 0.20f, 0.30f, 0.50f, 0.85f, 0.70f, 0.25f),
            "Air"    => new VibrationalField(0.25f, 0.30f, 0.40f, 0.65f, 0.78f, 0.45f, 0.72f),
            "Source" => new VibrationalField(0.12f, 0.15f, 0.28f, 0.55f, 0.40f, 0.80f, 0.85f),
            _        => new VibrationalField(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f)
        };

        // -------------------------------------------------------------------------
        // Math helpers
        // -------------------------------------------------------------------------

        public static VibrationalField Lerp(VibrationalField a, VibrationalField b, float t)
        {
            t = Mathf.Clamp01(t);
            return new VibrationalField(
                Mathf.Lerp(a.red,    b.red,    t),
                Mathf.Lerp(a.orange, b.orange, t),
                Mathf.Lerp(a.yellow, b.yellow, t),
                Mathf.Lerp(a.green,  b.green,  t),
                Mathf.Lerp(a.blue,   b.blue,   t),
                Mathf.Lerp(a.indigo, b.indigo, t),
                Mathf.Lerp(a.violet, b.violet, t));
        }

        public void LerpToward(VibrationalField target, float t)
        {
            t      = Mathf.Clamp01(t);
            red    = Mathf.Lerp(red,    target.red,    t);
            orange = Mathf.Lerp(orange, target.orange, t);
            yellow = Mathf.Lerp(yellow, target.yellow, t);
            green  = Mathf.Lerp(green,  target.green,  t);
            blue   = Mathf.Lerp(blue,   target.blue,   t);
            indigo = Mathf.Lerp(indigo, target.indigo, t);
            violet = Mathf.Lerp(violet, target.violet, t);
        }

        public void Clamp01()
        {
            red    = Mathf.Clamp01(red);
            orange = Mathf.Clamp01(orange);
            yellow = Mathf.Clamp01(yellow);
            green  = Mathf.Clamp01(green);
            blue   = Mathf.Clamp01(blue);
            indigo = Mathf.Clamp01(indigo);
            violet = Mathf.Clamp01(violet);
        }

        public override string ToString() =>
            $"[R={red:F2} O={orange:F2} Y={yellow:F2} G={green:F2} B={blue:F2} I={indigo:F2} V={violet:F2}]";
    }
}
