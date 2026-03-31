// SpiritsCrossing — NpcAutonomySystem.cs
// The engine of NPC freedom. Runs alongside NpcEvolutionSystem.
//
// Every step, for each NPC:
//   1. DESIRE  — compute desire scores from brain state + environment + memory
//   2. CHOOSE  — the highest desire wins (no scripting, no overrides)
//   3. ACT     — execute the chosen desire: create, explore, bond, learn, travel
//   4. REMEMBER — integrate the experience as a memory imprint
//   5. BOND    — update phase synchrony with co-located NPCs
//   6. BECOME  — recalculate emergent identity from all lived experience
//
// DESIRE COMPUTATION
//   Create  = coherence × creativeUrge × (1 - recentCreations)
//   Explore = activity × noveltyHunger × (1 - terrainFamiliarity)
//   Bond    = socialDrive × nearbyBeings × (1 - bondSaturation)
//   Rest    = (1 - activity) × fatigue
//   Teach   = creationCount × bondCount × coherence
//   Learn   = wonderDrive × noveltyAvailable × (1 - memoryFullness)
//   Travel  = exploreDrive × coherence × worldsUnvisited × (experience > threshold)
//
// Desires are NOT assigned. They emerge from the NPC's current state.
// The same NPC will desire different things at different moments in its life.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Cosmos;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.World;

namespace SpiritsCrossing.Autonomous
{
    public class NpcAutonomySystem : MonoBehaviour
    {
        public static NpcAutonomySystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // ---------------------------------------------------------------------
        // Inspector
        // ---------------------------------------------------------------------
        [Header("Desire Weights")]
        [Range(0f, 2f)] public float createWeight  = 1.0f;
        [Range(0f, 2f)] public float exploreWeight = 1.0f;
        [Range(0f, 2f)] public float bondWeight    = 1.0f;
        [Range(0f, 2f)] public float restWeight    = 1.0f;
        [Range(0f, 2f)] public float teachWeight   = 0.8f;
        [Range(0f, 2f)] public float learnWeight   = 1.2f;
        [Range(0f, 2f)] public float travelWeight  = 0.6f;

        [Header("Thresholds")]
        [Tooltip("Minimum experience level before travel desire activates.")]
        [Range(0f, 1f)] public float travelExperienceThreshold = 0.25f;
        [Tooltip("Minimum coherence to attempt creation.")]
        [Range(0f, 1f)] public float creationCoherenceMin = 0.30f;
        [Tooltip("Probability per step that a Create desire actually produces a creation.")]
        [Range(0f, 0.1f)] public float creationProbability = 0.02f;
        [Tooltip("Probability per step that a Travel desire actually triggers migration.")]
        [Range(0f, 0.05f)] public float travelProbability = 0.005f;

        [Header("Memory")]
        public float memoryDecayRate = 0.995f;

        [Header("Becoming")]
        [Tooltip("Divergence threshold that triggers a 'becoming' event.")]
        [Range(0.1f, 0.5f)] public float becomingThreshold = 0.20f;

        [Header("Debug")]
        public bool logDesires    = false;
        public bool logCreations  = true;
        public bool logTravel     = true;
        public bool logBecoming   = true;

        // ---------------------------------------------------------------------
        // Events
        // ---------------------------------------------------------------------
        public event Action<string, VibrationalCreation> OnNpcCreated;       // archetypeId, creation
        public event Action<string, string, string>      OnNpcTraveled;      // archetypeId, fromPlanet, toPlanet
        public event Action<string, EmergentIdentity>    OnNpcBecame;        // archetypeId, new identity
        public event Action<string, NpcDesire>           OnNpcDesireChanged; // archetypeId, newDesire

        // ---------------------------------------------------------------------
        // State
        // ---------------------------------------------------------------------
        private readonly Dictionary<string, NpcAutonomyState> _states = new();

        // World creations — shared across all NPCs, persisted
        public List<VibrationalCreation> WorldCreations { get; private set; } = new();

        // ---------------------------------------------------------------------
        // Initialization
        // ---------------------------------------------------------------------
        public void Initialize(List<NpcAutonomyState> savedStates, List<VibrationalCreation> savedCreations)
        {
            _states.Clear();
            if (savedStates != null)
                foreach (var s in savedStates) _states[s.archetypeId] = s;

            WorldCreations = savedCreations ?? new List<VibrationalCreation>();
            Debug.Log($"[NpcAutonomySystem] Initialized {_states.Count} autonomy states, " +
                      $"{WorldCreations.Count} world creations.");
        }

