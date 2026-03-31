// SpiritsCrossing — UpsilonNodeBrain.cs
// A living vibrational oscillator — the cellular unit of resonance for every
// animal and AI in the world.
//
// Unlike the static VibrationalField profile (which is loaded once and never
// changes), the UpsilonNodeBrain:
//
//   OSCILLATES  — 7 internal phases advance each tick with Kuramoto coupling.
//                 The live field is the amplitude vector, continuously moving.
//                 Green (band 3) is the hub: all other bands couple toward it.
//
//   SENSES      — Any system may call Sense(field, strength) to feed an
//                 environmental vibration into this node. Sources are not
//                 filtered by planet or area — anything can reach this node.
//                 Player field, other animals, world elements, ruins, portals.
//
//   DISCOVERS   — When sensory input in any band significantly departs from
//                 the entity's baseline (noveltyThreshold), it is "novel."
//                 Novel input accumulates into DiscoveryMemory via EMA.
//                 DiscoveryLevel tracks how much unfamiliar vibration is
//                 currently being processed. OnDiscovery fires when the node
//                 crosses into meaningful discovery.
//
//   REMEMBERS   — Hebbian plasticity: bands that co-activate with the hub
//                 band strengthen their coupling weight. Over many encounters
//                 each node builds a unique coupling history — two animals
//                 that have had the same experiences will converge; animals
//                 with different histories will diverge.
//
// Setup: Drop on any companion prefab or NPC GameObject. Set entityId to
//        match the animalId / archetypeId. The node self-registers with
//        VibrationalResonanceSystem, which will seed it from the static
//        profile and thereafter use LiveField for harmony computation.
//
// The static profile becomes the BASELINE — the animal's nature.
// LiveField is who the animal IS right now, shaped by everything it has met.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpiritsCrossing.Vibration
{
    public class UpsilonNodeBrain : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Identity")]
        [Tooltip("Must match the animalId or archetypeId registered with VibrationalResonanceSystem.")]
        public string entityId;

        [Header("Oscillator Tuning")]
        [Range(0.01f, 0.20f)]
        [Tooltip("Brain tick step. Lower = smoother but heavier.")]
        public float dt = 0.05f;

        [Header("Sensory Input")]
        [Range(0f, 1f)]
        [Tooltip("How strongly environmental fields shift this node's amplitudes.")]
        public float inputGain = 0.30f;

        [Header("Discovery")]
        [Range(0f, 1f)]
        [Tooltip("How strongly the discovery memory influences live amplitudes.")]
        public float discoveryInfluence = 0.18f;

        [Range(0f, 0.5f)]
        [Tooltip("Minimum deviation from baseline before input is considered 'novel'.")]
        public float noveltyThreshold = 0.15f;

        [Header("Hebbian Plasticity")]
        [Range(0.990f, 0.9999f)]
        [Tooltip("Per-tick decay of coupling weights. Closer to 1 = longer memory.")]
        public float plasticityDecay = 0.998f;

        [Range(0f, 0.01f)]
        [Tooltip("Learning rate for hub-band co-activation. η in Δw = η·Aᵢ·Aₕᵤᵦ.")]
        public float plasticityRate = 0.002f;

        [Header("Debug")]
        public bool logDiscovery = true;

        // -------------------------------------------------------------------------
        // Public read-only output
        // -------------------------------------------------------------------------

        /// <summary>The entity's current oscillating vibrational field.
        /// This is what VibrationalResonanceSystem uses for harmony computation.</summary>
        public VibrationalField LiveField { get; private set; } = new VibrationalField();

        /// <summary>EMA of novel vibrations this node has encountered.
        /// Reflects what the entity has been shaped by outside its nature.</summary>
        public VibrationalField DiscoveryMemory { get; private set; } = new VibrationalField();

        /// <summary>0–1: how much novel vibration is currently being processed.
        /// High = encountering something genuinely new. Used by animators and audio.</summary>
        public float DiscoveryLevel { get; private set; }

        /// <summary>Whether this node has been seeded with a baseline field.</summary>
        public bool IsInitialised { get; private set; }

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Fires when DiscoveryLevel rises above the meaningful threshold.
        /// Carries the live field at the moment of discovery.</summary>
        public event Action<VibrationalField> OnDiscovery;

        // -------------------------------------------------------------------------
        // Internal oscillator state
        // -------------------------------------------------------------------------
        private readonly float[] _phase     = new float[7];
        private readonly float[] _freq      = new float[7];
        private readonly float[] _amplitude = new float[7];
        private readonly float[] _baseline  = new float[7];

        // Hebbian coupling weight of each band to the hub (green = 3)
        private readonly float[] _plastic   = new float[7];

        // Sensory input accumulator — filled by Sense(), consumed each tick
        private readonly float[] _sensoryAccum = new float[7];

        // Discovery memory per band
        private readonly float[] _discMem   = new float[7];

        private float _tickTimer;
        private float _prevDiscovery;

        // Band 3 (green) is the coupling hub — mirrors RBE-1 and SpiritBrainController
        private const int HUB = 3;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------
        private void OnEnable()
        {
            // Self-register — VRS will seed our baseline once its profiles are loaded
            if (VibrationalResonanceSystem.Instance != null)
                VibrationalResonanceSystem.Instance.RegisterUpsilonNode(entityId, this);
        }

        private void OnDisable()
        {
            if (VibrationalResonanceSystem.Instance != null)
                VibrationalResonanceSystem.Instance.UnregisterUpsilonNode(entityId);
        }

        private void Update()
        {
            if (!IsInitialised) return;

            _tickTimer += Time.deltaTime;
            if (_tickTimer < dt) return;
            _tickTimer = 0f;
            Tick();
        }

        // -------------------------------------------------------------------------
        // Initialisation — called by VibrationalResonanceSystem after profile load
        // -------------------------------------------------------------------------

        /// <summary>
        /// Seed this node from the entity's static vibrational profile.
        /// The profile becomes the baseline — LiveField starts here and evolves from it.
        /// </summary>
        public void Initialise(VibrationalField baselineField)
        {
            for (int i = 0; i < 7; i++)
            {
                _baseline[i]  = baselineField.GetBand(i);
                _amplitude[i] = _baseline[i];
                _phase[i]     = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

                // Natural frequency: upper-end spread — all bands oscillate faster,
                // ranging from 1.10 (red) up to 1.45 (violet), ±0.04 jitter.
                _freq[i]      = Mathf.Lerp(1.10f, 1.45f, i / 6f) +
                                UnityEngine.Random.Range(-0.04f, 0.04f);
                _plastic[i]   = 0f;
                _discMem[i]   = 0f;
            }

            UpdateLiveField();
            UpdateDiscoveryMemoryField();
            IsInitialised = true;

            Debug.Log($"[UpsilonNodeBrain] {entityId} initialised. " +
                      $"Baseline={baselineField.DominantBandName()} dominant.");
        }

        // -------------------------------------------------------------------------
        // Environmental sensing — call from VibrationalResonanceSystem each tick
        //
        // Any source may call this: player field, another animal's LiveField,
        // a portal emitter, a ruin, a realm. No source is filtered.
        // Inputs accumulate in _sensoryAccum and are consumed in Tick().
        //
        // strength: 0–1, how strongly this source registers on this node.
        // -------------------------------------------------------------------------
        public void Sense(VibrationalField source, float strength)
        {
            if (!IsInitialised || source == null) return;
            float s = Mathf.Clamp01(strength);
            for (int i = 0; i < 7; i++)
                _sensoryAccum[i] = Mathf.Clamp01(_sensoryAccum[i] + source.GetBand(i) * s * dt);
        }

        // -------------------------------------------------------------------------
        // Oscillator tick
        // -------------------------------------------------------------------------
        private void Tick()
        {
            // --- Phase update — Kuramoto coupling to hub and neighbours ---
            // Hebbian plasticity amplifies coupling to hub for co-active bands
            float[] newPhase = new float[7];
            for (int k = 0; k < 7; k++)
            {
                newPhase[k] = _phase[k] + dt * _freq[k];

                if (k != HUB)
                {
                    float hubCoupling = 0.22f + _plastic[k] * 0.14f; // Hebbian-amplified, tighter sync
                    newPhase[k] += dt * hubCoupling * Mathf.Sin(_phase[HUB] - _phase[k]);
                }

                // Neighbour coupling (ring topology)
                int left  = (k + 6) % 7;
                int right = (k + 1) % 7;
                newPhase[k] += dt * 0.08f * Mathf.Sin(_phase[left]  - _phase[k]);
                newPhase[k] += dt * 0.08f * Mathf.Sin(_phase[right] - _phase[k]);
                newPhase[k]  = Mathf.Repeat(newPhase[k], Mathf.PI * 2f);
            }

            // --- Amplitude update — baseline + sensory + discovery memory ---
            float noveltySum = 0f;
            for (int k = 0; k < 7; k++)
            {
                // Base weight lifted to 0.70: amplitudes ride the upper range by default.
                float target = Mathf.Clamp01(
                    0.70f * _baseline[k] +
                    inputGain * _sensoryAccum[k] +
                    discoveryInfluence * _discMem[k]);

                _amplitude[k] = Mathf.Clamp01(_amplitude[k] + dt * (target - _amplitude[k]));

                // --- Discovery: how novel is this input relative to baseline? ---
                float novelty = Mathf.Max(0f,
                    Mathf.Abs(_sensoryAccum[k] - _baseline[k]) - noveltyThreshold);

                // Discovery memory: slow EMA weighted by novelty
                float discTarget = _sensoryAccum[k] * novelty;
                _discMem[k] = Mathf.Clamp01(
                    plasticityDecay * _discMem[k] +
                    (1f - plasticityDecay) * discTarget * 20f); // scale so memory is legible

                noveltySum += novelty;

                // --- Hebbian plasticity: bands co-active with hub strengthen ---
                _plastic[k] = Mathf.Clamp01(
                    plasticityDecay * _plastic[k] +
                    plasticityRate * _amplitude[k] * _amplitude[HUB]);
            }

            // --- Discovery level: smoothed overall novelty ---
            _prevDiscovery = DiscoveryLevel;
            float rawDiscovery = noveltySum / 7f;
            DiscoveryLevel = Mathf.Clamp01(
                DiscoveryLevel + dt * (rawDiscovery - DiscoveryLevel) * 8f);

            // Fire discovery event when crossing into meaningful range
            if (DiscoveryLevel >= 0.10f && _prevDiscovery < 0.05f)
            {
                if (logDiscovery)
                    Debug.Log($"[UpsilonNodeBrain] {entityId} — discovery! " +
                              $"level={DiscoveryLevel:F2} dominant={LiveField.DominantBandName()}");
                OnDiscovery?.Invoke(LiveField);
            }

            // --- Clear sensory accumulator for next frame ---
            for (int k = 0; k < 7; k++) _sensoryAccum[k] = 0f;

            Array.Copy(newPhase, _phase, 7);
            UpdateLiveField();
            UpdateDiscoveryMemoryField();
        }

        // -------------------------------------------------------------------------
        // Field helpers
        // -------------------------------------------------------------------------
        private void UpdateLiveField()
        {
            for (int i = 0; i < 7; i++) LiveField.SetBand(i, _amplitude[i]);
        }

        private void UpdateDiscoveryMemoryField()
        {
            for (int i = 0; i < 7; i++) DiscoveryMemory.SetBand(i, _discMem[i]);
        }

        // -------------------------------------------------------------------------
        // Public helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// How coherent is this node's live field internally?
        /// High coherence = bands are aligned — the entity is resonantly stable.
        /// </summary>
        public float Coherence() => LiveField.Coherence();

        /// <summary>
        /// Harmony between this node's LiveField and another field.
        /// </summary>
        public float HarmonyWith(VibrationalField other) => LiveField.Harmony(other);

        /// <summary>
        /// The strongest band in the discovery memory — what is this entity
        /// currently learning the most about?
        /// </summary>
        public string PrimaryDiscoveryBand() => DiscoveryMemory.DominantBandName();
    }
}
