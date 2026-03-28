// SpiritsCrossing — VRBootstrapInstaller.cs
// Add ONE of these to the Bootstrap scene alongside GameBootstrapper.
// It automatically executes all 4 VR setup steps and re-runs on every scene load.
//
// Step 1 — Create VRInputAdapter singleton and bind XR rig transforms
// Step 2 — Create VRHapticsController and wire all game events
// Step 3 — Set HardwarePhysicalInputReader.motionSource = XR on every instance
// Step 4 — Set BreathMovementInterpreter.useVRGesturesWhenActive = true
//
// Additional auto-setup:
//   - Creates SpiritProfileLoader if missing
//   - Creates SpiritBrainOrchestrator in cave scenes
//   - Creates PhysicalInputBridge and assigns HardwarePhysicalInputReader
//   - Logs a clear status report each scene load so you can confirm wiring

using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpiritsCrossing.SpiritAI;
using SpiritsCrossing.BiometricInput;
using SpiritsCrossing.Companions;
using SpiritsCrossing.Memory;
using SpiritsCrossing.Cosmos;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Lifecycle;
using SpiritsCrossing.World;
using V243.SandstoneCave;

namespace SpiritsCrossing.VR
{
    public class VRBootstrapInstaller : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("VR Input")]
        [Tooltip("Optional: assign your XR Origin's Camera Offset/Main Camera. " +
                 "Auto-detected by name if null.")]
        public Transform xrHeadTransform;
        public Transform xrLeftHandTransform;
        public Transform xrRightHandTransform;

        [Header("Motion Source")]
        [Tooltip("Applied to every HardwarePhysicalInputReader found in scene.")]
        public HardwarePhysicalInputReader.MotionSourceType motionSourceOverride
            = HardwarePhysicalInputReader.MotionSourceType.XR;

        [Header("Biometric Reader Mode")]
        [Tooltip("When true, installs HardwarePhysicalInputReader. " +
                 "When false, installs SimulatedPhysicalInputReader (AI/testing).")]
        public bool useHardwareReader = true;
        public string simulatedArchetype = "Seated";

        [Header("Debug")]
        public bool printStatusEachScene = true;

        // -------------------------------------------------------------------------
        // Internal references (kept to avoid FindObjectOfType on every scene load)
        // -------------------------------------------------------------------------
        private VRInputAdapter             _inputAdapter;
        private VRHapticsController        _haptics;
        private SpiritProfileLoader        _profileLoader;
        private CompanionBondSystem        _companionBonds;
        private ResonanceMemorySystem      _memorySystem;
        private CosmosGenerationSystem     _cosmosSystem;
        private VibrationalResonanceSystem  _vibrationSystem;
        private LifecycleSystem             _lifecycleSystem;
        private SourceCommunionSystem       _communionSystem;
        private CosmosObserverMode          _cosmosObserver;
        private VibrationalMessenger        _messenger;
        private MeditationMode              _meditationMode;
        private WorldSystem                 _worldSystem;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            RunAllSteps();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RunAllSteps();
        }

        // -------------------------------------------------------------------------
        // Master installer
        // -------------------------------------------------------------------------
        private void RunAllSteps()
        {
            Step1_VRInputAdapter();
            Step2_VRHapticsController();
            Step3_HardwareReader();
            Step4_BreathInterpreter();
            Step5_SupportingSystems();
            Step6_CompanionAndMemory();

            if (printStatusEachScene)
                PrintStatus();
        }

        // =========================================================================
        // STEP 1 — VRInputAdapter
        // =========================================================================
        private void Step1_VRInputAdapter()
        {
            // Create if missing
            if (_inputAdapter == null)
                _inputAdapter = FindObjectOfType<VRInputAdapter>();

            if (_inputAdapter == null)
            {
                var go = new GameObject("[VRInputAdapter]");
                DontDestroyOnLoad(go);
                _inputAdapter = go.AddComponent<VRInputAdapter>();
            }

            // Auto-detect XR rig transforms by common Unity XR Origin naming
            if (xrHeadTransform == null)
                xrHeadTransform = FindTransformByName("Main Camera", "CenterEyeAnchor");

            if (xrLeftHandTransform == null)
                xrLeftHandTransform = FindTransformByName("LeftHand Controller",
                                                           "LeftHandAnchor", "Left Controller");

            if (xrRightHandTransform == null)
                xrRightHandTransform = FindTransformByName("RightHand Controller",
                                                            "RightHandAnchor", "Right Controller");

            // Bind to adapter (only overwrite if we found something)
            if (xrHeadTransform      != null) _inputAdapter.headTransform      = xrHeadTransform;
            if (xrLeftHandTransform  != null) _inputAdapter.leftHandTransform  = xrLeftHandTransform;
            if (xrRightHandTransform != null) _inputAdapter.rightHandTransform = xrRightHandTransform;
        }

        // =========================================================================
        // STEP 2 — VRHapticsController
        // =========================================================================
        private void Step2_VRHapticsController()
        {
            if (_haptics == null)
                _haptics = FindObjectOfType<VRHapticsController>();

            if (_haptics == null)
            {
                // Attach to same GameObject as the installer for clean hierarchy
                _haptics = gameObject.AddComponent<VRHapticsController>();
            }

            // Re-wire scene-specific references (orchestrator/cave change per scene)
            var orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();
            if (orchestrator != null)
                _haptics.spiritOrchestrator = orchestrator;

            var cave = FindObjectOfType<CaveSessionController>();
            if (cave != null)
                _haptics.caveSession = cave;
        }

        // =========================================================================
        // STEP 3 — HardwarePhysicalInputReader (or SimulatedPhysicalInputReader)
        // =========================================================================
        private void Step3_HardwareReader()
        {
            // Look for an existing reader in the scene
            var existingHardware = FindObjectOfType<HardwarePhysicalInputReader>();
            var existingSim      = FindObjectOfType<SimulatedPhysicalInputReader>();

            if (useHardwareReader)
            {
                if (existingHardware == null && existingSim == null)
                {
                    // Create hardware reader on CaveSystems if it exists, else on installer
                    var parent = FindTransformByName("CaveSystems") ?? transform;
                    existingHardware = parent.gameObject.AddComponent<HardwarePhysicalInputReader>();
                }

                // Apply motion source override to all hardware readers
                foreach (var reader in FindObjectsOfType<HardwarePhysicalInputReader>())
                    reader.motionSource = motionSourceOverride;
            }
            else
            {
                if (existingHardware == null && existingSim == null)
                {
                    var parent = FindTransformByName("CaveSystems") ?? transform;
                    var sim    = parent.gameObject.AddComponent<SimulatedPhysicalInputReader>();
                    sim.archetypeId = simulatedArchetype;
                }
            }

            // Wire into PhysicalInputBridge (Step 5 may have already created it)
            var bridge = FindObjectOfType<PhysicalInputBridge>();
            if (bridge != null)
            {
                IPhysicalInputReader reader = useHardwareReader
                    ? (IPhysicalInputReader)FindObjectOfType<HardwarePhysicalInputReader>()
                    : FindObjectOfType<SimulatedPhysicalInputReader>();

                if (reader != null && bridge.readerComponent == null)
                    bridge.readerComponent = reader as MonoBehaviour;
            }
        }

        // =========================================================================
        // STEP 4 — BreathMovementInterpreter VR gesture flag
        // =========================================================================
        private void Step4_BreathInterpreter()
        {
            foreach (var interp in FindObjectsOfType<BreathMovementInterpreter>())
            {
                interp.useVRGesturesWhenActive = true;
                // Disable debug keyboard in VR builds so gestures take priority
                if (XRSettings.isDeviceActive)
                    interp.useDebugKeyboardInput = false;
            }
        }

        // =========================================================================
        // STEP 5 — Supporting systems (SpiritProfileLoader, PhysicalInputBridge, Orchestrator)
        // =========================================================================
        private void Step5_SupportingSystems()
        {
            // SpiritProfileLoader
            if (_profileLoader == null)
                _profileLoader = FindObjectOfType<SpiritProfileLoader>();

            if (_profileLoader == null)
            {
                var go = new GameObject("[SpiritProfileLoader]");
                DontDestroyOnLoad(go);
                _profileLoader = go.AddComponent<SpiritProfileLoader>();
            }

            // PhysicalInputBridge — only needed in scenes with a BreathMovementInterpreter
            var breathInterp = FindObjectOfType<BreathMovementInterpreter>();
            if (breathInterp != null)
            {
                var bridge = FindObjectOfType<PhysicalInputBridge>();
                if (bridge == null)
                {
                    var parent = breathInterp.transform.parent ?? breathInterp.transform;
                    bridge = parent.gameObject.AddComponent<PhysicalInputBridge>();
                }
                if (bridge.breathInterpreter == null)
                    bridge.breathInterpreter = breathInterp;
            }

            // SpiritBrainOrchestrator — only in cave scenes
            if (breathInterp != null)
            {
                var orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();
                if (orchestrator == null)
                {
                    var parent = breathInterp.transform.parent ?? breathInterp.transform;
                    orchestrator = parent.gameObject.AddComponent<SpiritBrainOrchestrator>();
                }

                // Wire breath interpreter
                if (orchestrator.breathInterpreter == null)
                    orchestrator.breathInterpreter = breathInterp;

                // Update haptics reference
                if (_haptics != null && _haptics.spiritOrchestrator == null)
                    _haptics.spiritOrchestrator = orchestrator;
            }
        }

        // =========================================================================
        // STEP 6 — CompanionBondSystem + ResonanceMemorySystem
        // =========================================================================
        private void Step6_CompanionAndMemory()
        {
            // CompanionBondSystem
            if (_companionBonds == null)
                _companionBonds = FindObjectOfType<CompanionBondSystem>();
            if (_companionBonds == null)
            {
                var go = new GameObject("[CompanionBondSystem]");
                DontDestroyOnLoad(go);
                _companionBonds = go.AddComponent<CompanionBondSystem>();
            }

            // ResonanceMemorySystem
            if (_memorySystem == null)
                _memorySystem = FindObjectOfType<ResonanceMemorySystem>();
            if (_memorySystem == null)
            {
                var go = new GameObject("[ResonanceMemorySystem]");
                DontDestroyOnLoad(go);
                _memorySystem = go.AddComponent<ResonanceMemorySystem>();
            }

            // CosmosGenerationSystem
            if (_cosmosSystem == null)
                _cosmosSystem = FindObjectOfType<CosmosGenerationSystem>();
            if (_cosmosSystem == null)
            {
                var go = new GameObject("[CosmosGenerationSystem]");
                DontDestroyOnLoad(go);
                _cosmosSystem = go.AddComponent<CosmosGenerationSystem>();
            }

            // VibrationalResonanceSystem
            if (_vibrationSystem == null)
                _vibrationSystem = FindObjectOfType<VibrationalResonanceSystem>();
            if (_vibrationSystem == null)
            {
                var go = new GameObject("[VibrationalResonanceSystem]");
                DontDestroyOnLoad(go);
                _vibrationSystem = go.AddComponent<VibrationalResonanceSystem>();
            }

            // LifecycleSystem
            if (_lifecycleSystem == null) _lifecycleSystem = FindObjectOfType<LifecycleSystem>();
            if (_lifecycleSystem == null) { var go = new GameObject("[LifecycleSystem]"); DontDestroyOnLoad(go); _lifecycleSystem = go.AddComponent<LifecycleSystem>(); }

            // SourceCommunionSystem
            if (_communionSystem == null) _communionSystem = FindObjectOfType<SourceCommunionSystem>();
            if (_communionSystem == null) { var go = new GameObject("[SourceCommunionSystem]"); DontDestroyOnLoad(go); _communionSystem = go.AddComponent<SourceCommunionSystem>(); }

            // CosmosObserverMode
            if (_cosmosObserver == null) _cosmosObserver = FindObjectOfType<CosmosObserverMode>();
            if (_cosmosObserver == null) { var go = new GameObject("[CosmosObserverMode]"); DontDestroyOnLoad(go); _cosmosObserver = go.AddComponent<CosmosObserverMode>(); }

            // VibrationalMessenger
            if (_messenger == null) _messenger = FindObjectOfType<VibrationalMessenger>();
            if (_messenger == null) { var go = new GameObject("[VibrationalMessenger]"); DontDestroyOnLoad(go); _messenger = go.AddComponent<VibrationalMessenger>(); }

            // MeditationMode
            if (_meditationMode == null) _meditationMode = FindObjectOfType<MeditationMode>();
            if (_meditationMode == null) { var go = new GameObject("[MeditationMode]"); DontDestroyOnLoad(go); _meditationMode = go.AddComponent<MeditationMode>(); }

            // WorldSystem
            if (_worldSystem == null) _worldSystem = FindObjectOfType<WorldSystem>();
            if (_worldSystem == null) { var go = new GameObject("[WorldSystem]"); DontDestroyOnLoad(go); _worldSystem = go.AddComponent<WorldSystem>(); }
        }

        // =========================================================================
        // Status report
        // =========================================================================
        private void PrintStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════════╗");
            sb.AppendLine("║  VRBootstrapInstaller — Scene Wiring Status  ║");
            sb.AppendLine("╠══════════════════════════════════════════════╣");
            sb.AppendLine($"║  Scene:            {SceneManager.GetActiveScene().name,-27}║");
            sb.AppendLine($"║  VR Active:        {XRSettings.isDeviceActive,-27}║");
            sb.AppendLine("╠══════════════════════════════════════════════╣");
            sb.AppendLine($"║  [1] VRInputAdapter:      {StatusIcon(_inputAdapter)} {Name(_inputAdapter),-22}║");
            sb.AppendLine($"║      Head:                {StatusIcon(xrHeadTransform)} {Name(xrHeadTransform),-22}║");
            sb.AppendLine($"║      LeftHand:            {StatusIcon(xrLeftHandTransform)} {Name(xrLeftHandTransform),-22}║");
            sb.AppendLine($"║      RightHand:           {StatusIcon(xrRightHandTransform)} {Name(xrRightHandTransform),-22}║");
            sb.AppendLine($"║  [2] VRHapticsController: {StatusIcon(_haptics)} {Name(_haptics),-22}║");
            sb.AppendLine($"║      SpiritOrchestrator:  {StatusIcon(_haptics?.spiritOrchestrator)} {Name(_haptics?.spiritOrchestrator as Object),-22}║");
            sb.AppendLine($"║      CaveSession:         {StatusIcon(_haptics?.caveSession)} {Name(_haptics?.caveSession),-22}║");

            var hw  = FindObjectOfType<HardwarePhysicalInputReader>();
            var sim = FindObjectOfType<SimulatedPhysicalInputReader>();
            sb.AppendLine($"║  [3] HardwareReader:      {StatusIcon(hw ?? (Object)sim)} {(hw != null ? $"HW motionSource={hw.motionSource}" : sim != null ? $"SIM archetype={sim.archetypeId}" : "NOT FOUND"),-22}║");

            var interp = FindObjectOfType<BreathMovementInterpreter>();
            sb.AppendLine($"║  [4] BreathInterpreter:   {StatusIcon(interp)} VR gestures={(interp?.useVRGesturesWhenActive.ToString() ?? "N/A"),-19}║");

            var bridge = FindObjectOfType<PhysicalInputBridge>();
            sb.AppendLine($"║  [+] PhysicalInputBridge: {StatusIcon(bridge)} {Name(bridge),-22}║");
            var loader = FindObjectOfType<SpiritProfileLoader>();
            sb.AppendLine($"║  [+] SpiritProfileLoader: {StatusIcon(loader)} loaded={(loader?.IsLoaded.ToString() ?? "N/A"),-22}║");
            sb.AppendLine($"║  [+] CompanionBondSystem: {StatusIcon(_companionBonds)} loaded={(CompanionBondSystem.Instance?.IsLoaded.ToString() ?? "N/A"),-19}║");
            var memory = FindObjectOfType<ResonanceMemorySystem>();
            var learning = UniverseStateManager.Instance?.Current.learningState;
            sb.AppendLine($"║  [+] ResonanceMemory:     {StatusIcon(memory)} src={(learning?.sourceConnectionLevel.ToString("F2") ?? "N/A")} elem={(learning?.dominantElement ?? "?"),-13}║");
            var cosmos = FindObjectOfType<CosmosGenerationSystem>();
            sb.AppendLine($"║  [+] CosmosGeneration:    {StatusIcon(cosmos)} R={(CosmosGenerationSystem.Instance?.R_Universe.ToString("F3") ?? "N/A"),-24}║");
            var vib = FindObjectOfType<VibrationalResonanceSystem>();
            var vf  = VibrationalResonanceSystem.Instance?.PlayerField;
            sb.AppendLine($"║  [+] VibrationalField:    {StatusIcon(vib)} dom={(vf?.DominantBandName() ?? "N/A")} top={(VibrationalResonanceSystem.Instance?.HighestHarmonyAnimal ?? "?"),-12}║");
            var lc  = UniverseStateManager.Instance?.Current.lifecycle;
            string phase   = lc?.currentPhase.ToString() ?? "N/A";
            string depth   = SourceCommunionSystem.Instance?.MaxDepth.ToString("F2") ?? "—";
            sb.AppendLine($"║  [+] Lifecycle:           {StatusIcon(_lifecycleSystem)} {phase,-14} cycle={(lc?.cycleCount ?? 0),-3} depth={depth,-7}║");
            var med = MeditationMode.Instance;
            sb.AppendLine($"║  [+] MeditationMode:      {StatusIcon(med)} {(med?.GetStatusString() ?? "N/A"),-30}║");
            var ws = WorldSystem.Instance;
            int ruins = ws?.TotalDiscovered ?? 0;
            string wname = ws?.CurrentActiveWorld?.planetId ?? "cosmos";
            sb.AppendLine($"║  [+] WorldSystem:         {StatusIcon(ws)} ruins={ruins,-3} world={wname,-22}║");
            sb.AppendLine("╚══════════════════════════════════════════════╝");

            Debug.Log(sb.ToString());
        }

        // =========================================================================
        // Helpers
        // =========================================================================

        /// <summary>Find a Transform anywhere in the scene by one of several possible names.</summary>
        private static Transform FindTransformByName(params string[] names)
        {
            foreach (string n in names)
            {
                var go = GameObject.Find(n);
                if (go != null) return go.transform;
            }
            return null;
        }

        private static string StatusIcon(Object obj) => obj != null ? "✓" : "✗";
        private static string StatusIcon(object obj) => obj != null ? "✓" : "✗";
        private static string Name(Object obj)       => obj != null ? obj.name : "—";
    }
}
