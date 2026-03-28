// SpiritsCrossing — UniverseState.cs
// Full persistent game container. Owned and serialized by UniverseStateManager.
// Aggregates player identity, session history, planet states, myth state,
// portal decisions, realm outcomes, and universe cycle values.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Companions;
using SpiritsCrossing.Memory;

namespace SpiritsCrossing
{
    // -------------------------------------------------------------------------
    // Per-planet persistent record.
    // Wraps PlanetNodeController's runtime values for save/load.
    // -------------------------------------------------------------------------
    [Serializable]
    public class PlanetState
    {
        public string planetId;
        public bool   unlocked;
        public int    visitCount;

        [Range(0f, 1f)] public float affinityScore;
        [Range(0f, 1f)] public float growth        = 0.25f;
        [Range(0f, 1f)] public float healing       = 0.25f;
        [Range(0f, 1f)] public float rebirthCharge = 0.10f;

        // Summary of last visit outcome for environmental callbacks
        public float lastCelebration;
        public float lastContrast;
        public float lastImportance;
        public string lastVisitUtc;

        public void RecordVisit(RealmOutcome outcome)
        {
            visitCount++;
            lastCelebration = outcome.celebration;
            lastContrast    = outcome.contrast;
            lastImportance  = outcome.importance;
            lastVisitUtc    = outcome.utcTimestamp;

            growth        = Mathf.Clamp01(growth        + outcome.celebration * 0.06f + outcome.importance * 0.04f);
            healing       = Mathf.Clamp01(healing       + outcome.celebration * 0.04f - outcome.contrast   * 0.01f);
            rebirthCharge = Mathf.Clamp01(rebirthCharge + outcome.contrast    * 0.05f + outcome.importance * 0.03f);
        }
    }

    // -------------------------------------------------------------------------
    // Root save container.
    // -------------------------------------------------------------------------
    [Serializable]
    public class UniverseState
    {
        public string saveVersion = "1.0";
        public string lastPlayedUtc;
        public int    totalSessionCount;

        // Long-term player identity (accumulates across all sessions)
        public PlayerIdentityProfile playerIdentity = new PlayerIdentityProfile();

        // Rolling persistent resonance snapshot (EMA of all session samples)
        public PlayerResponseSample persistentResonance = new PlayerResponseSample();

        // Full session history (most recent last; cap at 50 for save size)
        public List<SessionResonanceResult> sessionHistory = new List<SessionResonanceResult>();

        // Per-planet persistent state
        public List<PlanetState> planets = new List<PlanetState>();

        // Myth layer
        public MythState mythState = new MythState();

        // Most recent portal decision (used to re-enter cave state on load)
        public PortalDecision lastPortalDecision;

        // Most recent realm outcome
        public RealmOutcome lastRealmOutcome;

        // Universe cycle values (mirrors UniverseCycleDirector)
        [Range(0f, 1f)] public float universeBirthPotential;
        [Range(0f, 1f)] public float universeRebirthPotential;

        // Companion bonds (all 26 animal companions across 4 elements)
        public List<CompanionBondState> companions = new List<CompanionBondState>();

        // Resonance learning — the game's memory of who the player is
        public ResonanceLearningState learningState = new ResonanceLearningState();

        // -------------------------------------------------------------------------

        public PlanetState GetOrCreatePlanet(string planetId)
        {
            foreach (var p in planets)
                if (p.planetId == planetId) return p;

            var newPlanet = new PlanetState { planetId = planetId };
            planets.Add(newPlanet);
            return newPlanet;
        }

        public void RecordSession(SessionResonanceResult result)
        {
            sessionHistory.Add(result);
            if (sessionHistory.Count > 50)
                sessionHistory.RemoveAt(0);

            totalSessionCount++;
            lastPlayedUtc = DateTime.UtcNow.ToString("o");

            persistentResonance.BlendIn(result.resonanceSample, 0.15f);
            playerIdentity.IntegrateSession(result.resonanceSample);
            // Learning state integration (ResonanceMemorySystem also subscribes, but
            // we also integrate here so the state is correct immediately on load)
            learningState.IntegrateSession(result.resonanceSample);

            // Update the planet that was targeted this session
            if (!string.IsNullOrEmpty(result.currentAffinityPlanet))
            {
                var planet = GetOrCreatePlanet(result.currentAffinityPlanet);
                planet.affinityScore = Mathf.Clamp01(Mathf.Max(planet.affinityScore, result.currentAffinityScore * 0.1f));
            }
        }

        public void RecordRealmOutcome(RealmOutcome outcome)
        {
            lastRealmOutcome = outcome;
            lastPlayedUtc    = DateTime.UtcNow.ToString("o");

            if (!string.IsNullOrEmpty(outcome.planetId))
                GetOrCreatePlanet(outcome.planetId).RecordVisit(outcome);

            RecalculateUniverseCycle();
        }

        public CompanionBondState GetOrCreateBond(string animalId)
        {
            foreach (var b in companions)
                if (b.animalId == animalId) return b;
            var bond = new CompanionBondState { animalId = animalId };
            companions.Add(bond);
            return bond;
        }

        public void RecordCompanionEncounter(string animalId, float resonanceScore)
        {
            var bond = GetOrCreateBond(animalId);
            bond.encounterCount++;
            bond.lastSeenUtc = DateTime.UtcNow.ToString("o");
            // Bond grows slowly per encounter if resonance above threshold
            if (resonanceScore > 0.25f)
                bond.bondLevel = Mathf.Clamp01(bond.bondLevel + resonanceScore * 0.02f);
        }

        public CompanionBondState GetActiveCompanion()
        {
            foreach (var b in companions)
                if (b.isActive) return b;
            return null;
        }

        public void SetActiveCompanion(string animalId)
        {
            foreach (var b in companions) b.isActive = false;
            if (!string.IsNullOrEmpty(animalId))
                GetOrCreateBond(animalId).isActive = true;
        }

        public void RecalculateUniverseCycle()
        {
            if (planets.Count == 0) return;

            float growth = 0f, healing = 0f, rebirth = 0f;
            foreach (var p in planets) { growth += p.growth; healing += p.healing; rebirth += p.rebirthCharge; }
            float n = planets.Count;

            universeBirthPotential  = Mathf.Clamp01((growth / n) * 0.55f + (healing / n) * 0.45f);
            universeRebirthPotential = Mathf.Clamp01((rebirth / n) * 0.65f + (1f - healing / n) * 0.35f);
        }
    }
}
