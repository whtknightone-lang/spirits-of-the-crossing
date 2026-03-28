// SpiritsCrossing — SpiritProfileLoader.cs
// Singleton MonoBehaviour that loads spirit_profiles.json from StreamingAssets
// and makes SpiritDriveProfile objects available to SpiritBrainController instances.
//
// Setup:
//   1. Copy SpiritsCrossing_Core/SpiritAI/spirit_profiles.json into your Unity
//      project's Assets/StreamingAssets/ folder (or a subfolder you configure below).
//   2. Add SpiritProfileLoader to a manager GameObject in the Bootstrap scene.
//      It will persist across scene loads automatically.
//   3. SpiritBrainController looks up SpiritProfileLoader.Instance on Start().

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace SpiritsCrossing.SpiritAI
{
    public class SpiritProfileLoader : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------
        public static SpiritProfileLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Config
        // -------------------------------------------------------------------------
        [Tooltip("Path relative to StreamingAssets, e.g. 'spirit_profiles.json'")]
        public string jsonFileName = "spirit_profiles.json";

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------
        public bool IsLoaded { get; private set; }
        public SpiritProfileCollection Profiles { get; private set; }

        private readonly Dictionary<string, SpiritDriveProfile>       _spiritIndex  = new();
        private readonly Dictionary<string, PlanetEnvironmentProfile> _planetIndex  = new();

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return LoadProfiles();
        }

        // -------------------------------------------------------------------------
        // Loading
        // -------------------------------------------------------------------------
        private IEnumerator LoadProfiles()
        {
            string path = Path.Combine(Application.streamingAssetsPath, jsonFileName);

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android StreamingAssets requires UnityWebRequest
            using var req = UnityWebRequest.Get(path);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SpiritProfileLoader] Failed to load {path}: {req.error}");
                yield break;
            }
            ParseJson(req.downloadHandler.text);
#else
            // All other platforms can read directly
            yield return null; // one frame to let scene initialise
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SpiritProfileLoader] spirit_profiles.json not found at {path}. " +
                                 "Copy SpiritsCrossing_Core/SpiritAI/spirit_profiles.json into " +
                                 "Assets/StreamingAssets/.");
                yield break;
            }
            ParseJson(File.ReadAllText(path));
#endif
        }

        private void ParseJson(string json)
        {
            try
            {
                Profiles = JsonUtility.FromJson<SpiritProfileCollection>(json);
                if (Profiles == null)
                {
                    Debug.LogError("[SpiritProfileLoader] Failed to parse spirit_profiles.json.");
                    return;
                }

                _spiritIndex.Clear();
                _planetIndex.Clear();

                foreach (var s in Profiles.spirits)
                {
                    if (!string.IsNullOrEmpty(s.archetypeId))
                        _spiritIndex[s.archetypeId] = s;
                }
                foreach (var p in Profiles.planets)
                {
                    if (!string.IsNullOrEmpty(p.planetId))
                        _planetIndex[p.planetId] = p;
                }

                IsLoaded = true;
                Debug.Log($"[SpiritProfileLoader] Loaded {_spiritIndex.Count} spirit archetypes " +
                          $"and {_planetIndex.Count} planet profiles.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpiritProfileLoader] JSON parse error: {e.Message}");
            }
        }

        // -------------------------------------------------------------------------
        // Accessors
        // -------------------------------------------------------------------------

        /// <summary>Get a spirit drive profile by archetype ID. Returns null if not found.</summary>
        public SpiritDriveProfile GetProfile(string archetypeId)
        {
            if (!IsLoaded) return null;
            _spiritIndex.TryGetValue(archetypeId, out var profile);
            return profile;
        }

        /// <summary>Get a planet environment profile by planet ID. Returns null if not found.</summary>
        public PlanetEnvironmentProfile GetPlanetProfile(string planetId)
        {
            if (!IsLoaded) return null;
            _planetIndex.TryGetValue(planetId, out var profile);
            return profile;
        }

        /// <summary>
        /// Return a hazard modifier (0–1) for a given planet that scales
        /// environmental intensity — consumed by realm audio/visual systems.
        /// </summary>
        public float GetPlanetHazard(string planetId)
        {
            var profile = GetPlanetProfile(planetId);
            return profile?.worldBias.hazard ?? 0.4f;
        }

        /// <summary>
        /// Return the preferred spirit archetypes for a given planet.
        /// Used by CaveSessionController to decide which spirits awaken most readily.
        /// </summary>
        public List<string> GetPreferredArchetypes(string planetId)
        {
            var profile = GetPlanetProfile(planetId);
            return profile?.preferredArchetypes ?? new List<string>();
        }

        /// <summary>
        /// Score how well a player response sample aligns with the preferred
        /// archetypes of a target planet. Higher = stronger planet affinity.
        /// </summary>
        public float ScorePlayerForPlanet(string planetId, PlayerResponseSample sample)
        {
            var preferred = GetPreferredArchetypes(planetId);
            if (preferred.Count == 0 || sample == null) return 0f;

            float total = 0f;
            int   count = 0;

            foreach (var archetypeId in preferred)
            {
                var profile = GetProfile(archetypeId);
                if (profile == null) continue;
                var dw = profile.driveWeights;

                float score =
                    dw.rest    * sample.stillnessScore      +
                    dw.seek    * sample.flowScore           +
                    dw.signal  * sample.pairSyncScore       +
                    dw.explore * sample.wonderScore         +
                    dw.flee    * (1f - sample.distortionScore) +
                    dw.attack  * sample.spinScore;

                total += score;
                count++;
            }

            return count > 0 ? Mathf.Clamp01(total / count) : 0f;
        }
    }
}
