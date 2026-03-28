// SpiritsCrossing — CosmosClockSystem.cs
// The cosmos is alive between player sessions.
//
// On game start, reads the UTC timestamp of the last session.
// Computes elapsed real-world hours.
// Advances NpcEvolutionSystem and PlanetAutonomySystem by that time.
// Generates a "what happened while you were away" event log.
//
// This is the implementation of the AME v7 Memory system stub:
//   (ai_multiverse_engine_v7/engine/systems.py: class Memory: def run(worlds): pass)
// — that `pass` was always the placeholder for this.
//
// The RUE Universe.time counter advances even when the player is not there.
// The star keeps emitting. The planets keep orbiting. The NPCs keep evolving.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpiritsCrossing.Autonomous
{
    [Serializable]
    public class CosmosEvent
    {
        public string eventType;    // "PlanetGrew" | "NpcShifted" | "SolarFlare" | "RuinDiscovered"
        public string subject;      // planetId or archetypeId
        public string description;
        public string utcTimestamp;
    }

    public class CosmosClockSystem : MonoBehaviour
    {
        public static CosmosClockSystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Debug")]
        public bool logAwayReport = true;
        [Tooltip("Max hours to simulate on return (prevents very long catch-ups).")]
        public float maxElapsedHours = 72f;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<List<CosmosEvent>> OnAwayEventsReady;  // fired after catch-up

        // -------------------------------------------------------------------------
        // Public state
        // -------------------------------------------------------------------------
        public float          ElapsedHours   { get; private set; }
        public List<CosmosEvent> LastEvents  { get; private set; } = new List<CosmosEvent>();
        public bool           HasAwayEvents  => LastEvents.Count > 0;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private IEnumerator Start()
        {
            // Wait for all dependent systems to load
            yield return new WaitUntil(() =>
                CosmosGenerationSystem.Instance?.IsLoaded == true &&
                NpcEvolutionSystem.Instance != null &&
                PlanetAutonomySystem.Instance != null);

            yield return null; // one frame for all Start()s to complete

            RunCatchUp();

            // Subscribe to session end so we update the clock
            if (UniverseStateManager.Instance != null)
                UniverseStateManager.Instance.OnSessionApplied += OnSessionEnded;
        }

        private void OnDestroy()
        {
            if (UniverseStateManager.Instance != null)
                UniverseStateManager.Instance.OnSessionApplied -= OnSessionEnded;
        }

        // -------------------------------------------------------------------------
        // Main catch-up — runs once on start
        // -------------------------------------------------------------------------
        private void RunCatchUp()
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            // First session — no last timestamp
            if (string.IsNullOrEmpty(universe.lastSessionUtc))
            {
                universe.lastSessionUtc = DateTime.UtcNow.ToString("o");
                UniverseStateManager.Instance.Save();
                Debug.Log("[CosmosClockSystem] First session — cosmos clock started.");
                return;
            }

            // Compute elapsed time
            if (!DateTime.TryParse(universe.lastSessionUtc, out DateTime lastUtc))
            {
                universe.lastSessionUtc = DateTime.UtcNow.ToString("o");
                return;
            }

            ElapsedHours = (float)(DateTime.UtcNow - lastUtc).TotalHours;
            ElapsedHours = Mathf.Clamp(ElapsedHours, 0f, maxElapsedHours);

            if (ElapsedHours < 0.01f)
            {
                Debug.Log("[CosmosClockSystem] Less than 1 minute away — no catch-up needed.");
                return;
            }

            Debug.Log($"[CosmosClockSystem] {ElapsedHours:F1}h elapsed. Running cosmos catch-up...");

            LastEvents.Clear();

            // Advance NPCs
            var npcEvents = NpcEvolutionSystem.Instance?.AdvanceByElapsedTime(ElapsedHours);
            if (npcEvents != null)
                foreach (var e in npcEvents)
                    LastEvents.Add(new CosmosEvent
                    {
                        eventType   = "NpcShifted",
                        subject     = e,
                        description = e,
                        utcTimestamp = DateTime.UtcNow.ToString("o")
                    });

            // Advance planets
            var planetEvents = PlanetAutonomySystem.Instance?.AdvanceByElapsedTime(ElapsedHours);
            if (planetEvents != null)
                foreach (var e in planetEvents)
                {
                    string type = e.Contains("flare") ? "SolarFlare" : "PlanetGrew";
                    LastEvents.Add(new CosmosEvent
                    {
                        eventType   = type,
                        subject     = type == "SolarFlare" ? "star" : e.Split(' ')[0],
                        description = e,
                        utcTimestamp = DateTime.UtcNow.ToString("o")
                    });
                }

            // Save updated state
            UniverseStateManager.Instance?.Save();
            OnAwayEventsReady?.Invoke(LastEvents);

            if (logAwayReport)
                Debug.Log($"[CosmosClockSystem] Catch-up complete. {LastEvents.Count} events:\n" +
                          string.Join("\n", LastEvents.ConvertAll(e => $"  • {e.description}")));
        }

        // -------------------------------------------------------------------------
        // Record session end time whenever a session completes
        // -------------------------------------------------------------------------
        private void OnSessionEnded(SessionResonanceResult _)
        {
            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;
            universe.lastSessionUtc = DateTime.UtcNow.ToString("o");
            // UniverseStateManager will auto-save after session
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Human-readable summary of what the cosmos did while the player was away.
        /// Used for the return greeting screen.
        /// </summary>
        public string GetAwayReport()
        {
            if (ElapsedHours < 0.01f) return "The cosmos awaited your return.";
            if (LastEvents.Count == 0)
                return $"The cosmos turned for {FormatHours(ElapsedHours)}. All was still.";

            int npcShifts   = 0, grows = 0, flares = 0;
            foreach (var e in LastEvents)
            {
                if (e.eventType == "NpcShifted")  npcShifts++;
                if (e.eventType == "PlanetGrew")   grows++;
                if (e.eventType == "SolarFlare")   flares++;
            }

            var parts = new List<string>();
            parts.Add($"Gone for {FormatHours(ElapsedHours)}.");
            if (grows    > 0) parts.Add($"{grows} planet(s) grew.");
            if (npcShifts> 0) parts.Add($"{npcShifts} spirit(s) evolved.");
            if (flares   > 0) parts.Add("A solar flare swept the cosmos.");
            return string.Join(" ", parts);
        }

        /// <summary>
        /// The universe step count equivalent to elapsed time (for RUE logging).
        /// RUE Universe.time is measured in discrete steps.
        /// </summary>
        public int ElapsedUniverseSteps()
            => Mathf.RoundToInt(ElapsedHours * 720f); // 720 steps per hour (from NpcEvolutionSystem)

        private static string FormatHours(float h)
        {
            if (h < 1f)   return $"{Mathf.RoundToInt(h * 60)} minutes";
            if (h < 24f)  return $"{h:F1} hours";
            return $"{h / 24f:F1} days";
        }
    }
}
