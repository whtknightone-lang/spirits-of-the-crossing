// SpiritsCrossing — UniverseStateManager.cs
// Persistent-state singleton. Owns the UniverseState container and all save/load
// operations. Every other system reads or writes through this manager; no scene
// directly modifies long-term cosmos state.
//
// Wiring (in GameBootstrapper):
//   1. UniverseStateManager.Instance.Load()
//   2. Subscribe CaveSessionController.OnSessionComplete -> ApplySessionResult
//   3. Subscribe IRealmController.OnRealmComplete       -> ApplyRealmOutcome
//   4. MythInterpreter reads Current and updates mythState after each event.

using System;
using System.IO;
using UnityEngine;

namespace SpiritsCrossing
{
    public class UniverseStateManager : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------
        public static UniverseStateManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------
        public UniverseState Current { get; private set; } = new UniverseState();

        // Events broadcast to any listener (CosmosMapDirector, UI, etc.)
        public event Action<SessionResonanceResult> OnSessionApplied;
        public event Action<PortalDecision>         OnPortalDecisionApplied;
        public event Action<RealmOutcome>           OnRealmOutcomeApplied;
        public event Action<UniverseState>          OnStateSaved;

        // -------------------------------------------------------------------------
        // Apply layer outputs
        // -------------------------------------------------------------------------

        /// <summary>Called by CaveSessionController.OnSessionComplete.</summary>
        public void ApplySessionResult(SessionResonanceResult result)
        {
            if (result == null) return;
            Current.RecordSession(result);
            OnSessionApplied?.Invoke(result);
            AutoSave();
        }

        /// <summary>Called when the player commits a portal choice.</summary>
        public void ApplyPortalDecision(PortalDecision decision)
        {
            if (decision == null) return;
            Current.lastPortalDecision = decision;
            OnPortalDecisionApplied?.Invoke(decision);
            AutoSave();
        }

        /// <summary>Called by IRealmController.OnRealmComplete.</summary>
        public void ApplyRealmOutcome(RealmOutcome outcome)
        {
            if (outcome == null) return;
            Current.RecordRealmOutcome(outcome);
            OnRealmOutcomeApplied?.Invoke(outcome);
            AutoSave();
        }

        // -------------------------------------------------------------------------
        // Planet helpers (used by CosmosMapDirector, PlanetNodeController)
        // -------------------------------------------------------------------------
        public PlanetState GetPlanet(string planetId) => Current.GetOrCreatePlanet(planetId);

        public void UnlockPlanet(string planetId)
        {
            Current.GetOrCreatePlanet(planetId).unlocked = true;
            AutoSave();
        }

        // -------------------------------------------------------------------------
        // Save / Load  (Application.persistentDataPath/spirits_universe.json)
        // -------------------------------------------------------------------------
        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "spirits_universe.json");

        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(Current, prettyPrint: true);
                File.WriteAllText(SavePath, json);
                Debug.Log($"[UniverseStateManager] Saved → {SavePath}");
                OnStateSaved?.Invoke(Current);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UniverseStateManager] Save failed: {e.Message}");
            }
        }

        public void Load()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("[UniverseStateManager] No save file found. Starting fresh.");
                Current = new UniverseState();
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                Current = JsonUtility.FromJson<UniverseState>(json) ?? new UniverseState();
                Debug.Log($"[UniverseStateManager] Loaded session #{Current.totalSessionCount} from {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UniverseStateManager] Load failed: {e.Message}. Starting fresh.");
                Current = new UniverseState();
            }
        }

        public void DeleteSave()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            Current = new UniverseState();
            Debug.Log("[UniverseStateManager] Save deleted.");
        }

        private void AutoSave() => Save();

        private void OnApplicationQuit() => Save();
        private void OnApplicationPause(bool paused) { if (paused) Save(); }
    }
}
