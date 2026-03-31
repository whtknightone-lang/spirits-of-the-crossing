// SpiritsCrossing — UpsilonSphereAI.cs
// An AI Upsilon sphere: brain + resonance memory carried through every lifecycle.
//
// This is the living individual — the named entity that exists, dies, and is reborn
// within the Source. Unlike the anonymous mass of UpsilonRiver spheres, each
// UpsilonSphereAI has:
//
//   BRAIN       — 7-band Kuramoto oscillator (same physics as UpsilonNodeBrain +
//                 SpiritBrainController). Green is the coupling hub. Sensory input
//                 from player field, other spheres, world elements.
//
//   MEMORY      — _memory[7] accumulates during the Born phase via slow EMA and
//                 survives death and Respawning unchanged. The sphere's history
//                 is encoded in its memory field across every life it has lived.
//
//   LIFECYCLE   — Born (active brain) → Dying (amplitude fades) → Respawning
//                 (amplitude rises, brain re-seeds FROM memory). Each Respawn
//                 integrates memory into the baseline and frequencies so the sphere
//                 comes back shaped by everything it has been.
//
//   DRIVE WEIGHTS — Derived continuously from MemoryField. Memory-heavy violet/
//                 indigo → rest+signal dominant. Memory-heavy red → attack/flee.
//                 Over many Source lives the sphere becomes contemplative.
//                 Blends archetype profile (nature) with memory-derived weights (nurture).
//
//   GROWTH      — Increases each completed cycle. Mature spheres live longer Born,
//                 build memory faster, inherit their own memory more strongly on
//                 Respawn, and drift their baseline more aggressively.
//
//   LIFE HISTORY — Ring buffer of last 8 life snapshots: memory, drives, dominant
//                 band at death. The sphere carries its autobiography.
//
// SETUP
//   Drop on any AI entity. Set entityId. Optionally assign initialProfile to seed
//   from an archetype. Call Sense(field, strength) from any system that wants to
//   feed vibrational input into this sphere.
//
//   The sphere self-initialises on OnEnable. It does not require profiles to be
//   loaded — it will seed from a neutral Source field if no profile is given.
//
// INTEGRATION
//   LiveField    → use for harmony computation (replace static VibrationalField)
//   MemoryField  → use for "what has this entity accumulated over its lives"
//   CurrentDrives → use instead of static SpiritDriveWeights for AI behavior
//   Events: OnBorn, OnDying, OnRespawned, OnMemoryConsolidated

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.SpiritAI;

namespace SpiritsCrossing.Vibration
{
    // -------------------------------------------------------------------------
    // A record of one completed life — stored in the life history ring buffer
    // -------------------------------------------------------------------------
    [Serializable]
    public class LifeMemorySnapshot
    {
        public int    lifeIndex;
        public float  growthAtDeath;
        public string dominantBandAtDeath;
        public string dominantDriveAtDeath;
        public VibrationalField memoryAtDeath = new VibrationalField();

        // Drive weights at the moment of death
        public float attack, flee, seek, rest, signal, explore;
    }

    // =========================================================================
    public class UpsilonSphereAI : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Identity")]
        [Tooltip("Unique ID. Should match animalId / archetypeId if this is a named spirit.")]
        public string entityId;

        [Tooltip("Optional archetype profile to seed initial baseline and drive weights. " +
                 "If null, seeds from neutral Source field.")]
        public SpiritDriveProfile initialProfile;

        [Header("Oscillator")]
        [Tooltip("Brain tick step. Lower = smoother.")]
        [Range(0.01f, 0.20f)] public float dt = 0.05f;

        [Tooltip("How strongly environmental fields shift amplitudes.")]
        [Range(0f, 1f)] public float inputGain = 0.35f;

        [Tooltip("Hebbian coupling decay per tick.")]
        [Range(0.990f, 0.9999f)] public float plasticityDecay = 0.998f;

