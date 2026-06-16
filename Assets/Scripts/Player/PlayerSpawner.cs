using Unity.Cinemachine;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject knightPrefab;
    [SerializeField] private GameObject magePrefab;
    [SerializeField] private GameObject archerPrefab;

    [SerializeField] private Transform spawnPoint;

    private void Start()
    {
        SpawnPlayer();
    }

    private void SpawnPlayer()
    {
        GameObject prefab = null;

        switch (WorldState.PlayerClass)
        {
            case "Knight":
                prefab = knightPrefab;
                break;

            case "Mage":
                prefab = magePrefab;
                break;

            case "Archer":
                prefab = archerPrefab;
                break;
        }

        GameObject player =
            Instantiate(
                prefab,
                spawnPoint.position,
                Quaternion.identity
            );

        CinemachineCamera cam =
            FindFirstObjectByType<CinemachineCamera>();

        if (cam != null)
        {
            cam.Follow = player.transform;
        }
    }
}