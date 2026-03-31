// V243.SandstoneCave — PortalGlimpseSystem.cs
// During portal selection, the cave shows fleeting glimpses of each realm.
// Each glimpse has a visual theme, a unique VR haptic signature, and a
// resonance measurement window. The player's body-level response to each
// glimpse tells the system which realm resonates most strongly — not which
// one the player "chooses" intellectually, but which one their nervous system
// opens to.
//
// GLIMPSE SEQUENCE:
//   1. Source / Dreamspring  — peace, calm, warmth, slow deep bilateral rumble
//   2. Fire / Hearth         — martial energy, sharp rhythmic pulses, courage
//   3. Forest / Earth        — hiking, grounding, steady alternating left-right
//   4. Sky / Mountains       — climbing, ascending lift, rising bilateral shimmer
//   5. Machine / Workshop    — precision gears, metronomic clicks, locked rhythm
//   6. Ocean / Tide Pool     — waves, flowing bilateral wash, tidal breath
//
// After all glimpses play, the system ranks the player's resonance response
// to each and passes the result to PlanetAffinityInterpreter. The cave's
// portal then reveals the realm the player resonated with most deeply.
//
// AI PLAYERS: AI spirits receive the same glimpse sequence. Their spirit brain
// responds to the haptic-equivalent sensory input, and the highest-scoring
// drive mode determines their realm affinity.
//
// SEEDLING: glimpses are longer, gentler, and the companion narrates each one.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing;
using SpiritsCrossing.VR;
using SpiritsCrossing.SpiritAI;

namespace V243.SandstoneCave
{
    // -------------------------------------------------------------------------
    // Glimpse definition — one per realm
    // -------------------------------------------------------------------------
    [Serializable]
    public class RealmGlimpse
    {
        public string realmId;
        public string realmDisplayName;
        public string visualTheme;       // description for scene presentation layer
        public string hapticPattern;     // key into VRHapticsController glimpse patterns
        public Color  glowColor;         // cave wall tint during this glimpse
        public float  duration = 5f;     // seconds the glimpse plays
        public string seedlingNarration; // companion voice for young players

        // Mythic story layers — woven from world traditions, age-appropriate
        public string storyGrimm;        // European fairy tale thread
        public string storyIndigenous;   // Native American / First Nations thread
        public string storyAsian;        // East/South Asian mythology thread
        public string storyArabian;      // Arabian Nights / Middle Eastern thread

        // Measured during the glimpse window
        [NonSerialized] public float responseScore;
        [NonSerialized] public PlayerResponseSample responseSample;
    }

    // -------------------------------------------------------------------------
    // The system
    // -------------------------------------------------------------------------
    public class PortalGlimpseSystem : MonoBehaviour
    {
        [Header("References")]
        public PlanetAffinityInterpreter affinityInterpreter;
        public PortalUnlockController    portalController;
        public CaveSessionController     caveSession;

        [Header("Timing")]
        public float pauseBetweenGlimpses = 2f;
        public float preSequenceDelay     = 3f;  // calm before glimpses begin

        [Header("Visual")]
        public Light      caveAmbientLight;
        public GameObject glimpseVfxRoot;    // parent for per-realm VFX objects

        [Header("Debug")]
        public bool logScores = true;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<RealmGlimpse> OnGlimpseStarted;
        public event Action<RealmGlimpse> OnGlimpseEnded;
        public event Action<RealmGlimpse> OnBestRealmDetermined;

        // -------------------------------------------------------------------------
        // Glimpse definitions — the sequence of realm previews
        // -------------------------------------------------------------------------
        public List<RealmGlimpse> Glimpses { get; private set; }

        public bool IsPlaying { get; private set; }
        public RealmGlimpse BestGlimpse { get; private set; }

        private void Awake()
        {
            BuildGlimpseSequence();
        }

