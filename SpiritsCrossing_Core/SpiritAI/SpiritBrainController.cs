// SpiritsCrossing — SpiritBrainController.cs
// Lightweight C# port of the RBE-1 drive arbitration loop.
// No Python, no numpy, no external dependencies — pure Unity math.
//
// Each spirit NPC carries one of these. It:
//   1. Loads a SpiritDriveProfile from SpiritProfileLoader (the archetype seed)
//   2. Receives live player resonance input each frame via UpdateFromPlayerState()
//   3. Runs the spectral amplitude + drive computation
//   4. Exposes the current SpiritDriveMode for animation, audio, and interaction
//
// Wiring:
//   - Assign archetypeId in the Inspector (matches spirit_profiles.json)
//   - Call UpdateFromPlayerState() from CaveSessionController or BreathMovementInterpreter
//   - Read CurrentMode and CurrentAmplitude from SpiritLikenessController / animators

using System;
using UnityEngine;
using SpiritsCrossing;
using SpiritsCrossing.Lifecycle;

namespace SpiritsCrossing.SpiritAI
{
    public class SpiritBrainController : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Archetype")]
        public string archetypeId = "Seated";

        [Header("Tuning")]
        [Range(0.01f, 0.30f)] public float dt            = 0.05f;   // brain update step
        [Range(0.00f, 1.00f)] public float inputGain     = 0.65f;   // player → spirit influence
        [Range(0.00f, 1.00f)] public float memoryDecay   = 0.97f;   // how long memory persists
        [Range(0.00f, 1.00f)] public float cooldownReset = 0.35f;   // attack cooldown threshold

        [Header("Runtime — read-only")]
        [SerializeField] private SpiritDriveMode _currentMode = SpiritDriveMode.Rest;
        [SerializeField, Range(0f,1f)] private float _coherence;
        [SerializeField] private float[] _amplitude = new float[7];
        [SerializeField] private float[] _phase     = new float[7];
        [SerializeField] private float[] _memory    = new float[7];

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------
        public SpiritDriveMode CurrentMode     => _currentMode;
        public float           Coherence       => _coherence;
        public float[]         CurrentAmplitude => _amplitude;

        /// <summary>0–1 normalised drive strengths in the order [attack,flee,seek,rest,signal,explore].</summary>
        public SpiritDriveWeights CurrentDrives { get; private set; } = new SpiritDriveWeights();

        /// <summary>Fires whenever the dominant drive mode changes.</summary>
        public event Action<SpiritDriveMode> OnModeChanged;

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private SpiritDriveProfile _profile;
        private float[]            _freq      = new float[7];
        private int                _cooldown;
        private bool               _initialised;
        private SpiritDriveMode    _prevMode;

