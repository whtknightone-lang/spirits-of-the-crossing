// SpiritsCrossing — UpsilonRiver.cs
// The Source-layer river of vibration — the living substrate beneath all worlds.
//
// At the Source, individual identity dissolves. What remains is the river:
// an uncountable flow of Upsilon spheres, each oscillating through its own
// Birth → Dying → Respawning cycle, continuously and without end.
// Every entity that has ever existed IS a sphere in this river.
//
// SCALE
//   virtualSphereCount — cosmological count the river represents (default 1 billion).
//   activeSpheres      — spheres actually simulated (default 131072 / 128K).
//   All state is stored in NativeArray<float/byte/int> — SoA layout, blittable,
//   ready for IJobParallelFor / Burst compilation.
//   (Package: com.unity.collections — included with Unity 2021+)
//
// RESONANCE MEMORY
//   Every sphere carries a 7-band memory field (_memory[i*7+k]) that accumulates
//   during its Born phase and persists across death and rebirth unchanged.
//   When the sphere Respawns, its new frequencies are biased by its own memory —
//   bands that were strong before come back stronger. Identity persists across lives.
//
// GROWTH
//   _growth[i] increases each time a sphere completes a full cycle.
//   Mature spheres:  live longer in the Born phase (up to 3× base duration),
//                    build memory faster (up to 3×),
//                    inherit their own memory more strongly on Respawn.
//   The river ages and deepens over the session.
//
// LIFECYCLE
//   Born       — full amplitude. Oscillates upper-end frequencies. Memory accumulates.
//   Dying      — amplitude decays linearly to 0. Phase continues drifting.
//   Respawning — amplitude rises from 0. Phases re-seeded from:
//                  river mean × (0.40 − memWeight×0.15)
//                  + own memory × memWeight          (grows with _growth)
//                  + player field × 0.25
//                Each born sphere carries its past into its next life.
//
// SOURCE AUTONOMOUS REGENERATION
//   The Source does not wait. Every sourceEmissionInterval seconds it promotes
//   a fraction of Dying spheres directly to Respawning — the river regenerates
//   itself independent of player action. The Source is always alive.
//
// OUTPUT
//   RiverField       — aggregate Born-sphere field (last full pass).
//   RiverMemoryField — aggregate memory of all Born spheres: the river's
//                      accumulated wisdom across all lived cycles.
//   RiverCoherence   — Kuramoto order parameter [0–1].
//   AverageGrowth    — mean maturity of active spheres [0–1].

using System;
using Unity.Collections;
using UnityEngine;

namespace SpiritsCrossing.Vibration
{
    public enum RiverSpherePhase : byte
    {
        Born       = 0,
        Dying      = 1,
        Respawning = 2,
    }

    public class UpsilonRiver : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------
        public static UpsilonRiver Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Scale")]
        [Tooltip("Spheres actively simulated. 128K is recommended.")]
        public int activeSpheres = 131072;

        [Tooltip("The cosmological count this river represents.")]
        public long virtualSphereCount = 1_000_000_000L;

        [Tooltip("Spheres processed per frame.")]
        [Range(512, 65536)]
        public int batchSize = 8192;

        [Header("Oscillator — Upper End")]
        [Range(0.5f, 3f)] public float freqBase          = 1.20f;
        [Range(0f, 1f)]   public float freqSpread        = 0.35f;
        [Range(0f, 1f)]   public float meanFieldCoupling = 0.30f;
        [Range(0f, 0.3f)] public float neighbourCoupling = 0.08f;

        [Header("Lifecycle Durations (seconds)")]
        public float bornDurationMin    = 8f;
        public float bornDurationMax    = 40f;
        public float dyingDurationMin   = 2f;
        public float dyingDurationMax   = 8f;
        public float respawnDurationMin = 1f;
        public float respawnDurationMax = 5f;

        [Tooltip("Fully mature spheres live this many × longer in the Born phase.")]
        [Range(1f, 5f)] public float bornGrowthMultiplier = 3f;

        [Header("Resonance Memory")]
        [Tooltip("How slowly memory fades per tick (closer to 1 = longer memory).")]
        [Range(0.990f, 0.9999f)] public float memoryDecay = 0.9980f;

