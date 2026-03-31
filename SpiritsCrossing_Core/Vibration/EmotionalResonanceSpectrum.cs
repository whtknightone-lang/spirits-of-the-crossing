// SpiritsCrossing — EmotionalResonanceSpectrum.cs
// The unified emotional-spiritual spectrum.
//
// Five simultaneous continuous bands:
//   Stillness  — meditation depth, breath coherence, calm. The ground.
//   Peace      — sustained calm + low distortion + source alignment. The settling.
//   Joy        — movement joy, wonder, playful engagement. The rising.
//   Love       — social sync + calm + source alignment held together. The opening.
//   Source     — wonder + sourceAlignment + breathCoherence at depth. The dissolution.
//
// These are NOT a ladder — they are a living shape. A player can hold
// high Stillness AND high Joy (meditative dance) simultaneously.
// The spectrum is computed from live PlayerResonanceState every frame
// and propagated across all planets and worlds.

using System;
using UnityEngine;
using SpiritsCrossing.SpiritAI;

namespace SpiritsCrossing.Vibration
{
    [Serializable]
    public class EmotionalResonanceSpectrum
    {
        // -----------------------------------------------------------------
        // Five bands (all 0–1, simultaneous, continuous)
        // -----------------------------------------------------------------
        [Range(0f, 1f)] public float stillness;
        [Range(0f, 1f)] public float peace;
        [Range(0f, 1f)] public float joy;
        [Range(0f, 1f)] public float love;
        [Range(0f, 1f)] public float source;

        // -----------------------------------------------------------------
        // Constructors
        // -----------------------------------------------------------------
        public EmotionalResonanceSpectrum() { }

        public EmotionalResonanceSpectrum(float stillness, float peace, float joy, float love, float source)
        {
            this.stillness = stillness;
            this.peace     = peace;
            this.joy       = joy;
            this.love      = love;
            this.source    = source;
        }

        // -----------------------------------------------------------------
        // Band access by index / name
        // -----------------------------------------------------------------
        public const int BandCount = 5;

        public float GetBand(int index) => index switch
        {
            0 => stillness, 1 => peace, 2 => joy, 3 => love, 4 => source, _ => 0f
        };

        public void SetBand(int index, float value)
        {
            switch (index)
            {
                case 0: stillness = value; break;
                case 1: peace     = value; break;
                case 2: joy       = value; break;
                case 3: love      = value; break;
                case 4: source    = value; break;
            }
        }

        public float GetBand(string name) => name switch
        {
            "stillness" => stillness,
            "peace"     => peace,
            "joy"       => joy,
            "love"      => love,
            "source"    => source,
            _           => 0f
        };

        public static string BandName(int index) => index switch
        {
            0 => "stillness", 1 => "peace", 2 => "joy", 3 => "love", 4 => "source",
            _ => "unknown"
        };

        // -----------------------------------------------------------------
        // Computed properties
        // -----------------------------------------------------------------

        /// <summary>
        /// Breadth of practice — average activation across all 5 bands.
        /// 0 = nothing active, 1 = all bands fully lit.
        /// </summary>
        public float Fullness =>
            (stillness + peace + joy + love + source) / (float)BandCount;

        /// <summary>
        /// Spiritual depth — weighted toward the higher bands.
        /// Stillness grounds but doesn't deepen the score as much.
        /// Source carries the heaviest weight.
        /// </summary>
        public float Depth
        {
            get
            {
                // Weights: stillness=0.5, peace=0.75, joy=1.0, love=1.25, source=1.5
                float weighted = stillness * 0.50f +
                                 peace     * 0.75f +
                                 joy       * 1.00f +
                                 love      * 1.25f +
                                 source    * 1.50f;
                // Normalize by max possible (5.0)
                return Mathf.Clamp01(weighted / 5.0f);
            }
        }

        /// <summary>
        /// Integration — Kuramoto-style phase coherence across the 5 bands.
        /// High coherence = all bands are at similar levels (integrated practice).
        /// Computed as 1 - normalized variance.
        /// </summary>
        public float Coherence
        {
            get
            {
                float mean = Fullness;
                float variance = 0f;
                for (int i = 0; i < BandCount; i++)
                {
                    float diff = GetBand(i) - mean;
                    variance += diff * diff;
                }
                variance /= BandCount;
                // Max variance when one band is 1 and rest are 0 ≈ 0.16
                // Normalize so 0 variance → coherence 1.0
                return Mathf.Clamp01(1f - variance / 0.20f);
            }
        }

        /// <summary>
        /// Index of the currently dominant (strongest) band.
        /// </summary>
        public int DominantBandIndex
        {
            get
            {
                int best = 0;
                float bestVal = stillness;
                if (peace  > bestVal) { bestVal = peace;  best = 1; }
                if (joy    > bestVal) { bestVal = joy;    best = 2; }
                if (love   > bestVal) { bestVal = love;   best = 3; }
                if (source > bestVal) { bestVal = source; best = 4; }
                return best;
            }
        }

        /// <summary>Name of the currently dominant band.</summary>
        public string DominantBandName => BandName(DominantBandIndex);

        /// <summary>
        /// True when all 5 bands are simultaneously above the given threshold.
        /// Default 0.3 — this is rare and powerful.
        /// </summary>
        public bool IsFullSpectrum(float threshold = 0.3f) =>
            stillness >= threshold &&
            peace     >= threshold &&
            joy       >= threshold &&
            love      >= threshold &&
            source    >= threshold;

