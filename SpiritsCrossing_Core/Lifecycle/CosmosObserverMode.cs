// SpiritsCrossing — CosmosObserverMode.cs
// Available during InSource and as a standalone cosmos viewing mode.
//
// Shows every known player (friends/family) as a colored ghost presence
// on the cosmos map. Their color is their dominant vibrational band.
// Ghost opacity: Born = 0.85, InSource = 0.55 (translucent, liminal).
//
// For solo play, generates NPC presences from spirit_profiles.json —
// each archetype appears near its natural planet with an archetypal field.
//
// Players can leave a vibrational imprint on any planet they visit.
// Others sense these imprints when they visit the same planet.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.SpiritAI;
using SpiritsCrossing.Cosmos;

namespace SpiritsCrossing.Lifecycle
{
    public class CosmosObserverMode : MonoBehaviour
    {
        public static CosmosObserverMode Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("NPC Presences (solo play)")]
        [Tooltip("Generate NPC presences from spirit profiles if no real players are present.")]
        public bool generateNpcPresences = true;

        [Header("Observation Range")]
        [Tooltip("Distance on cosmos map within which presences are considered 'near' a planet.")]
        public float nearPlanetRange = 5f;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<CosmosPresence>   OnPresenceDiscovered;  // new presence seen
        public event Action<VibrationalMessage> OnImprintReceived;   // message on a planet

        // -------------------------------------------------------------------------
        // Public state
        // -------------------------------------------------------------------------
        public IReadOnlyList<CosmosPresence>    AllPresences   => _presences;
        public IReadOnlyList<VibrationalMessage> AllImprints   => _imprints;

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private readonly List<CosmosPresence>    _presences  = new();
        private readonly List<VibrationalMessage> _imprints  = new();

