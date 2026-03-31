// SpiritsCrossing — RUELiveLoop.cs
// C# equivalent of rue-engine/engine/engine_loop.py  EngineLoop.step()
//
// RUE Python:
//   class EngineLoop:
//       def step(self):
//           energy = self.universe.step()      # advance planets + emit star energy
//           for agent in self.agents:
//               agent.step(energy)             # update SnowflakeBrain + activity
//           return energy
//
// In C# the universe physics already runs frame-by-frame inside
// CosmosGenerationSystem.Update() (Kuramoto R_universe) and
// PlanetAutonomySystem (batch catch-up on session start).
// What was missing: the agent-side of EngineLoop.step() during live play.
//
// This MonoBehaviour fills that gap. Every liveStepInterval seconds it:
//   1. Calls NpcEvolutionSystem.AdvanceLiveStep()   → StepNpc × all NPCs
//   2. Pushes evolved spectral into VibrationalResonanceSystem  (Gap 2 fix)
//   3. Increments UniverseState.universeTimeSteps   (Gap 4 fix, RUE Universe.time)
//
// The step rate is kept deliberately slow (default 2 s / step) so the 21-node
// Kuramoto oscillator doesn't dominate CPU. Each NPC runs one SnowflakeBrain
// update per tick. Between ticks, harmony scoring uses the last pushed spectral.
//
// Setup: Auto-created by VRBootstrapInstaller.Step6_CompanionAndMemory().

using UnityEngine;
using V243.SandstoneCave;

namespace SpiritsCrossing.Autonomous
{
    public class RUELiveLoop : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------
        public static RUELiveLoop Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("RUE Step Rate")]
        [Tooltip("Seconds of real time between each RUE EngineLoop step.\n" +
                 "Lower = more responsive NPC evolution, higher CPU cost.\n" +
                 "Default 2s means ~30 RUE steps/minute during live play.\n" +
                 "Compare: rue-engine runs at up to 1000 steps per sim run.")]
        [Range(0.5f, 10f)] public float liveStepInterval = 2.0f;

        [Tooltip("Suspend live steps while no session is running " +
                 "(player on cosmos map). NPCs still evolve during sessions.")]
        public bool pauseWhenNoSession = false;

        [Header("Debug")]
        public bool logEveryStep;

        // -------------------------------------------------------------------------
        // Public state (mirrors RUE EngineLoop properties)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Steps advanced during this play session (resets each game launch).
        /// Not the same as universeTimeSteps which persists across saves.
        /// Equivalent to reading Universe.time at a snapshot.
        /// </summary>
        public long SessionSteps { get; private set; }

        /// <summary>
        /// Average activity level across all NPCs at the last step.
        /// Equivalent to statistics.mean(agent.activity for agent in engine.agents).
        /// </summary>
        public float LastMeanActivity { get; private set; }

        /// <summary>
        /// Average coherence across all NPCs at the last step.
        /// Equivalent to statistics.mean(brain.coherence() for brain in ...).
        /// </summary>
        public float LastMeanCoherence { get; private set; }

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private float _timer;
        private CaveSessionController _cave; // cached — used only when pauseWhenNoSession = true

        // -------------------------------------------------------------------------
        // Update — the live EngineLoop
        // -------------------------------------------------------------------------
        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < liveStepInterval) return;
            _timer = 0f;

            // Optionally suspend while the ritual cave session is not running.
            // CaveSessionController is cached to avoid per-tick FindObjectOfType.
            if (pauseWhenNoSession)
            {
                if (_cave == null) _cave = FindObjectOfType<CaveSessionController>();
                if (_cave == null || !_cave.sessionRunning) return;
            }

            var npcSystem = NpcEvolutionSystem.Instance;
            if (npcSystem == null) return;

            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            // Advance all NPCs one RUE step + push spectral to VibrationalSystem
            npcSystem.AdvanceLiveStep();

            // Increment the persistent RUE Universe.time counter
            universe.universeTimeSteps++;
            SessionSteps++;

            // Compute live summary stats (mirrors run_simulation.py logging)
            float totalActivity  = 0f;
            float totalCoherence = 0f;
            int   count          = 0;
            foreach (var state in universe.npcStates)
            {
                totalActivity  += state.activity;
                totalCoherence += state.coherence;
                count++;
            }

            if (count > 0)
            {
                LastMeanActivity  = totalActivity  / count;
                LastMeanCoherence = totalCoherence / count;
            }

            if (logEveryStep)
                Debug.Log($"[RUELiveLoop] step={universe.universeTimeSteps} " +
                          $"activity={LastMeanActivity:F3} coherence={LastMeanCoherence:F3}");
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Status string matching the run_simulation.py console output format.
        /// Useful for debug UI.
        /// </summary>
        public string GetStatusLine()
        {
            var universe = UniverseStateManager.Instance?.Current;
            long step = universe?.universeTimeSteps ?? 0;
            return $"step={step:D6}  avg_activity={LastMeanActivity:F3}  " +
                   $"avg_coherence={LastMeanCoherence:F3}";
        }
    }
}
