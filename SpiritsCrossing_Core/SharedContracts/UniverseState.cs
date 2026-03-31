// SpiritsCrossing — UniverseState.cs
// Full persistent game container. Owned and serialized by UniverseStateManager.
// Aggregates player identity, session history, planet states, myth state,
// portal decisions, realm outcomes, and universe cycle values.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Companions;
using SpiritsCrossing.Memory;
using SpiritsCrossing.Lifecycle;
using SpiritsCrossing.World;
using SpiritsCrossing.Autonomous;

namespace SpiritsCrossing
{
    // -------------------------------------------------------------------------
    // Dryad whisper progress — how many whisper lines each dryad has delivered.
    // Stored here so dryads remember across sessions what they have already said.
    // -------------------------------------------------------------------------
    [Serializable]
    public class DryadWhisperEntry
    {
        public string dryadId;
        public int    whisperIndex; // index of the NEXT line to deliver (0 = nothing spoken yet)
    }

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

        // Age tier — set at profile creation, determines myth filtering,
        // companion behavior, AI learning, and solo/co-op mode
        public AgeTier ageTier = AgeTier.Voyager;

        /// <summary>Cached tier config. Not serialized — rebuilt from ageTier on load.</summary>
        [NonSerialized] private AgeTierProfile _ageTierConfig;
        public AgeTierProfile AgeTierConfig =>
            _ageTierConfig ?? (_ageTierConfig = AgeTierProfile.ForTier(ageTier));

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

        // RUE Universe.time — total simulation steps elapsed across all sessions.
        // Incremented by RUELiveLoop during live play and by RecordRealmOutcome.
        // Equivalent to Universe.time in rue-engine/simulation/universe.py.
        public long universeTimeSteps;

        // Companion bonds (all 26 animal companions across 4 elements)
        public List<CompanionBondState> companions = new List<CompanionBondState>();

        // Autonomous world state
        public List<NpcEvolutionState>    npcStates             = new List<NpcEvolutionState>();
        public List<PlanetAutonomyState>  planetAutonomyStates  = new List<PlanetAutonomyState>();
        public string                     lastSessionUtc;

        // NPC autonomy — memories, creations, bonds, emergent identities
        public List<NpcAutonomyState>     npcAutonomyStates     = new List<NpcAutonomyState>();
        public List<VibrationalCreation>  worldCreations        = new List<VibrationalCreation>();

        // User companion assignments (primary / elemental / realm / session)
        public CompanionAssignment userAssignment = new CompanionAssignment();

        // User companion rules (trigger → action pairs)
        public CompanionRuleSet companionRules = new CompanionRuleSet();

        // Resonance learning — the game's memory of who the player is
        public ResonanceLearningState learningState = new ResonanceLearningState();

        // Lifecycle — Birth / Source / Rebirth cycle state
        public LifecycleState lifecycle = new LifecycleState();

        // Discovered ruins — ancient and newer ruins found across all planets
        public List<string> discoveredRuinIds = new List<string>();

        // Forest World — dryad whisper progress (which lines each dryad has spoken)
        public List<DryadWhisperEntry> dryadWhisperProgress = new List<DryadWhisperEntry>();

        // Portal sites discovered and activated across all worlds
        public List<DiscoveredPortalSiteRecord> discoveredPortalSites = new List<DiscoveredPortalSiteRecord>();

        // Vibrational messages sent to planets (cap 20)
        public List<VibrationalMessage> sentMessages = new List<VibrationalMessage>();

        // Known player/NPC presences on the cosmos map
        public List<CosmosPresence> knownPresences = new List<CosmosPresence>();

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

            // Each realm visit counts as RUE universe steps proportional to its duration.
            // 720 steps/hour (from NpcEvolutionSystem.STEPS_PER_HOUR) → ~1 step/5 seconds.
            // A typical realm outcome doesn't carry exact duration, so we use a fixed
            // per-visit quantum that approximates a meaningful universe advancement.
            universeTimeSteps += 720;   // 1 simulated hour per realm visit

            // Age-tier contrast dampening — younger tiers experience less contrast
            outcome.contrast *= AgeTierConfig.contrastDampen;

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

        /// <summary>The animalId of the companion currently accompanying the player, or null.</summary>
        public string activeCompanionId => GetActiveCompanion()?.animalId;

        public List<VibrationalMessage> GetMessagesByPlanet(string planetId)
        {
            var result = new List<VibrationalMessage>();
            foreach (var m in sentMessages)
                if (m.planetId == planetId) result.Add(m);
            return result;
        }

        // -------------------------------------------------------------------------
        // Portal site discovery helpers
        // -------------------------------------------------------------------------
        public void AddDiscoveredPortalSite(DiscoveredPortalSiteRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.siteId)) return;
            foreach (var s in discoveredPortalSites)
                if (s.siteId == record.siteId) return; // already recorded
            discoveredPortalSites.Add(record);
        }

        public DiscoveredPortalSiteRecord GetDiscoveredPortalSite(string siteId)
        {
            foreach (var s in discoveredPortalSites)
                if (s.siteId == siteId) return s;
            return null;
        }

        /// <summary>
        /// Called by PortalTransitionController when the player travels to another planet
        /// via a world-travel portal site. Unlocks the planet and logs the travel timestamp.
        /// </summary>
        public void RecordWorldTravel(string planetId)
        {
            if (string.IsNullOrEmpty(planetId)) return;
            var planet = GetOrCreatePlanet(planetId);
            planet.unlocked = true;
            lastPlayedUtc = DateTime.UtcNow.ToString("o");
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
