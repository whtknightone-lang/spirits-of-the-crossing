// SpiritsCrossing — VRCosmicHapticsSystem.cs
// Haptic feedback for everything that grows, resonates, and becomes.
//
// The original VRHapticsController handles cave ritual, portal, spirit, and
// gesture haptics. This system extends into the living cosmos — every creature,
// every planet, every terrain, every dragon, every moment of growth is felt.
//
// DESIGN PRINCIPLE
//   Haptics are not notifications. They are the body's memory of resonance.
//   A companion bonding feels different from an elder dragon arriving.
//   A planet growing feels different from the cosmos synchronizing.
//   The player's hands learn the language of the cosmos through touch.
//
// EVENT SOURCES WIRED
//
//   Companion Spirit Animals
//     Bond tier change     → warmth pulse, intensity scales with tier
//     Fully bonded         → deep bilateral embrace (long sustained)
//     Intention change     → approaching=gentle pull, helping=warm hold,
//                            tricking=quick jab, departing=fading drift
//     Animal departs       → slow single-hand fade (the hand the animal was on)
//
//   Elder Spirit Animals / Elder Dragons
//     Elder dragon appears → massive slow bilateral rumble (the ground shakes)
//     Elder dragon departs → fading bilateral pulse (ancient wings receding)
//     Dragon sighting      → sharp bilateral pulse (awe)
//
//   Planets / Universe / Galaxy / Cosmos
//     Planet growth        → gentle bilateral pulse proportional to growth delta
//     Universe sync change → continuous subtle bilateral hum that tracks R_universe
//     Cosmos birth         → ascending 5-pulse bilateral cascade
//     Cosmos rebirth       → descending-then-ascending pulse (death and renewal)
//
//   Terrain Resonance
//     Terrain discovered   → terrain-specific single pulse
//     Entered region       → onset pulse matching terrain character
//     Exited region        → brief fade-out
//
//   Vibrational / Emotional Field
//     Dominant band change → side-alternating tap (band index determines side)
//     Resonance lock       → sustained bilateral resonance (deep alignment)
//     Full spectrum        → ascending bilateral shimmer (all bands alive)
//     Depth threshold      → deepening bilateral rumble
//     Personal best        → celebratory double-pulse
//
//   Martial World
//     Ball court victory   → rhythmic bilateral drumbeat (the game is won)
//     Dojo mastered        → sharp bilateral strike then calm hold
//     Krishna teaching     → deep sustained bilateral peace (the field speaks)
//     Maize Forge complete → ascending triple pulse (mud → wood → maize)
//     Mastery tier advance → single pulse, intensity scales with tier
//     Middleworld achieved → massive bilateral cascade (balance found)
//
//   NPC Evolution
//     Archetype shift      → brief bilateral shimmer (something changed nearby)
//
//   Resonance Memory
//     Source threshold      → deepening bilateral warmth (the Source opens)
//     Dominant element change → element-specific pulse pattern

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using SpiritsCrossing.Companions;
using SpiritsCrossing.DragonRealms;
using SpiritsCrossing.Cosmos;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.World;
using SpiritsCrossing.MartialWorld;
using SpiritsCrossing.Memory;

namespace SpiritsCrossing.VR
{
    public class VRCosmicHapticsSystem : MonoBehaviour
    {
        public static VRCosmicHapticsSystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // ---------------------------------------------------------------------
        // Inspector
        // ---------------------------------------------------------------------
        [Header("Global")]
        [Range(0f, 1f)] public float globalIntensity = 0.85f;

        [Header("Companion")]
        [Range(0f, 1f)] public float companionBondPulse   = 0.40f;
        [Range(0f, 1f)] public float companionFullBond     = 0.70f;
        public float companionPulseDuration = 0.20f;
        public float companionEmbraceLength = 1.5f;

        [Header("Elder Dragon")]
        [Range(0f, 1f)] public float dragonAppearIntensity = 0.90f;
        public float dragonRumbleDuration  = 2.5f;

        [Header("Cosmos")]
        [Range(0f, 1f)] public float planetGrowthPulse   = 0.25f;
        [Range(0f, 1f)] public float cosmosEventIntensity = 0.80f;