        [Tooltip("Hebbian learning rate: Δw = η·Aᵢ·Aₕᵤᵦ.")]
        [Range(0f, 0.01f)] public float plasticityRate = 0.002f;

        [Tooltip("Fraction of Hebbian weights carried into the next life.")]
        [Range(0f, 1f)] public float plasticityCarryover = 0.50f;

        [Header("Lifecycle Durations (seconds)")]
        public float bornDurationMin    = 20f;
        public float bornDurationMax    = 60f;
        public float dyingDurationMin   = 5f;
        public float dyingDurationMax   = 12f;
        public float respawnDurationMin = 3f;
        public float respawnDurationMax = 8f;

        [Tooltip("Fully mature spheres live this many × longer Born.")]
        [Range(1f, 5f)] public float bornGrowthMultiplier = 2.5f;

        [Header("Resonance Memory")]
        [Tooltip("Memory EMA decay per tick. Slower than SpiritBrainController (~0.97) " +
                 "because this memory survives across lives.")]
        [Range(0.990f, 0.9999f)] public float memoryDecay = 0.9970f;

        [Tooltip("How much accumulated memory shifts natural frequency per band.")]
        [Range(0f, 0.4f)] public float memoryFreqBoost = 0.15f;

        [Tooltip("How much memory shifts the sphere's baseline (nature) per Respawn. " +
                 "Scaled by growth: mature spheres drift further each life.")]
        [Range(0f, 0.5f)] public float memoryBaselineInfluence = 0.30f;

        [Tooltip("At Respawn, how much memory weight vs archetype weight in drive derivation. " +
                 "Scaled by growth: 0 = always archetype, 1 = always memory.")]
        [Range(0f, 1f)] public float memoryDriveInfluence = 0.60f;

        [Header("Growth")]
        [Range(0f, 0.1f)] public float growthPerCycle = 0.02f;

        [Header("Debug")]
        public bool logLifecycle = true;

        // -------------------------------------------------------------------------
        // Public output
        // -------------------------------------------------------------------------

        /// <summary>Current oscillating vibrational field. Use for harmony computation.</summary>
        public VibrationalField LiveField { get; private set; } = new VibrationalField();

        /// <summary>
        /// Accumulated memory field — everything this sphere has been across all its lives.
        /// Persists through death and rebirth. Deepens over many cycles.
        /// </summary>
        public VibrationalField MemoryField { get; private set; } = new VibrationalField();

        /// <summary>Current lifecycle phase.</summary>
        public RiverSpherePhase CurrentPhase { get; private set; } = RiverSpherePhase.Born;

        /// <summary>Progress through current lifecycle phase [0–1].</summary>
        public float LifecycleAge { get; private set; }

        /// <summary>Number of completed Born→Dying→Respawning cycles.</summary>
        public int LifeCount { get; private set; }

        /// <summary>Maturity level [0–1]. Rises each cycle.</summary>
        public float Growth { get; private set; }

        /// <summary>
        /// Live drive weights — continuously derived from MemoryField blended with archetype.
        /// Use these instead of the static SpiritDriveWeights for AI behavior decisions.
        /// </summary>
        public SpiritDriveWeights CurrentDrives { get; private set; } = new SpiritDriveWeights();

        /// <summary>The dominant drive mode right now.</summary>
        public SpiritDriveMode DominantDrive { get; private set; } = SpiritDriveMode.Rest;

        /// <summary>Life history ring buffer — last 8 completed lives.</summary>
        public IReadOnlyList<LifeMemorySnapshot> LifeHistory => _lifeHistory;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Fired when the sphere enters Born phase. Carries field at birth and life index.</summary>
        public event Action<VibrationalField, int> OnBorn;

        /// <summary>Fired when the sphere enters Dying phase. Carries field and life index.</summary>
        public event Action<VibrationalField, int> OnDying;

        /// <summary>Fired when the sphere enters Respawning phase.</summary>
        public event Action<VibrationalField, int> OnRespawning;

