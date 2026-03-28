// SpiritsCrossing — PlanetAutonomySystem.cs
// Planets evolve autonomously using RUE orbital mechanics and star energy.
// The cosmos is alive whether or not the player is present.
//
// RUE math used directly:
//   rue-engine/simulation/planet.py:  angle += 0.01 / radius  (Keplerian orbit)
//   rue-engine/simulation/star.py:    E = 5.0 + flare (Bernoulli p=0.01, bonus=10)
//   rue-engine/simulation/universe.py: planets orbit, star emits each step
//
// Planet growth driven by orbital field physics:
//   G(t) += K(r) * F(r) * backgroundRate * dt   [logistic saturation near 1.0]
//
// Solar flares: when the star flares, each planet receives a boost
//   proportional to its field strength: flareBoost[planet] = flare * F(r) / F_max
//
// Decay: planets that have never been visited slowly lose growth
//   (entropy — the cosmos needs resonant observers)
//
// AME v7 Discovery: hidden cities → visible via discovery accumulation.
//   Applied here: planets have "discovery events" (ruins discovered, NPC shifts)
//   that are triggered by autonomous evolution reaching thresholds.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Cosmos;

namespace SpiritsCrossing.Autonomous
{
    [Serializable]
    public class PlanetAutonomyState
    {
        public string planetId;
        public float  orbitalAngle;      // current position (radians)
        public float  autonomousGrowth;  // growth from autonomous simulation (separate from player)
        public float  totalFlareEnergy;  // cumulative flare energy received
        public int    flareEventCount;
        public string lastFlareUtc;
        public bool   hasDecayed;        // true if decay has begun
    }

    public class PlanetAutonomySystem : MonoBehaviour
    {
        public static PlanetAutonomySystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // RUE constants (from rue-engine)
        // -------------------------------------------------------------------------
        private const float KEPLERIAN_BASE      = 0.01f;   // planet.py: angular_speed = 0.01/radius
        private const float STAR_BASE_ENERGY    = 5.0f;    // config.py STAR_BASE_ENERGY
        private const float SOLAR_FLARE_BONUS   = 10.0f;   // config.py SOLAR_FLARE_BONUS
        private const float SOLAR_FLARE_PROB    = 0.01f;   // config.py SOLAR_FLARE_PROBABILITY
        private const float BACKGROUND_RATE     = 0.00012f; // slow autonomous growth per step
        private const float DECAY_RATE          = 0.0001f;  // growth loss per hour if unvisited
        private const float STEPS_PER_HOUR      = 360f;    // planet simulation steps per real hour

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private bool _loaded;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            InitStates();
            _loaded = true;
        }

        private void InitStates()
        {
            var universe = UniverseStateManager.Instance?.Current;
            var cosmos   = CosmosGenerationSystem.Instance?.Data;
            if (universe == null || cosmos == null) return;

            foreach (var planet in cosmos.planets)
            {
                bool found = false;
                foreach (var s in universe.planetAutonomyStates)
                    if (s.planetId == planet.planetId) { found = true; break; }

                if (!found)
                    universe.planetAutonomyStates.Add(new PlanetAutonomyState
                    {
                        planetId        = planet.planetId,
                        orbitalAngle    = planet.meanEquilibriumPhase,
                        autonomousGrowth = 0f,
                    });
            }
        }