        [Header("Terrain")]
        [Range(0f, 1f)] public float terrainDiscoverPulse = 0.35f;
        [Range(0f, 1f)] public float terrainEnterPulse    = 0.30f;

        [Header("Vibrational")]
        [Range(0f, 1f)] public float bandChangeTap      = 0.35f;
        [Range(0f, 1f)] public float resonanceLockPulse = 0.55f;
        [Range(0f, 1f)] public float fullSpectrumPulse  = 0.60f;

        [Header("Martial")]
        [Range(0f, 1f)] public float martialVictoryPulse   = 0.70f;
        [Range(0f, 1f)] public float krishnaTeachingPulse   = 0.50f;
        [Range(0f, 1f)] public float middleworldIntensity    = 0.85f;

        [Header("Debug")]
        public bool logHapticEvents = false;

        // ---------------------------------------------------------------------
        // Internal
        // ---------------------------------------------------------------------
        private InputDevice _left;
        private InputDevice _right;
        private readonly List<InputDevice> _buf = new();
        private float _universeHumPhase;
        private float _prevR;

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------
        private void Start()
        {
            RefreshDevices();
            StartCoroutine(DeferredWire());
        }

        private void OnDestroy() => Unwire();

        private void Update()
        {
            if (!_left.isValid || !_right.isValid) RefreshDevices();
            TickUniverseHum();
        }

