// SpiritsCrossing — ElderDragonCosmicSpawner.cs
// Elder dragons are not bound to their home realm. They are cosmic beings
// that roam the entire universe. They appear wherever the conditions call them —
// on established worlds, on young planets taking their first breath, and on
// worlds the player has never visited.
//
// A dragon appearing on a world it doesn't "belong" to is a mythic event.
// An Air Dragon landing on the Ocean world means the wind has found the waves.
// A Fire Dragon walking through the Forest means the old trees are ready for
// transformation. These crossings are rare, powerful, and story-generating.
//
// ELDER DRAGONS:
//   Elder Air Dragon    — appears where wonder and spin are high, or where a
//                         new world is forming and needs its first breath of wind
//   Elder Earth Dragon  — appears where stillness and patience run deep, or
//                         where a young world needs roots
//   Elder Fire Dragon   — appears where courage and intensity blaze, or where
//                         a world has grown complacent and needs transformation
//   Elder Water Dragon  — appears where flow and calm run together, or where
//                         a world is parched and needs the first rain
//   Elder Source Dragon — the rarest. Appears only at the convergence of all
//                         four elements, or on a world approaching rebirth.
//                         Not tied to any element. It is the origin.
//
// NEW / BECOMING WORLDS:
//   Planets with visitCount <= 2 are considered "becoming." Elder dragons are
//   drawn to becoming worlds because new things need ancient guidance.
//   The first dragon to visit a becoming world imprints its element on that
//   world's early growth — a founding blessing.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Companions;
using SpiritsCrossing.Cosmos;

namespace SpiritsCrossing.DragonRealms
{
    // -------------------------------------------------------------------------
    // Elder dragon definition
    // -------------------------------------------------------------------------
    [Serializable]
    public class ElderDragonProfile
    {
        public string dragonId;
        public string displayName;
        public string element;           // "Air", "Earth", "Fire", "Water", "Source"
        public string homeRealmId;       // where it lives naturally
        public string companionAnimalId; // which companion registry animal it maps to

        // Resonance affinity — what draws this dragon to a world
        public float affinityWonder;
        public float affinitySpin;
        public float affinityCalm;
        public float affinityFlow;
        public float affinitySource;
        public float affinityDistortion;

        // Mythic story for when this dragon appears on a foreign world
        public string cosmicArrivalStory;

        // Story for when it appears on a becoming world
        public string becomingWorldStory;
    }

    // -------------------------------------------------------------------------
    // A dragon sighting — records where and when an elder dragon appeared
    // -------------------------------------------------------------------------
    [Serializable]
    public class DragonSighting
    {
        public string dragonId;
        public string planetId;
        public string worldId;
        public float  resonanceAtSighting;
        public bool   isBecomingWorld;
        public bool   isForeignWorld;      // dragon is not on its home realm
        public string utcTimestamp;
    }

    // =========================================================================
    public class ElderDragonCosmicSpawner : MonoBehaviour
    {
        public static ElderDragonCosmicSpawner Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Timing")]
        public float evaluationInterval = 5.0f;  // how often we check for dragon appearances

        [Header("Thresholds")]
        [Tooltip("Minimum resonance score for a dragon to consider appearing.")]
        [Range(0f, 1f)] public float spawnThreshold        = 0.35f;
        [Range(0f, 1f)] public float foreignWorldThreshold = 0.50f;  // higher bar for non-home worlds
        [Range(0f, 1f)] public float becomingWorldBonus    = 0.25f;  // bonus for young planets
        [Range(0f, 1f)] public float sourceConvergenceThreshold = 0.70f;

        [Header("Limits")]
        public int maxSimultaneousDragons = 2;
        public int maxSightingsPerSession = 4;

        [Header("Debug")]
        public bool logSightings = true;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<ElderDragonProfile, string> OnElderDragonAppeared;   // dragon, planetId
        public event Action<ElderDragonProfile, string> OnElderDragonDeparted;   // dragon, planetId
        public event Action<DragonSighting>             OnDragonSightingRecorded;

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------
        public List<ElderDragonProfile> Dragons { get; private set; }
        public List<DragonSighting>     SessionSightings { get; private set; } = new List<DragonSighting>();

        private readonly HashSet<string> _activeDragons = new HashSet<string>();
        private float _evalTimer;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            BuildDragonProfiles();
        }

        private void Update()
        {
            _evalTimer += Time.deltaTime;
            if (_evalTimer < evaluationInterval) return;
            _evalTimer = 0f;

            if (SessionSightings.Count >= maxSightingsPerSession) return;
            if (_activeDragons.Count >= maxSimultaneousDragons) return;

            EvaluateCosmicSpawns();
        }

