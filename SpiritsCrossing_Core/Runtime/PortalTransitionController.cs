// SpiritsCrossing — PortalTransitionController.cs
// Handles the physical transition between the cave/lobby scene and realm scenes.
//
// TRANSITION SEQUENCE
//   1. Portal commits (PortalRevealSystem.OnPortalCommitted fires)
//   2. Brief resonance pulse — the portal's reveal flares to max, audio swells
//   3. Fade out (configurable duration)
//   4. Unity scene load (async, by realm ID)
//   5. On scene load: find IRealmController in new scene, call BeginRealm()
//   6. Fade in
//
// SCENE NAME REGISTRY
//   Maps realm IDs to Unity scene names. Extend as scenes are added.
//   Keep in sync with PortalRealmRegistry in PortalDecision.cs.
//
// ON REALM COMPLETE
//   IRealmController.OnRealmComplete fires → RecordOutcome → fade back to cave.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpiritsCrossing.DragonRealms;

namespace SpiritsCrossing.Runtime
{
    public class PortalTransitionController : MonoBehaviour
    {
        public static PortalTransitionController Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Scene Names — must match Build Settings")]
        public string caveSceneName   = "CaveScene";
        public string forestSceneName = "ForestScene";
        public string oceanSceneName  = "OceanScene";
        public string fireSceneName   = "FireScene";
        public string skySceneName    = "SkyScene";
        public string sourceSceneName  = "SourceScene";
        public string martialSceneName = "MartialScene";

        [Header("Evolved Realm Scenes")]
        public string deepForestSceneName     = "DeepForestScene";
        public string abyssalOceanSceneName   = "AbyssalOceanScene";
        public string innerForgeSceneName     = "InnerForgeScene";
        public string skyCathedralSceneName   = "SkyCathedralScene";
        public string sourceWellspringSceneName = "SourceWellspringScene";

        [Header("Elder Dragon Realm Scenes")]
        public string elderSkySceneName    = "ElderSkyScene";
        public string elderForestSceneName = "ElderForestScene";
        public string elderFireSceneName   = "ElderFireScene";
        public string elderOceanSceneName  = "ElderOceanScene";
        public string elderSourceSceneName = "ElderSourceScene";

        [Header("Transition Timing")]
        public float pulseDuration  = 1.2f;  // resonance flare before fade
        public float fadeOutDuration = 0.8f;
        public float fadeInDuration  = 1.0f;

        [Header("Debug")]
        public bool skipSceneLoad = false;  // test transitions without scene load

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        // Fires at the start of each transition phase for audio/visual systems to subscribe
        public event Action<string> OnTransitionBegin;    // realmId
        public event Action<float>  OnFadeProgress;       // 0–1 during fade
        public event Action<string> OnRealmSceneReady;    // realmId (after load, before BeginRealm)
        public event Action         OnReturnToCave;
        public event Action<string> OnWorldTravelBegin;   // targetPlanetId
        public event Action<string> OnAncientRuinEntered; // ruinId
        public event Action<string> OnElderDragonSummoned; // dragonElement

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------
        public bool IsTransitioning { get; private set; }
        private IRealmController _activeRealm;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void OnEnable()
        {
            if (PortalRevealSystem.Instance != null)
                PortalRevealSystem.Instance.OnPortalCommitted += HandlePortalCommitted;
            if (PortalSiteActivationSystem.Instance != null)
                PortalSiteActivationSystem.Instance.OnPortalSiteCommitted += HandleSiteCommitted;
        }

        private void OnDisable()
        {
            if (PortalRevealSystem.Instance != null)
                PortalRevealSystem.Instance.OnPortalCommitted -= HandlePortalCommitted;
            if (PortalSiteActivationSystem.Instance != null)
                PortalSiteActivationSystem.Instance.OnPortalSiteCommitted -= HandleSiteCommitted;
        }

        // -------------------------------------------------------------------------
        // Portal commit handler
        // -------------------------------------------------------------------------
        private void HandlePortalCommitted(PortalState portal, PortalDecision decision)
        {
            if (IsTransitioning) return;
            StartCoroutine(TransitionToRealm(decision));
        }

