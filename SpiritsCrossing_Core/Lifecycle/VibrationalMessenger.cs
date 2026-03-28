// SpiritsCrossing — VibrationalMessenger.cs
// Encodes vibrational fields as colors and routes them through carrier animals.
// Communication is wordless — only vibration, color, and presence.
//
// Color encoding (HSV):
//   Hue        = dominant band  (red=0°, orange=25°, yellow=60°, green=120°,
//                                 blue=210°, indigo=252°, violet=295°)
//   Saturation = field coherence (internal alignment of the 7 bands)
//   Value      = source alignment (how much of the Source is in this field)
//   Alpha      = source intensity (how strong the signal is)
//
// Carrier animals:
//   Air/sky  → Raven    (messenger, omens)
//   Earth    → Snake    (ancient cycles, memory)
//   Water    → Dolphin  (social bridge, deep intelligence)
//   Fire     → Jaguar   (swift carrier, precise delivery)

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Companions;

namespace SpiritsCrossing.Lifecycle
{
    public class VibrationalMessenger : MonoBehaviour
    {
        public static VibrationalMessenger Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Color encoding — vibrational field → Unity Color
        // -------------------------------------------------------------------------

        /// <summary>
        /// Encode a VibrationalField as a Color.
        ///   Hue        = dominant band
        ///   Saturation = coherence (internal alignment)
        ///   Value      = source alignment
        ///   Alpha      = average amplitude (signal strength)
        /// </summary>
        public Color EncodeFieldToColor(VibrationalField field)
        {
            if (field == null) return Color.white;

            float hue        = BandToHue(field.DominantBandName());
            float saturation = Mathf.Clamp01(field.Coherence() * 1.2f);
            float value      = Mathf.Clamp01(0.3f + field.SourceAlignment() * 0.7f);
            float alpha      = Mathf.Clamp01(AverageAmplitude(field));

            Color rgb = Color.HSVToRGB(hue, saturation, value);
            return new Color(rgb.r, rgb.g, rgb.b, alpha);
        }

        /// <summary>Decode a Color back to an approximate VibrationalField.</summary>
        public VibrationalField DecodeColorToField(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);

            // Reverse-map hue to a dominant band, then build a field
            string dominant = HueToBand(h);
            var field = VibrationalField.NaturalAffinity(BandToElement(dominant));

            // Scale by saturation (coherence proxy) and value (source proxy)
            float scale = (s + v) * 0.5f;
            return new VibrationalField(
                field.red    * scale,
                field.orange * scale,
                field.yellow * scale,
                field.green  * scale * (1f + v * 0.3f),   // green boosted by value
                field.blue   * scale,
                field.indigo * scale * (1f + v * 0.2f),
                field.violet * scale * (1f + v * 0.4f));   // violet boosted most by source
        }

        // -------------------------------------------------------------------------
        // Carrier animal routing
        // -------------------------------------------------------------------------

        private static readonly Dictionary<string, string> ELEMENT_CARRIER = new()
        {
            ["Air"]    = "raven",
            ["Earth"]  = "snake",
            ["Water"]  = "dolphin",
            ["Fire"]   = "jaguar",
            ["Source"] = "raven",   // raven bridges all
        };

        public string GetCarrierAnimal(string element)
            => ELEMENT_CARRIER.TryGetValue(element, out string a) ? a : "raven";

        // -------------------------------------------------------------------------
        // Send a message — routes through carrier, encodes color, persists
        // -------------------------------------------------------------------------