        // NPC archetype → natural planet mapping
        private static readonly Dictionary<string, string> ARCHETYPE_PLANET = new()
        {
            ["Seated"]        = "ForestHeart",
            ["FlowDancer"]    = "WaterFlow",
            ["Dervish"]       = "SkySpiral",
            ["PairA"]         = "ForestHeart",
            ["PairB"]         = "WaterFlow",
            ["EarthDragon"]   = "ForestHeart",
            ["FireDragon"]    = "DarkContrast",
            ["WaterDragon"]   = "WaterFlow",
            ["ElderAirDragon"]= "SkySpiral",
        };

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return new WaitUntil(() => SpiritProfileLoader.Instance?.IsLoaded ?? false);
            LoadAllPresences();
        }

        // -------------------------------------------------------------------------
        // Load presences: real players first, then fill with NPC ghosts
        // -------------------------------------------------------------------------
        private void LoadAllPresences()
        {
            _presences.Clear();

            // Real player presences from UniverseState
            var universe = UniverseStateManager.Instance?.Current;
            if (universe?.knownPresences != null)
                _presences.AddRange(universe.knownPresences);

            // NPC presences for solo play
            if (generateNpcPresences)
                GenerateNpcPresences();

            // Load persisted imprints
            if (universe?.sentMessages != null)
                _imprints.AddRange(universe.sentMessages);

            Debug.Log($"[CosmosObserverMode] {_presences.Count} presences, {_imprints.Count} imprints loaded.");
        }

        private void GenerateNpcPresences()
        {
            var loader = SpiritProfileLoader.Instance;
            if (loader == null) return;

            int npcIndex = 0;
            foreach (var profile in loader.Profiles.spirits)
            {
                string planetId = ARCHETYPE_PLANET.TryGetValue(profile.archetypeId, out string p)
                    ? p : "SourceVeil";

                var sig = profile.spectralSignature;
                var field = new VibrationalField(
                    sig.red, sig.orange, sig.yellow, sig.green, sig.blue, sig.indigo, sig.violet);

                var presence = new CosmosPresence
                {
                    playerId         = $"npc_{profile.archetypeId}",
                    playerName       = profile.archetypeId,
                    vibrationalField = field,
                    planetId         = planetId,
                    cyclePhase       = PlayerCyclePhase.Born,
                    dominantElement  = profile.element ?? "Air",
                    lastSeenUtc      = DateTime.UtcNow.ToString("o"),
                    glowIntensity    = profile.coherenceBaseline,
                    isInSource       = false,
                    isAncientMemory  = false,
                };
                _presences.Add(presence);
                npcIndex++;
            }
            Debug.Log($"[CosmosObserverMode] Generated {npcIndex} NPC presences.");
        }

        // -------------------------------------------------------------------------
        // Planet growth pulse — how "alive" a planet is right now
        // -------------------------------------------------------------------------
        public float GetPlanetGrowthPulse(string planetId)
        {
            float growth = CosmosGenerationSystem.Instance?.GetLivePlanetGrowth(planetId) ?? 0f;
            // Tightened: base 0.50, swing ±0.15 — matches PlanetNodeController.GetGrowthPulse()
            float pulse = growth * (0.50f + 0.15f * Mathf.Sin(Time.time * 0.8f));
            return Mathf.Clamp01(pulse);
        }

        // -------------------------------------------------------------------------
        // Presence queries
        // -------------------------------------------------------------------------
        public List<CosmosPresence> GetPresencesNearPlanet(string planetId)
        {
            var result = new List<CosmosPresence>();
            foreach (var p in _presences)
                if (p.planetId == planetId) result.Add(p);
            return result;
        }

        public List<CosmosPresence> GetPresencesInSource()
        {
            var result = new List<CosmosPresence>();
            foreach (var p in _presences)
                if (p.isInSource) result.Add(p);
            return result;
        }

        public CosmosPresence GetPresence(string playerId)
        {
            foreach (var p in _presences) if (p.playerId == playerId) return p;
            return null;
        }

        // -------------------------------------------------------------------------
        // Imprint — leave a vibrational mark on a planet
        // -------------------------------------------------------------------------
        public void LeaveImprint(string planetId)
        {
            var vib = VibrationalResonanceSystem.Instance?.PlayerField;
            if (vib == null) return;

            var lc       = UniverseStateManager.Instance?.Current.lifecycle;
            var learning = UniverseStateManager.Instance?.Current.learningState;

            var msg = new VibrationalMessage
            {
                senderId      = "player",
                senderName    = "You",
                senderField   = new VibrationalField(
                    vib.red, vib.orange, vib.yellow, vib.green, vib.blue, vib.indigo, vib.violet),
                planetId      = planetId,
                sentUtc       = DateTime.UtcNow.ToString("o"),
                sourceIntensity = learning?.sourceConnectionLevel ?? 0f,
                isFromSource  = lc?.IsInSource ?? false,
            };

            // Color-encode via VibrationalMessenger
            var encoded = VibrationalMessenger.Instance?.EncodeFieldToColor(msg.senderField)
                          ?? Color.white;
            msg.encodedR = encoded.r; msg.encodedG = encoded.g;
            msg.encodedB = encoded.b; msg.encodedA = encoded.a;

            // Persist (cap at 20)
            var universe = UniverseStateManager.Instance?.Current;
            if (universe != null)
            {
                universe.sentMessages.Add(msg);
                if (universe.sentMessages.Count > 20)
                    universe.sentMessages.RemoveAt(0);
                UniverseStateManager.Instance.Save();
            }

            _imprints.Add(msg);
            Debug.Log($"[CosmosObserverMode] Imprint left on {planetId} " +
                      $"(dom={msg.senderField.DominantBandName()} source={msg.sourceIntensity:F2})");
        }

        // -------------------------------------------------------------------------
        // Receive imprints for a planet
        // -------------------------------------------------------------------------
        public List<VibrationalMessage> GetPlanetMessages(string planetId)
        {
            var result = new List<VibrationalMessage>();
            foreach (var m in _imprints)
                if (m.planetId == planetId) result.Add(m);
            return result;
        }

        public List<VibrationalMessage> GetSourceImprints()
        {
            var result = new List<VibrationalMessage>();
            foreach (var m in _imprints) if (m.isFromSource) result.Add(m);
            return result;
        }

        // -------------------------------------------------------------------------
        // Register an incoming presence (multiplayer hookup point)
        // -------------------------------------------------------------------------
        public void RegisterPresence(CosmosPresence presence)
        {
            // Remove old entry for same player
            _presences.RemoveAll(p => p.playerId == presence.playerId);
            _presences.Add(presence);

            var universe = UniverseStateManager.Instance?.Current;
            if (universe != null)
            {
                universe.knownPresences.RemoveAll(p => p.playerId == presence.playerId);
                universe.knownPresences.Add(presence);
            }

            OnPresenceDiscovered?.Invoke(presence);
        }

        // -------------------------------------------------------------------------
        // Update player's own presence in the cosmos map
        // -------------------------------------------------------------------------
        private void Update()
        {
            // Push current player presence every 5 seconds
            _updateTimer += Time.deltaTime;
            if (_updateTimer < 5f) return;
            _updateTimer = 0f;
            UpdateOwnPresence();
        }

        private float _updateTimer;

        private void UpdateOwnPresence()
        {
            var universe = UniverseStateManager.Instance?.Current;
            var learning = universe?.learningState;
            var lc       = universe?.lifecycle;
            if (universe == null) return;

            var presence = CosmosPresence.FromUniverseState(
                "player", "You",
                universe.persistentResonance,
                universe.lastRealmOutcome?.planetId ?? "SkySpiral",
                lc?.currentPhase ?? PlayerCyclePhase.Born,
                learning?.dominantElement ?? "Air",
                learning?.sourceConnectionLevel ?? 0f);

            presence.isAncientMemory = (lc?.cycleCount ?? 0) >= 2;

            // Update in presences list
            _presences.RemoveAll(p => p.playerId == "player");
            _presences.Add(presence);
        }
    }
}