        private void HandleSiteCommitted(string siteId, PortalDecision decision)
        {
            if (IsTransitioning) return;

            switch (decision.destinationType)
            {
                case PortalDestinationType.Realm:
                    StartCoroutine(TransitionToRealm(decision));
                    break;
                case PortalDestinationType.WorldTravel:
                    StartCoroutine(TransitionToWorld(decision, siteId));
                    break;
                case PortalDestinationType.AncientRuin:
                    StartCoroutine(TransitionToRuin(decision, siteId));
                    break;
                case PortalDestinationType.ElderDragonEncounter:
                    StartCoroutine(TransitionToElderDragon(decision, siteId));
                    break;
            }
        }

        // -------------------------------------------------------------------------
        // Transition coroutine
        // -------------------------------------------------------------------------
        private IEnumerator TransitionToRealm(PortalDecision decision)
        {
            IsTransitioning = true;
            string realmId  = decision.targetRealmId;
            string sceneName = SceneNameForRealm(realmId);

            OnTransitionBegin?.Invoke(realmId);
            Debug.Log($"[PortalTransitionController] Transition begin → {realmId} (scene: {sceneName})");

            // Phase 1: resonance pulse
            yield return new WaitForSeconds(pulseDuration);

            // Phase 2: fade out
            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                OnFadeProgress?.Invoke(Mathf.Clamp01(t / fadeOutDuration));
                yield return null;
            }
            OnFadeProgress?.Invoke(1f);

            // Phase 3: load scene
            if (!skipSceneLoad && !string.IsNullOrEmpty(sceneName))
            {
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (op != null) yield return op;
            }
            else
            {
                yield return null; // single frame in test mode
            }

            // Phase 4: find realm controller and begin
            _activeRealm = FindObjectOfType<MonoBehaviour>() as IRealmController;
            // Note: FindObjectOfType can't search by interface directly in older Unity versions.
            // In production: use a scene bootstrap component that registers the IRealmController.
            if (_activeRealm == null)
            {
                // Fallback: search all MonoBehaviours
                foreach (var mb in FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb is IRealmController rc) { _activeRealm = rc; break; }
                }
            }

            if (_activeRealm != null)
            {
                _activeRealm.OnRealmComplete += HandleRealmComplete;
                OnRealmSceneReady?.Invoke(realmId);

                var snapshot  = UniverseStateManager.Instance?.Current.persistentResonance
                                ?? new PlayerResponseSample();
                var planetState = UniverseStateManager.Instance?.Current
                                  .GetOrCreatePlanet(_activeRealm.PlanetId);
                _activeRealm.BeginRealm(snapshot, planetState);
                Debug.Log($"[PortalTransitionController] BeginRealm: {_activeRealm.RealmId}");
            }
            else
            {
                Debug.LogWarning($"[PortalTransitionController] No IRealmController found in scene {sceneName}.");
            }

