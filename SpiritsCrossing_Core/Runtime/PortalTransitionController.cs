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
        public string sourceSceneName = "SourceScene";

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
        }

        private void OnDisable()
        {
            if (PortalRevealSystem.Instance != null)
                PortalRevealSystem.Instance.OnPortalCommitted -= HandlePortalCommitted;
        }

        // -------------------------------------------------------------------------
        // Portal commit handler
        // -------------------------------------------------------------------------
        private void HandlePortalCommitted(PortalState portal, PortalDecision decision)
        {
            if (IsTransitioning) return;
            StartCoroutine(TransitionToRealm(decision));
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
        // Scene name registry
        // -------------------------------------------------------------------------
        private string SceneNameForRealm(string realmId) => realmId switch
        {
            "ForestRealm" => forestSceneName,
            "OceanRealm"  => oceanSceneName,
            "FireRealm"   => fireSceneName,
            "SkyRealm"    => skySceneName,
            "SourceRealm" => sourceSceneName,
            _             => string.Empty,
        };
    }
}