        // -------------------------------------------------------------------------
        // Advance all planets by elapsed real-world hours
        // -------------------------------------------------------------------------
        public List<string> AdvanceByElapsedTime(float elapsedHours)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(elapsedHours * STEPS_PER_HOUR));
            var events = new List<string>();

            var universe = UniverseStateManager.Instance?.Current;
            var cosmos   = CosmosGenerationSystem.Instance?.Data;
            if (universe == null || cosmos == null) return events;

            float flareMaxF = cosmos.sourceField.FieldStrength(8.0f); // normalization

            for (int s = 0; s < steps; s++)
            {
                // ---- Star emits (RUE star.emit_energy) ----
                float flare = UnityEngine.Random.value < SOLAR_FLARE_PROB ? SOLAR_FLARE_BONUS : 0f;
                bool isFlareTick = flare > 0f;

                // ---- Advance each planet ----
                foreach (var state in universe.planetAutonomyStates)
                {
                    var config = cosmos.GetPlanet(state.planetId);
                    if (config == null) continue;

                    float r = config.orbitalRadius;
                    float F = config.fieldStrength;
                    float K = config.couplingConstant;

                    // Keplerian orbital advance (planet.py: angle += 0.01/radius per step)
                    state.orbitalAngle = (state.orbitalAngle + KEPLERIAN_BASE / r) % (Mathf.PI * 2f);

                    // Autonomous growth: G += K * F * backgroundRate * (1 - G) [logistic saturation]
                    float growth = K * F * BACKGROUND_RATE * (1f - state.autonomousGrowth);
                    state.autonomousGrowth = Mathf.Clamp01(state.autonomousGrowth + growth);

                    // Solar flare boost — closer planets receive more (proportional to F(r)/F_max)
                    if (isFlareTick)
                    {
                        float flareBoost = flare * (F / flareMaxF) * 0.02f;
                        state.autonomousGrowth = Mathf.Clamp01(state.autonomousGrowth + flareBoost);
                        state.totalFlareEnergy += flare;
                        state.flareEventCount++;
                        state.lastFlareUtc = DateTime.UtcNow.ToString("o");
                    }
                }
            }

            // ---- Apply decay for unvisited planets ----
            foreach (var state in universe.planetAutonomyStates)
            {
                var planetState = universe.GetOrCreatePlanet(state.planetId);
                if (planetState.visitCount == 0 && state.autonomousGrowth > 0f)
                {
                    float decayAmount = DECAY_RATE * elapsedHours;
                    state.autonomousGrowth = Mathf.Max(0f, state.autonomousGrowth - decayAmount);
                    state.hasDecayed = true;
                }

                // Merge autonomous growth into the main planet state
                float prev = planetState.growth;
                planetState.growth = Mathf.Clamp01(
                    Mathf.Max(planetState.growth, state.autonomousGrowth * 0.6f));

                if (planetState.growth - prev > 0.02f)
                    events.Add($"{state.planetId} grew to {planetState.growth:F2} " +
                               $"(autonomous K={CosmosGenerationSystem.Instance?.GetCouplingConstant(CosmosGenerationSystem.Instance?.GetPlanetConfig(state.planetId)?.orbitalRadius ?? 16):F3})");
            }

            // ---- Flare events ----
            int flares = 0;
            foreach (var state in universe.planetAutonomyStates)
                if (state.flareEventCount > 0) flares = Mathf.Max(flares, state.flareEventCount);
            if (flares > 0)
                events.Add($"Solar flare affected the cosmos ({flares} flare event(s) in {elapsedHours:F1}h)");

            universe.RecalculateUniverseCycle();

            Debug.Log($"[PlanetAutonomySystem] Advanced by {elapsedHours:F1}h ({steps} steps). " +
                      $"Events: {events.Count}");
            return events;
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------
        public float GetOrbitalAngle(string planetId)
        {
            foreach (var s in UniverseStateManager.Instance?.Current.planetAutonomyStates ?? new System.Collections.Generic.List<PlanetAutonomyState>())
                if (s.planetId == planetId) return s.orbitalAngle;
            return 0f;
        }

        public float GetAutonomousGrowth(string planetId)
        {
            foreach (var s in UniverseStateManager.Instance?.Current.planetAutonomyStates ?? new System.Collections.Generic.List<PlanetAutonomyState>())
                if (s.planetId == planetId) return s.autonomousGrowth;
            return 0f;
        }

        /// <summary>Star energy available to a planet right now (with live flare roll).</summary>
        public float SampleStarEnergy(string planetId)
        {
            var config = CosmosGenerationSystem.Instance?.GetPlanetConfig(planetId);
            if (config == null) return STAR_BASE_ENERGY;
            float flare = UnityEngine.Random.value < SOLAR_FLARE_PROB ? SOLAR_FLARE_BONUS : 0f;
            return config.fieldStrength + flare;
        }
    }
}