            // Phase 5: fade in
            t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                OnFadeProgress?.Invoke(1f - Mathf.Clamp01(t / fadeInDuration));
                yield return null;
            }
            OnFadeProgress?.Invoke(0f);

            IsTransitioning = false;
        }

        // -------------------------------------------------------------------------
        // Realm complete → record outcome → return to cave
        // -------------------------------------------------------------------------
        private void HandleRealmComplete(RealmOutcome outcome)
        {
            if (_activeRealm != null)
                _activeRealm.OnRealmComplete -= HandleRealmComplete;

            UniverseStateManager.Instance?.Current.RecordRealmOutcome(outcome);
            UniverseStateManager.Instance?.Save();

            Debug.Log($"[PortalTransitionController] Realm complete: {outcome.realmId} " +
                      $"celebration={outcome.celebration:F2} myths={string.Join(",", outcome.mythTriggerKeys)}");

            StartCoroutine(ReturnToCave());
        }

        private IEnumerator ReturnToCave()
        {
            IsTransitioning = true;

            // Fade out
            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                OnFadeProgress?.Invoke(Mathf.Clamp01(t / fadeOutDuration));
                yield return null;
            }

            if (!skipSceneLoad)
            {
                var op = SceneManager.LoadSceneAsync(caveSceneName, LoadSceneMode.Single);
                if (op != null) yield return op;
            }

            OnReturnToCave?.Invoke();
            PortalRevealSystem.Instance?.ResetAllPortals();

            // Fade in
            t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                OnFadeProgress?.Invoke(1f - Mathf.Clamp01(t / fadeInDuration));
                yield return null;
            }
            OnFadeProgress?.Invoke(0f);

            IsTransitioning = false;
            _activeRealm = null;
            Debug.Log("[PortalTransitionController] Returned to cave.");
        }

        // -------------------------------------------------------------------------
        // World travel transition
        // -------------------------------------------------------------------------
        private IEnumerator TransitionToWorld(PortalDecision decision, string siteId)
        {
            IsTransitioning = true;
            string targetPlanet = decision.targetPlanetId;

            OnWorldTravelBegin?.Invoke(targetPlanet);
            Debug.Log($"[PortalTransitionController] World travel begin → planet {targetPlanet}");

            yield return new WaitForSeconds(pulseDuration);

            // Fade out
            yield return RunFadeOut();

            // Load the target planet's scene
            string sceneName = SceneNameForRealm(decision.targetRealmId);
            if (!skipSceneLoad && !string.IsNullOrEmpty(sceneName))
            {
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (op != null) yield return op;
            }

            // Record the world travel
            var universe = UniverseStateManager.Instance?.Current;
            universe?.RecordWorldTravel(targetPlanet);
            UniverseStateManager.Instance?.Save();

            // Find realm controller in new scene
            yield return BindRealmController(decision.targetRealmId);

            // Fade in
            yield return RunFadeIn();

            // Reset the site so it can be used again on return
            PortalSiteActivationSystem.Instance?.ResetSiteCommit(siteId);
            IsTransitioning = false;
        }

        // -------------------------------------------------------------------------
        // Ancient ruin transition (additive scene load)
        // -------------------------------------------------------------------------
        private IEnumerator TransitionToRuin(PortalDecision decision, string siteId)
        {
            IsTransitioning = true;
            string ruinId = decision.targetRuinId;

            OnAncientRuinEntered?.Invoke(ruinId);
            Debug.Log($"[PortalTransitionController] Entering ancient ruin: {ruinId}");

            yield return new WaitForSeconds(pulseDuration * 0.6f); // shorter pulse for ruins

            // Fade out
            yield return RunFadeOut();

            // Load ruin sub-scene additively within the current world
            string ruinSceneName = $"Ruin_{ruinId}";
            if (!skipSceneLoad)
            {
                var op = SceneManager.LoadSceneAsync(ruinSceneName, LoadSceneMode.Additive);
                if (op != null) yield return op;
            }

            // Find and initialize AncientRuinController
            var ruinController = UnityEngine.Object.FindObjectOfType<AncientRuinController>();
            if (ruinController != null)
            {
                ruinController.OnRuinComplete += HandleRuinComplete;
                ruinController.BeginRuin(ruinId);
            }

            // Fade in
            yield return RunFadeIn();

            IsTransitioning = false;
        }

        private void HandleRuinComplete(string ruinId)
        {
            var ruinController = UnityEngine.Object.FindObjectOfType<AncientRuinController>();
            if (ruinController != null)
                ruinController.OnRuinComplete -= HandleRuinComplete;

            // Unload the ruin sub-scene
            string ruinSceneName = $"Ruin_{ruinId}";
            if (!skipSceneLoad)
                SceneManager.UnloadSceneAsync(ruinSceneName);

            Debug.Log($"[PortalTransitionController] Exited ancient ruin: {ruinId}");
        }

        // -------------------------------------------------------------------------
        // Elder dragon encounter (no scene load — summon at convergence)
        // -------------------------------------------------------------------------
        private IEnumerator TransitionToElderDragon(PortalDecision decision, string siteId)
        {
            IsTransitioning = true;
            string dragonElement = decision.targetDragonElement;

            OnElderDragonSummoned?.Invoke(dragonElement);
            Debug.Log($"[PortalTransitionController] Elder dragon convergence: {dragonElement}");

            // Extended pulse — this is a mythic moment
            yield return new WaitForSeconds(pulseDuration * 2f);

            // Trigger the elder dragon spawn at the convergence site
            var spawner = ElderDragonCosmicSpawner.Instance;
            if (spawner != null)
            {
                // Find the matching dragon by element
                ElderDragonProfile targetDragon = null;
                if (spawner.Dragons != null)
                {
                    foreach (var d in spawner.Dragons)
                    {
                        if (d.element == dragonElement) { targetDragon = d; break; }
                    }
                }

                if (targetDragon != null)
                {
                    // Get the site to know which planet we're on
                    var siteDef = PortalSiteActivationSystem.Instance?.GetSiteDefinition(siteId);
                    string planetId = siteDef?.planetId ?? "Unknown";

                    // Activate elder myth strongly
                    var myth = UniverseStateManager.Instance?.Current?.mythState;
                    myth?.Activate(dragonElement.ToLower(), "elder_convergence", 0.90f);
                    myth?.Activate("elder", "elder_convergence", 0.85f);

                    Debug.Log($"[PortalTransitionController] {targetDragon.displayName} " +
                              $"summoned at convergence on {planetId}.");
                }
            }

            // Reset the convergence site
            PortalSiteActivationSystem.Instance?.ResetSiteCommit(siteId);
            IsTransitioning = false;
        }

        // -------------------------------------------------------------------------
        // Shared fade helpers
        // -------------------------------------------------------------------------
        private IEnumerator RunFadeOut()
        {
            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                OnFadeProgress?.Invoke(Mathf.Clamp01(t / fadeOutDuration));
                yield return null;
            }
            OnFadeProgress?.Invoke(1f);
        }

        private IEnumerator RunFadeIn()
        {
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                OnFadeProgress?.Invoke(1f - Mathf.Clamp01(t / fadeInDuration));
                yield return null;
            }
            OnFadeProgress?.Invoke(0f);
        }

        /// <summary>Find an IRealmController in the current scene and call BeginRealm.</summary>
        private IEnumerator BindRealmController(string realmId)
        {
            yield return null; // wait one frame for scene objects to initialize

            _activeRealm = FindObjectOfType<MonoBehaviour>() as IRealmController;
            if (_activeRealm == null)
            {
                foreach (var mb in FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb is IRealmController rc) { _activeRealm = rc; break; }
                }
            }

            if (_activeRealm != null)
            {
                _activeRealm.OnRealmComplete += HandleRealmComplete;
                OnRealmSceneReady?.Invoke(realmId);

                var snapshot = UniverseStateManager.Instance?.Current.persistentResonance
                               ?? new PlayerResponseSample();
                var planetState = UniverseStateManager.Instance?.Current
                                  .GetOrCreatePlanet(_activeRealm.PlanetId);
                _activeRealm.BeginRealm(snapshot, planetState);
                Debug.Log($"[PortalTransitionController] BeginRealm: {_activeRealm.RealmId}");
            }
            else
            {
                Debug.LogWarning($"[PortalTransitionController] No IRealmController found for {realmId}.");
            }
        }

        // -------------------------------------------------------------------------
        // Scene name registry
        // -------------------------------------------------------------------------
        private string SceneNameForRealm(string realmId) => realmId switch
        {
            // Base realms
            "ForestRealm" => forestSceneName,
            "OceanRealm"  => oceanSceneName,
            "FireRealm"   => fireSceneName,
            "SkyRealm"    => skySceneName,
            "SourceRealm"  => sourceSceneName,
            "MartialRealm" => martialSceneName,

            // Evolved realms
            "DeepForestRealm"       => deepForestSceneName,
            "AbyssalOceanRealm"     => abyssalOceanSceneName,
            "InnerForgeRealm"       => innerForgeSceneName,
            "SkyCathedralRealm"     => skyCathedralSceneName,
            "SourceWellspringRealm" => sourceWellspringSceneName,

            // Elder dragon realms
            "ElderSkyRealm"    => elderSkySceneName,
            "ElderForestRealm" => elderForestSceneName,
            "ElderFireRealm"   => elderFireSceneName,
            "ElderOceanRealm"  => elderOceanSceneName,
            "ElderSourceRealm" => elderSourceSceneName,

            _ => string.Empty,
        };
    }
}