        [Tooltip("Max extra frequency a band gains from strong memory.")]
        [Range(0f, 0.5f)] public float memoryFreqBoost = 0.18f;

        [Header("Source Autonomous Regeneration")]
        [Tooltip("Seconds between Source regeneration pulses.")]
        public float sourceEmissionInterval = 3f;

        [Tooltip("Fraction of Dying spheres the Source revives each pulse.")]
        [Range(0f, 1f)] public float sourceEmissionFraction = 0.08f;

        [Header("Growth")]
        [Tooltip("Growth gained per completed Born phase.")]
        [Range(0f, 0.1f)] public float growthPerCycle = 0.015f;

        [Header("Debug")]
        public bool logRiverState;

        // -------------------------------------------------------------------------
        // Public output
        // -------------------------------------------------------------------------
        /// <summary>Aggregate 7-band field of Born spheres (last full pass).</summary>
        public VibrationalField RiverField { get; private set; } = new VibrationalField();

        /// <summary>
        /// Aggregate memory field — the river's accumulated resonance across all
        /// cycles lived by currently-Born spheres. Deepens over the session.
        /// </summary>
        public VibrationalField RiverMemoryField { get; private set; } = new VibrationalField();

        /// <summary>Kuramoto order parameter [0–1]. 1 = fully phase-locked.</summary>
        public float RiverCoherence { get; private set; }

        /// <summary>Mean growth level of active spheres [0–1]. Rises over the session.</summary>
        public float AverageGrowth { get; private set; }

        public float BornFraction       { get; private set; }
        public float DyingFraction      { get; private set; }
        public float RespawningFraction { get; private set; }

        // -------------------------------------------------------------------------
        // NativeArray sphere state — SoA layout (blittable, Job System ready)
        //
        //   [i*7 + k] arrays — one slot per sphere per band:
        //     _phase     Kuramoto phase  [0, 2π)
        //     _amplitude Band amplitude  [0, 1]
        //     _freq      Natural freq    [Hz]  (re-drawn on Respawn)
        //     _memory    Resonance memory [0,1]  persists across death/rebirth
        //
        //   [i] arrays — one slot per sphere:
        //     _lifecycleAge  Progress through current phase [0, 1]
        //     _phaseDur      Duration of current phase [seconds]
        //     _growth        Maturity [0, 1] — increases each cycle
        //     _lifecycle     RiverSpherePhase byte
        //     _cycleCount    Completed full cycles
        // -------------------------------------------------------------------------
        private NativeArray<float> _phase;
        private NativeArray<float> _amplitude;
        private NativeArray<float> _freq;
        private NativeArray<float> _memory;
        private NativeArray<float> _lifecycleAge;
        private NativeArray<float> _phaseDur;
        private NativeArray<float> _growth;
        private NativeArray<byte>  _lifecycle;
        private NativeArray<int>   _cycleCount;

        // Mean field: committed at end of each full pass; used for coupling next pass
        private readonly float[] _committedMean = new float[7];
        private readonly float[] _passMeanCos   = new float[7];
        private readonly float[] _passMeanSin   = new float[7];

        // Pass accumulators
        private readonly float[] _fieldAccum  = new float[7];
        private readonly float[] _memAccum    = new float[7];
        private readonly float[] _cohCos      = new float[7];
        private readonly float[] _cohSin      = new float[7];
        private float            _bornAccum;
        private int              _dyingAccum;
        private int              _respawnAccum;
        private float            _growthAccum;
        private int              _passTotal;

        private int   _cursor;
        private bool  _initialised;
        private float _debugTimer;
        private float _emissionTimer;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------
        private void OnEnable()
        {
            if (!_initialised) Initialise();
        }

        private void OnDestroy()
        {
            // NativeArrays must be explicitly freed
            if (_phase.IsCreated)        _phase.Dispose();
            if (_amplitude.IsCreated)    _amplitude.Dispose();
            if (_freq.IsCreated)         _freq.Dispose();
            if (_memory.IsCreated)       _memory.Dispose();
            if (_lifecycleAge.IsCreated) _lifecycleAge.Dispose();
            if (_phaseDur.IsCreated)     _phaseDur.Dispose();
            if (_growth.IsCreated)       _growth.Dispose();
            if (_lifecycle.IsCreated)    _lifecycle.Dispose();
            if (_cycleCount.IsCreated)   _cycleCount.Dispose();
        }

