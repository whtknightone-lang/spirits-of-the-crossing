// SpiritsCrossing — NpcEvolutionSystem.cs
// Autonomous NPC learning and evolution driven by RUE SnowflakeBrain math.
//
// Every NPC archetype runs its own 21-node Kuramoto oscillator brain,
// fed by the orbital field energy F(r) of its home planet.
// The brain evolves independently of the player.
//
// RUE math used directly (from rue-engine/engine/agents/snowflake_brain.py):
//   phase[i] += 0.02 * Σ sin(phase[j] - phase[i]) + 0.001 * external_energy
//   coherence  = |mean(exp(i*phase))|
//
// RUE behavior (from rue-engine/engine/ai/behavior.py):
//   activity = 0.5 * coherence + 0.05 * external_energy
//
// AME v7 Evolution system (from ai_multiverse_engine_v7/engine/systems.py):
//   resonance += uniform(0, 0.2) per step   →   here: resonance += activity * dt
//
// Spectral signature drifts slowly toward the planet's orbital equilibrium.
// When resonance crosses an archetype-shift threshold, the NPC's dominant
// archetype updates — it has genuinely changed through independent experience.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Cosmos;

namespace SpiritsCrossing.Autonomous
{
    // -------------------------------------------------------------------------
    // Persistent NPC evolution state (serialized into UniverseState)
    // -------------------------------------------------------------------------
    [Serializable]
    public class NpcEvolutionState
    {
        public string archetypeId;
        public string planetId;

        // 21-node Kuramoto phases (SnowflakeBrain) — stored as compact floats
        // Phases mod 2π, but we store as 0–1 (/ 2π) for save compactness
        public float[] phasesNorm = new float[21]; // 0–1 range

        public float  coherence;       // Kuramoto order parameter
        public float  activity;        // 0.5*coherence + 0.05*E (RUE behavior)
        public float  resonance;       // accumulated over time
        public float  energy;          // accumulated energy from orbital field

        // Spectral drift: how far the NPC has drifted from its initial signature
        // [red, orange, yellow, green, blue, indigo, violet]
        public float[] spectralDrift = new float[7];

        // Current dominant archetype (may shift over time)
        public string currentArchetype;
        public int    archetypeShiftCount;
        public string lastShiftUtc;

        public float TotalResonance() => resonance + coherence * 0.5f;
    }

    // =========================================================================
    // Runtime system
    // =========================================================================
    public class NpcEvolutionSystem : MonoBehaviour
    {
        public static NpcEvolutionSystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // RUE constants (from rue-engine config + behavior)
        // -------------------------------------------------------------------------
        private const int   BRAIN_SIZE             = 21;    // rue-engine BRAIN_SIZE
        private const float KURAMOTO_COUPLING       = 0.02f; // SnowflakeBrain coupling
        private const float ENERGY_INPUT_WEIGHT    = 0.001f; // external energy influence
        private const float COHERENCE_ACTIVITY_W   = 0.50f; // RUE behavior.py
        private const float ENERGY_ACTIVITY_W      = 0.05f; // RUE behavior.py
        private const float RESONANCE_ACCUM_RATE   = 0.008f; // AME v7 evolution.resonance
        private const float SPECTRAL_DRIFT_RATE    = 0.0002f;
        private const float ARCHETYPE_SHIFT_THRESH = 0.75f;  // resonance level to trigger shift
        private const float STEPS_PER_HOUR         = 720f;   // simulation steps per real hour

        // NPC archetype → home planet
        private static readonly Dictionary<string, string> ARCHETYPE_PLANET = new()
        {
            ["Seated"]        = "ForestHeart",   ["FlowDancer"]    = "WaterFlow",
            ["Dervish"]       = "SkySpiral",     ["PairA"]         = "ForestHeart",
            ["PairB"]         = "WaterFlow",     ["EarthDragon"]   = "ForestHeart",
            ["FireDragon"]    = "DarkContrast",  ["WaterDragon"]   = "WaterFlow",
            ["ElderAirDragon"]= "SkySpiral",
        };

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private readonly Dictionary<string, float[]> _runtimePhases = new();
        private bool _loaded;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return new WaitUntil(() => CosmosGenerationSystem.Instance?.IsLoaded ?? false);
            LoadOrInitStates();
            _loaded = true;
        }

        // -------------------------------------------------------------------------
        // Initialise / load NPC states
        // -------------------------------------------------------------------------
        private void LoadOrInitStates()
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            // Ensure one state per archetype
            foreach (var archetypeId in ARCHETYPE_PLANET.Keys)
            {
                bool found = false;
                foreach (var s in universe.npcStates)
                    if (s.archetypeId == archetypeId) { found = true; break; }

                if (!found)
                    universe.npcStates.Add(CreateInitialState(archetypeId));
            }