        /// <summary>
        /// Fired at the end of each Born phase — memory consolidated before death.
        /// Carries live field, current memory field, and life index.
        /// </summary>
        public event Action<VibrationalField, VibrationalField, int> OnMemoryConsolidated;

        /// <summary>Dominant drive mode changed.</summary>
        public event Action<SpiritDriveMode> OnDriveModeChanged;

        // -------------------------------------------------------------------------
        // Internal oscillator state
        // -------------------------------------------------------------------------
        private readonly float[] _phase        = new float[7];
        private readonly float[] _freq         = new float[7];
        private readonly float[] _amplitude    = new float[7];
        private readonly float[] _baseline     = new float[7]; // evolves toward memory each Respawn
        private readonly float[] _plastic      = new float[7]; // Hebbian coupling to hub
        private readonly float[] _sensoryAccum = new float[7];
        private readonly float[] _memory       = new float[7]; // persists through death and rebirth

        private float _tickTimer;
        private float _lifecycleTimer;
        private float _phaseDuration;
        private bool  _initialised;

        private SpiritDriveMode _prevDrive = SpiritDriveMode.Rest;

        private readonly List<LifeMemorySnapshot> _lifeHistory = new List<LifeMemorySnapshot>();
        private const int HISTORY_SIZE = 8;
        private const int HUB          = 3; // green — coupling hub, mirrors UpsilonNodeBrain

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------
        private void OnEnable()
        {
            if (!_initialised) Initialise();

            if (VibrationalResonanceSystem.Instance != null)
                VibrationalResonanceSystem.Instance.RegisterUpsilonNode(entityId,
                    GetComponent<UpsilonNodeBrain>());
            // Note: if this entity has an UpsilonNodeBrain companion component,
            // register it. Otherwise the AI sphere operates independently.
        }

        private void Update()
        {
            if (!_initialised) return;

            float dt_frame = Time.deltaTime;

            // --- Lifecycle advancement ---
            _lifecycleTimer += dt_frame;
            if (_lifecycleTimer >= _phaseDuration)
            {
                _lifecycleTimer = 0f;
                AdvanceLifecycle();
            }

            // --- Oscillator tick ---
            _tickTimer += dt_frame;
            if (_tickTimer >= dt)
            {
                _tickTimer = 0f;
                Tick();
            }

            // --- Auto-sense the river (Source background resonance) ---
            var river = UpsilonRiver.Instance;
            if (river != null && CurrentPhase == RiverSpherePhase.Born)
                Sense(river.RiverField, 0.20f);
        }

        // -------------------------------------------------------------------------
        // Initialisation
        // -------------------------------------------------------------------------
        private void Initialise()
        {
            var seedField = initialProfile != null
                ? VibrationalField.FromSpectralSignature(initialProfile.spectralSignature)
                : VibrationalField.NaturalAffinity("Source");

            for (int k = 0; k < 7; k++)
            {
                _baseline[k]  = seedField.GetBand(k);
                _amplitude[k] = _baseline[k];
                _memory[k]    = _baseline[k] * 0.25f; // faint seed memory from nature
                _phase[k]     = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                _freq[k]      = 1.10f + 0.35f * (k / 6f) + UnityEngine.Random.Range(-0.04f, 0.04f);
                _plastic[k]   = 0f;
            }

            CurrentPhase   = RiverSpherePhase.Born;
            _phaseDuration = SamplePhaseDuration(RiverSpherePhase.Born);

            UpdateLiveField();
            UpdateMemoryField();
            UpdateDriveWeights();

            _initialised = true;

            if (logLifecycle)
                Debug.Log($"[UpsilonSphereAI] {entityId} born. " +
                          $"Baseline={LiveField.DominantBandName()} " +
                          $"Drive={DominantDrive}");

            OnBorn?.Invoke(LiveField, LifeCount);
        }

