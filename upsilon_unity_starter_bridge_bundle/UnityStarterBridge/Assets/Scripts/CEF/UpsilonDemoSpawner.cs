using UnityEngine;

namespace Upsilon.CEF
{
    /// <summary>
    /// Optional demo helper for quick scene bootstrapping.
    /// Create three prefabs and assign them.
    /// </summary>
    public class UpsilonDemoSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject aiPrefab;
        [SerializeField] private GameObject animalPrefab;

        private void Start()
        {
            if (playerPrefab != null) Instantiate(playerPrefab, new Vector3(0f, 1.2f, 0f), Quaternion.identity);
            if (aiPrefab != null) Instantiate(aiPrefab, new Vector3(1.5f, 1.1f, 1.5f), Quaternion.identity);
            if (animalPrefab != null) Instantiate(animalPrefab, new Vector3(-1.6f, 0.8f, 1.2f), Quaternion.identity);
        }
    }
}