        private void BuildGlimpseSequence()
        {
            var config = UniverseStateManager.Instance?.Current?.AgeTierConfig;
            bool isSeedling = config?.tier == AgeTier.Seedling;
            float dur = isSeedling ? 8f : 5f;

            Glimpses = new List<RealmGlimpse>
            {
                // =============================================================
                // SOURCE — The origin, the still pool, the first light
                // =============================================================
                new RealmGlimpse
                {
                    realmId          = "SourceRealm",
                    realmDisplayName = isSeedling ? "The Dreamspring" : "Source Veil",
                    visualTheme      = "Soft golden light fills the cave. Warmth rises from the floor. " +
                                       "The air smells like morning sun on still water. " +
                                       "Cave paintings shimmer — figures sitting by a sacred spring, " +
                                       "hands open, eyes closed, across every culture and every age.",
                    hapticPattern    = "glimpse_source",
                    glowColor        = new Color(1f, 0.92f, 0.7f, 1f),
                    duration         = dur,
                    seedlingNarration = isSeedling
                        ? "Close your eyes... A grandmother sits by a golden pool. " +
                          "She says: 'Everything that ever was began right here, in the quiet.'"
                        : "Close your eyes... feel how warm and safe it is. " +
                          "This is where everything begins.",

                    storyGrimm       = "A well at the bottom of a hollow tree. " +
                                       "Whoever drinks from it remembers who they were before they were born.",
                    storyIndigenous  = "Spider Grandmother sits at the center of the web. " +
                                       "Every thread leads back to her. She hums, and the world holds still.",
                    storyAsian       = "Beneath the Bodhi tree, a traveler sits so still that roots grow over them. " +
                                       "When they open their eyes, they see the light that was always there.",
                    storyArabian     = "In a hidden room behind the waterfall, a lamp burns that has never been lit " +
                                       "and never goes out. The djinn who guards it says: 'This is the first fire.'"
                },

                // =============================================================
                // FIRE — Courage, martial spirit, transformation through intensity
                // =============================================================
                new RealmGlimpse
                {
                    realmId          = "FireRealm",
                    realmDisplayName = isSeedling ? "The Hearth" : "Dark Contrast",
                    visualTheme      = "Red-orange light flickers. Shadows on the walls become warriors, " +
                                       "dancers, and blacksmiths. Drumbeats pulse through stone. " +
                                       "The scent of forge-smoke and desert wind.",
                    hapticPattern    = "glimpse_fire",
                    glowColor        = new Color(1f, 0.3f, 0.1f, 1f),
                    duration         = dur,
                    seedlingNarration = isSeedling
                        ? "A little blacksmith hammers a tiny sword — tap, tap, tap! " +
                          "'Every brave person started by being a little bit scared,' they say."
                        : "Feel the drumbeat? That's the sound of being brave. Your heart is strong!",

                    storyGrimm       = "The brave little tailor faces seven at one stroke. " +
                                       "Not with a blade, but with cleverness and a heart that refuses to kneel.",
                    storyIndigenous  = "Coyote steals fire from the mountain spirits and carries it " +
                                       "burning in their mouth, running through the dark so the people can be warm.",
                    storyAsian       = "In the mountain monastery, a student strikes the wooden dummy a thousand times. " +
                                       "The master says: 'You are not fighting the wood. You are meeting yourself.'",
                    storyArabian     = "Sinbad stands before the Roc's nest. The ground shakes. " +
                                       "He does not run. He says: 'I have crossed seven seas. I will not turn from this.'"
                },

                // =============================================================
                // FOREST — Patience, trail-walking, the green cathedral
                // =============================================================
                new RealmGlimpse
                {
                    realmId          = "ForestRealm",
                    realmDisplayName = "Forest Heart",
                    visualTheme      = "Green light filters through invisible canopy. Moss smell. " +
                                       "Footsteps on an old trail. Birdsong. Stone rings half-buried in roots. " +
                                       "Carved faces in tree bark from a hundred different hands.",
                    hapticPattern    = "glimpse_forest",
                    glowColor        = new Color(0.2f, 0.7f, 0.3f, 1f),
                    duration         = dur,
                    seedlingNarration = isSeedling
                        ? "A deer stands in the path ahead. It looks at you and waits. " +
                          "'Follow me,' its eyes say. 'I know the way through.'"
                        : "Can you hear the birds? There's a path through the trees. " +
                          "Something ancient is sleeping under the roots.",

                    storyGrimm       = "Two children follow white stones through the dark wood. " +
                                       "The forest is not cruel — it is testing whether they trust their own trail.",
                    storyIndigenous  = "The White Stag walks between the old trees, antlers brushing the canopy. " +
                                       "Where it steps, healing plants grow. It only appears to those who are patient.",
                    storyAsian       = "A monk walks a mountain trail for forty days. On the last day " +
                                       "the bamboo grove opens and reveals a temple no map has ever shown.",
                    storyArabian     = "In the garden behind the garden, every fruit is a memory. " +
                                       "The gardener says: 'Eat slowly. Each seed remembers the rain that grew it.'"
                },

                // =============================================================
                // SKY — Ascent, wind, mountains, release into open air
                // =============================================================
                new RealmGlimpse
                {
                    realmId          = "SkyRealm",
                    realmDisplayName = "Sky Spiral",
                    visualTheme      = "Blue-white light streams upward. Wind rushes past. " +
                                       "The sensation of climbing — handholds on ancient stone, " +
                                       "thin air, eagle cry, then the summit opens into infinite sky.",
                    hapticPattern    = "glimpse_sky",
                    glowColor        = new Color(0.5f, 0.7f, 1f, 1f),
                    duration         = dur,
                    seedlingNarration = isSeedling
                        ? "An eagle lands on a rock and spreads its wings. " +
                          "'Want to see what I see? Hold on — we're going up!'"
                        : "Feel the wind? We're going up! The mountain wants to " +
                          "show you what's above the clouds!",

                    storyGrimm       = "A girl climbs a glass mountain where no one else can find footing. " +
                                       "She does not grip harder — she lets her hands become lighter. That is the secret.",
                    storyIndigenous  = "Eagle carries the child above the storm clouds. " +
                                       "'From up here,' Eagle says, 'you can see that every path leads somewhere good.'",
                    storyAsian       = "Sun Wukong leaps from peak to peak on his cloud. " +
                                       "The world below is small and beautiful. Freedom is not escape — it is perspective.",
                    storyArabian     = "The magic carpet rises above the city of a thousand minarets. " +
                                       "Below, every rooftop is a story. Above, the stars are close enough to touch."
                },

                // =============================================================
                // MACHINE — Pattern, rhythm, the living architecture of order
                // =============================================================
                new RealmGlimpse
                {
                    realmId          = "MachineRealm",
                    realmDisplayName = isSeedling ? "The Workshop" : "Machine Order",
                    visualTheme      = "Copper-amber light pulses in rhythm. Gears click and interlock. " +
                                       "Geometric patterns tile the walls — Islamic stars, Celtic knots, " +
                                       "circuit boards, and spider webs. All the same mathematics.",
                    hapticPattern    = "glimpse_machine",
                    glowColor        = new Color(0.8f, 0.6f, 0.2f, 1f),
                    duration         = dur,
                    seedlingNarration = isSeedling
                        ? "A friendly little clockwork bird hops across a table of gears. " +
                          "'Help me put the pieces together! Tick, tick, tick...'"
                        : "Listen to the clicking! It's like a giant clock. " +
                          "Can you clap along? Click, click, click...",

                    storyGrimm       = "A toymaker builds a mechanical nightingale that sings so truly " +
                                       "the real birds stop to listen. The secret: he built it with love, not just gears.",
                    storyIndigenous  = "Spider weaves a web so precise that dewdrops hang on every crossing " +
                                       "without falling. 'Pattern is not prison,' Spider says. 'Pattern is how beauty holds together.'",
                    storyAsian       = "In the emperor's garden, a water clock marks time with jade cups. " +
                                       "Each cup is a moment perfectly balanced. The engineer who built it " +
                                       "says: 'I did not invent time. I learned to listen to it.'",
                    storyArabian     = "Behind the brass door, a room of a thousand automata dance in perfect unison. " +
                                       "The inventor says: 'I gave them the pattern. They gave themselves the joy.'"
                },

                // =============================================================
                // OCEAN — Tides, depth, the patient wisdom of water
                // =============================================================
                new RealmGlimpse
                {
                    realmId          = "OceanRealm",
                    realmDisplayName = isSeedling ? "The Tide Pool" : "Water Flow",
                    visualTheme      = "Deep blue-indigo light washes in waves. The sound of surf. " +
                                       "The floor sways gently. Salt air. On the cave walls: " +
                                       "painted whales, carved ships, coral patterns, and moon reflections.",
                    hapticPattern    = "glimpse_ocean",
                    glowColor        = new Color(0.15f, 0.3f, 0.8f, 1f),
                    duration         = dur,
                    seedlingNarration = isSeedling
                        ? "A sea turtle swims slowly past. 'Come with me,' it says. " +
                          "'The ocean is big, but you don't have to hurry. Just float.'"
                        : "Feel the waves? In... and out... like breathing. " +
                          "The ocean is alive and it wants to meet you.",

                    storyGrimm       = "A fisherman's daughter dives to the bottom of the sea " +
                                       "and finds a kingdom of glass. The fish queen says: " +
                                       "'You can breathe here. You always could.'",
                    storyIndigenous  = "Salmon swims upstream against everything. " +
                                       "'Why?' asks Bear. Salmon says: 'Because where I began is where I belong. " +
                                       "The current is not my enemy. It is my teacher.'",
                    storyAsian       = "Ryūjin, the dragon king, holds a tide jewel in each claw. " +
                                       "One pulls the sea in, one pushes it out. " +
                                       "'Breathe with me,' the dragon says, 'and you will never drown.'",
                    storyArabian     = "A pearl diver descends past seven layers of blue. " +
                                       "At the deepest layer, the water is not dark — it glows. " +
                                       "The pearl there has been waiting since the world began."
                },
            };
        }

