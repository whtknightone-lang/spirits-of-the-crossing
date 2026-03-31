// SpiritsCrossing — CompanionIntentionSystem.cs
// Companion agency layer. Animals have their own intentions — they are not
// just mirrors of the player. They arrive, serve their purpose, and leave
// on their own terms.
//
// INTENTIONS:
//   Wandering   — not engaged, living own life in the world
//   Approaching — moving toward player, purpose not yet declared
//   Helping     — arrived because player needs this element
//   Tricking    — arrived to break a habitual pattern, will vanish after
//   Learning    — shadowing and observing, updating own evolution state
//   Riding      — peaceful co-presence, neither helping nor leaving
//   Departing   — chosen to leave, drifts away naturally
//
// ARRIVAL TRIGGERS (checked every 2s per animal):
//   Help:    player distortion > 0.55, or realm contrast > 0.65, or low harmony
//   Trick:   player too calm/predictable > 40s, and animal has high explore drive
//   Learn:   player doing something novel (high wonder rising), and animal's
//            own resonance would benefit from observing
//   Ride:    harmony > 0.60, no specific need, just companionship
//
// DEPARTURE:
//   After each visit a stayRoll determines if the animal lingers.
//   stayProbability = bondLevel * harmony * tierMultiplier
//   Tier 1 (wild): 0.3×   Tier 2 (medium): 0.6×   Tier 3 (elder): 0.9×
//   Below threshold → Departing state → natural drift away.
//   Tricksters always depart after the trick regardless of bond.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.SpiritAI;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Autonomous;

namespace SpiritsCrossing.Companions
{
    // -------------------------------------------------------------------------
    // Intention state for one companion
    // -------------------------------------------------------------------------
    public enum CompanionIntention
    {
        Wandering,   // not engaged
        Approaching, // moving in, purpose undeclared
        Helping,     // supporting a distressed player
        Tricking,    // disrupting a habitual pattern
        Learning,    // observing and absorbing
        Riding,      // peaceful co-presence
        Departing,   // leaving on own terms
        Playing,     // Seedling tier: joyful play instead of trick
    }

    [Serializable]
    public class CompanionIntentionState
    {
        public string            animalId;
        public CompanionIntention intention = CompanionIntention.Wandering;
        public float             intentionTimer;   // seconds in current intention
        public float             visitStayScore;   // computed at visit start

        // Trickster tracking
        public float tricksterCharge;   // rises when player is predictable
        public bool  trickActive;

        // Learning tracking
        public float observationDepth;  // how much the animal has learned this visit

        public bool IsEngaged => intention != CompanionIntention.Wandering &&
                                 intention != CompanionIntention.Departing;
    }

    // =========================================================================
    public class CompanionIntentionSystem : MonoBehaviour
    {
        public static CompanionIntentionSystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Thresholds")]
        [Range(0f, 1f)] public float helpDistressThreshold  = 0.55f;
        [Range(0f, 1f)] public float helpHarmonyThreshold   = 0.30f; // below this = help arrives
        [Range(0f, 1f)] public float trickCalmThreshold     = 0.65f; // above this = trickster activates
        [Range(0f, 1f)] public float learnWonderThreshold   = 0.50f;
        [Range(0f, 1f)] public float rideHarmonyThreshold   = 0.60f;

        [Header("Timing")]
        public float evalInterval       = 2.0f;   // seconds between intention evaluations
        public float trickActivateTime  = 40f;    // seconds of predictable behavior before trick
        public float trickDuration      = 8.0f;   // how long trickster misbehaves
        public float maxIntentionTime   = 120f;   // max stay before forced departure check
        public float departDuration     = 12f;    // seconds for natural drift-away

        [Header("Stay Probability")]
        [Range(0f, 1f)] public float tier1StayMult = 0.30f;
        [Range(0f, 1f)] public float tier2StayMult = 0.60f;
        [Range(0f, 1f)] public float tier3StayMult = 0.90f;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<string, CompanionIntention> OnIntentionChanged; // animalId, newIntention
        public event Action<string>                     OnTrickBegins;      // animalId
        public event Action<string>                     OnTrickEnds;
        public event Action<string>                     OnAnimalDeparts;    // animalId

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------
        private readonly Dictionary<string, CompanionIntentionState> _states = new();
        private float  _evalTimer;
        private float  _playerCalmTimer;  // how long player has been calm+predictable
        private float  _prevPlayerCalm;