        /// <summary>Get or create autonomy state for an NPC.</summary>
        public NpcAutonomyState GetOrCreate(string archetypeId, string planetId)
        {
            if (_states.TryGetValue(archetypeId, out var existing)) return existing;

            var state = new NpcAutonomyState
            {
                archetypeId    = archetypeId,
                homePlanetId   = planetId,
                currentPlanetId = planetId,
                memorySignature = new float[7],
                desireScores    = new float[7],
            };
            state.visitedPlanetIds.Add(planetId);
            _states[archetypeId] = state;
            return state;
        }

        // =================================================================
        // CORE STEP — called by NpcEvolutionSystem after each evolution step
        // =================================================================

        /// <summary>
        /// Run one autonomy step for an NPC. This is where freedom lives.
        /// </summary>
        public void StepAutonomy(NpcEvolutionState evoState)
        {
            if (evoState == null) return;
            var auto = GetOrCreate(evoState.archetypeId, evoState.planetId);

            // 1. Decay memories
            auto.DecayMemories(memoryDecayRate);

            // 2. Compute desires
            ComputeDesires(evoState, auto);

            // 3. Choose and act on highest desire
            NpcDesire chosen = ChooseDesire(auto);
            if (chosen != auto.currentDesire)
            {
                auto.currentDesire = chosen;
                OnNpcDesireChanged?.Invoke(evoState.archetypeId, chosen);
            }

            // 4. Execute the desire
            ExecuteDesire(chosen, evoState, auto);

            // 5. Update bonds with co-located NPCs
            UpdateBonds(evoState, auto);

            // 6. Recalculate emergent identity
            UpdateEmergentIdentity(evoState, auto);
        }

        // =================================================================
        // DESIRE COMPUTATION — emergent, not scripted
        // =================================================================

        private void ComputeDesires(NpcEvolutionState evo, NpcAutonomyState auto)
        {
            float coherence  = evo.coherence;
            float activity   = evo.activity;
            float experience = auto.ExperienceLevel;
            int   memories   = auto.memories.Count;
            int   creations  = auto.totalCreations;
            int   friends    = auto.FriendCount;
            int   worlds     = auto.WorldsVisited;

            // Spectral personality — which bands are strong determines desires
            float[] spec = NpcEvolutionSystem.Instance?.GetEffectiveSpectral(evo.archetypeId)
                           ?? new float[7];
            float violet = spec.Length > 6 ? spec[6] : 0f; // wonder/explore
            float indigo = spec.Length > 5 ? spec[5] : 0f; // social/bond
            float green  = spec.Length > 3 ? spec[3] : 0f; // rest/calm
            float yellow = spec.Length > 2 ? spec[2] : 0f; // seek/learn
            float red    = spec.Length > 0 ? spec[0] : 0f; // intensity/create

            // Creative urge: high coherence + intensity + not recently created
            float recentCreationDampen = creations > 0 ? Mathf.Clamp01(1f - creations * 0.05f) : 1f;
            auto.desireScores[0] = createWeight * coherence * (red + violet) * 0.5f * recentCreationDampen;

            // Explore: activity + wonder + unfamiliar terrain
            float terrainNovelty = 1f - Mathf.Clamp01(memories * 0.01f);
            auto.desireScores[1] = exploreWeight * activity * violet * terrainNovelty;

            // Bond: social drive + nearby beings
            float bondSat = friends > 0 ? Mathf.Clamp01(friends * 0.15f) : 0f;
            auto.desireScores[2] = bondWeight * indigo * (1f - bondSat);

            // Rest: inverse activity + calm
            auto.desireScores[3] = restWeight * (1f - activity) * green;

            // Teach: have creations + have friends + coherent enough to share
            float teachReady = (creations > 0 && friends > 0) ? coherence : 0f;
            auto.desireScores[4] = teachWeight * teachReady;

            // Learn: wonder + novelty available + memory not full
            float memorySpace = 1f - Mathf.Clamp01(memories / 100f);
            auto.desireScores[5] = learnWeight * yellow * memorySpace;

            // Travel: explore drive + coherence + experience + worlds unvisited
            float travelReady = experience >= travelExperienceThreshold ? 1f : 0f;
            float worldNovelty = Mathf.Clamp01(1f - worlds * 0.15f);
            auto.desireScores[6] = travelWeight * violet * coherence * worldNovelty * travelReady;

            if (logDesires)
                Debug.Log($"[NpcAutonomy] {evo.archetypeId} desires: " +
                          $"Create={auto.desireScores[0]:F2} Explore={auto.desireScores[1]:F2} " +
                          $"Bond={auto.desireScores[2]:F2} Rest={auto.desireScores[3]:F2} " +
                          $"Teach={auto.desireScores[4]:F2} Learn={auto.desireScores[5]:F2} " +
                          $"Travel={auto.desireScores[6]:F2}");
        }