            // Load runtime phases from persistent state
            foreach (var state in universe.npcStates)
            {
                float[] phases = new float[BRAIN_SIZE];
                for (int i = 0; i < BRAIN_SIZE; i++)
                    phases[i] = state.phasesNorm[i] * Mathf.PI * 2f;
                _runtimePhases[state.archetypeId] = phases;
            }

            Debug.Log($"[NpcEvolutionSystem] Loaded {universe.npcStates.Count} NPC evolution states.");
        }

        private NpcEvolutionState CreateInitialState(string archetypeId)
        {
            string planetId = ARCHETYPE_PLANET.TryGetValue(archetypeId, out string p) ? p : "SourceVeil";
            var state = new NpcEvolutionState
            {
                archetypeId      = archetypeId,
                planetId         = planetId,
                currentArchetype = archetypeId,
                phasesNorm       = new float[BRAIN_SIZE],
                spectralDrift    = new float[7],
            };
            // Random initial phases (uniform 0–1, normalised from 0–2π)
            for (int i = 0; i < BRAIN_SIZE; i++)
                state.phasesNorm[i] = UnityEngine.Random.value;
            return state;
        }

        // -------------------------------------------------------------------------
        // Advance all NPCs by elapsed real-world hours
        // Called by CosmosClockSystem on session load
        // -------------------------------------------------------------------------
        public List<string> AdvanceByElapsedTime(float elapsedHours)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(elapsedHours * STEPS_PER_HOUR));
            var events = new List<string>();

            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return events;

            foreach (var state in universe.npcStates)
            {
                string prevArch = state.currentArchetype;
                StepNpc(state, steps);
                if (state.currentArchetype != prevArch)
                    events.Add($"{state.archetypeId} shifted → {state.currentArchetype} " +
                               $"(resonance={state.resonance:F2})");
            }

            Debug.Log($"[NpcEvolutionSystem] Advanced {universe.npcStates.Count} NPCs by " +
                      $"{elapsedHours:F1}h ({steps} steps). Events: {events.Count}");
            return events;
        }

        // -------------------------------------------------------------------------
        // Core NPC step — RUE SnowflakeBrain + behavior + AME evolution
        // -------------------------------------------------------------------------
        private void StepNpc(NpcEvolutionState state, int steps)
        {
            if (!_runtimePhases.TryGetValue(state.archetypeId, out float[] phases))
            {
                phases = new float[BRAIN_SIZE];
                for (int i = 0; i < BRAIN_SIZE; i++)
                    phases[i] = state.phasesNorm[i] * Mathf.PI * 2f;
                _runtimePhases[state.archetypeId] = phases;
            }

            float E = GetPlanetEnergy(state.planetId);

            for (int s = 0; s < steps; s++)
            {
                // SnowflakeBrain Kuramoto update (rue-engine/agents/snowflake_brain.py)
                float[] coupling = new float[BRAIN_SIZE];
                for (int i = 0; i < BRAIN_SIZE; i++)
                    for (int j = 0; j < BRAIN_SIZE; j++)
                        coupling[i] += Mathf.Sin(phases[j] - phases[i]);

                for (int i = 0; i < BRAIN_SIZE; i++)
                    phases[i] = (phases[i] + KURAMOTO_COUPLING * coupling[i] +
                                 ENERGY_INPUT_WEIGHT * E) % (Mathf.PI * 2f);

                // Coherence (Kuramoto order parameter)
                float cosSum = 0f, sinSum = 0f;
                for (int i = 0; i < BRAIN_SIZE; i++)
                { cosSum += Mathf.Cos(phases[i]); sinSum += Mathf.Sin(phases[i]); }
                state.coherence = Mathf.Sqrt(cosSum * cosSum + sinSum * sinSum) / BRAIN_SIZE;

                // Activity (RUE behavior.py: activity = 0.5*coherence + 0.05*E)
                state.activity = COHERENCE_ACTIVITY_W * state.coherence + ENERGY_ACTIVITY_W * E;

                // Energy accumulation (RUE agent: energy += 0.1 * activity)
                state.energy += 0.1f * state.activity;

                // Resonance (AME v7 evolution: resonance += uniform(0, 0.2))
                // Here: deterministic, resonance += activity * dt
                state.resonance = Mathf.Clamp01(state.resonance + RESONANCE_ACCUM_RATE * state.activity);

                // Spectral drift toward orbital equilibrium (slow, meaningful change)
                DriftSpectral(state);
            }

            // Save phases back to normalised persistent form
            for (int i = 0; i < BRAIN_SIZE; i++)
                state.phasesNorm[i] = phases[i] / (Mathf.PI * 2f);

            // Check archetype shift
            CheckArchetypeShift(state);
        }

        // -------------------------------------------------------------------------
        // Spectral drift — NPC gradually aligns with its planet's orbital field
        // -------------------------------------------------------------------------
        private static readonly string[] BANDS = { "red","orange","yellow","green","blue","indigo","violet" };

        private void DriftSpectral(NpcEvolutionState state)
        {
            var config = CosmosGenerationSystem.Instance?.GetPlanetConfig(state.planetId);
            if (config == null) return;

            float[] equilibrium = new float[7]
            {
                config.equilibriumSpectral.red,    config.equilibriumSpectral.orange,
                config.equilibriumSpectral.yellow, config.equilibriumSpectral.green,
                config.equilibriumSpectral.blue,   config.equilibriumSpectral.indigo,
                config.equilibriumSpectral.violet,
            };

            for (int i = 0; i < 7; i++)
            {
                float drift = (equilibrium[i] - 0.5f) * SPECTRAL_DRIFT_RATE * state.activity;
                state.spectralDrift[i] = Mathf.Clamp(state.spectralDrift[i] + drift, -0.25f, 0.25f);
            }
        }

        // -------------------------------------------------------------------------
        // Archetype shift — NPC resonance passed a threshold, they've changed
        // (AME v7 Discovery: discovery > 1 → hidden=False, applied to NPC identity)
        // -------------------------------------------------------------------------
        private void CheckArchetypeShift(NpcEvolutionState state)
        {
            if (state.resonance < ARCHETYPE_SHIFT_THRESH) return;

            var config = CosmosGenerationSystem.Instance?.GetPlanetConfig(state.planetId);
            if (config == null) return;

            string newArch = config.npcPopulation.naturalArchetype;
            if (newArch == state.currentArchetype) return;

            // Reset resonance after shift (new cycle begins)
            state.resonance *= 0.4f;
            state.currentArchetype = newArch;
            state.archetypeShiftCount++;
            state.lastShiftUtc = DateTime.UtcNow.ToString("o");

            Debug.Log($"[NpcEvolutionSystem] {state.archetypeId} shifted to {newArch} " +
                      $"(shift #{state.archetypeShiftCount})");
        }

        // -------------------------------------------------------------------------
        // Star field energy at a planet (RUE star model)
        // -------------------------------------------------------------------------
        private float GetPlanetEnergy(string planetId)
        {
            var config = CosmosGenerationSystem.Instance?.GetPlanetConfig(planetId);
            if (config == null) return 5.0f;
            // Star base + occasional flare (Bernoulli p=0.01)
            float flare = UnityEngine.Random.value < 0.01f ? 10.0f : 0.0f;
            return config.fieldStrength + flare;
        }

        // -------------------------------------------------------------------------
        // Public read API
        // -------------------------------------------------------------------------
        public NpcEvolutionState GetState(string archetypeId)
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return null;
            foreach (var s in universe.npcStates)
                if (s.archetypeId == archetypeId) return s;
            return null;
        }

        public float GetCoherence(string archetypeId) => GetState(archetypeId)?.coherence ?? 0f;
        public float GetActivity(string archetypeId)  => GetState(archetypeId)?.activity  ?? 0f;
        public string GetCurrentArchetype(string archetypeId)
            => GetState(archetypeId)?.currentArchetype ?? archetypeId;

        /// <summary>The effective spectral field for this NPC (baseline + drift).</summary>
        public float[] GetEffectiveSpectral(string archetypeId)
        {
            var state = GetState(archetypeId);
            if (state == null) return new float[7];

            var config = CosmosGenerationSystem.Instance?.GetPlanetConfig(state.planetId);
            float[] baseline = config != null ? new float[7]
            {
                config.equilibriumSpectral.red,    config.equilibriumSpectral.orange,
                config.equilibriumSpectral.yellow, config.equilibriumSpectral.green,
                config.equilibriumSpectral.blue,   config.equilibriumSpectral.indigo,
                config.equilibriumSpectral.violet,
            } : new float[7];

            float[] effective = new float[7];
            for (int i = 0; i < 7; i++)
                effective[i] = Mathf.Clamp01(baseline[i] + state.spectralDrift[i]);
            return effective;
        }
    }
}