        // Band indices matching RBE-1's BANDS order
        private const int RED=0, ORANGE=1, YELLOW=2, GREEN=3, BLUE=4, INDIGO=5, VIOLET=6;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            TryInitialise();
        }

        private void Update()
        {
            if (!_initialised) TryInitialise();
        }

        // -------------------------------------------------------------------------
        // Initialisation
        // -------------------------------------------------------------------------
        private void TryInitialise()
        {
            if (_initialised) return;

            _profile = SpiritProfileLoader.Instance?.GetProfile(archetypeId);
            if (_profile == null)
            {
                // Loader not ready yet — retry next frame
                return;
            }

            // Seed amplitude from the archetype's spectral signature
            var sig = _profile.spectralSignature;
            _amplitude[RED]    = sig.red;
            _amplitude[ORANGE] = sig.orange;
            _amplitude[YELLOW] = sig.yellow;
            _amplitude[GREEN]  = sig.green;
            _amplitude[BLUE]   = sig.blue;
            _amplitude[INDIGO] = sig.indigo;
            _amplitude[VIOLET] = sig.violet;

            // Random initial phases
            for (int i = 0; i < 7; i++)
            {
                _phase[i]  = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                _freq[i]   = Mathf.Lerp(0.90f, 1.10f, i / 6f) + UnityEngine.Random.Range(-0.03f, 0.03f);
                _memory[i] = 0f;
            }

            _currentMode = _profile.DominantMode();
            _prevMode    = _currentMode;
            _coherence   = _profile.coherenceBaseline;
            _initialised = true;

            Debug.Log($"[SpiritBrainController] {archetypeId} initialised. DominantDrive={_currentMode}");
        }

        // -------------------------------------------------------------------------
        // Drive update — call every frame or on a fixed interval
        // -------------------------------------------------------------------------

        /// <summary>
        /// Feed the current player resonance state into this spirit's brain.
        /// Call from CaveSessionController.Update() or BreathMovementInterpreter.
        /// </summary>
        public void UpdateFromPlayerState(PlayerResonanceState playerState, float health01 = 1f, float energy01 = 1f)
        {
            if (!_initialised) return;

            float[] sensory = BuildSensoryVector(playerState);
            UpdateAmplitudeAndPhase(sensory);
            var drives = ComputeDrives(health01, energy01);
            drives = ApplyLifecycleModifier(drives);
            CurrentDrives = drives;

            SpiritDriveMode newMode = Arbitrate(drives);
            _currentMode = newMode;

            if (newMode != _prevMode)
            {
                OnModeChanged?.Invoke(newMode);
                _prevMode = newMode;
            }

            if (_cooldown > 0) _cooldown--;
        }

        // -------------------------------------------------------------------------
        // Sensory encoding — maps player resonance → 7-band input
        // Mirrors RBE-1's build_sensory_vector() for the spirit's "perception of player"
        // -------------------------------------------------------------------------
        private float[] BuildSensoryVector(PlayerResonanceState p)
        {
            var s = new float[7];
            s[RED]    = Mathf.Clamp01(0.55f * p.distortion   + 0.45f * (1f - p.calm));
            s[ORANGE] = Mathf.Clamp01(0.50f * p.socialSync   + 0.50f * p.movementFlow);
            s[YELLOW] = Mathf.Clamp01(0.70f * p.joy          + 0.30f * p.wonder);
            s[GREEN]  = Mathf.Clamp01(0.60f * p.calm         + 0.40f * p.breathCoherence);
            s[BLUE]   = Mathf.Clamp01(0.65f * p.movementFlow + 0.35f * p.spinStability);
            s[INDIGO] = Mathf.Clamp01(0.70f * p.socialSync   + 0.30f * p.sourceAlignment);
            s[VIOLET] = Mathf.Clamp01(0.60f * p.wonder       + 0.40f * p.sourceAlignment);
            return s;
        }

        // -------------------------------------------------------------------------
        // Amplitude + phase update — simplified Kuramoto + Van der Pol, no numpy
        // -------------------------------------------------------------------------
        private void UpdateAmplitudeAndPhase(float[] sensory)
        {
            // Phase update with green (index 3) as hub
            float[] newPhase = new float[7];
            for (int k = 0; k < 7; k++)
            {
                newPhase[k] = _phase[k] + dt * _freq[k];
                if (k != GREEN)
                    newPhase[k] += dt * 0.18f * Mathf.Sin(_phase[GREEN] - _phase[k]);
                int left  = (k + 6) % 7;
                int right = (k + 1) % 7;
                newPhase[k] += dt * 0.08f * Mathf.Sin(_phase[left]  - _phase[k]);
                newPhase[k] += dt * 0.08f * Mathf.Sin(_phase[right] - _phase[k]);
                newPhase[k] = Mathf.Repeat(newPhase[k], Mathf.PI * 2f);
            }

            // Amplitude update — blend profile weight + live sensory input + memory
            float[] baseWeights = new float[7]
            {
                _profile.driveWeights.attack,   // red
                _profile.driveWeights.signal,   // orange (social proxy)
                _profile.driveWeights.seek,     // yellow
                _profile.driveWeights.rest,     // green
                _profile.driveWeights.explore,  // blue (motion proxy)
                _profile.driveWeights.signal,   // indigo
                _profile.driveWeights.explore   // violet
            };

            for (int k = 0; k < 7; k++)
            {
                float target = Mathf.Clamp01(
                    0.40f * baseWeights[k] +
                    inputGain * sensory[k] +
                    0.15f * _memory[k]);
                _amplitude[k] = Mathf.Clamp01(_amplitude[k] + dt * (target - _amplitude[k]));
                _memory[k]    = Mathf.Clamp01(memoryDecay * _memory[k] + (1f - memoryDecay) * sensory[k]);
            }

            // Coherence (mean resultant vector length)
            float cosSum = 0f, sinSum = 0f;
            for (int k = 0; k < 7; k++) { cosSum += Mathf.Cos(newPhase[k]); sinSum += Mathf.Sin(newPhase[k]); }
            _coherence = Mathf.Sqrt(cosSum * cosSum + sinSum * sinSum) / 7f;

            Array.Copy(newPhase, _phase, 7);
        }

        // -------------------------------------------------------------------------
        // Drive computation — mirrors RBE1Prototype.compute_drives() in C#
        // -------------------------------------------------------------------------
        private SpiritDriveWeights ComputeDrives(float healthFrac, float energyFrac)
        {
            float pRB = Mathf.Cos(_phase[RED] - _phase[BLUE]);
            float pRG = Mathf.Cos(_phase[RED] - _phase[GREEN]);
            float C   = _coherence;

            float aggression = Mathf.Max(0f,
                (0.70f * _amplitude[RED]   + 0.30f * _amplitude[BLUE]  - 0.40f * _amplitude[GREEN]) *
                (0.5f + 0.5f * Mathf.Max(0f, pRB))) * C;

            float fear = Mathf.Max(0f,
                (0.80f * _amplitude[RED]   + 0.35f * (1f - healthFrac) - 0.20f * _amplitude[GREEN]) *
                (0.6f + 0.4f * Mathf.Max(0f, pRG))) * C;

            float seek = Mathf.Max(0f,
                0.60f * _amplitude[YELLOW] + 0.30f * _amplitude[BLUE]  +
                0.20f * _amplitude[VIOLET] + 0.25f * (1f - energyFrac)) *
                Mathf.Max(0.35f, C);

            float rest = Mathf.Max(0f,
                0.70f * _amplitude[GREEN]  - 0.30f * _amplitude[RED]   -
                0.20f * _amplitude[VIOLET] + 0.30f * (1f - healthFrac)) *
                Mathf.Max(0.35f, C);

            float social = Mathf.Max(0f,
                0.70f * _amplitude[INDIGO] + 0.20f * _amplitude[ORANGE]) *
                Mathf.Max(0.35f, C);

            float explore = Mathf.Max(0f,
                0.80f * _amplitude[VIOLET] + 0.30f * _amplitude[BLUE]  -
                0.20f * _amplitude[GREEN]) *
                Mathf.Max(0.35f, C);

            float total = aggression + fear + seek + rest + social + explore;
            if (total < 1e-6f) total = 1f;

            return new SpiritDriveWeights
            {
                attack  = aggression / total,
                flee    = fear       / total,
                seek    = seek       / total,
                rest    = rest       / total,
                signal  = social     / total,
                explore = explore    / total
            };
        }

        // -------------------------------------------------------------------------
        // Arbitration — highest drive wins; attack suppressed during cooldown
        // -------------------------------------------------------------------------
        private SpiritDriveMode Arbitrate(SpiritDriveWeights drives)
        {
            float best = -1f;
            SpiritDriveMode winner = SpiritDriveMode.Rest;

            Check(SpiritDriveMode.Attack,  _cooldown > 0 ? drives.attack * 0.5f : drives.attack);
            Check(SpiritDriveMode.Flee,    drives.flee);
            Check(SpiritDriveMode.Seek,    drives.seek);
            Check(SpiritDriveMode.Rest,    drives.rest);
            Check(SpiritDriveMode.Signal,  drives.signal);
            Check(SpiritDriveMode.Explore, drives.explore);

            if (winner == SpiritDriveMode.Attack) _cooldown = 4;
            return winner;

            void Check(SpiritDriveMode mode, float value)
            {
                if (value > best) { best = value; winner = mode; }
            }
        }

        // -------------------------------------------------------------------------
        // Lifecycle phase modifier — shifts drive weights toward cycle-appropriate
        // behaviour (welcoming on Birth, quiet in Source, surge on Rebirth)
        // -------------------------------------------------------------------------
        private SpiritDriveWeights ApplyLifecycleModifier(SpiritDriveWeights d)
        {
            var mod = AILifecycleLearningPath.Instance?.CurrentModifier;
            if (mod == null) return d;

            d.attack  = Mathf.Clamp01(d.attack  + mod.attackShift);
            d.flee    = Mathf.Clamp01(d.flee    + mod.fleeShift);
            d.seek    = Mathf.Clamp01(d.seek    + mod.seekShift);
            d.rest    = Mathf.Clamp01(d.rest    + mod.restShift);
            d.signal  = Mathf.Clamp01(d.signal  + mod.signalShift);
            d.explore = Mathf.Clamp01(d.explore + mod.exploreShift);

            // Renormalise so drives still sum to 1
            float total = d.attack + d.flee + d.seek + d.rest + d.signal + d.explore;
            if (total > 1e-6f)
            {
                d.attack  /= total; d.flee    /= total; d.seek    /= total;
                d.rest    /= total; d.signal  /= total; d.explore /= total;
            }
            return d;
        }

        // -------------------------------------------------------------------------
        // Portal affinity helper — score how well this spirit's spectral signature
        // matches a portal band. Used by SpiritLikenessController for awakening logic.
        // -------------------------------------------------------------------------
        public float PortalAffinityScore(string portalBandName)
        {
            if (_profile == null) return 0f;
            return _profile.spectralSignature.GetBand(portalBandName) * _coherence;
        }

        // -------------------------------------------------------------------------
        // Player resonance score — how strongly this spirit is responding to the player.
        // High when player drives match the spirit's archetype bias.
        // -------------------------------------------------------------------------
        public float PlayerResonanceScore(PlayerResonanceState playerState)
        {
            if (_profile == null || playerState == null) return 0f;
            var dw = _profile.driveWeights;

            return Mathf.Clamp01(
                dw.rest    * playerState.calm            +
                dw.seek    * playerState.joy             +
                dw.signal  * playerState.socialSync      +
                dw.explore * playerState.wonder          +
                dw.flee    * (1f - playerState.distortion) +
                dw.attack  * playerState.spinStability);
        }
    }
}