        private NpcDesire ChooseDesire(NpcAutonomyState auto)
        {
            float best = -1f;
            int bestIdx = 3; // default to Rest
            for (int i = 0; i < auto.desireScores.Length; i++)
            {
                if (auto.desireScores[i] > best)
                {
                    best = auto.desireScores[i];
                    bestIdx = i;
                }
            }
            return (NpcDesire)bestIdx;
        }

        // =================================================================
        // DESIRE EXECUTION — the NPC acts on its choice
        // =================================================================

        private void ExecuteDesire(NpcDesire desire, NpcEvolutionState evo, NpcAutonomyState auto)
        {
            switch (desire)
            {
                case NpcDesire.Create:
                    TryCreate(evo, auto);
                    break;
                case NpcDesire.Explore:
                    // Exploration is passive — terrain affinity already guides placement
                    // Record a learning memory from current environment
                    ImprintEnvironment(evo, auto, "explore");
                    break;
                case NpcDesire.Bond:
                    // Bonding happens in UpdateBonds — boost bond growth rate this step
                    break;
                case NpcDesire.Rest:
                    // Rest consolidates memory — faster decay of weak memories, stronger vivid ones
                    ConsolidateMemory(auto);
                    break;
                case NpcDesire.Teach:
                    TryTeach(evo, auto);
                    break;
                case NpcDesire.Learn:
                    ImprintEnvironment(evo, auto, "learn");
                    break;
                case NpcDesire.Travel:
                    TryTravel(evo, auto);
                    break;
            }
        }

        // --- CREATE ---
        private void TryCreate(NpcEvolutionState evo, NpcAutonomyState auto)
        {
            if (evo.coherence < creationCoherenceMin) return;
            if (UnityEngine.Random.value > creationProbability) return;

            // Generate a novel pattern from the NPC's unique state
            float[] pattern = new float[7];
            float[] spec = NpcEvolutionSystem.Instance?.GetEffectiveSpectral(evo.archetypeId)
                           ?? new float[7];

            MemoryImprint inspiration = auto.StrongestMemory();

            for (int i = 0; i < 7; i++)
            {
                // Blend: own spectral (40%) + memory signature (30%) + strongest memory (20%) + noise (10%)
                float memory = auto.memorySignature[i];
                float inspire = (inspiration != null) ? inspiration.vibrationalTrace[i] : 0.5f;
                float noise = UnityEngine.Random.Range(-0.1f, 0.1f);
                pattern[i] = Mathf.Clamp01(
                    spec[i] * 0.40f + memory * 0.30f + inspire * 0.20f + noise + 0.05f);
            }

            var creation = new VibrationalCreation
            {
                creationId         = $"creation_{evo.archetypeId}_{auto.totalCreations}",
                creatorArchetypeId = evo.archetypeId,
                planetId           = auto.currentPlanetId,
                terrainType        = auto.identity.dominantQuality ?? "unknown",
                pattern            = pattern,
                coherence          = evo.coherence,
                inspirationSource  = inspiration?.sourceTag ?? "self",
                utcCreated         = DateTime.UtcNow.ToString("o"),
            };

            WorldCreations.Add(creation);
            auto.creationIds.Add(creation.creationId);
            auto.totalCreations++;

            // Creating IS an experience — remember it
            auto.AddMemory(new MemoryImprint
            {
                sourceTag        = "creation",
                sourceId         = creation.creationId,
                vibrationalTrace = (float[])pattern.Clone(),
                strength         = evo.coherence * 0.8f,
                emotionalValence = 0.8f, // creating feels good
                utcTimestamp     = creation.utcCreated,
            });

            OnNpcCreated?.Invoke(evo.archetypeId, creation);
            if (logCreations)
                Debug.Log($"[NpcAutonomy] {evo.archetypeId} CREATED: {creation.creationId} " +
                          $"coherence={creation.coherence:F2} inspired by {creation.inspirationSource}");
        }