        // Trickster archetypes — these animals have a trickster drive
        private static readonly HashSet<string> TRICKSTERS = new()
        {
            "raven", "chipmunk", "jaguar", "panther", "hawk"
        };

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            // Initialise intention states for all known companions
            var loader = CompanionBondSystem.Instance;
            if (loader == null) return;
            foreach (var profile in loader.AllProfiles)
                _states[profile.animalId] = new CompanionIntentionState { animalId = profile.animalId };
        }

        private void Update()
        {
            TrackPlayerPredictability();

            _evalTimer += Time.deltaTime;
            if (_evalTimer >= evalInterval)
            {
                _evalTimer = 0f;
                EvaluateAllIntentions();
            }

            TickActiveIntentions();
        }

        // -------------------------------------------------------------------------
        // Track how predictable (boring?) the player is being
        // -------------------------------------------------------------------------
        private void TrackPlayerPredictability()
        {
            var state = SpiritBrainOrchestrator.Instance?.CurrentPlayerState;
            if (state == null) return;

            float calm = state.calm;
            // If player is very calm and not moving much, trickster charge builds
            if (calm >= trickCalmThreshold && state.movementFlow < 0.25f)
                _playerCalmTimer += Time.deltaTime;
            else
                _playerCalmTimer = Mathf.Max(0f, _playerCalmTimer - Time.deltaTime * 0.5f);

            _prevPlayerCalm = calm;
        }

        // -------------------------------------------------------------------------
        // Main intention evaluation — runs every evalInterval seconds
        // -------------------------------------------------------------------------
        private void EvaluateAllIntentions()
        {
            var playerState = SpiritBrainOrchestrator.Instance?.CurrentPlayerState;
            var vrs         = VibrationalResonanceSystem.Instance;
            var bondSys     = CompanionBondSystem.Instance;
            if (playerState == null || vrs == null) return;

            // Read player's assignment once per evaluation cycle
            var assignment = CompanionAssignmentManager.Instance?.Assignment;

            // Myth modifier: companion sensitivity makes companions more responsive
            float companionSens = UniverseStateManager.Instance?.Current.mythState.companionSensitivity ?? 0f;
            float sensScale = 1f - companionSens * 0.15f;

            foreach (var kvp in _states)
            {
                string id     = kvp.Key;
                var    istate = kvp.Value;
                var    profile = bondSys?.GetProfile(id);
                if (profile == null) continue;

                float rawHarmony = vrs.GetHarmony(id);
                float bondLevel  = bondSys?.GetBondLevel(id) ?? 0f;
                var   npcEvo     = NpcEvolutionSystem.Instance?.GetState(profile.npcArchetype ?? id);

                // --- Assignment integration ---
                // An ongoing relationship strengthens the resonance channel.
                // Assigned animals notice the player more readily and stay longer.
                // This is not control — it is the channel being open.
                float assignmentBonus = ComputeAssignmentBonus(id, assignment, bondLevel);
                float harmony         = Mathf.Clamp01(rawHarmony + assignmentBonus);

                // If already departing, let it play out
                if (istate.intention == CompanionIntention.Departing) continue;

                // --- Decide new intention ---
                CompanionIntention newIntention = DecideIntention(
                    id, profile, istate, playerState, harmony, bondLevel, npcEvo, assignmentBonus);

                if (newIntention != istate.intention)
                    SetIntention(istate, newIntention, harmony, bondLevel, profile.tier, assignmentBonus > 0f);
            }
        }

