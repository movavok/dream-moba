using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GamePlayerSpawner : NetworkBehaviour
{
    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject trainingDummyPrefab;
    [SerializeField] private bool spawnTrainingDummy;
    [SerializeField] private Transform trainingDummySpawnPoint;
    [SerializeField] private Vector2 trainingDummyOffset = new Vector2(1.5f, 0f);

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform[] blueTeamSpawnPoints = new Transform[3];
    [SerializeField] private Transform[] redTeamSpawnPoints = new Transform[3];

    private bool playersSpawned = false;
    private readonly Dictionary<ulong, int> assignedTeamSlots = new();

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

        if (playerPrefab == null)
        {
            Debug.LogError("GamePlayerSpawner: Player Prefab is not assigned!");
            return;
        }

        TeamManager teamManager = GetComponent<TeamManager>();
        if (teamManager != null &&
            teamManager.AssignmentMode == TeamAssignmentMode.Alternate &&
            (!HasThreePoints(blueTeamSpawnPoints) ||
             !HasThreePoints(redTeamSpawnPoints)))
        {
            Debug.LogError(
                "GamePlayerSpawner: Assign three Blue Team and three Red Team " +
                "Spawn Points in the Inspector."
            );
            return;
        }

        playersSpawned = true;
        var clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        clientIds.Sort();

        foreach (ulong clientId in clientIds)
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

            short teamId = teamManager != null
                ? teamManager.GetTeamId(clientId) : (short)0;
            Transform spawnPoint = GetSpawnPoint(clientId, teamId);
            if (spawnPoint == null)
            {
                Debug.LogError($"GamePlayerSpawner: No spawn point for client {clientId}.");
                continue;
            }

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
        }

        if (spawnTrainingDummy)
            SpawnTrainingDummy();
    }

    public Transform GetSpawnPoint(ulong clientId, short teamId)
    {
        TeamManager teamManager = GetComponent<TeamManager>();
        var clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        clientIds.Sort();

        if (teamManager != null &&
            teamManager.AssignmentMode == TeamAssignmentMode.Alternate &&
            (teamId == 1 || teamId == 2))
        {
            Transform[] teamPoints = teamId == 1
                ? blueTeamSpawnPoints : redTeamSpawnPoints;

            if (!assignedTeamSlots.TryGetValue(clientId, out int teammateIndex))
            {
                teammateIndex = 0;
                foreach (ulong otherId in clientIds)
                {
                    if (otherId >= clientId)
                        break;

                    if (teamManager.GetTeamId(otherId) == teamId)
                        teammateIndex++;
                }

                assignedTeamSlots[clientId] = teammateIndex;
            }

            if (teamPoints != null && teammateIndex < teamPoints.Length &&
                teamPoints[teammateIndex] != null)
            {
                Transform preferred = teamPoints[teammateIndex];
                if (!IsOccupied(preferred, clientId))
                    return preferred;

                foreach (Transform point in teamPoints)
                    if (point != null && !IsOccupied(point, clientId))
                        return point;

                return preferred;
            }

            Debug.LogError($"GamePlayerSpawner: Missing spawn point for team {teamId}.");
            return null;
        }

        int index = clientIds.IndexOf(clientId);
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[Mathf.Max(0, index) % spawnPoints.Length];
            if (point != null)
                return point;
        }

        // Other team modes have no dedicated house yet. Any assigned house point
        // is sufficient until those modes receive their own spawn rules.
        Transform[] housePoints = teamId == 2
            ? redTeamSpawnPoints : blueTeamSpawnPoints;
        Transform assigned = FirstAssignedPoint(housePoints);
        if (assigned != null)
            return assigned;

        housePoints = teamId == 2 ? blueTeamSpawnPoints : redTeamSpawnPoints;
        return FirstAssignedPoint(housePoints);
    }

    private static Transform FirstAssignedPoint(Transform[] points)
    {
        if (points == null)
            return null;

        foreach (Transform point in points)
            if (point != null)
                return point;

        return null;
    }

    private static bool HasThreePoints(Transform[] points)
    {
        if (points == null || points.Length < 3)
            return false;

        for (int i = 0; i < 3; i++)
            if (points[i] == null)
                return false;

        return true;
    }

    private bool IsOccupied(Transform point, ulong clientId)
    {
        foreach (NetworkObject player in NetworkManager.SpawnManager.SpawnedObjects.Values)
            if (player.IsPlayerObject && player.OwnerClientId != clientId &&
                Vector2.Distance(player.transform.position, point.position) < 0.75f)
                return true;

        return false;
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