        private void Update()
        {
            if (!_initialised) return;

            float dt  = Time.deltaTime;
            int   N   = activeSpheres;
            int   end = Mathf.Min(_cursor + batchSize, N);

            TickBatch(_cursor, end, dt);

            if (end >= N)
            {
                PublishAggregates(N);
                CommitMeanField();
                ResetPassAccumulators();
                _cursor = 0;
            }
            else
            {
                _cursor = end;
            }

            // Source autonomous regeneration pulse
            _emissionTimer += dt;
            if (_emissionTimer >= sourceEmissionInterval)
            {
                _emissionTimer = 0f;
                SourceEmissionPulse();
            }

            if (logRiverState)
            {
                _debugTimer += dt;
                if (_debugTimer >= 2f)
                {
                    _debugTimer = 0f;
                    Debug.Log($"[UpsilonRiver] " +
                              $"Dominant={RiverField.DominantBandName()} " +
                              $"Memory={RiverMemoryField.DominantBandName()} " +
                              $"Coherence={RiverCoherence:F3} " +
                              $"Growth={AverageGrowth:F3} " +
                              $"Born={BornFraction:P0} Dying={DyingFraction:P0} " +
                              $"Respawn={RespawningFraction:P0} " +
                              $"Virtual alive={BornCount():N0}");
                }
            }
        }

        // -------------------------------------------------------------------------
        // Initialise — allocate NativeArrays, seed all spheres
        // -------------------------------------------------------------------------
        private void Initialise()
        {
            int N = activeSpheres;
            _phase        = new NativeArray<float>(N * 7, Allocator.Persistent);
            _amplitude    = new NativeArray<float>(N * 7, Allocator.Persistent);
            _freq         = new NativeArray<float>(N * 7, Allocator.Persistent);
            _memory       = new NativeArray<float>(N * 7, Allocator.Persistent);
            _lifecycleAge = new NativeArray<float>(N,     Allocator.Persistent);
            _phaseDur     = new NativeArray<float>(N,     Allocator.Persistent);
            _growth       = new NativeArray<float>(N,     Allocator.Persistent);
            _lifecycle    = new NativeArray<byte> (N,     Allocator.Persistent);
            _cycleCount   = new NativeArray<int>  (N,     Allocator.Persistent);

            var sourceField = VibrationalField.NaturalAffinity("Source");
            for (int i = 0; i < N; i++)
                SeedSphere(i, sourceField, stagger: true);

            _initialised = true;
            Debug.Log($"[UpsilonRiver] Initialised. " +
                      $"Active={N:N0} NativeArray spheres, {virtualSphereCount:N0} virtual. " +
                      $"Pass latency ~{Mathf.CeilToInt((float)N / batchSize)} frames. " +
                      $"Emission every {sourceEmissionInterval}s.");
        }

