// SpiritsCrossing — ResonanceHUD.cs
// Player-facing resonance display. Shows the living state of the player's
// vibrational field at all times.
//
// DISPLAY ELEMENTS
//
//   7 Band Bars        — one per ROYGBIV band, driven by PlayerField directly.
//                        They breathe with the field — no discrete tiers shown.
//                        Color matches band: red → violet spectrum.
//
//   Companion Arc      — a single arc showing the harmony of the highest-resonance
//                        companion currently active or nearby. Colored by element.
//
//   Ruin Proximity     — thin arc showing progress toward the nearest undiscovered
//                        ancient ruin (0–1). Pulses faintly when above 0.5.
//
//   Portal Glow        — subtle ambient glow whose color blends toward whichever
//                        portal is currently most revealed. Invisible at rest.
//
// UPDATE RATE
//   All values are read every 0.2 seconds and smoothly interpolated each frame
//   to avoid visual jitter from per-frame field fluctuations.
//
// WIRING
//   Assign the 7 band Image (UI Image with fillAmount) slots in the inspector.
//   Companion arc and ruin arc are also UI Image fill arcs.
//   Portal glow is a CanvasGroup or Image alpha.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.World;
using SpiritsCrossing.Runtime;

namespace SpiritsCrossing.UI
{
    public class ResonanceHUD : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector — band bars (assign UI Image components, use fillAmount)
        // -------------------------------------------------------------------------
        [Header("Band Bars (UI Image, fillAmount = band value)")]
        public Image barRed;
        public Image barOrange;
        public Image barYellow;
        public Image barGreen;
        public Image barBlue;
        public Image barIndigo;
        public Image barViolet;

        [Header("Companion Arc")]
        public Image companionArc;           // fill arc 0–1 = harmony score
        public Image companionElementIcon;   // optional element icon sprite

        [Header("Ruin Proximity Arc")]
        public Image ruinProximityArc;       // fill arc 0–1 = discovery progress
        public float ruinPulseSpeed = 1.5f;

        [Header("Portal Glow")]
        public Image portalGlowImage;        // alpha driven by highest portal reveal
        public float portalGlowMax = 0.35f;  // max alpha for glow

        [Header("Smoothing")]
        [Range(1f, 15f)] public float displaySmoothSpeed = 6f;
        public float dataRefreshInterval = 0.2f; // how often to read from systems

        [Header("Band Colors")]
        public Color colorRed    = new Color(0.90f, 0.15f, 0.10f);
        public Color colorOrange = new Color(0.95f, 0.45f, 0.10f);
        public Color colorYellow = new Color(0.95f, 0.88f, 0.15f);
        public Color colorGreen  = new Color(0.20f, 0.85f, 0.30f);
        public Color colorBlue   = new Color(0.20f, 0.45f, 0.95f);
        public Color colorIndigo = new Color(0.28f, 0.10f, 0.75f);
        public Color colorViolet = new Color(0.62f, 0.15f, 0.92f);

        // -------------------------------------------------------------------------
        // Internal display targets (what we're interpolating toward)
        // -------------------------------------------------------------------------
        private float _tRed, _tOrange, _tYellow, _tGreen, _tBlue, _tIndigo, _tViolet;
        private float _tCompanionHarmony;
        private float _tRuinProgress;
        private float _tPortalGlow;
        private Color _targetCompanionColor = Color.white;
        private Color _targetPortalColor    = Color.white;

        // Current smooth display values
        private float _dRed, _dOrange, _dYellow, _dGreen, _dBlue, _dIndigo, _dViolet;
        private float _dCompanionHarmony;
        private float _dRuinProgress;
        private float _dPortalGlow;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            // Apply band colors
            SetImageColor(barRed,    colorRed);
            SetImageColor(barOrange, colorOrange);
            SetImageColor(barYellow, colorYellow);
            SetImageColor(barGreen,  colorGreen);
            SetImageColor(barBlue,   colorBlue);
            SetImageColor(barIndigo, colorIndigo);
            SetImageColor(barViolet, colorViolet);