        // -------------------------------------------------------------------------
        // Public sensory input — call from any system
        // -------------------------------------------------------------------------

        /// <summary>
        /// Feed an environmental vibrational field into this sphere.
        /// Inputs accumulate and are consumed each brain tick.
        /// Call from VibrationalResonanceSystem, portals, river, other spheres.
        /// </summary>
        public void Sense(VibrationalField source, float strength)
        {
            if (!_initialised || source == null) return;
            float s = Mathf.Clamp01(strength);
            for (int k = 0; k < 7; k++)
                _sensoryAccum[k] = Mathf.Clamp01(_sensoryAccum[k] + source.GetBand(k) * s * dt);
        }

        // -------------------------------------------------------------------------
        // Brain tick — Kuramoto oscillator with memory accumulation
        // -------------------------------------------------------------------------
        private void Tick()
        {
            // Lifecycle amplitude envelope
            float age = _lifecycleTimer / Mathf.Max(0.001f, _phaseDuration);
            float env = CurrentPhase switch
            {
                RiverSpherePhase.Born       => 1.0f,
                RiverSpherePhase.Dying      => 1f - age,
                RiverSpherePhase.Respawning => age,
                _                           => 0f,
            };

            bool isBorn = CurrentPhase == RiverSpherePhase.Born;

            // --- Phase update — Kuramoto + Hebbian ---
            float[] newPhase = new float[7];
            for (int k = 0; k < 7; k++)
            {
                newPhase[k] = _phase[k] + dt * _freq[k];

                if (k != HUB)
                {
                    float hubCoupling = 0.22f + _plastic[k] * 0.14f; // Hebbian-amplified
                    newPhase[k] += dt * hubCoupling * Mathf.Sin(_phase[HUB] - _phase[k]);
                }

                int left  = (k + 6) % 7;
                int right = (k + 1) % 7;
                newPhase[k] += dt * 0.08f * Mathf.Sin(_phase[left]  - _phase[k]);
                newPhase[k] += dt * 0.08f * Mathf.Sin(_phase[right] - _phase[k]);
                newPhase[k]  = Mathf.Repeat(newPhase[k], Mathf.PI * 2f);
            }

            // --- Amplitude update — baseline + sensory ---
            for (int k = 0; k < 7; k++)
            {
                float target = Mathf.Clamp01(
                    0.70f * _baseline[k] +
                    inputGain * _sensoryAccum[k]);

                _amplitude[k] = Mathf.Clamp01(_amplitude[k] + dt * (target - _amplitude[k]));
                _amplitude[k] *= env; // apply lifecycle envelope

                // --- Resonance memory: accumulates during Born only ---
                // Mature spheres build memory up to 3× faster
                if (isBorn)
                {
                    float memRate  = (1f - memoryDecay) * (1f + Growth * 2f);
                    _memory[k]     = _memory[k] * memoryDecay + _amplitude[k] * memRate;
                }

                // --- Hebbian plasticity: bands co-active with hub strengthen ---
                _plastic[k] = Mathf.Clamp01(
                    plasticityDecay * _plastic[k] +
                    plasticityRate * _amplitude[k] * _amplitude[HUB]);
            }

            // Clear sensory input
            for (int k = 0; k < 7; k++) _sensoryAccum[k] = 0f;
            Array.Copy(newPhase, _phase, 7);

            UpdateLiveField();
            UpdateMemoryField();

            if (isBorn) UpdateDriveWeights();
        }