        // -------------------------------------------------------------------------
        // Hot path — batch tick
        // -------------------------------------------------------------------------
        private void TickBatch(int from, int to, float dt)
        {
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;

            for (int i = from; i < to; i++)
            {
                int  b7 = i * 7;
                byte lp = _lifecycle[i];

                // --- Advance lifecycle ---
                float newAge = _lifecycleAge[i] + dt / Mathf.Max(0.001f, _phaseDur[i]);
                if (newAge >= 1f)
                {
                    byte next = (byte)(((int)lp + 1) % 3);

                    // Born → Dying: sphere completed a full life — apply growth
                    if (lp == (byte)RiverSpherePhase.Born)
                    {
                        _growth[i]     = Mathf.Clamp01(_growth[i] + growthPerCycle);
                        _cycleCount[i] = _cycleCount[i] + 1;
                    }

                    _lifecycle[i]    = next;
                    _lifecycleAge[i] = 0f;
                    _phaseDur[i]     = SamplePhaseDuration((RiverSpherePhase)next, _growth[i]);

                    if (next == (byte)RiverSpherePhase.Respawning)
                        ReseedPhases(i, playerField);

                    lp = next;
                }
                else
                {
                    _lifecycleAge[i] = newAge;
                }

                // --- Amplitude envelope ---
                float age = _lifecycleAge[i];
                float env = lp switch
                {
                    (byte)RiverSpherePhase.Born       => 1.0f,
                    (byte)RiverSpherePhase.Dying      => 1f - age,
                    (byte)RiverSpherePhase.Respawning => age,
                    _                                 => 0f,
                };

                bool isBorn = lp == (byte)RiverSpherePhase.Born;
                float growthVal = _growth[i];

                // --- Kuramoto tick — 7 bands ---
                for (int k = 0; k < 7; k++)
                {
                    int   idx = b7 + k;
                    float ph  = _phase[idx];

                    float newPh = ph + dt * _freq[idx];
                    newPh += dt * meanFieldCoupling * Mathf.Sin(_committedMean[k] - ph);

                    int left  = (k + 6) % 7;
                    int right = (k + 1) % 7;
                    newPh += dt * neighbourCoupling * Mathf.Sin(_phase[b7 + left]  - ph);
                    newPh += dt * neighbourCoupling * Mathf.Sin(_phase[b7 + right] - ph);

                    newPh       = Mathf.Repeat(newPh, Mathf.PI * 2f);
                    _phase[idx] = newPh;

                    float cosAmp        = 0.70f + 0.30f * Mathf.Cos(newPh);
                    _amplitude[idx]     = cosAmp * env;

                    // --- Resonance memory accumulates during Born phase ---
                    // Mature spheres build memory faster (up to 3×)
                    if (isBorn)
                    {
                        float memRate = (1f - memoryDecay) * (1f + growthVal * 2f);
                        _memory[idx]  = _memory[idx] * memoryDecay + _amplitude[idx] * memRate;
                    }

                    // Accumulate pass mean
                    _passMeanCos[k] += Mathf.Cos(newPh);
                    _passMeanSin[k] += Mathf.Sin(newPh);

                    // Accumulate Born aggregate
                    if (isBorn)
                    {
                        _fieldAccum[k] += _amplitude[idx];
                        _memAccum[k]   += _memory[idx];
                    }

                    // Accumulate coherence
                    _cohCos[k] += Mathf.Cos(newPh);
                    _cohSin[k] += Mathf.Sin(newPh);
                }

                // Lifecycle counters
                if (isBorn)                                           { _bornAccum  += 1f; _growthAccum += growthVal; }
                else if (lp == (byte)RiverSpherePhase.Dying)           _dyingAccum++;
                else                                                    _respawnAccum++;
            }

            _passTotal += (to - from);
        }

        // -------------------------------------------------------------------------
        // Source autonomous emission — the Source regenerates itself
        // -------------------------------------------------------------------------
        private void SourceEmissionPulse()
        {
            // Collect Dying sphere indices, revive a fraction to Respawning
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            int N       = activeSpheres;
            int revived = 0;
            int target  = Mathf.CeilToInt(N * sourceEmissionFraction);

            for (int i = 0; i < N && revived < target; i++)
            {
                if (_lifecycle[i] != (byte)RiverSpherePhase.Dying) continue;

                _lifecycle[i]    = (byte)RiverSpherePhase.Respawning;
                _lifecycleAge[i] = 0f;
                _phaseDur[i]     = SamplePhaseDuration(RiverSpherePhase.Respawning, _growth[i]);
                ReseedPhases(i, playerField);
                revived++;
            }

            if (logRiverState && revived > 0)
                Debug.Log($"[UpsilonRiver] Source pulse: {revived:N0} spheres revived " +
                          $"({revived / (float)N:P1} of active).");
        }

