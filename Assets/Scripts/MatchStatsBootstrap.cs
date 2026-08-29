using Unity.Netcode;
using UnityEngine;

public class MatchNetworkBootstrap : NetworkBehaviour
{
    [SerializeField] private GameObject matchStatsManagerPrefab;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
            return;

        if (MatchStatsManager.Instance != null)
            return;

        GameObject manager = Instantiate(
            matchStatsManagerPrefab
        );

        NetworkObject networkObject =
            manager.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError(
                "MatchStatsManager prefab has no NetworkObject!"
            );
            Destroy(manager);
            return;
        }

        networkObject.Spawn();

        Debug.Log(
            "[MatchNetworkBootstrap] MatchStatsManager spawned."
        );
    }
}