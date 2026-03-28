// SpiritsCrossing — MythInterpreter.cs
// Reads SessionResonanceResult and RealmOutcome events from UniverseStateManager
// and applies myth activation rules. Writes back into UniverseState.mythState.
//
// Rule philosophy: myths activate from observable player behaviour patterns,
// not arbitrary flags. Each rule maps a measurable threshold to a myth key
// and a source tag so the history is inspectable.

using UnityEngine;

namespace SpiritsCrossing
{
    public class MythInterpreter : MonoBehaviour
    {
        [Header("Ritual Activation Thresholds")]
        [Range(0f, 1f)] public float sourceThreshold  = 0.70f;
        [Range(0f, 1f)] public float forestThreshold  = 0.65f;
        [Range(0f, 1f)] public float stormThreshold   = 0.60f;
        [Range(0f, 1f)] public float elderThreshold   = 0.72f;
        [Range(0f, 1f)] public float ruinThreshold    = 0.55f;

        [Header("Realm Activation Thresholds")]
        [Range(0f, 1f)] public float skyMythOnFlow    = 0.70f;
        [Range(0f, 1f)] public float oceanMythOnCalm  = 0.65f;
        [Range(0f, 1f)] public float fireMythOnContrast = 0.65f;

        [Header("History Pattern — visit streak to trigger history myth")]
        public int visitStreakForHistoryMyth = 3;

        private void OnEnable()
        {
            if (UniverseStateManager.Instance == null) return;
            UniverseStateManager.Instance.OnSessionApplied      += OnSessionApplied;
            UniverseStateManager.Instance.OnRealmOutcomeApplied += OnRealmOutcomeApplied;
        }

        private void OnDisable()
        {
            if (UniverseStateManager.Instance == null) return;
            UniverseStateManager.Instance.OnSessionApplied      -= OnSessionApplied;
            UniverseStateManager.Instance.OnRealmOutcomeApplied -= OnRealmOutcomeApplied;
        }

        // -------------------------------------------------------------------------
        // Session rules (cave ritual → myth activation)
        // -------------------------------------------------------------------------
        private void OnSessionApplied(SessionResonanceResult result)
        {
            var myth  = UniverseStateManager.Instance.Current.mythState;
            var s     = result.resonanceSample;

            // Source alignment → "source" myth
            if (s.sourceAlignmentScore >= sourceThreshold)
                myth.Activate("source", "ritual", s.sourceAlignmentScore);

            // High flow + calm + low distortion → "forest" myth
            float forestSignal = (s.flowScore + s.calmScore) * 0.5f - s.distortionScore * 0.3f;
            if (forestSignal >= forestThreshold)
                myth.Activate("forest", "ritual", forestSignal);

            // High distortion → "storm" myth
            if (s.distortionScore >= stormThreshold)
                myth.Activate("storm", "ritual", s.distortionScore);

            // High stillness + source + wonder → "elder" myth
            float elderSignal = (s.stillnessScore + s.sourceAlignmentScore + s.wonderScore) / 3f;
            if (elderSignal >= elderThreshold)
                myth.Activate("elder", "ritual", elderSignal);

            // Wonder + high session count → ruin echo
            if (s.wonderScore >= ruinThreshold && result.chakraBandsCompleted >= 6)
                myth.Activate("ruin", "ritual", s.wonderScore);

            // Explicit myth triggers emitted by the session itself
            foreach (var key in result.mythTriggerKeys)
                myth.Activate(key, "ritual", 0.7f);

            // Decay other myths slightly at each session end
            myth.DecayAll(0.04f);

            // Unlock planet if affinity found
            if (!string.IsNullOrEmpty(result.currentAffinityPlanet) && result.portalUnlocked)
                UniverseStateManager.Instance.UnlockPlanet(result.currentAffinityPlanet);

            Debug.Log($"[MythInterpreter] Session processed. Active myths: {myth.activeMyths.Count}");
        }

        // -------------------------------------------------------------------------
        // Realm rules (realm visit → myth activation)
        // -------------------------------------------------------------------------
        private void OnRealmOutcomeApplied(RealmOutcome outcome)
        {
            var myth = UniverseStateManager.Instance.Current.mythState;

            // SkyRealm: high skyFlow activates sky myth
            if (outcome.realmId == "SkyRealm" && outcome.skyFlowFinal >= skyMythOnFlow)
                myth.Activate("sky", "realm", outcome.skyFlowFinal);

            // Fire: high contrast activates fire myth
            if (outcome.realmId == "FireRealm" && outcome.contrast >= fireMythOnContrast)
                myth.Activate("fire", "realm", outcome.contrast);

            // Ocean: high harmony activates ocean myth
            if (outcome.realmId == "OceanRealm" && outcome.harmonyFinal >= oceanMythOnCalm)
                myth.Activate("ocean", "realm", outcome.harmonyFinal);

            // Any realm completion + high celebration reinforces planet's myth
            if (outcome.realmCompleted && outcome.celebration >= 0.65f)
                myth.Activate(PlanetToMythKey(outcome.planetId), "realm", outcome.celebration);

            // Explicit myth triggers from the realm
            foreach (var key in outcome.mythTriggerKeys)
                myth.Activate(key, "realm", 0.65f);

            // History pattern: visit streak to same planet
            CheckVisitStreak(outcome.planetId, myth);

            Debug.Log($"[MythInterpreter] Realm outcome processed. Realm={outcome.realmId} Active myths={myth.activeMyths.Count}");
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------
        private void CheckVisitStreak(string planetId, MythState myth)
        {
            if (string.IsNullOrEmpty(planetId)) return;
            var planet = UniverseStateManager.Instance.GetPlanet(planetId);
            if (planet.visitCount >= visitStreakForHistoryMyth)
                myth.Activate("history_" + planetId.ToLower(), "history", Mathf.Clamp01(planet.visitCount * 0.1f));
        }

        private static string PlanetToMythKey(string planetId) => planetId?.ToLower() switch
        {
            "forestheart"   => "forest",
            "skyspiral"     => "sky",
            "sourceveil"    => "source",
            "waterflow"     => "ocean",
            "machineorder"  => "machine",
            "darkcontrast"  => "storm",
            _               => "unknown"
        };
    }
}