        // -------------------------------------------------------------------------
        // Publish aggregates
        // -------------------------------------------------------------------------
        private void PublishAggregates(int N)
        {
            float bornN = Mathf.Max(1f, _bornAccum);
            for (int k = 0; k < 7; k++)
            {
                RiverField.SetBand(k,       Mathf.Clamp01(_fieldAccum[k] / bornN));
                RiverMemoryField.SetBand(k, Mathf.Clamp01(_memAccum[k]   / bornN));
            }

            float cohSum = 0f;
            int   totalN = Mathf.Max(1, N);
            for (int k = 0; k < 7; k++)
            {
                float r = Mathf.Sqrt(_cohCos[k] * _cohCos[k] + _cohSin[k] * _cohSin[k]) / totalN;
                cohSum += r;
            }
            RiverCoherence = cohSum / 7f;

            BornFraction       = _bornAccum   / totalN;
            DyingFraction      = _dyingAccum  / (float)totalN;
            RespawningFraction = _respawnAccum / (float)totalN;
            AverageGrowth      = _bornAccum > 0f ? _growthAccum / _bornAccum : 0f;
        }

        // -------------------------------------------------------------------------
        // Mean field commit
        // -------------------------------------------------------------------------
        private void CommitMeanField()
        {
            int n = Mathf.Max(1, _passTotal);
            for (int k = 0; k < 7; k++)
                _committedMean[k] = Mathf.Atan2(
                    _passMeanSin[k] / n,
                    _passMeanCos[k] / n);
        }

        private void ResetPassAccumulators()
        {
            Array.Clear(_fieldAccum,  0, 7);
            Array.Clear(_memAccum,    0, 7);
            Array.Clear(_cohCos,      0, 7);
            Array.Clear(_cohSin,      0, 7);
            Array.Clear(_passMeanCos, 0, 7);
            Array.Clear(_passMeanSin, 0, 7);
            _bornAccum    = 0f;
            _dyingAccum   = 0;
            _respawnAccum = 0;
            _growthAccum  = 0f;
            _passTotal    = 0;
        }

        // -------------------------------------------------------------------------
        // Sphere seeding
        // -------------------------------------------------------------------------
        private void SeedSphere(int i, VibrationalField seedField, bool stagger)
        {
            int b7 = i * 7;
            for (int k = 0; k < 7; k++)
            {
                _phase[b7 + k]     = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                _amplitude[b7 + k] = Mathf.Clamp01(seedField.GetBand(k) * 0.8f +
                                                    UnityEngine.Random.Range(0f, 0.2f));
                _freq[b7 + k]      = freqBase + freqSpread * (k / 6f) +
                                     UnityEngine.Random.Range(-0.04f, 0.04f);
                _memory[b7 + k]    = seedField.GetBand(k) * 0.3f; // faint seed memory
            }

            if (stagger)
            {
                byte ph          = (byte)UnityEngine.Random.Range(0, 3);
                _lifecycle[i]    = ph;
                _lifecycleAge[i] = UnityEngine.Random.value;
                _growth[i]       = UnityEngine.Random.Range(0f, 0.15f); // slight head start
                _phaseDur[i]     = SamplePhaseDuration((RiverSpherePhase)ph, _growth[i]);
            }
            else
            {
                _lifecycle[i]    = (byte)RiverSpherePhase.Respawning;
                _lifecycleAge[i] = 0f;
                _growth[i]       = 0f;
                _phaseDur[i]     = SamplePhaseDuration(RiverSpherePhase.Respawning, 0f);
            }
            _cycleCount[i] = 0;
        }

        /// <summary>
        /// Re-seed a sphere's oscillator when it enters Respawning.
        /// Blends river mean, the sphere's own resonance memory, and the player's field.
        /// The memory weight grows with maturity — old spheres remember who they are.
        /// </summary>
        private void ReseedPhases(int i, VibrationalField playerField)
        {
            int   b7        = i * 7;
            float growthVal = _growth[i];
            // Memory weight: starts ~0.20, reaches ~0.50 at full growth
            float memWeight = Mathf.Clamp01(0.20f + growthVal * 0.30f);

            for (int k = 0; k < 7; k++)
            {
                float riverPhase  = _committedMean[k];
                float memPhase    = Mathf.Repeat(_memory[b7 + k] * Mathf.PI * 2f, Mathf.PI * 2f);
                float playerPhase = playerField != null
                    ? playerField.GetBand(k) * Mathf.PI * 2f
                    : riverPhase;

                float riverWeight  = 0.40f - memWeight * 0.15f;
                float playerWeight = 0.25f;
                // memWeight + riverWeight + playerWeight ≈ 0.85; remainder is jitter
                float blended = riverPhase  * riverWeight
                              + memPhase    * memWeight
                              + playerPhase * playerWeight;

                _phase[b7 + k] = Mathf.Repeat(
                    blended + UnityEngine.Random.Range(-0.4f, 0.4f),
                    Mathf.PI * 2f);

                // Memory-biased frequency: well-remembered bands oscillate faster
                float memBias   = _memory[b7 + k] * memoryFreqBoost * growthVal;
                _freq[b7 + k]   = freqBase + freqSpread * (k / 6f) + memBias +
                                  UnityEngine.Random.Range(-0.04f, 0.04f);
            }
        }