        private void RefreshDevices()
        {
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, _buf);
            if (_buf.Count > 0) _left = _buf[0];
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, _buf);
            if (_buf.Count > 0) _right = _buf[0];
        }

        // Deferred wiring — wait for singletons to initialize
        private IEnumerator DeferredWire()
        {
            yield return null; yield return null; // 2 frames
            Wire();
        }

        // =================================================================
        // EVENT WIRING
        // =================================================================
        private void Wire()
        {
            // --- Companion Spirit Animals ---
            if (CompanionBondSystem.Instance != null)
            {
                CompanionBondSystem.Instance.OnBondTierChanged     += OnCompanionBondTier;
                CompanionBondSystem.Instance.OnCompanionFullyBonded += OnCompanionFullBond;
            }
            if (CompanionIntentionSystem.Instance != null)
            {
                CompanionIntentionSystem.Instance.OnIntentionChanged += OnCompanionIntention;
                CompanionIntentionSystem.Instance.OnAnimalDeparts    += OnCompanionDeparts;
            }

            // --- Elder Dragons ---
            if (ElderDragonCosmicSpawner.Instance != null)
            {
                ElderDragonCosmicSpawner.Instance.OnElderDragonAppeared += OnDragonAppeared;
                ElderDragonCosmicSpawner.Instance.OnElderDragonDeparted += OnDragonDeparted;
            }

            // --- Cosmos / Planets / Universe ---
            if (CosmosGenerationSystem.Instance != null)
            {
                CosmosGenerationSystem.Instance.OnPlanetGrowthUpdated           += OnPlanetGrowth;
                CosmosGenerationSystem.Instance.OnUniverseSynchronizationChanged += OnUniverseSync;
                CosmosGenerationSystem.Instance.OnCosmosBirth                    += OnCosmosBirth;
                CosmosGenerationSystem.Instance.OnCosmosRebirth                  += OnCosmosRebirth;
            }

            // --- Terrain ---
            if (TerrainResonanceSystem.Instance != null)
            {
                TerrainResonanceSystem.Instance.OnTerrainDiscovered   += OnTerrainDiscovered;
                TerrainResonanceSystem.Instance.OnPlayerEnteredRegion += OnTerrainEntered;
                TerrainResonanceSystem.Instance.OnPlayerExitedRegion  += OnTerrainExited;
            }

            // --- Vibrational / Emotional ---
            if (VibrationalResonanceSystem.Instance != null)
            {
                VibrationalResonanceSystem.Instance.OnPlayerDominantBandChanged += OnDominantBandChanged;
                VibrationalResonanceSystem.Instance.OnResonanceLock             += OnResonanceLock;
                VibrationalResonanceSystem.Instance.OnResonanceRelease          += OnResonanceRelease;
            }
            if (EmotionalFieldPropagation.Instance != null)
            {
                EmotionalFieldPropagation.Instance.OnFullSpectrumReached += OnFullSpectrum;
                EmotionalFieldPropagation.Instance.OnDepthThreshold      += OnEmotionalDepth;
            }

            // --- Martial World ---
            if (MartialWorldSpawner.Instance != null)
            {
                MartialWorldSpawner.Instance.OnBallCourtVictory    += OnBallCourtVictory;
                MartialWorldSpawner.Instance.OnDojoMastered        += OnDojoMastered;
                MartialWorldSpawner.Instance.OnKrishnaTeaching     += OnKrishnaTeaching;
                MartialWorldSpawner.Instance.OnForgeComplete       += OnForgeComplete;
                MartialWorldSpawner.Instance.OnMasteryTierAdvanced += OnMasteryTierAdvanced;
            }

            // --- Resonance Memory (personal growth) ---
            if (ResonanceMemorySystem.Instance != null)
            {
                ResonanceMemorySystem.Instance.OnPersonalBestExceeded  += OnPersonalBest;
                ResonanceMemorySystem.Instance.OnSourceThresholdReached += OnSourceThreshold;
            }

            // --- World / Ruins ---
            if (WorldSystem.Instance != null)
                WorldSystem.Instance.OnRuinDiscovered += OnRuinDiscovered;

            Log("[VRCosmicHapticsSystem] Wired to all cosmic event sources.");
        }

        private void Unwire()
        {
            if (CompanionBondSystem.Instance != null)
            {
                CompanionBondSystem.Instance.OnBondTierChanged     -= OnCompanionBondTier;
                CompanionBondSystem.Instance.OnCompanionFullyBonded -= OnCompanionFullBond;
            }
            if (CompanionIntentionSystem.Instance != null)
            {
                CompanionIntentionSystem.Instance.OnIntentionChanged -= OnCompanionIntention;
                CompanionIntentionSystem.Instance.OnAnimalDeparts    -= OnCompanionDeparts;
            }
            if (ElderDragonCosmicSpawner.Instance != null)
            {
                ElderDragonCosmicSpawner.Instance.OnElderDragonAppeared -= OnDragonAppeared;
                ElderDragonCosmicSpawner.Instance.OnElderDragonDeparted -= OnDragonDeparted;
            }
            if (CosmosGenerationSystem.Instance != null)
            {
                CosmosGenerationSystem.Instance.OnPlanetGrowthUpdated           -= OnPlanetGrowth;
                CosmosGenerationSystem.Instance.OnUniverseSynchronizationChanged -= OnUniverseSync;
                CosmosGenerationSystem.Instance.OnCosmosBirth                    -= OnCosmosBirth;
                CosmosGenerationSystem.Instance.OnCosmosRebirth                  -= OnCosmosRebirth;
            }
            if (TerrainResonanceSystem.Instance != null)
            {
                TerrainResonanceSystem.Instance.OnTerrainDiscovered   -= OnTerrainDiscovered;
                TerrainResonanceSystem.Instance.OnPlayerEnteredRegion -= OnTerrainEntered;
                TerrainResonanceSystem.Instance.OnPlayerExitedRegion  -= OnTerrainExited;
            }
            if (VibrationalResonanceSystem.Instance != null)
            {
                VibrationalResonanceSystem.Instance.OnPlayerDominantBandChanged -= OnDominantBandChanged;
                VibrationalResonanceSystem.Instance.OnResonanceLock             -= OnResonanceLock;
                VibrationalResonanceSystem.Instance.OnResonanceRelease          -= OnResonanceRelease;
            }
            if (EmotionalFieldPropagation.Instance != null)
            {
                EmotionalFieldPropagation.Instance.OnFullSpectrumReached -= OnFullSpectrum;
                EmotionalFieldPropagation.Instance.OnDepthThreshold      -= OnEmotionalDepth;
            }
            if (MartialWorldSpawner.Instance != null)
            {
                MartialWorldSpawner.Instance.OnBallCourtVictory    -= OnBallCourtVictory;
                MartialWorldSpawner.Instance.OnDojoMastered        -= OnDojoMastered;
                MartialWorldSpawner.Instance.OnKrishnaTeaching     -= OnKrishnaTeaching;
                MartialWorldSpawner.Instance.OnForgeComplete       -= OnForgeComplete;
                MartialWorldSpawner.Instance.OnMasteryTierAdvanced -= OnMasteryTierAdvanced;
            }
            if (ResonanceMemorySystem.Instance != null)
            {
                ResonanceMemorySystem.Instance.OnPersonalBestExceeded  -= OnPersonalBest;
                ResonanceMemorySystem.Instance.OnSourceThresholdReached -= OnSourceThreshold;
            }
            if (WorldSystem.Instance != null)
                WorldSystem.Instance.OnRuinDiscovered -= OnRuinDiscovered;
        }

        // =================================================================
        // COMPANION SPIRIT ANIMALS
        // =================================================================

        private void OnCompanionBondTier(string animalId, CompanionBondTier tier)
        {
            // Warmth pulse that grows with bond depth
            float tierScale = tier switch
            {
                CompanionBondTier.Distant   => 0.2f,
                CompanionBondTier.Curious   => 0.4f,
                CompanionBondTier.Bonded    => 0.7f,
                CompanionBondTier.Companion => 1.0f,
                _                           => 0.3f,
            };
            float i = companionBondPulse * tierScale * globalIntensity;
            StartCoroutine(Bilateral(i, companionPulseDuration));
            Log($"Companion bond: {animalId} → {tier}");
        }

        private void OnCompanionFullBond(string animalId)
        {
            // Deep bilateral embrace — the companion has chosen you
            StartCoroutine(Sustained(companionFullBond * globalIntensity, companionEmbraceLength));
            Log($"Companion fully bonded: {animalId}");
        }

        private void OnCompanionIntention(string animalId, CompanionIntention intention)
        {
            float i = globalIntensity;
            switch (intention)
            {
                case CompanionIntention.Approaching:
                    // Gentle pull toward the approaching hand
                    StartCoroutine(SingleFade(_left, 0.25f * i, 0.4f));
                    break;
                case CompanionIntention.Helping:
                    // Warm sustained hold — the companion is here for you
                    StartCoroutine(Sustained(0.30f * i, 0.8f));
                    break;
                case CompanionIntention.Tricking:
                    // Quick unexpected jab — the trickster strikes
                    StartCoroutine(Single(_right, 0.55f * i, 0.06f));
                    break;
                case CompanionIntention.Playing:
                    // Playful alternating taps
                    StartCoroutine(AlternatingTaps(0.30f * i, 0.08f, 3, 0.15f));
                    break;
                case CompanionIntention.Learning:
                    // Subtle bilateral hum — watching, absorbing
                    StartCoroutine(Bilateral(0.15f * i, 0.3f));
                    break;
                case CompanionIntention.Riding:
                    // Peaceful gentle bilateral warmth
                    StartCoroutine(Bilateral(0.20f * i, 0.5f));
                    break;
            }
        }

        private void OnCompanionDeparts(string animalId)
        {
            // Slow fade — the companion drifts away
            StartCoroutine(SingleFade(_left, 0.30f * globalIntensity, 0.8f));
        }

        // =================================================================
        // ELDER SPIRIT ANIMALS / ELDER DRAGONS
        // =================================================================

        private void OnDragonAppeared(ElderDragonProfile dragon, string planetId)
        {
            // Massive slow bilateral rumble — the ground itself acknowledges
            float i = dragonAppearIntensity * globalIntensity;

            // Element-specific character
            switch (dragon.element)
            {
                case "Fire":
                    StartCoroutine(DragonFireArrival(i));
                    break;
                case "Water":
                    StartCoroutine(DragonWaterArrival(i));
                    break;
                case "Earth":
                    StartCoroutine(DragonEarthArrival(i));
                    break;
                case "Air":
                    StartCoroutine(DragonAirArrival(i));
                    break;
                default: // Source
                    StartCoroutine(DragonSourceArrival(i));
                    break;
            }
            Log($"Elder dragon appeared: {dragon.displayName} on {planetId}");
        }

        // Fire dragon: sharp escalating pulses like a forge igniting
        private IEnumerator DragonFireArrival(float i)
        {
            for (int p = 0; p < 5; p++)
            {
                float ramp = 0.4f + (p / 4f) * 0.6f;
                Pulse(_left,  i * ramp, 0.10f);
                Pulse(_right, i * ramp, 0.10f);
                yield return new WaitForSeconds(0.25f);
            }
            yield return Sustained(i * 0.6f, dragonRumbleDuration * 0.5f);
        }

        // Water dragon: tidal wave wash, left to right, building
        private IEnumerator DragonWaterArrival(float i)
        {
            float end = Time.time + dragonRumbleDuration;
            float phase = 0f;
            while (Time.time < end)
            {
                phase += Time.deltaTime;
                float wave = (Mathf.Sin(phase * 1.2f) + 1f) * 0.5f;
                Pulse(_left,  i * wave * 0.7f, 0.08f);
                yield return new WaitForSeconds(0.10f);
                Pulse(_right, i * wave * 0.6f, 0.08f);
                yield return new WaitForSeconds(0.12f);
            }
        }

        // Earth dragon: deep sustained low rumble — the planet shifts
        private IEnumerator DragonEarthArrival(float i)
        {
            yield return Sustained(i * 0.5f, dragonRumbleDuration);
            // Final deep bilateral thud
            yield return Bilateral(i * 0.8f, 0.25f);
        }

        // Air dragon: rapid ascending shimmer — lighter and faster
        private IEnumerator DragonAirArrival(float i)
        {
            float end = Time.time + dragonRumbleDuration;
            float t = 0f;
            while (Time.time < end)
            {
                t += Time.deltaTime;
                float ramp = Mathf.Clamp01(t / dragonRumbleDuration);
                float intensity = i * (0.5f - ramp * 0.25f);
                float gap = Mathf.Lerp(0.4f, 0.08f, ramp);
                Pulse(_left, intensity, 0.04f);
                Pulse(_right, intensity, 0.04f);
                yield return new WaitForSeconds(gap);
            }
        }

        // Source dragon: deep bilateral warmth that slowly builds to full
        private IEnumerator DragonSourceArrival(float i)
        {
            float end = Time.time + dragonRumbleDuration * 1.5f;
            float t = 0f;
            while (Time.time < end)
            {
                t += Time.deltaTime;
                float ramp = Mathf.Clamp01(t / (dragonRumbleDuration * 1.5f));
                float intensity = i * ramp * 0.7f;
                Pulse(_left,  intensity, 0.10f);
                Pulse(_right, intensity, 0.10f);
                yield return new WaitForSeconds(0.15f);
            }
        }

        private void OnDragonDeparted(ElderDragonProfile dragon, string planetId)
        {
            StartCoroutine(SingleFade(_left,  0.45f * globalIntensity, 1.2f));
            StartCoroutine(SingleFade(_right, 0.40f * globalIntensity, 1.4f));
            Log($"Elder dragon departed: {dragon.displayName}");
        }

        // =================================================================
        // PLANETS / UNIVERSE / COSMOS
        // =================================================================

        private void OnPlanetGrowth(string planetId, float newGrowth)
        {
            // Gentle bilateral pulse proportional to growth amount
            float i = planetGrowthPulse * Mathf.Clamp01(newGrowth) * globalIntensity;
            if (i > 0.05f) StartCoroutine(Bilateral(i, 0.15f));
        }

        private void OnUniverseSync(float rUniverse)
        {
            _prevR = rUniverse; // used by TickUniverseHum
        }

        // Continuous subtle bilateral hum that tracks R_universe
        // The more synchronized the cosmos, the more the player feels it
        private void TickUniverseHum()
        {
            if (_prevR < 0.1f) return; // below perceptible threshold

            _universeHumPhase += Time.deltaTime;
            if (_universeHumPhase < 2.0f) return; // hum every 2 seconds
            _universeHumPhase = 0f;

            float i = _prevR * 0.15f * globalIntensity; // very subtle
            Pulse(_left,  i, 0.04f);
            Pulse(_right, i, 0.04f);
        }

        private void OnCosmosBirth()
        {
            // Ascending 5-pulse bilateral cascade — a universe is born
            StartCoroutine(CosmosBirthCascade());
            Log("COSMOS BIRTH — ascending cascade");
        }

        private IEnumerator CosmosBirthCascade()
        {
            float[] intensities = { 0.3f, 0.45f, 0.6f, 0.75f, 0.9f };
            foreach (float v in intensities)
            {
                float i = v * cosmosEventIntensity * globalIntensity;
                Pulse(_left,  i, 0.20f);
                Pulse(_right, i, 0.20f);
                yield return new WaitForSeconds(0.30f);
            }
        }

        private void OnCosmosRebirth()
        {
            // Descending then ascending — death and renewal
            StartCoroutine(CosmosRebirthSequence());
            Log("COSMOS REBIRTH — death and renewal");
        }

        private IEnumerator CosmosRebirthSequence()
        {
            // Descend
            float[] down = { 0.8f, 0.6f, 0.4f, 0.2f };
            foreach (float v in down)
            {
                float i = v * cosmosEventIntensity * globalIntensity;
                Pulse(_left, i, 0.15f); Pulse(_right, i, 0.15f);
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(0.5f); // silence — the void
            // Ascend
            float[] up = { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f };
            foreach (float v in up)
            {
                float i = v * cosmosEventIntensity * globalIntensity;
                Pulse(_left, i, 0.20f); Pulse(_right, i, 0.20f);
                yield return new WaitForSeconds(0.25f);
            }
        }

        // =================================================================
        // TERRAIN RESONANCE
        // =================================================================

        private void OnTerrainDiscovered(TerrainRegion region, float pull)
        {
            // Terrain-character pulse
            float i = terrainDiscoverPulse * Mathf.Clamp01(pull * 1.5f) * globalIntensity;
            switch (region.terrainType)
            {
                case TerrainType.Cave: case TerrainType.Fissure: case TerrainType.Ravine:
                    // Deep low pulse — going down into something
                    StartCoroutine(Sustained(i, 0.5f));
                    break;
                case TerrainType.Mountain: case TerrainType.Crag: case TerrainType.Caldera:
                    // Sharp upward pulse — height, exposure
                    StartCoroutine(Bilateral(i * 1.2f, 0.12f));
                    break;
                case TerrainType.River: case TerrainType.Lake: case TerrainType.InlandOcean:
                    // Flowing wash — left then right
                    Pulse(_left, i, 0.15f);
                    StartCoroutine(DelayedPulse(_right, i * 0.8f, 0.15f, 0.1f));
                    break;
                default:
                    // Hill, Valley, Plateau, Glacier — gentle bilateral
                    StartCoroutine(Bilateral(i, 0.20f));
                    break;
            }
            Log($"Terrain discovered: {region.regionName} ({region.terrainType})");
        }

        private void OnTerrainEntered(TerrainRegion region)
        {
            float i = terrainEnterPulse * globalIntensity;
            StartCoroutine(Bilateral(i, 0.15f));
        }

        private void OnTerrainExited(TerrainRegion region)
        {
            StartCoroutine(SingleFade(_left, 0.15f * globalIntensity, 0.3f));
        }

        // =================================================================
        // VIBRATIONAL / EMOTIONAL FIELD
        // =================================================================

        private static readonly string[] BAND_ORDER =
            { "red","orange","yellow","green","blue","indigo","violet" };

        private void OnDominantBandChanged(string bandName)
        {
            int idx = Array.IndexOf(BAND_ORDER, bandName);
            bool isLeft = (idx % 2 == 0);
            InputDevice dev = isLeft ? _left : _right;
            StartCoroutine(Single(dev, bandChangeTap * globalIntensity, 0.08f));
        }

        private void OnResonanceLock(string animalId)
        {
            // Deep alignment — sustained bilateral resonance
            StartCoroutine(Sustained(resonanceLockPulse * globalIntensity, 1.0f));
            Log($"Resonance lock: {animalId}");
        }

        private void OnResonanceRelease(string animalId)
        {
            StartCoroutine(SingleFade(_right, 0.20f * globalIntensity, 0.4f));
        }

        private void OnFullSpectrum(float coherence)
        {
            // Ascending bilateral shimmer — all bands alive at once
            StartCoroutine(FullSpectrumShimmer(coherence));
            Log($"Full spectrum: coherence={coherence:F2}");
        }

        private IEnumerator FullSpectrumShimmer(float coherence)
        {
            float i = fullSpectrumPulse * Mathf.Clamp01(coherence) * globalIntensity;
            for (int p = 0; p < 7; p++) // one pulse per band
            {
                float ramp = (p + 1) / 7f;
                Pulse(_left,  i * ramp, 0.05f);
                Pulse(_right, i * ramp, 0.05f);
                yield return new WaitForSeconds(0.10f);
            }
        }

        private void OnEmotionalDepth(int thresholdIndex, float depth)
        {
            // Deepening bilateral rumble — each threshold deeper than the last
            float i = (0.30f + thresholdIndex * 0.15f) * globalIntensity;
            StartCoroutine(Sustained(Mathf.Clamp01(i), 0.4f + thresholdIndex * 0.2f));
        }

        // =================================================================
        // MARTIAL WORLD
        // =================================================================

        private void OnBallCourtVictory(BallCourtRecord court)
        {
            // Rhythmic bilateral drumbeat — the Hero Twins' victory
            StartCoroutine(MartialDrumbeat(martialVictoryPulse * globalIntensity, 6, 0.20f));
            Log($"Ball court victory: {court.courtName}");
        }

        private IEnumerator MartialDrumbeat(float i, int beats, float gap)
        {
            bool left = true;
            for (int b = 0; b < beats; b++)
            {
                Pulse(left ? _left : _right, i, 0.08f);
                left = !left;
                yield return new WaitForSeconds(gap);
            }
            // Final bilateral thud
            yield return Bilateral(i * 0.9f, 0.15f);
        }

        private void OnDojoMastered(DojoRecord dojo)
        {
            // Sharp bilateral strike then calm sustained hold
            StartCoroutine(StrikeThenCalm(martialVictoryPulse * globalIntensity));
            Log($"Dojo mastered: {dojo.dojoName}");
        }

        private IEnumerator StrikeThenCalm(float i)
        {
            Pulse(_left, i, 0.06f); Pulse(_right, i, 0.06f);
            yield return new WaitForSeconds(0.15f);
            yield return Sustained(i * 0.35f, 0.8f);
        }

        private void OnKrishnaTeaching(KrishnaFieldRecord field)
        {
            // Deep sustained bilateral peace — the field speaks
            StartCoroutine(Sustained(krishnaTeachingPulse * globalIntensity, 2.0f));
            Log($"Krishna teaching at {field.fieldName}");
        }

        private void OnForgeComplete(MaizeForgeRecord forge)
        {
            // Ascending triple pulse: mud → wood → maize
            StartCoroutine(ForgeAscension());
            Log($"Maize Forge complete: {forge.forgeName}");
        }

        private IEnumerator ForgeAscension()
        {
            float i = martialVictoryPulse * globalIntensity;
            // Mud — low, heavy
            yield return Sustained(i * 0.4f, 0.4f);
            yield return new WaitForSeconds(0.2f);
            // Wood — sharper, stronger
            yield return Bilateral(i * 0.65f, 0.20f);
            yield return new WaitForSeconds(0.2f);
            // Maize — bright, complete, alive
            yield return Bilateral(i * 0.9f, 0.25f);
            yield return new WaitForSeconds(0.1f);
            yield return Bilateral(i, 0.15f);
        }

        private void OnMasteryTierAdvanced(int tier, MasteryRealm realm)
        {
            float tierScale = Mathf.Clamp01(tier / 23f);

            if (tier == 10) // Middleworld
            {
                StartCoroutine(MiddleworldCascade());
                return;
            }

            float i = (0.25f + tierScale * 0.45f) * globalIntensity;
            StartCoroutine(Bilateral(i, 0.18f));
        }

        private IEnumerator MiddleworldCascade()
        {
            // Balance found — descend through Xibalba then ascend to Heaven
            float i = middleworldIntensity * globalIntensity;
            // Descend (9 quick pulses, fading)
            for (int p = 9; p >= 1; p--)
            {
                Pulse(_left, i * (p / 9f) * 0.5f, 0.05f);
                Pulse(_right, i * (p / 9f) * 0.5f, 0.05f);
                yield return new WaitForSeconds(0.12f);
            }
            // Silence — the middle
            yield return new WaitForSeconds(0.6f);
            // Bilateral deep hold — balance
            yield return Sustained(i * 0.7f, 1.5f);
            // Ascend (13 pulses, rising)
            for (int p = 1; p <= 13; p++)
            {
                float ramp = p / 13f;
                Pulse(_left, i * ramp * 0.6f, 0.05f);
                Pulse(_right, i * ramp * 0.6f, 0.05f);
                yield return new WaitForSeconds(0.10f);
            }
            // Final strong bilateral
            yield return Bilateral(i, 0.25f);
            Log("MIDDLEWORLD ACHIEVED — balance cascade");
        }

        // =================================================================
        // RESONANCE MEMORY / PERSONAL GROWTH
        // =================================================================

        private void OnPersonalBest(string dimension, float newBest)
        {
            // Celebratory double-pulse
            float i = 0.55f * globalIntensity;
            StartCoroutine(DoublePulse(i, 0.15f, 0.10f));
            Log($"Personal best: {dimension}={newBest:F2}");
        }

        private void OnSourceThreshold(int thresholdIndex, float level)
        {
            // Deepening bilateral warmth — the Source opens further
            float i = (0.40f + thresholdIndex * 0.10f) * globalIntensity;
            StartCoroutine(Sustained(Mathf.Clamp01(i), 1.0f + thresholdIndex * 0.5f));
            Log($"Source threshold {thresholdIndex}: level={level:F2}");
        }

        // =================================================================
        // WORLD / RUINS
        // =================================================================

        private void OnRuinDiscovered(RuinRecord ruin)
        {
            bool isAncient = ruin.Layer == WorldLayer.Ancient;
            float i = (isAncient ? 0.50f : 0.35f) * globalIntensity;
            if (isAncient)
                StartCoroutine(Sustained(i, 0.6f)); // deep ancient rumble
            else
                StartCoroutine(Bilateral(i, 0.20f));
        }

        // =================================================================
        // HAPTIC PRIMITIVES
        // =================================================================

        private IEnumerator Bilateral(float intensity, float duration)
        {
            Pulse(_left, intensity, duration);
            Pulse(_right, intensity, duration);
            yield return new WaitForSeconds(duration);
        }

        private IEnumerator Sustained(float intensity, float duration)
        {
            float end = Time.time + duration;
            while (Time.time < end)
            {
                Pulse(_left,  intensity, 0.05f);
                Pulse(_right, intensity, 0.05f);
                yield return new WaitForSeconds(0.06f);
            }
        }

        private IEnumerator Single(InputDevice dev, float intensity, float duration)
        {
            Pulse(dev, intensity, duration);
            yield return new WaitForSeconds(duration);
        }

        private IEnumerator SingleFade(InputDevice dev, float intensity, float duration)
        {
            float start = Time.time;
            while (Time.time - start < duration)
            {
                float t = 1f - (Time.time - start) / duration;
                Pulse(dev, intensity * t, 0.04f);
                yield return new WaitForSeconds(0.05f);
            }
        }

        private IEnumerator DoublePulse(float intensity, float duration, float gap)
        {
            yield return Bilateral(intensity, duration);
            yield return new WaitForSeconds(gap);
            yield return Bilateral(intensity * 0.75f, duration);
        }

        private IEnumerator AlternatingTaps(float intensity, float duration, int count, float gap)
        {
            bool left = true;
            for (int i = 0; i < count; i++)
            {
                Pulse(left ? _left : _right, intensity, duration);
                left = !left;
                yield return new WaitForSeconds(gap);
            }
        }

        private IEnumerator DelayedPulse(InputDevice dev, float intensity, float duration, float delay)
        {
            yield return new WaitForSeconds(delay);
            Pulse(dev, intensity, duration);
        }

        // =================================================================
        // CORE SEND
        // =================================================================

        private static void Pulse(InputDevice device, float amplitude, float duration)
        {
            if (!device.isValid) return;
            device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
        }

        private void Log(string msg)
        {
            if (logHapticEvents) Debug.Log($"[VRCosmicHapticsSystem] {msg}");
        }
    }
}