        // --- TRAVEL ---
        private void TryTravel(NpcEvolutionState evo, NpcAutonomyState auto)
        {
            if (UnityEngine.Random.value > travelProbability) return;
            if (auto.ExperienceLevel < travelExperienceThreshold) return;

            // Choose destination by resonance pull with distant worlds
            var cosmos = CosmosGenerationSystem.Instance;
            if (cosmos?.Data == null) return;

            string bestPlanet = null;
            float bestPull = 0f;
            float[] spec = NpcEvolutionSystem.Instance?.GetEffectiveSpectral(evo.archetypeId)
                           ?? new float[7];

            foreach (var planet in cosmos.Data.planets)
            {
                if (planet.planetId == auto.currentPlanetId) continue;

                // Compute resonance pull with distant planet's equilibrium
                float pull = 0f;
                var eq = planet.equilibriumSpectral;
                float[] eqArr = { eq.red, eq.orange, eq.yellow, eq.green, eq.blue, eq.indigo, eq.violet };
                for (int i = 0; i < 7; i++)
                    pull += spec[i] * eqArr[i];
                pull /= 7f;

                // Novelty bonus for unvisited worlds
                if (!auto.visitedPlanetIds.Contains(planet.planetId))
                    pull *= 1.5f;

                if (pull > bestPull) { bestPull = pull; bestPlanet = planet.planetId; }
            }

            if (bestPlanet == null) return;

            string from = auto.currentPlanetId;
            auto.currentPlanetId = bestPlanet;
            evo.planetId = bestPlanet; // update evolution system too
            auto.totalTravels++;
            if (!auto.visitedPlanetIds.Contains(bestPlanet))
                auto.visitedPlanetIds.Add(bestPlanet);

            // Remember the journey
            auto.AddMemory(new MemoryImprint
            {
                sourceTag        = "travel",
                sourceId         = bestPlanet,
                vibrationalTrace = spec,
                strength         = 0.75f,
                emotionalValence = 0.6f,
                utcTimestamp     = DateTime.UtcNow.ToString("o"),
            });

            OnNpcTraveled?.Invoke(evo.archetypeId, from, bestPlanet);
            if (logTravel)
                Debug.Log($"[NpcAutonomy] {evo.archetypeId} TRAVELED: {from} → {bestPlanet} " +
                          $"(pull={bestPull:F2} worlds={auto.WorldsVisited})");
        }

        // --- TEACH ---
        private void TryTeach(NpcEvolutionState evo, NpcAutonomyState auto)
        {
            // Find a bonded NPC and share a creation
            if (auto.creationIds.Count == 0 || auto.bonds.Count == 0) return;

            foreach (var bond in auto.bonds)
            {
                if (bond.bondLevel < 0.30f) continue;
                string otherId = bond.OtherNpc(evo.archetypeId);
                if (otherId == null) continue;

                // The other NPC learns from the creation
                if (_states.TryGetValue(otherId, out var otherAuto) && auto.creationIds.Count > 0)
                {
                    string cid = auto.creationIds[auto.creationIds.Count - 1];
                    VibrationalCreation creation = null;
                    foreach (var c in WorldCreations)
                        if (c.creationId == cid) { creation = c; break; }

                    if (creation != null)
                    {
                        otherAuto.AddMemory(new MemoryImprint
                        {
                            sourceTag        = "teaching",
                            sourceId         = evo.archetypeId,
                            vibrationalTrace = (float[])creation.pattern.Clone(),
                            strength         = bond.bondLevel * 0.6f,
                            emotionalValence = 0.5f,
                            utcTimestamp     = DateTime.UtcNow.ToString("o"),
                        });
                        creation.encounterCount++;
                        bond.sharedExperienceCount++;
                    }
                }
                break; // teach one NPC per step
            }
        }

        // --- IMPRINT ENVIRONMENT ---
        private void ImprintEnvironment(NpcEvolutionState evo, NpcAutonomyState auto, string source)
        {
            float[] spec = NpcEvolutionSystem.Instance?.GetEffectiveSpectral(evo.archetypeId)
                           ?? new float[7];

            // Blend with terrain if available
            var terrain = TerrainResonanceSystem.Instance?.CurrentPlayerRegion;
            if (terrain != null)
            {
                var terrainField = terrain.EffectiveField();
                float[] tf = { terrainField.red, terrainField.orange, terrainField.yellow,
                               terrainField.green, terrainField.blue, terrainField.indigo, terrainField.violet };
                for (int i = 0; i < 7; i++)
                    spec[i] = Mathf.Clamp01(spec[i] * 0.6f + tf[i] * 0.4f);
            }

            auto.AddMemory(new MemoryImprint
            {
                sourceTag        = source,
                sourceId         = terrain?.regionId ?? auto.currentPlanetId,
                vibrationalTrace = spec,
                strength         = evo.activity * 0.5f,
                emotionalValence = evo.coherence - 0.5f, // coherence > 0.5 = positive experience
                utcTimestamp     = DateTime.UtcNow.ToString("o"),
            });
        }