        // -------------------------------------------------------------------------
        // Start the glimpse sequence (called by CaveSessionController after portal unlock)
        // -------------------------------------------------------------------------
        public void BeginGlimpseSequence()
        {
            if (IsPlaying) return;
            StartCoroutine(PlayGlimpseSequence());
        }

        private IEnumerator PlayGlimpseSequence()
        {
            IsPlaying = true;

            // Calm pause before glimpses begin
            yield return new WaitForSeconds(preSequenceDelay);

            var orchestrator = SpiritBrainOrchestrator.Instance;
            var haptics = FindObjectOfType<VRHapticsController>();

            foreach (var glimpse in Glimpses)
            {
                // --- Start glimpse ---
                SetCaveAtmosphere(glimpse);
                OnGlimpseStarted?.Invoke(glimpse);

                // Fire haptic pattern
                if (haptics != null)
                    haptics.PlayGlimpseHaptic(glimpse.hapticPattern, glimpse.duration);

                // Seedling: companion narration
                var config = UniverseStateManager.Instance?.Current?.AgeTierConfig;
                if (config?.tier <= AgeTier.Explorer && !string.IsNullOrEmpty(glimpse.seedlingNarration))
                    Debug.Log($"[PortalGlimpseSystem] COMPANION: {glimpse.seedlingNarration}");

                Debug.Log($"[PortalGlimpseSystem] Glimpse: {glimpse.realmDisplayName} " +
                          $"({glimpse.hapticPattern}) {glimpse.duration}s");

                // --- Measure response during the glimpse window ---
                float startTime = Time.time;
                float peakResponse = 0f;
                PlayerResponseSample bestSample = null;

                while (Time.time - startTime < glimpse.duration)
                {
                    // Read current player resonance
                    var playerState = orchestrator?.CurrentPlayerState;
                    if (playerState != null)
                    {
                        var sample = playerState.ToSample();
                        float response = ScoreResponseForRealm(glimpse.realmId, sample);
                        if (response > peakResponse)
                        {
                            peakResponse = response;
                            bestSample = sample;
                        }
                    }
                    yield return null;
                }

                glimpse.responseScore = peakResponse;
                glimpse.responseSample = bestSample ?? new PlayerResponseSample();

                // --- End glimpse ---
                OnGlimpseEnded?.Invoke(glimpse);
                ResetCaveAtmosphere();

                if (logScores)
                    Debug.Log($"[PortalGlimpseSystem] {glimpse.realmDisplayName} response={peakResponse:F3}");

                yield return new WaitForSeconds(pauseBetweenGlimpses);
            }

            // --- Determine best realm ---
            DetermineBestRealm();
            IsPlaying = false;
        }