        private CompanionIntention DecideIntention(
            string id, CompanionProfile profile, CompanionIntentionState istate,
            PlayerResonanceState playerState, float harmony, float bondLevel,
            NpcEvolutionState npcEvo, float assignmentBonus = 0f)
        {
            // ---- DEPARTURE: time up and stay roll failed ----
            if (istate.IsEngaged && istate.intentionTimer >= maxIntentionTime)
            {
                if (!ShouldStay(bondLevel, harmony, profile.tier, assignmentBonus > 0f))
                    return CompanionIntention.Departing;
            }

            // ---- TRICK / PLAY: player too predictable + this is a trickster ----
            // Age-tier aware: Seedling gets Playing, Explorer gets gentle trick, Voyager full trick.
            var ageTierConfig = UniverseStateManager.Instance?.Current?.AgeTierConfig;
            string trickMode = ageTierConfig?.tricksterMode ?? "full";
            float effectiveTrickTime = trickActivateTime + (assignmentBonus > 0f ? 30f : 0f);
            if (trickMode == "gentle") effectiveTrickTime *= 1.5f; // gentler = more patience
            if (TRICKSTERS.Contains(id) &&
                _playerCalmTimer >= effectiveTrickTime &&
                !istate.trickActive &&
                harmony > 0.30f)
            {
                if (trickMode == "play")
                    return CompanionIntention.Playing; // Seedling: joyful play instead
                return CompanionIntention.Tricking;
            }

            // ---- HELP: player in distress ----
            bool playerDistressed = playerState.distortion >= helpDistressThreshold * sensScale ||
                                    harmony < helpHarmonyThreshold / Mathf.Max(0.5f, sensScale);
            if (playerDistressed && MatchesElement(profile.element, playerState))
                return CompanionIntention.Helping;

            // ---- LEARN: novel player behavior, animal wants to observe ----
            bool novelPlayerState = playerState.wonder >= learnWonderThreshold * sensScale &&
                                    vrs.GetHarmonyVelocity(id) > 0.01f; // harmony rising
            float npcResonanceNeed = npcEvo != null ? (1f - npcEvo.resonance) : 0.5f;
            if (novelPlayerState && npcResonanceNeed > 0.3f && harmony > 0.40f)
                return CompanionIntention.Learning;

            // ---- RIDE: good harmony, no specific need ----
            if (harmony >= rideHarmonyThreshold * sensScale)
                return CompanionIntention.Riding;

            // ---- WANDER: harmony too low to engage ----
            return CompanionIntention.Wandering;
        }

        // -------------------------------------------------------------------------
        // Set intention and notify behavior controller
        // -------------------------------------------------------------------------
        private void SetIntention(CompanionIntentionState istate, CompanionIntention newIntention,
            float harmony, float bondLevel, int tier, bool isAssigned = false)
        {
            var prev = istate.intention;
            istate.intention      = newIntention;
            istate.intentionTimer = 0f;

            // Compute stay score at arrival
            // Assigned animals receive a flat stay bonus — the relationship matters to them too.
            if (newIntention != CompanionIntention.Wandering &&
                newIntention != CompanionIntention.Departing)
            {
                float mult       = tier switch { 1 => tier1StayMult, 2 => tier2StayMult, _ => tier3StayMult };
                float assignBonus = isAssigned ? 0.18f : 0f;  // ~18% flat stay lift for assigned
                istate.visitStayScore = Mathf.Clamp01(bondLevel * harmony * mult + assignBonus);
            }

            OnIntentionChanged?.Invoke(istate.animalId, newIntention);
            ApplyIntentionToBehavior(istate, newIntention);

            Debug.Log($"[CompanionIntentionSystem] {istate.animalId}: " +
                      $"{prev} → {newIntention} (stay={istate.visitStayScore:F2})");
        }

        // -------------------------------------------------------------------------
        // Apply intention as a behavior override
        // -------------------------------------------------------------------------
        private void ApplyIntentionToBehavior(CompanionIntentionState istate,
            CompanionIntention intention)
        {
            var ctrl = FindController(istate.animalId);
            if (ctrl == null) return;

            switch (intention)
            {
                case CompanionIntention.Helping:
                    ctrl.SetBehaviorOverride(CompanionRuleAction.Guard, maxIntentionTime);
                    break;

                case CompanionIntention.Tricking:
                    istate.trickActive = true;
                    // Explorer tier: half trick duration
                    float effectiveDuration = trickDuration;
                    var tierConfig = UniverseStateManager.Instance?.Current?.AgeTierConfig;
                    if (tierConfig?.tricksterMode == "gentle") effectiveDuration *= 0.5f;
                    ctrl.SetBehaviorOverride(CompanionRuleAction.Explore, effectiveDuration);
                    OnTrickBegins?.Invoke(istate.animalId);
                    Debug.Log($"[CompanionIntentionSystem] TRICK: {istate.animalId} breaking player's pattern!");
                    break;

                case CompanionIntention.Playing:
                    // Seedling: joyful, non-disruptive play — same Explore animation but shorter, no distortion
                    istate.trickActive = false;
                    ctrl.SetBehaviorOverride(CompanionRuleAction.Explore, trickDuration * 0.4f);
                    Debug.Log($"[CompanionIntentionSystem] PLAY: {istate.animalId} wants to play!");
                    break;

                case CompanionIntention.Learning:
                    istate.observationDepth = 0f;
                    // Learning: Follow at a distance — no override, use harmony-based natural follow
                    ctrl.ClearBehaviorOverride();
                    break;

                case CompanionIntention.Riding:
                    ctrl.ClearBehaviorOverride();  // harmony system handles it naturally
                    break;

                case CompanionIntention.Departing:
                    ctrl.SetBehaviorOverride(CompanionRuleAction.Release, departDuration);
                    OnAnimalDeparts?.Invoke(istate.animalId);
                    // Reset trickster charge so they can come back fresh
                    istate.tricksterCharge = 0f;
                    break;

                case CompanionIntention.Wandering:
                    ctrl.ClearBehaviorOverride();
                    break;
            }
        }