        // -------------------------------------------------------------------------
        // Dragon profiles
        // -------------------------------------------------------------------------
        private void BuildDragonProfiles()
        {
            Dragons = new List<ElderDragonProfile>
            {
                new ElderDragonProfile
                {
                    dragonId         = "elder_air_dragon",
                    displayName      = "Elder Air Dragon",
                    element          = "Air",
                    homeRealmId      = "SkyRealm",
                    companionAnimalId = "harpy_eagle",
                    affinityWonder   = 0.40f,
                    affinitySpin     = 0.35f,
                    affinityFlow     = 0.15f,
                    affinitySource   = 0.10f,
                    cosmicArrivalStory = "The sky darkens for a moment — not with cloud, but with wings. " +
                                         "The Elder Air Dragon descends from a height no eye can measure. " +
                                         "It has not come to stay. It has come to remind this world that the sky is listening.",
                    becomingWorldStory = "A young world draws its first breath. Above, something vast and ancient circles. " +
                                         "The Elder Air Dragon breathes wind into a world that has never known it. " +
                                         "This is the first weather. This is the first song."
                },
                new ElderDragonProfile
                {
                    dragonId         = "elder_earth_dragon",
                    displayName      = "Elder Earth Dragon",
                    element          = "Earth",
                    homeRealmId      = "ForestRealm",
                    companionAnimalId = "bison",
                    affinityCalm     = 0.40f,
                    affinitySource   = 0.30f,
                    affinityFlow     = 0.10f,
                    affinityWonder   = 0.20f,
                    cosmicArrivalStory = "The ground shifts — not violently, but with the slow intention of " +
                                         "something enormous turning over. The Elder Earth Dragon rises from below. " +
                                         "Its scales are the color of every stone that has ever been patient.",
                    becomingWorldStory = "A world without roots calls out in silence. From deep below, something stirs. " +
                                         "The Elder Earth Dragon pushes upward and the first mountain is born. " +
                                         "Where it breathes, the ground learns to hold."
                },
                new ElderDragonProfile
                {
                    dragonId         = "elder_fire_dragon",
                    displayName      = "Elder Fire Dragon",
                    element          = "Fire",
                    homeRealmId      = "FireRealm",
                    companionAnimalId = "fire_drake",
                    affinityDistortion = 0.30f,
                    affinitySpin     = 0.35f,
                    affinityWonder   = 0.20f,
                    affinitySource   = 0.15f,
                    cosmicArrivalStory = "Heat rises where there was none. The air crackles. A shape forms in the forge-light — " +
                                         "not destructive, but transformative. The Elder Fire Dragon does not burn. " +
                                         "It reminds this world what courage feels like.",
                    becomingWorldStory = "A cold, young world. Nothing has been tested here yet. " +
                                         "The Elder Fire Dragon arrives and lights the first hearth. " +
                                         "Not a wildfire — a campfire. The kind that brave stories are told around."
                },
                new ElderDragonProfile
                {
                    dragonId         = "elder_water_dragon",
                    displayName      = "Elder Water Dragon",
                    element          = "Water",
                    homeRealmId      = "OceanRealm",
                    companionAnimalId = "whale",
                    affinityFlow     = 0.40f,
                    affinityCalm     = 0.30f,
                    affinitySource   = 0.20f,
                    affinityWonder   = 0.10f,
                    cosmicArrivalStory = "Something flows where there was no river. The air turns salt and sweet at once. " +
                                         "The Elder Water Dragon moves through solid ground as if it were ocean. " +
                                         "It brings the memory of tides to a world that has forgotten them.",
                    becomingWorldStory = "A dry, new world. Dust and silence. Then — a single drop. Then a river. " +
                                         "The Elder Water Dragon slides through the first valley it has carved, " +
                                         "and everywhere it passes, things begin to grow."
                },
                new ElderDragonProfile
                {
                    dragonId         = "elder_source_dragon",
                    displayName      = "Elder Source Dragon",
                    element          = "Source",
                    homeRealmId      = "SourceRealm",
                    companionAnimalId = "elder_white_stag",
                    affinitySource   = 0.50f,
                    affinityCalm     = 0.20f,
                    affinityWonder   = 0.20f,
                    affinityFlow     = 0.10f,
                    cosmicArrivalStory = "Light without source. Warmth without fire. The Elder Source Dragon does not arrive — " +
                                         "it becomes visible. It was always here. Every world carries a fragment of the origin. " +
                                         "When the fragments align, the dragon remembers itself.",
                    becomingWorldStory = "Before wind, before stone, before fire, before water — there is this. " +
                                         "The Elder Source Dragon coils at the center of the new world's first heartbeat. " +
                                         "It does not create. It witnesses. And by witnessing, it blesses."
                },
            };
        }