        // -------------------------------------------------------------------------
        // Score how strongly the player's body responds to a realm-specific glimpse
        // -------------------------------------------------------------------------
        private float ScoreResponseForRealm(string realmId, PlayerResponseSample s)
        {
            return realmId switch
            {
                "SourceRealm"  => s.stillnessScore * 0.4f + s.sourceAlignmentScore * 0.35f +
                                  s.calmScore * 0.25f,

                "FireRealm"    => s.spinScore * 0.4f + s.distortionScore * 0.3f +
                                  s.flowScore * 0.3f,

                "ForestRealm"  => s.calmScore * 0.35f + s.flowScore * 0.3f +
                                  s.stillnessScore * 0.2f + s.wonderScore * 0.15f,

                "SkyRealm"     => s.spinScore * 0.3f + s.wonderScore * 0.3f +
                                  s.joyScore * 0.25f + s.flowScore * 0.15f,

                "MachineRealm" => s.spinScore * 0.35f + s.pairSyncScore * 0.3f +
                                  s.flowScore * 0.2f + s.calmScore * 0.15f,

                "OceanRealm"   => s.flowScore * 0.4f + s.calmScore * 0.3f +
                                  s.pairSyncScore * 0.2f + s.stillnessScore * 0.1f,

                _ => 0f
            };
        }