        // -------------------------------------------------------------------------
        // Tick active intentions each frame
        // -------------------------------------------------------------------------
        private void TickActiveIntentions()
        {
            foreach (var kvp in _states)
            {
                var istate = kvp.Value;
                if (istate.intention == CompanionIntention.Wandering) continue;

                istate.intentionTimer += Time.deltaTime;

                // Tick trickster
                if (istate.intention == CompanionIntention.Tricking)
                    TickTrick(istate);

                // Tick learning — absorb from observation
                if (istate.intention == CompanionIntention.Learning)
                    TickLearning(istate);

                // Clear departing after drift-away complete
                if (istate.intention == CompanionIntention.Departing &&
                    istate.intentionTimer >= departDuration)
                    istate.intention = CompanionIntention.Wandering;
            }
        }

        private void TickTrick(CompanionIntentionState istate)
        {
            if (istate.intentionTimer >= trickDuration)
            {
                istate.trickActive = false;
                _playerCalmTimer   = 0f; // reset predictability timer after trick
                OnTrickEnds?.Invoke(istate.animalId);

                // Tricksters ALWAYS depart after the trick — may return later
                SetIntention(istate, CompanionIntention.Departing, 0f, 0f, 1);
            }
        }

        private void TickLearning(CompanionIntentionState istate)
        {
            var profile = CompanionBondSystem.Instance?.GetProfile(istate.animalId);
            if (profile == null) return;

            var npcEvo = NpcEvolutionSystem.Instance?.GetState(profile.npcArchetype ?? istate.animalId);
            if (npcEvo == null) return;

            // Animal observes player — slowly updates its own resonance
            float playerActivity = SpiritBrainOrchestrator.Instance?.CurrentPlayerState?.sourceAlignment ?? 0f;
            istate.observationDepth += playerActivity * 0.001f * Time.deltaTime;

            // When observation is deep enough, animal has learned — it may stay or depart
            if (istate.observationDepth >= 0.5f)
            {
                npcEvo.resonance = Mathf.Clamp01(npcEvo.resonance + 0.03f);
                float harmony = VibrationalResonanceSystem.Instance?.GetHarmony(istate.animalId) ?? 0f;
                float bond    = CompanionBondSystem.Instance?.GetBondLevel(istate.animalId) ?? 0f;

                if (ShouldStay(bond, harmony, profile.tier))
                    SetIntention(istate, CompanionIntention.Riding, harmony, bond, profile.tier);
                else
                    SetIntention(istate, CompanionIntention.Departing, 0f, 0f, profile.tier);
            }
        }

        // -------------------------------------------------------------------------
        // Stay decision
        // -------------------------------------------------------------------------
        private bool ShouldStay(float bondLevel, float harmony, int tier, bool isAssigned = false)
        {
            float mult       = tier switch { 1 => tier1StayMult, 2 => tier2StayMult, _ => tier3StayMult };
            float assignBonus = isAssigned ? 0.18f : 0f;
            float prob       = Mathf.Clamp01(bondLevel * harmony * mult + assignBonus);
            return UnityEngine.Random.value < prob;
        }

