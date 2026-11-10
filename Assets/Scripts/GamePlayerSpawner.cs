using UnityEngine;
using Unity.Netcode;

public class GamePlayerSpawner : NetworkBehaviour
{
    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject trainingDummyPrefab;
    [SerializeField] private Transform trainingDummySpawnPoint;
    [SerializeField] private Vector2 trainingDummyOffset = new Vector2(1.5f, 0f);

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    private bool playersSpawned = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Spawn players only on the server
        if (!IsServer)
            return;

        SpawnAllPlayers();
    }

    private void SpawnAllPlayers()
    {
        if (playersSpawned)
            return;

        playersSpawned = true;

        if (playerPrefab == null)
        {
            Debug.LogError("GamePlayerSpawner: Player Prefab is not assigned!");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("GamePlayerSpawner: No spawn points assigned!");
            return;
        }

        int spawnIndex = 0;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // Just in case, don't create a second Player
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId,
                out NetworkClient client))
            {
                if (client.PlayerObject != null)
                {
                    Debug.Log(
                        $"Player for client {clientId} already exists. Skipping."
                    );

                    continue;
                }
            }

            Transform spawnPoint =
                spawnPoints[spawnIndex % spawnPoints.Length];

            GameObject player = Instantiate(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            NetworkObject networkObject =
                player.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogError(
                    "GamePlayerSpawner: Player Prefab has no NetworkObject!"
                );

                Destroy(player);
                continue;
            }

            networkObject.SpawnAsPlayerObject(clientId);

            Debug.Log(
                $"Spawned player for client {clientId} at {spawnPoint.position}"
            );

            spawnIndex++;
        }

        SpawnTrainingDummy();
    }

    private void SpawnTrainingDummy()
    {
        if (trainingDummyPrefab == null)
            return;

        if (trainingDummySpawnPoint == null &&
            (spawnPoints == null || spawnPoints.Length == 0))
            return;

        Vector3 dummyPosition;

        if (trainingDummySpawnPoint != null)
        {
            dummyPosition = trainingDummySpawnPoint.position;
        }
        else
        {
            dummyPosition =
                spawnPoints[0].position +
                (Vector3)trainingDummyOffset;
        }

        GameObject dummy = Instantiate(
            trainingDummyPrefab,
            dummyPosition,
            Quaternion.identity
        );

        NetworkObject networkObject = dummy.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError(
                "GamePlayerSpawner: Training Dummy Prefab has no NetworkObject!"
            );
            Destroy(dummy);
            return;
        }

        // Spawn without a client owner so the dummy is a shared server object.
        networkObject.Spawn();
    }
}