        // --- CONSOLIDATE MEMORY ---
        private void ConsolidateMemory(NpcAutonomyState auto)
        {
            // Rest strengthens strong memories, weakens weak ones
            foreach (var m in auto.memories)
            {
                if (m.strength > 0.50f)
                    m.strength = Mathf.Min(1f, m.strength * 1.002f); // vivid memories grow slightly
                else
                    m.strength *= 0.98f; // weak memories fade faster during rest
            }
        }

        // =================================================================
        // BONDS — inter-NPC relationship through phase synchronization
        // =================================================================

        private void UpdateBonds(NpcEvolutionState evo, NpcAutonomyState auto)
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            foreach (var otherEvo in universe.npcStates)
            {
                if (otherEvo.archetypeId == evo.archetypeId) continue;
                if (otherEvo.planetId != auto.currentPlanetId) continue; // must share a world

                // Compute phase synchrony between the two brains
                float sync = ComputePhaseSynchrony(evo, otherEvo);

                // Find or create bond
                NpcBond bond = null;
                foreach (var b in auto.bonds)
                    if (b.Involves(otherEvo.archetypeId)) { bond = b; break; }

                if (bond == null)
                {
                    bond = new NpcBond
                    {
                        npcIdA = evo.archetypeId,
                        npcIdB = otherEvo.archetypeId,
                    };
                    auto.bonds.Add(bond);
                }

                bond.phaseSynchrony = sync;

                // Bond grows when sharing the same world and brains are in sync
                bool isDesiredBond = auto.currentDesire == NpcDesire.Bond;
                float growthRate = isDesiredBond ? 0.003f : 0.001f;
                bond.bondLevel = Mathf.Clamp01(bond.bondLevel + sync * growthRate);

                // Slow decay when not co-located
                if (otherEvo.planetId != auto.currentPlanetId)
                    bond.bondLevel = Mathf.Max(0f, bond.bondLevel - 0.0005f);

                bond.lastInteractionUtc = DateTime.UtcNow.ToString("o");
            }
        }

        private float ComputePhaseSynchrony(NpcEvolutionState a, NpcEvolutionState b)
        {
            // Kuramoto order parameter between the two NPCs' 21-node phase arrays
            float cosSum = 0f, sinSum = 0f;
            int N = Mathf.Min(a.phasesNorm.Length, b.phasesNorm.Length);
            for (int i = 0; i < N; i++)
            {
                float diff = (a.phasesNorm[i] - b.phasesNorm[i]) * Mathf.PI * 2f;
                cosSum += Mathf.Cos(diff);
                sinSum += Mathf.Sin(diff);
            }
            return Mathf.Sqrt(cosSum * cosSum + sinSum * sinSum) / N;
        }

        // =================================================================
        // BECOMING — emergent identity from lived experience
        // =================================================================

        private static readonly string[] QUALITY_NAMES =
        {
            "courage",     // red
            "discipline",  // orange
            "freedom",     // yellow
            "stillness",   // green
            "flow",        // blue
            "vision",      // indigo
            "wonder",      // violet
        };

        private static readonly string[] EARNED_NAMES =
        {
            "The Fierce One",           // red dominant
            "The One Who Endures",      // orange
            "The Free Spirit",          // yellow
            "The One Who Listens",      // green
            "The River Walker",         // blue
            "The Seer",                 // indigo
            "The One Who Wonders",      // violet
        };