        // -------------------------------------------------------------------------
        // Assignment bonus — relationship opens the resonance channel
        // -------------------------------------------------------------------------
        private static float ComputeAssignmentBonus(string animalId, CompanionAssignment assignment, float bondLevel)
        {
            if (assignment == null) return 0f;

            // Check every slot — primary gets the strongest signal
            bool isPrimary = assignment.primaryCompanion == animalId;
            bool isSession = assignment.sessionCompanion == animalId;
            bool isElement = assignment.airCompanion   == animalId ||
                             assignment.earthCompanion == animalId ||
                             assignment.waterCompanion == animalId ||
                             assignment.fireCompanion  == animalId;
            bool isRealm   = assignment.realmAnimals.Contains(animalId);

            if (!isPrimary && !isSession && !isElement && !isRealm) return 0f;

            // Primary = strongest channel. Realm = most specific. Element = general affinity.
            float slotBonus = isPrimary ? 0.12f
                            : isRealm  ? 0.10f
                            : isSession? 0.09f
                                       : 0.08f;

            // Bond depth scales the bonus — the longer the relationship, the more open the channel.
            // A bond of 0 = small invitation. A bond of 1 = full resonance channel open.
            return slotBonus * (0.40f + bondLevel * 0.60f);
        }

        // -------------------------------------------------------------------------
        // Does this companion's element match what the player currently needs?
        // -------------------------------------------------------------------------
        private static bool MatchesElement(string element, PlayerResonanceState s) =>
            element switch
            {
                "Fire"  => s.distortion > 0.5f || s.spinStability > 0.7f,
                "Water" => s.calm < 0.3f || s.movementFlow < 0.2f,
                "Earth" => s.breathCoherence < 0.3f || s.calm < 0.25f,
                "Air"   => s.wonder < 0.2f || s.sourceAlignment < 0.2f,
                _       => true
            };

        // -------------------------------------------------------------------------
        // External triggers (called by GameBootstrapper, realm systems, etc.)
        // -------------------------------------------------------------------------
        public void TriggerHelp(string animalId)
        {
            if (!_states.TryGetValue(animalId, out var istate)) return;
            var profile = CompanionBondSystem.Instance?.GetProfile(animalId);
            float harmony = VibrationalResonanceSystem.Instance?.GetHarmony(animalId) ?? 0f;
            float bond    = CompanionBondSystem.Instance?.GetBondLevel(animalId) ?? 0f;
            SetIntention(istate, CompanionIntention.Helping, harmony, bond, profile?.tier ?? 1);
        }

        public void TriggerDepart(string animalId)
        {
            if (!_states.TryGetValue(animalId, out var istate)) return;
            SetIntention(istate, CompanionIntention.Departing, 0f, 0f, 1);
        }

        // -------------------------------------------------------------------------
        // Public queries
        // -------------------------------------------------------------------------
        public CompanionIntention GetIntention(string animalId)
        {
            return _states.TryGetValue(animalId, out var s)
                ? s.intention : CompanionIntention.Wandering;
        }

        public bool IsTricking(string animalId)
            => _states.TryGetValue(animalId, out var s) && s.trickActive;

        public bool IsEngaged(string animalId)
            => _states.TryGetValue(animalId, out var s) && s.IsEngaged;

        public List<(string animalId, CompanionIntention intention)> GetActiveCompanions()
        {
            var result = new List<(string, CompanionIntention)>();
            foreach (var kvp in _states)
                if (kvp.Value.IsEngaged)
                    result.Add((kvp.Key, kvp.Value.intention));
            return result;
        }

        /// <summary>
        /// Summary string for UI — shows what companions are present and why.
        /// </summary>
        public string GetPresenceSummary()
        {
            var active = GetActiveCompanions();
            if (active.Count == 0) return "No companions present.";
            var parts = new System.Text.StringBuilder();
            foreach (var (id, intent) in active)
            {
                var profile = CompanionBondSystem.Instance?.GetProfile(id);
                string name  = profile?.displayName ?? id;
                string label = intent switch
                {
                    CompanionIntention.Helping   => "here to help",
                    CompanionIntention.Tricking  => "up to something",
                    CompanionIntention.Learning  => "watching and learning",
                    CompanionIntention.Riding    => "riding along",
                    CompanionIntention.Departing => "saying goodbye",
                    CompanionIntention.Playing   => "wants to play!",
                    _                            => "nearby"
                };
                parts.Append($"{name} — {label}\n");
            }
            return parts.ToString().TrimEnd();
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------
        private static CompanionBehaviorController FindController(string animalId)
        {
            foreach (var c in FindObjectsOfType<CompanionBehaviorController>())
                if (c.animalId == animalId) return c;
            return null;
        }
    }
}
