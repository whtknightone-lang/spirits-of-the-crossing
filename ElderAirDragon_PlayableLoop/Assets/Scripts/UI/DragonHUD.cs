
using UnityEngine;
using ArtificialUniverse.Dragon;

namespace ArtificialUniverse.UI
{
    public class DragonHUD : MonoBehaviour
    {
        [SerializeField] private ElderAirDragonStats targetStats;

        private void Awake()
        {
            if (targetStats == null) targetStats = FindObjectOfType<ElderAirDragonStats>();
        }

        private void OnGUI()
        {
            if (targetStats == null) return;

            GUI.Label(new Rect(20, 20, 240, 24), $"Wind Charge: {targetStats.windCharge:0.00}");
            GUI.Label(new Rect(20, 44, 240, 24), $"Harmony: {targetStats.harmony:0.00}");
            GUI.Label(new Rect(20, 68, 240, 24), $"Integrity: {targetStats.integrity:0.00}");
            GUI.Label(new Rect(20, 92, 240, 24), $"Resonance: {targetStats.resonance:0.00}");
            GUI.Label(new Rect(20, 124, 480, 24), "WASD steer | Space ascend | Shift dive | Q gust | E spiral | R pulse | F meditate");
        }
    }
}