        private void UpdateEmergentIdentity(NpcEvolutionState evo, NpcAutonomyState auto)
        {
            var id = auto.identity;

            // Compute emergent signature from: spectral drift (40%) + memory (30%) +
            // creation history (20%) + bond influence (10%)
            float[] spec = NpcEvolutionSystem.Instance?.GetEffectiveSpectral(evo.archetypeId)
                           ?? new float[7];

            for (int i = 0; i < 7; i++)
            {
                float creationInfluence = 0f;
                if (auto.totalCreations > 0)
                {
                    // Average of all creation patterns
                    int count = 0;
                    foreach (var cid in auto.creationIds)
                    {
                        foreach (var c in WorldCreations)
                        {
                            if (c.creationId == cid)
                            {
                                creationInfluence += c.pattern[i];
                                count++;
                                break;
                            }
                        }
                    }
                    if (count > 0) creationInfluence /= count;
                }

                float bondInfluence = 0f;
                if (auto.bonds.Count > 0)
                {
                    foreach (var bond in auto.bonds)
                    {
                        string otherId = bond.OtherNpc(evo.archetypeId);
                        if (_states.TryGetValue(otherId, out var otherAuto))
                            bondInfluence += otherAuto.memorySignature[i] * bond.bondLevel;
                    }
                    bondInfluence /= auto.bonds.Count;
                }

                id.signature[i] = Mathf.Clamp01(
                    spec[i] * 0.40f +
                    auto.memorySignature[i] * 0.30f +
                    creationInfluence * 0.20f +
                    bondInfluence * 0.10f);
            }

            // Dominant quality
            int bestBand = 0;
            for (int i = 1; i < 7; i++)
                if (id.signature[i] > id.signature[bestBand]) bestBand = i;

            string prevQuality = id.dominantQuality;
            id.dominantQuality = QUALITY_NAMES[bestBand];
            id.earnedName = EARNED_NAMES[bestBand];

            // Compute divergence from original archetype
            var config = CosmosGenerationSystem.Instance?.GetPlanetConfig(auto.homePlanetId);
            if (config != null)
            {
                float[] baseline = {
                    config.equilibriumSpectral.red,    config.equilibriumSpectral.orange,
                    config.equilibriumSpectral.yellow, config.equilibriumSpectral.green,
                    config.equilibriumSpectral.blue,   config.equilibriumSpectral.indigo,
                    config.equilibriumSpectral.violet };

                float divergence = 0f;
                for (int i = 0; i < 7; i++)
                    divergence += Mathf.Abs(id.signature[i] - baseline[i]);
                id.divergence = Mathf.Clamp01(divergence / 7f * 2f);
            }

            // Becoming — has the NPC transformed enough to earn a new identity?
            float prevDivergence = id.divergence;
            if (id.dominantQuality != prevQuality &&
                id.divergence >= becomingThreshold &&
                prevQuality != null)
            {
                id.becomingCount++;
                id.lastBecomingUtc = DateTime.UtcNow.ToString("o");

                OnNpcBecame?.Invoke(evo.archetypeId, id);
                if (logBecoming)
                    Debug.Log($"[NpcAutonomy] {evo.archetypeId} BECAME: \"{id.earnedName}\" " +
                              $"({id.dominantQuality}) divergence={id.divergence:F2} " +
                              $"becoming #{id.becomingCount}");
            }
        }

        // =================================================================
        // PUBLIC API
        // =================================================================

        public NpcAutonomyState GetState(string archetypeId)
            => _states.TryGetValue(archetypeId, out var s) ? s : null;

        public List<NpcAutonomyState> GetAllStates()
        {
            var result = new List<NpcAutonomyState>();
            foreach (var kvp in _states) result.Add(kvp.Value);
            return result;
        }

        /// <summary>Record a player encounter as a memory for NPCs on the same planet.</summary>
        public void RecordPlayerEncounter(string planetId, float[] playerField, float harmony)
        {
            if (harmony < 0.25f) return; // below awareness threshold

            foreach (var kvp in _states)
            {
                var auto = kvp.Value;
                if (auto.currentPlanetId != planetId) continue;

                auto.AddMemory(new MemoryImprint
                {
                    sourceTag        = "player",
                    sourceId         = "player",
                    vibrationalTrace = (float[])playerField.Clone(),
                    strength         = harmony * 0.6f,
                    emotionalValence = harmony - 0.5f,
                    utcTimestamp     = DateTime.UtcNow.ToString("o"),
                });
            }
        }

        /// <summary>Record a cosmic event (dragon, cosmos birth, etc.) for all NPCs.</summary>
        public void RecordCosmicEvent(string sourceTag, string sourceId, float[] field, float strength)
        {
            foreach (var kvp in _states)
            {
                kvp.Value.AddMemory(new MemoryImprint
                {
                    sourceTag        = sourceTag,
                    sourceId         = sourceId,
                    vibrationalTrace = (float[])field.Clone(),
                    strength         = strength,
                    emotionalValence = 0.7f, // cosmic events are awe-inspiring
                    utcTimestamp     = DateTime.UtcNow.ToString("o"),
                });
            }
        }
    }
}
