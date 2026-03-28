using UnityEngine;

namespace V243.SandstoneCave
{
    public class DemoBootstrapper : MonoBehaviour
    {
        public CaveSessionController sessionController;
        public PlanetAffinityInterpreter affinityInterpreter;
        public PlayerResponseTracker responseTracker;

        private void Start()
        {
            if (affinityInterpreter != null && (affinityInterpreter.planetProfiles == null || affinityInterpreter.planetProfiles.Count == 0))
            {
                affinityInterpreter.LoadDefaultProfiles();
            }

            if (responseTracker != null)
            {
                responseTracker.ResetTracking();
            }

            if (sessionController != null && !sessionController.sessionRunning)
            {
                sessionController.StartSession();
            }
        }

        private void OnGUI()
        {
            if (responseTracker == null || affinityInterpreter == null)
            {
                return;
            }

            GUI.Box(new Rect(16, 16, 320, 220), "Sandstone Cave Demo Debug");
            GUI.Label(new Rect(28, 46, 290, 22), "1: breath  2: flow  3: spin  4: pair");
            GUI.Label(new Rect(28, 68, 290, 22), "Q: calm  W: joy  E: wonder  R: distortion  T: source");

            CavePlayerResonanceState s = responseTracker.LiveState;
            GUI.Label(new Rect(28, 100, 280, 22), $"Breath {s.breathCoherence:0.00}  Flow {s.movementFlow:0.00}");
            GUI.Label(new Rect(28, 122, 280, 22), $"Spin {s.spinStability:0.00}  Pair {s.socialSync:0.00}");
            GUI.Label(new Rect(28, 144, 280, 22), $"Calm {s.calm:0.00}  Joy {s.joy:0.00}  Wonder {s.wonder:0.00}");
            GUI.Label(new Rect(28, 166, 280, 22), $"Distortion {s.distortion:0.00}  Source {s.sourceAlignment:0.00}");
            GUI.Label(new Rect(28, 192, 290, 22), $"Current Planet: {affinityInterpreter.currentAffinityPlanet}");
            GUI.Label(new Rect(28, 214, 290, 22), $"Achievable Planet: {affinityInterpreter.achievableAffinityPlanet}");
        }
    }
}
