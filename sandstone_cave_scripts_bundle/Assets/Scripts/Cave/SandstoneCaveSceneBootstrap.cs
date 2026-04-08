using UnityEngine;

namespace Upsilon.Cave
{
    public class SandstoneCaveSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private CaveResonanceDisk resonanceDisk;
        [SerializeField] private GameObject cefStateBusPrefab;

        private void Awake()
        {
            if (Upsilon.CEF.CEFStateBus.Instance == null && cefStateBusPrefab != null)
            {
                Instantiate(cefStateBusPrefab);
            }
        }

        private void Start()
        {
            if (resonanceDisk != null && player != null)
            {
                resonanceDisk.SetPlayer(player);
            }
        }
    }
}