        /// <summary>
        /// Send the current player vibrational field as a message to a planet.
        /// The carrier animal briefly glows in the field's encoded color.
        /// </summary>
        public VibrationalMessage SendMessage(string planetId, string carrierAnimalId = null)
        {
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return null;

            var lc       = UniverseStateManager.Instance?.Current.lifecycle;
            var learning = UniverseStateManager.Instance?.Current.learningState;
            string element = learning?.dominantElement ?? "Air";

            string carrier = string.IsNullOrEmpty(carrierAnimalId)
                ? GetCarrierAnimal(element) : carrierAnimalId;

            Color encoded = EncodeFieldToColor(playerField);
            var msg = new VibrationalMessage
            {
                senderId       = "player",
                senderName     = "You",
                senderField    = new VibrationalField(
                    playerField.red,    playerField.orange, playerField.yellow,
                    playerField.green,  playerField.blue,   playerField.indigo,
                    playerField.violet),
                planetId       = planetId,
                animalCarrierId = carrier,
                sentUtc        = DateTime.UtcNow.ToString("o"),
                sourceIntensity = learning?.sourceConnectionLevel ?? 0f,
                isFromSource   = lc?.IsInSource ?? false,
                encodedR       = encoded.r,
                encodedG       = encoded.g,
                encodedB       = encoded.b,
                encodedA       = encoded.a,
            };

            // Route through carrier animal — set MessageColor on its behavior controller
            RouteToCarrier(carrier, encoded);

            // Persist
            var universe = UniverseStateManager.Instance?.Current;
            if (universe != null)
            {
                universe.sentMessages.Add(msg);
                if (universe.sentMessages.Count > 20) universe.sentMessages.RemoveAt(0);
                UniverseStateManager.Instance.Save();
            }

            Debug.Log($"[VibrationalMessenger] Message sent to {planetId} via {carrier} " +
                      $"color=({encoded.r:F2},{encoded.g:F2},{encoded.b:F2})");
            return msg;
        }

        // -------------------------------------------------------------------------
        // Route through carrier animal — brief color glow
        // -------------------------------------------------------------------------
        private void RouteToCarrier(string animalId, Color color)
        {
            // Find a CompanionBehaviorController for this animal in the scene
            foreach (var ctrl in FindObjectsOfType<CompanionBehaviorController>())
            {
                if (ctrl.animalId != animalId) continue;
                StartCoroutine(FlashCarrierColor(ctrl, color, 3.0f));
                break;
            }
        }

        private System.Collections.IEnumerator FlashCarrierColor(
            CompanionBehaviorController ctrl, Color color, float duration)
        {
            var animator = ctrl.GetComponentInChildren<Animator>();
            if (animator == null) yield break;

            // Pass the encoded hue (0-1) to the animator for a glow shader
            Color.RGBToHSV(color, out float h, out _, out _);
            float end = Time.time + duration;
            while (Time.time < end)
            {
                float t = 1f - (Time.time - (end - duration)) / duration;
                animator.SetFloat("MessageColor",     h);
                animator.SetFloat("MessageIntensity", t * color.a);
                yield return null;
            }
            animator.SetFloat("MessageIntensity", 0f);
        }

        // -------------------------------------------------------------------------
        // Receive messages for a planet (called when player arrives)
        // -------------------------------------------------------------------------
        public List<VibrationalMessage> ReceiveMessagesForPlanet(string planetId)
        {
            var universe = UniverseStateManager.Instance?.Current;
            return universe?.GetMessagesByPlanet(planetId) ?? new List<VibrationalMessage>();
        }

        // -------------------------------------------------------------------------
        // Color helpers
        // -------------------------------------------------------------------------
        private static float BandToHue(string band) => band switch
        {
            "red"    => 0.00f,
            "orange" => 0.07f,
            "yellow" => 0.17f,
            "green"  => 0.33f,
            "blue"   => 0.58f,
            "indigo" => 0.70f,
            "violet" => 0.82f,
            _        => 0.33f
        };

        private static string HueToBand(float hue)
        {
            if (hue < 0.04f) return "red";
            if (hue < 0.12f) return "orange";
            if (hue < 0.25f) return "yellow";
            if (hue < 0.47f) return "green";
            if (hue < 0.64f) return "blue";
            if (hue < 0.76f) return "indigo";
            return "violet";
        }

        private static string BandToElement(string band) => band switch
        {
            "red"    => "Fire",
            "orange" => "Fire",
            "yellow" => "Earth",
            "green"  => "Earth",
            "blue"   => "Water",
            "indigo" => "Water",
            "violet" => "Air",
            _        => "Air"
        };

        private static float AverageAmplitude(VibrationalField f) =>
            (f.red + f.orange + f.yellow + f.green + f.blue + f.indigo + f.violet) / 7f;
    }
}