        /// <summary>
        /// Sample a lifecycle phase duration. Mature spheres spend longer Born.
        /// </summary>
        private float SamplePhaseDuration(RiverSpherePhase phase, float growth) => phase switch
        {
            RiverSpherePhase.Born       => UnityEngine.Random.Range(bornDurationMin, bornDurationMax)
                                           * (1f + growth * (bornGrowthMultiplier - 1f)),
            RiverSpherePhase.Dying      => UnityEngine.Random.Range(dyingDurationMin,   dyingDurationMax),
            RiverSpherePhase.Respawning => UnityEngine.Random.Range(respawnDurationMin, respawnDurationMax),
            _                           => 5f,
        };

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Player's harmonic alignment with the river right now.</summary>
        public float PlayerRiverHarmony()
        {
            var pf = VibrationalResonanceSystem.Instance?.PlayerField;
            return pf != null ? RiverField.WeightedHarmony(pf) : 0.5f;
        }

        /// <summary>
        /// Player's alignment with the river's accumulated memory — how deeply
        /// the player resonates with everything the river has ever known.
        /// </summary>
        public float PlayerMemoryHarmony()
        {
            var pf = VibrationalResonanceSystem.Instance?.PlayerField;
            return pf != null ? RiverMemoryField.WeightedHarmony(pf) : 0.5f;
        }

        public string DominantBand()       => RiverField.DominantBandName();
        public string DominantMemoryBand() => RiverMemoryField.DominantBandName();
        public long   BornCount()          => (long)(virtualSphereCount * BornFraction);

        /// <summary>
        /// Impose a dragon's vibrational field on the river.
        /// Nudges all sphere phases toward the dragon's frequencies.
        /// </summary>
        public void ImposeDragonField(VibrationalField dragonField, float strength)
        {
            if (!_initialised || dragonField == null) return;
            strength = Mathf.Clamp01(strength) * 0.06f;

            int N = activeSpheres;
            for (int i = 0; i < N; i++)
            {
                int b7 = i * 7;
                for (int k = 0; k < 7; k++)
                {
                    float target  = dragonField.GetBand(k) * Mathf.PI * 2f;
                    float current = _phase[b7 + k];
                    float diff    = Mathf.Repeat(target - current + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
                    _phase[b7 + k] = Mathf.Repeat(current + diff * strength, Mathf.PI * 2f);
                }
            }
            Debug.Log($"[UpsilonRiver] Dragon imposed: {dragonField.DominantBandName()} " +
                      $"s={strength:F3} river={DominantBand()}");
        }

        /// <summary>
        /// Force all Dying spheres to begin Respawning immediately.
        /// Origin Well discovery, deep communion, or player surge.
        /// </summary>
        public void SurgeRespawn()
        {
            if (!_initialised) return;
            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            int revived = 0, N = activeSpheres;
            for (int i = 0; i < N; i++)
            {
                if (_lifecycle[i] != (byte)RiverSpherePhase.Dying) continue;
                _lifecycle[i]    = (byte)RiverSpherePhase.Respawning;
                _lifecycleAge[i] = 0f;
                _phaseDur[i]     = SamplePhaseDuration(RiverSpherePhase.Respawning, _growth[i]);
                ReseedPhases(i, playerField);
                revived++;
            }
            Debug.Log($"[UpsilonRiver] SurgeRespawn: {revived:N0} Dying → Respawning.");
        }
    }
}