        // -----------------------------------------------------------------
        // Mapping to 7-band VibrationalField
        //
        //   Stillness → green (heart/peace) + indigo (deep resonance)
        //   Peace     → green + blue (flow/motion)
        //   Joy       → yellow (vitality) + orange (social warmth)
        //   Love      → orange + green + indigo
        //   Source    → violet (mystery) + indigo
        //
        // This lets the emotional spectrum propagate through ALL existing
        // vibrational infrastructure — companion harmony, portal resonance,
        // cave sculptures, planetary fields.
        // -----------------------------------------------------------------
        public VibrationalField ToVibrationalField()
        {
            return new VibrationalField(
                red:    Mathf.Clamp01(joy * 0.15f + love * 0.10f),                          // mild warmth from joy/love
                orange: Mathf.Clamp01(joy * 0.40f + love * 0.35f + peace * 0.10f),          // social warmth
                yellow: Mathf.Clamp01(joy * 0.55f + peace * 0.15f + stillness * 0.10f),     // vitality / radiance
                green:  Mathf.Clamp01(stillness * 0.40f + peace * 0.35f + love * 0.30f),    // heart / peace
                blue:   Mathf.Clamp01(peace * 0.35f + joy * 0.25f + stillness * 0.20f),     // flow / motion
                indigo: Mathf.Clamp01(love * 0.35f + source * 0.35f + stillness * 0.15f),   // deep resonance
                violet: Mathf.Clamp01(source * 0.55f + love * 0.20f + peace * 0.10f)        // mystery / source
            );
        }

        // -----------------------------------------------------------------
        // Factory: build from live PlayerResonanceState
        // -----------------------------------------------------------------
        public static EmotionalResonanceSpectrum FromPlayerResonanceState(PlayerResonanceState s)
        {
            // Stillness = meditation depth — breath coherence and calm
            float stillness = Mathf.Clamp01(
                s.breathCoherence * 0.50f +
                s.calm            * 0.40f +
                s.sourceAlignment * 0.10f);

            // Peace = sustained calm with low distortion and some source awareness
            float peace = Mathf.Clamp01(
                s.calm            * 0.45f +
                (1f - s.distortion) * 0.30f +
                s.sourceAlignment * 0.25f);

            // Joy = movement joy, wonder, playful engagement
            float joy = Mathf.Clamp01(
                s.joy          * 0.45f +
                s.wonder       * 0.25f +
                s.movementFlow * 0.30f);

            // Love = social sync + calm + source alignment held together
            // This is a multiplicative gate — all three must be present
            float loveBasis = Mathf.Clamp01(
                s.socialSync      * 0.40f +
                s.calm            * 0.30f +
                s.sourceAlignment * 0.30f);
            // Gentle multiplicative gating: if any core dimension is near zero, love dims
            float loveGate = Mathf.Clamp01(
                Mathf.Min(Mathf.Min(s.socialSync + 0.2f, s.calm + 0.2f), s.sourceAlignment + 0.2f));
            float love = loveBasis * loveGate;

            // Source = wonder + source alignment + breath coherence at depth
            // Also multiplicatively gated — requires multiple qualities at once
            float sourceBasis = Mathf.Clamp01(
                s.wonder          * 0.35f +
                s.sourceAlignment * 0.40f +
                s.breathCoherence * 0.25f);
            float sourceGate = Mathf.Clamp01(
                Mathf.Min(s.wonder + 0.15f, s.sourceAlignment + 0.15f));
            float source = sourceBasis * sourceGate;

            return new EmotionalResonanceSpectrum(stillness, peace, joy, love, source);
        }

        // -----------------------------------------------------------------
        // Math helpers
        // -----------------------------------------------------------------
        public void LerpToward(EmotionalResonanceSpectrum target, float t)
        {
            t         = Mathf.Clamp01(t);
            stillness = Mathf.Lerp(stillness, target.stillness, t);
            peace     = Mathf.Lerp(peace,     target.peace,     t);
            joy       = Mathf.Lerp(joy,       target.joy,       t);
            love      = Mathf.Lerp(love,      target.love,      t);
            source    = Mathf.Lerp(source,    target.source,    t);
        }

        public static EmotionalResonanceSpectrum Lerp(
            EmotionalResonanceSpectrum a, EmotionalResonanceSpectrum b, float t)
        {
            t = Mathf.Clamp01(t);
            return new EmotionalResonanceSpectrum(
                Mathf.Lerp(a.stillness, b.stillness, t),
                Mathf.Lerp(a.peace,     b.peace,     t),
                Mathf.Lerp(a.joy,       b.joy,       t),
                Mathf.Lerp(a.love,      b.love,      t),
                Mathf.Lerp(a.source,    b.source,    t));
        }

        public void Clamp01()
        {
            stillness = Mathf.Clamp01(stillness);
            peace     = Mathf.Clamp01(peace);
            joy       = Mathf.Clamp01(joy);
            love      = Mathf.Clamp01(love);
            source    = Mathf.Clamp01(source);
        }

        public override string ToString() =>
            $"[Still={stillness:F2} Peace={peace:F2} Joy={joy:F2} Love={love:F2} Source={source:F2}" +
            $" | Full={Fullness:F2} Depth={Depth:F2} Coh={Coherence:F2} Dom={DominantBandName}]";
    }
}