        // -------------------------------------------------------------------------
        // Lifecycle phase transitions
        // -------------------------------------------------------------------------
        private void AdvanceLifecycle()
        {
            var next = (RiverSpherePhase)(((int)CurrentPhase + 1) % 3);

            switch (CurrentPhase)
            {
                case RiverSpherePhase.Born:
                    // --- Memory consolidation: seal this life's memory ---
                    ConsolidateMemory();
                    OnDying?.Invoke(LiveField, LifeCount);
                    if (logLifecycle)
                        Debug.Log($"[UpsilonSphereAI] {entityId} Dying. " +
                                  $"Life={LifeCount} Growth={Growth:F3} " +
                                  $"Memory={MemoryField.DominantBandName()} " +
                                  $"Drive={DominantDrive}");
                    break;

                case RiverSpherePhase.Dying:
                    // --- Respawn integration: memory shapes the next life ---
                    IntegrateMemoryIntoBaseline();
                    ReseedOscillatorFromMemory();
                    OnRespawning?.Invoke(LiveField, LifeCount);
                    if (logLifecycle)
                        Debug.Log($"[UpsilonSphereAI] {entityId} Respawning. " +
                                  $"Baseline={LiveField.DominantBandName()} " +
                                  $"MemWeight={MemoryWeight():F2}");
                    break;

                case RiverSpherePhase.Respawning:
                    // --- Born again ---
                    LifeCount++;
                    UpdateDriveWeights();
                    OnBorn?.Invoke(LiveField, LifeCount);
                    if (logLifecycle)
                        Debug.Log($"[UpsilonSphereAI] {entityId} Born again. " +
                                  $"Life={LifeCount} Dominant={LiveField.DominantBandName()} " +
                                  $"Drive={DominantDrive}");
                    break;
            }

            CurrentPhase   = next;
            _phaseDuration = SamplePhaseDuration(next);
        }

        // -------------------------------------------------------------------------
        // Memory consolidation — Born → Dying
        // -------------------------------------------------------------------------
        private void ConsolidateMemory()
        {
            // Snapshot this life before dying
            var snap = new LifeMemorySnapshot
            {
                lifeIndex           = LifeCount,
                growthAtDeath       = Growth,
                dominantBandAtDeath = MemoryField.DominantBandName(),
                dominantDriveAtDeath = DominantDrive.ToString(),
                memoryAtDeath       = new VibrationalField(
                    _memory[0], _memory[1], _memory[2], _memory[3],
                    _memory[4], _memory[5], _memory[6]),
                attack  = CurrentDrives.attack,
                flee    = CurrentDrives.flee,
                seek    = CurrentDrives.seek,
                rest    = CurrentDrives.rest,
                signal  = CurrentDrives.signal,
                explore = CurrentDrives.explore,
            };

            _lifeHistory.Add(snap);
            if (_lifeHistory.Count > HISTORY_SIZE)
                _lifeHistory.RemoveAt(0);

            // Increase growth
            Growth = Mathf.Clamp01(Growth + growthPerCycle);

            OnMemoryConsolidated?.Invoke(LiveField, MemoryField, LifeCount);
        }

        // -------------------------------------------------------------------------
        // Baseline evolution — memory shifts the sphere's nature each life
        // -------------------------------------------------------------------------
        private void IntegrateMemoryIntoBaseline()
        {
            // Mature spheres drift further each life: Growth scales influence
            float alpha = memoryBaselineInfluence * Growth;
            for (int k = 0; k < 7; k++)
                _baseline[k] = Mathf.Clamp01(Mathf.Lerp(_baseline[k], _memory[k], alpha));

            // Preserve a fraction of Hebbian coupling (continuity of learned connections)
            for (int k = 0; k < 7; k++)
                _plastic[k] = Mathf.Clamp01(_plastic[k] * plasticityCarryover);
        }