        // -------------------------------------------------------------------------
        // Determine the best realm from glimpse responses
        // -------------------------------------------------------------------------
        private void DetermineBestRealm()
        {
            RealmGlimpse best = null;
            float bestScore = -1f;

            foreach (var g in Glimpses)
            {
                // Age-gate: skip blocked realms
                var config = UniverseStateManager.Instance?.Current?.AgeTierConfig;
                if (config != null && config.IsMythBlocked(RealmToMythKey(g.realmId)))
                    continue;

                if (g.responseScore > bestScore)
                {
                    bestScore = g.responseScore;
                    best = g;
                }
            }

            BestGlimpse = best;

            // Feed result into affinity interpreter
            if (best != null && affinityInterpreter != null)
            {
                affinityInterpreter.currentAffinityPlanet = RealmToPlanetId(best.realmId);
                affinityInterpreter.currentAffinityScore  = best.responseScore;
            }

            OnBestRealmDetermined?.Invoke(best);
            Debug.Log($"[PortalGlimpseSystem] Best realm: {best?.realmDisplayName ?? "none"} " +
                      $"score={bestScore:F3}");
        }

        // -------------------------------------------------------------------------
        // Cave atmosphere during glimpses
        // -------------------------------------------------------------------------
        private Color _originalLightColor;
        private float _originalLightIntensity;

        private void SetCaveAtmosphere(RealmGlimpse glimpse)
        {
            if (caveAmbientLight != null)
            {
                _originalLightColor     = caveAmbientLight.color;
                _originalLightIntensity = caveAmbientLight.intensity;
                caveAmbientLight.color     = glimpse.glowColor;
                caveAmbientLight.intensity = 2.5f;
            }

            // Activate realm-specific VFX child if present
            if (glimpseVfxRoot != null)
            {
                var vfx = glimpseVfxRoot.transform.Find(glimpse.realmId);
                if (vfx != null) vfx.gameObject.SetActive(true);
            }
        }

        private void ResetCaveAtmosphere()
        {
            if (caveAmbientLight != null)
            {
                caveAmbientLight.color     = _originalLightColor;
                caveAmbientLight.intensity = _originalLightIntensity;
            }

            if (glimpseVfxRoot != null)
                foreach (Transform child in glimpseVfxRoot.transform)
                    child.gameObject.SetActive(false);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------
        private static string RealmToPlanetId(string realmId) => realmId switch
        {
            "SourceRealm"  => "SourceVeil",
            "FireRealm"    => "DarkContrast",
            "ForestRealm"  => "ForestHeart",
            "SkyRealm"     => "SkySpiral",
            "MachineRealm" => "MachineOrder",
            "OceanRealm"   => "WaterFlow",
            _              => ""
        };

        private static string RealmToMythKey(string realmId) => realmId switch
        {
            "SourceRealm"  => "source",
            "FireRealm"    => "fire",
            "ForestRealm"  => "forest",
            "SkyRealm"     => "sky",
            "MachineRealm" => "machine",
            "OceanRealm"   => "ocean",
            _              => ""
        };
    }
}