            StartCoroutine(DataRefreshLoop());
        }

        private void Update()
        {
            float dt = Time.deltaTime * displaySmoothSpeed;

            // Smooth all display values toward targets
            _dRed    = Mathf.Lerp(_dRed,    _tRed,    dt);
            _dOrange = Mathf.Lerp(_dOrange, _tOrange, dt);
            _dYellow = Mathf.Lerp(_dYellow, _tYellow, dt);
            _dGreen  = Mathf.Lerp(_dGreen,  _tGreen,  dt);
            _dBlue   = Mathf.Lerp(_dBlue,   _tBlue,   dt);
            _dIndigo = Mathf.Lerp(_dIndigo, _tIndigo, dt);
            _dViolet = Mathf.Lerp(_dViolet, _tViolet, dt);

            _dCompanionHarmony = Mathf.Lerp(_dCompanionHarmony, _tCompanionHarmony, dt);
            _dRuinProgress     = Mathf.Lerp(_dRuinProgress,     _tRuinProgress,     dt);
            _dPortalGlow       = Mathf.Lerp(_dPortalGlow,       _tPortalGlow,       dt);

            // Apply to UI
            SetFill(barRed,    _dRed);
            SetFill(barOrange, _dOrange);
            SetFill(barYellow, _dYellow);
            SetFill(barGreen,  _dGreen);
            SetFill(barBlue,   _dBlue);
            SetFill(barIndigo, _dIndigo);
            SetFill(barViolet, _dViolet);

            // Companion arc
            SetFill(companionArc, _dCompanionHarmony);
            if (companionArc != null)
                companionArc.color = Color.Lerp(companionArc.color, _targetCompanionColor,
                                                Time.deltaTime * 3f);

            // Ruin proximity arc + pulse
            float pulse = _dRuinProgress > 0.5f
                ? 1.0f + Mathf.Sin(Time.time * ruinPulseSpeed) * 0.08f
                : 1.0f;
            SetFill(ruinProximityArc, _dRuinProgress * pulse);

            // Portal glow
            if (portalGlowImage != null)
            {
                var c = portalGlowImage.color;
                c.a = _dPortalGlow * portalGlowMax;
                portalGlowImage.color = Color.Lerp(portalGlowImage.color,
                    new Color(_targetPortalColor.r, _targetPortalColor.g, _targetPortalColor.b, c.a),
                    Time.deltaTime * 2f);
                portalGlowImage.color = c;
            }
        }

        // -------------------------------------------------------------------------
        // Data refresh coroutine (reads from systems every 0.2s)
        // -------------------------------------------------------------------------
        private IEnumerator DataRefreshLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(dataRefreshInterval);
                RefreshData();
            }
        }

        private void RefreshData()
        {
            // --- Band values from PlayerField ---
            var field = VibrationalResonanceSystem.Instance?.PlayerField;
            if (field != null)
            {
                _tRed    = field.red;
                _tOrange = field.orange;
                _tYellow = field.yellow;
                _tGreen  = field.green;
                _tBlue   = field.blue;
                _tIndigo = field.indigo;
                _tViolet = field.violet;
            }

            // --- Companion harmony ---
            var resonance = VibrationalResonanceSystem.Instance;
            if (resonance != null)
            {
                _tCompanionHarmony   = resonance.HighestHarmonyScore;
                string topAnimal     = resonance.HighestHarmonyAnimal;
                _targetCompanionColor = ElementColorForAnimal(topAnimal);
            }

            // --- Ruin proximity ---
            var worldSys = WorldSystem.Instance;
            if (worldSys != null)
            {
                var (ruin, progress) = worldSys.NearestUndiscoveredAncient();
                _tRuinProgress = ruin != null ? progress : 0f;
            }

            // --- Portal glow ---
            var portalSys = PortalRevealSystem.Instance;
            if (portalSys != null)
            {
                var topPortal = portalSys.GetHighestRevealPortal();
                if (topPortal != null)
                {
                    _tPortalGlow       = topPortal.revealAmount;
                    _targetPortalColor = PortalColor(topPortal.portalId);
                }
            }
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------
        private static void SetFill(Image img, float value)
        {
            if (img != null) img.fillAmount = Mathf.Clamp01(value);
        }

        private static void SetImageColor(Image img, Color color)
        {
            if (img != null) img.color = color;
        }

        private Color ElementColorForAnimal(string animalId)
        {
            if (string.IsNullOrEmpty(animalId)) return Color.white;
            // Simple heuristic — production: look up from CompanionRegistry
            if (animalId.Contains("hawk") || animalId.Contains("eagle") ||
                animalId.Contains("raven") || animalId.Contains("parrot"))
                return colorYellow;    // Air → yellow
            if (animalId.Contains("dolphin") || animalId.Contains("whale") ||
                animalId.Contains("osprey"))
                return colorBlue;     // Water → blue
            if (animalId.Contains("stag") || animalId.Contains("bison") ||
                animalId.Contains("tiger") || animalId.Contains("snake"))
                return colorGreen;    // Earth → green
            if (animalId.Contains("jaguar") || animalId.Contains("lion") ||
                animalId.Contains("fire"))
                return colorRed;      // Fire → red
            return colorViolet;       // Source / unknown
        }

        private Color PortalColor(string portalId) => portalId switch
        {
            "ForestCalm"        => colorGreen,
            "OceanSurf"         => colorBlue,
            "FireMilitary"      => colorRed,
            "AirDancing"        => colorYellow,
            "SourceEnergyWells" => colorViolet,
            _                   => Color.white,
        };
    }
}