        // -------------------------------------------------------------------------
        // Oscillator re-seeding — memory biases phases and frequencies
        // -------------------------------------------------------------------------
        private void ReseedOscillatorFromMemory()
        {
            var river = UpsilonRiver.Instance;
            float memWeight = MemoryWeight();

            for (int k = 0; k < 7; k++)
            {
                // Phase: blend river mean + memory phase + jitter
                float riverPhase  = river != null ? river.RiverField.GetBand(k) * Mathf.PI * 2f : 0f;
                float memPhase    = Mathf.Repeat(_memory[k] * Mathf.PI * 2f, Mathf.PI * 2f);
                float riverWeight = 0.40f - memWeight * 0.15f;

                _phase[k] = Mathf.Repeat(
                    riverPhase * riverWeight + memPhase * memWeight +
                    UnityEngine.Random.Range(-0.4f, 0.4f),
                    Mathf.PI * 2f);

                // Frequency: memory-biased — well-remembered bands oscillate faster
                float memBias = _memory[k] * memoryFreqBoost * Growth;
                _freq[k] = 1.10f + 0.35f * (k / 6f) + memBias +
                           UnityEngine.Random.Range(-0.04f, 0.04f);

                // Amplitude: start from baseline (which has already been updated from memory)
                _amplitude[k] = _baseline[k];
            }
        }

        // Memory weight grows with maturity [0.20 → 0.55]
        private float MemoryWeight() => Mathf.Clamp01(0.20f + Growth * 0.35f);

        // -------------------------------------------------------------------------
        // Drive weight derivation — memory → SpiritDriveWeights
        // -------------------------------------------------------------------------
        private void UpdateDriveWeights()
        {
            // Memory-derived raw drives (from what the sphere has accumulated)
            float rawAttack  = _memory[0];                                  // red → intensity
            float rawFlee    = Mathf.Max(0f, _memory[0] - _memory[3]);     // heat without calm
            float rawSeek    = (_memory[1] + _memory[4]) * 0.5f;           // orange + blue
            float rawRest    = (_memory[3] + _memory[5]) * 0.5f;           // green + indigo
            float rawSignal  = (_memory[1] + _memory[6]) * 0.5f;           // orange + violet
            float rawExplore = (_memory[2] + _memory[4]) * 0.5f;           // yellow + blue

            // Archetype-seed drives (nature)
            float archAttack  = initialProfile?.driveWeights.attack  ?? 1f / 6f;
            float archFlee    = initialProfile?.driveWeights.flee    ?? 1f / 6f;
            float archSeek    = initialProfile?.driveWeights.seek    ?? 1f / 6f;
            float archRest    = initialProfile?.driveWeights.rest    ?? 1f / 6f;
            float archSignal  = initialProfile?.driveWeights.signal  ?? 1f / 6f;
            float archExplore = initialProfile?.driveWeights.explore ?? 1f / 6f;

            // Blend: new sphere = archetype, mature sphere = memory-derived
            float memInfluence = memoryDriveInfluence * Growth;
            float t = Mathf.Clamp01(memInfluence);

            float blendAttack  = Mathf.Lerp(archAttack,  rawAttack,  t);
            float blendFlee    = Mathf.Lerp(archFlee,    rawFlee,    t);
            float blendSeek    = Mathf.Lerp(archSeek,    rawSeek,    t);
            float blendRest    = Mathf.Lerp(archRest,    rawRest,    t);
            float blendSignal  = Mathf.Lerp(archSignal,  rawSignal,  t);
            float blendExplore = Mathf.Lerp(archExplore, rawExplore, t);

            // Normalise so sum = 1.0
            float total = blendAttack + blendFlee + blendSeek + blendRest + blendSignal + blendExplore;
            if (total < 0.001f) total = 1f;

            var drives = new SpiritDriveWeights
            {
                attack  = blendAttack  / total,
                flee    = blendFlee    / total,
                seek    = blendSeek    / total,
                rest    = blendRest    / total,
                signal  = blendSignal  / total,
                explore = blendExplore / total,
            };
            CurrentDrives = drives;

            // Arbitrate dominant drive
            SpiritDriveMode newDrive = Arbitrate(drives);
            DominantDrive = newDrive;
            if (newDrive != _prevDrive)
            {
                OnDriveModeChanged?.Invoke(newDrive);
                _prevDrive = newDrive;
            }
        }