        // -------------------------------------------------------------------------
        // Cosmic evaluation — which dragons should appear, and where?
        // -------------------------------------------------------------------------
        private void EvaluateCosmicSpawns()
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            var cosmosGen = CosmosGenerationSystem.Instance?.Data;
            var playerSample = universe.persistentResonance;

            // Evaluate each planet for each dragon
            foreach (var dragon in Dragons)
            {
                if (_activeDragons.Contains(dragon.dragonId)) continue;

                (string bestPlanet, float bestScore, bool isBeccoming) = FindBestPlanet(
                    dragon, universe, cosmosGen, playerSample);

                if (bestPlanet == null) continue;

                bool isForeign = !IsHomePlanet(dragon, bestPlanet);
                float threshold = isForeign ? foreignWorldThreshold : spawnThreshold;

                // Source Dragon requires all-element convergence
                if (dragon.element == "Source")
                    threshold = sourceConvergenceThreshold;

                if (bestScore >= threshold)
                    SpawnDragon(dragon, bestPlanet, bestScore, isBeccoming, isForeign);
            }
        }

        private (string planetId, float score, bool isBecoming) FindBestPlanet(
            ElderDragonProfile dragon, UniverseState universe,
            CosmosGenerationCollection cosmosGen, PlayerResponseSample playerSample)
        {
            string bestPlanet = null;
            float  bestScore  = 0f;
            bool   bestIsBecoming = false;

            // Check all known planets
            foreach (var planet in universe.planets)
            {
                float score = ScoreDragonForPlanet(dragon, planet, cosmosGen, playerSample);

                // Becoming world bonus — young planets attract ancient guidance
                bool isBecoming = planet.visitCount <= 2;
                if (isBecoming)
                    score += becomingWorldBonus;

                if (score > bestScore)
                {
                    bestScore      = score;
                    bestPlanet     = planet.planetId;
                    bestIsBecoming = isBecoming;
                }
            }

            // Also check cosmos-generated planets not yet in universe.planets
            if (cosmosGen != null)
            {
                foreach (var gen in cosmosGen.planets)
                {
                    bool alreadyKnown = false;
                    foreach (var p in universe.planets)
                        if (p.planetId == gen.planetId) { alreadyKnown = true; break; }

                    if (alreadyKnown) continue;

                    // Unvisited cosmos planet = becoming world
                    float score = ScoreDragonForGeneratedPlanet(dragon, gen, playerSample);
                    score += becomingWorldBonus * 1.5f; // extra bonus for truly new worlds

                    if (score > bestScore)
                    {
                        bestScore      = score;
                        bestPlanet     = gen.planetId;
                        bestIsBecoming = true;
                    }
                }
            }

            return (bestPlanet, bestScore, bestIsBecoming);
        }

        // -------------------------------------------------------------------------
        // Scoring
        // -------------------------------------------------------------------------
        private float ScoreDragonForPlanet(ElderDragonProfile dragon, PlanetState planet,
            CosmosGenerationCollection cosmosGen, PlayerResponseSample playerSample)
        {
            float score = 0f;

            // Player resonance affinity
            score += dragon.affinityWonder     * playerSample.wonderScore;
            score += dragon.affinitySpin       * playerSample.spinScore;
            score += dragon.affinityCalm       * playerSample.calmScore;
            score += dragon.affinityFlow       * playerSample.flowScore;
            score += dragon.affinitySource     * playerSample.sourceAlignmentScore;
            score += dragon.affinityDistortion * playerSample.distortionScore;

            // Planet health and growth attract dragons
            score += planet.growth  * 0.15f;
            score += planet.healing * 0.10f;

            // Planets near rebirth attract Source Dragon
            if (dragon.element == "Source")
                score += planet.rebirthCharge * 0.30f;

            // Spectral affinity from cosmos generation data
            var gen = cosmosGen?.GetPlanet(planet.planetId);
            if (gen != null)
                score += ElementalSpectralBonus(dragon.element, gen.equilibriumSpectral);

            return Mathf.Clamp01(score);
        }

        private float ScoreDragonForGeneratedPlanet(ElderDragonProfile dragon,
            GeneratedPlanetConfig gen, PlayerResponseSample playerSample)
        {
            float score = 0f;

            score += dragon.affinityWonder     * playerSample.wonderScore;
            score += dragon.affinitySpin       * playerSample.spinScore;
            score += dragon.affinityCalm       * playerSample.calmScore;
            score += dragon.affinityFlow       * playerSample.flowScore;
            score += dragon.affinitySource     * playerSample.sourceAlignmentScore;
            score += dragon.affinityDistortion * playerSample.distortionScore;

            score += ElementalSpectralBonus(dragon.element, gen.equilibriumSpectral);

            return Mathf.Clamp01(score);
        }

        private float ElementalSpectralBonus(string element, PlanetSpectralSignature spec)
        {
            return element switch
            {
                "Air"    => spec.yellow * 0.4f + spec.violet * 0.3f,
                "Earth"  => spec.green  * 0.4f + spec.red    * 0.2f,
                "Fire"   => spec.red    * 0.4f + spec.orange * 0.3f,
                "Water"  => spec.blue   * 0.4f + spec.indigo * 0.3f,
                "Source" => spec.violet * 0.3f + spec.green  * 0.2f + spec.indigo * 0.2f,
                _        => 0f
            };
        }

        // -------------------------------------------------------------------------
        // Spawn and departure
        // -------------------------------------------------------------------------
        private void SpawnDragon(ElderDragonProfile dragon, string planetId,
            float score, bool isBecoming, bool isForeign)
        {
            _activeDragons.Add(dragon.dragonId);

            var sighting = new DragonSighting
            {
                dragonId             = dragon.dragonId,
                planetId             = planetId,
                worldId              = planetId,
                resonanceAtSighting  = score,
                isBecomingWorld      = isBecoming,
                isForeignWorld       = isForeign,
                utcTimestamp         = DateTime.UtcNow.ToString("o")
            };
            SessionSightings.Add(sighting);

            // Activate the companion animal that represents this dragon
            CompanionBondSystem.Instance?.SetActiveCompanion(dragon.companionAnimalId);

            // Myth activation — dragon appearances are always mythic
            var myth = UniverseStateManager.Instance?.Current?.mythState;
            string elementMyth = dragon.element.ToLower();
            if (elementMyth == "source") elementMyth = "source";
            myth?.Activate(elementMyth, "elder_dragon_cosmic", 0.80f);
            myth?.Activate("elder", "elder_dragon_cosmic", 0.75f);

            // Becoming world: the dragon imprints its element
            if (isBecoming)
            {
                myth?.Activate("wonder", "dragon_founding", 0.60f);
                var planet = UniverseStateManager.Instance?.Current?.GetOrCreatePlanet(planetId);
                if (planet != null)
                    planet.growth = Mathf.Clamp01(planet.growth + 0.10f);
            }

            OnElderDragonAppeared?.Invoke(dragon, planetId);
            OnDragonSightingRecorded?.Invoke(sighting);

            if (logSightings)
            {
                string context = isBecoming ? "BECOMING WORLD" : (isForeign ? "FOREIGN WORLD" : "HOME WORLD");
                Debug.Log($"[ElderDragonCosmicSpawner] {dragon.displayName} appears on {planetId} " +
                          $"({context}) score={score:F3}");

                string story = isBecoming ? dragon.becomingWorldStory : dragon.cosmicArrivalStory;
                Debug.Log($"[ElderDragonCosmicSpawner] STORY: {story}");
            }

            // Schedule departure
            float stayDuration = isBecoming ? 120f : 60f; // becoming worlds: longer stay
            StartCoroutine(DepartAfter(dragon, planetId, stayDuration));
        }

        private System.Collections.IEnumerator DepartAfter(ElderDragonProfile dragon,
            string planetId, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _activeDragons.Remove(dragon.dragonId);
            OnElderDragonDeparted?.Invoke(dragon, planetId);

            if (logSightings)
                Debug.Log($"[ElderDragonCosmicSpawner] {dragon.displayName} departs from {planetId}.");
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------
        private bool IsHomePlanet(ElderDragonProfile dragon, string planetId)
        {
            return dragon.homeRealmId switch
            {
                "SkyRealm"     => planetId == "SkySpiral",
                "ForestRealm"  => planetId == "ForestHeart",
                "FireRealm"    => planetId == "DarkContrast",
                "OceanRealm"   => planetId == "WaterFlow",
                "SourceRealm"  => planetId == "SourceVeil",
                _              => false
            };
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------
        public bool IsDragonPresent(string dragonId) => _activeDragons.Contains(dragonId);

        public ElderDragonProfile GetDragon(string dragonId)
        {
            foreach (var d in Dragons) if (d.dragonId == dragonId) return d;
            return null;
        }

        public List<DragonSighting> GetSightingsForPlanet(string planetId)
        {
            var result = new List<DragonSighting>();
            foreach (var s in SessionSightings)
                if (s.planetId == planetId) result.Add(s);
            return result;
        }

        public bool HasDragonEverVisited(string planetId)
        {
            foreach (var s in SessionSightings)
                if (s.planetId == planetId) return true;
            return false;
        }

        /// <summary>Get whichever elder dragon is currently present on a planet, if any.</summary>
        public ElderDragonProfile GetActiveDragonOnPlanet(string planetId)
        {
            foreach (var s in SessionSightings)
            {
                if (s.planetId != planetId) continue;
                if (_activeDragons.Contains(s.dragonId))
                    return GetDragon(s.dragonId);
            }
            return null;
        }
    }
}