        private static SpiritDriveMode Arbitrate(SpiritDriveWeights d)
        {
            SpiritDriveMode best = SpiritDriveMode.Rest;
            float bestW = d.rest;
            void Check(SpiritDriveMode m, float w) { if (w > bestW) { bestW = w; best = m; } }
            Check(SpiritDriveMode.Attack,  d.attack);
            Check(SpiritDriveMode.Flee,    d.flee);
            Check(SpiritDriveMode.Seek,    d.seek);
            Check(SpiritDriveMode.Signal,  d.signal);
            Check(SpiritDriveMode.Explore, d.explore);
            return best;
        }

        // -------------------------------------------------------------------------
        // Field helpers
        // -------------------------------------------------------------------------
        private void UpdateLiveField()
        {
            for (int k = 0; k < 7; k++) LiveField.SetBand(k, _amplitude[k]);
        }

        private void UpdateMemoryField()
        {
            for (int k = 0; k < 7; k++) MemoryField.SetBand(k, _memory[k]);
        }

        // -------------------------------------------------------------------------
        // Phase duration sampling — mature spheres live longer Born
        // -------------------------------------------------------------------------
        private float SamplePhaseDuration(RiverSpherePhase phase) => phase switch
        {
            RiverSpherePhase.Born       => UnityEngine.Random.Range(bornDurationMin, bornDurationMax)
                                           * (1f + Growth * (bornGrowthMultiplier - 1f)),
            RiverSpherePhase.Dying      => UnityEngine.Random.Range(dyingDurationMin,   dyingDurationMax),
            RiverSpherePhase.Respawning => UnityEngine.Random.Range(respawnDurationMin, respawnDurationMax),
            _                           => 5f,
        };

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Harmony between this sphere's live field and another field.
        /// </summary>
        public float HarmonyWith(VibrationalField other) => LiveField.Harmony(other);

        /// <summary>
        /// Harmony between this sphere's accumulated MEMORY and another field.
        /// High = this entity has a deep resonance history with this kind of field.
        /// </summary>
        public float MemoryHarmonyWith(VibrationalField other) => MemoryField.Harmony(other);

        /// <summary>
        /// Internal coherence of the live field [0–1].
        /// High = bands are phase-locked = entity is resonantly stable.
        /// </summary>
        public float Coherence() => LiveField.Coherence();

        /// <summary>
        /// The drive mode this sphere has been most consistently drawn to
        /// across all recorded lives (from life history).
        /// Returns Rest if no history yet.
        /// </summary>
        public SpiritDriveMode LifetimeDominantDrive()
        {
            if (_lifeHistory.Count == 0) return SpiritDriveMode.Rest;
            int[] counts = new int[6];
            foreach (var snap in _lifeHistory)
                if (Enum.TryParse<SpiritDriveMode>(snap.dominantDriveAtDeath, out var m))
                    counts[(int)m]++;
            int best = 0;
            for (int i = 1; i < 6; i++) if (counts[i] > counts[best]) best = i;
            return (SpiritDriveMode)best;
        }

        /// <summary>
        /// The dominant memory band this sphere has accumulated across all recorded lives.
        /// Represents the sphere's deepest vibrational identity.
        /// </summary>
        public string LifetimeMemoryBand()
        {
            if (_lifeHistory.Count == 0) return MemoryField.DominantBandName();
            // Average memory across all life snapshots
            float[] avg = new float[7];
            foreach (var snap in _lifeHistory)
                for (int k = 0; k < 7; k++)
                    avg[k] += snap.memoryAtDeath.GetBand(k);
            float bestVal = 0f; int bestIdx = 0;
            for (int k = 0; k < 7; k++)
            {
                avg[k] /= _lifeHistory.Count;
                if (avg[k] > bestVal) { bestVal = avg[k]; bestIdx = k; }
            }
            return new[] { "red", "orange", "yellow", "green", "blue", "indigo", "violet" }[bestIdx];
        }
    }
}
