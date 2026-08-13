using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class RespawnManager : NetworkBehaviour
{
    public static RespawnManager Instance;

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform respawnPoint;

    [SerializeField] private float respawnTime = 5f;

    private void Awake()
    {
        Instance = this;
    }

    public void RespawnPlayer(Health deadPlayer)
    {
        if (!IsServer)
            return;

        Vector3 deathPosition = deadPlayer.transform.position;

        Health nearestAlly = FindNearestAlly(deadPlayer); 
        
        ulong clientId = deadPlayer.NetworkObject.OwnerClientId;

        ClientRpcParams clientRpcParams =
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };

        if (nearestAlly != null)
        {
            Debug.Log(
                "Camera target: " +
                nearestAlly.NetworkObject.OwnerClientId
            );

            SetCameraTargetClientRpc(
                nearestAlly.NetworkObject.NetworkObjectId,
                clientRpcParams
            );
        }
        else
        {
            Debug.Log("No allies. Camera stays at death position.");

            SetCameraPositionClientRpc(
                deathPosition,
                clientRpcParams
            );
        }

        StartCoroutine(RespawnCoroutine(clientId, deadPlayer));
    }

    private Health FindNearestAlly(Health deadPlayer) 
    { 
        Team deadPlayerTeam = deadPlayer.GetComponent<Team>(); 
        
        Health[] players = FindObjectsByType<Health>(FindObjectsInactive.Exclude); 
        
        Health nearestAlly = null; 
        
        float nearestDistance = Mathf.Infinity; 
        
        foreach (Health player in players) 
        { 
            // Ignore the dead player itself 
            if (player == deadPlayer) 
                continue;

            if (!player.IsAlive()) 
                continue; 

            Team playerTeam = player.GetComponent<Team>(); 

            // Check if this is an ally 
            if (playerTeam.TeamId.Value != deadPlayerTeam.TeamId.Value) 
                continue; 

            float distance = Vector3.Distance( deadPlayer.transform.position, player.transform.position ); 

            if (distance < nearestDistance) 
            { 
                nearestDistance = distance; 
                nearestAlly = player; 
            } 
        } 

        return nearestAlly; 
    }

    
    [ClientRpc]
    private void SetCameraTargetClientRpc(
        ulong targetNetworkObjectId,
        ClientRpcParams clientRpcParams = default)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(targetNetworkObjectId, out NetworkObject target))
        {
            PlayerCamera.Instance.FollowPlayer(target.transform);
        }
    }

    [ClientRpc]
    private void SetCameraPositionClientRpc(
        Vector3 position,
        ClientRpcParams clientRpcParams = default)
    {
        PlayerCamera.Instance.MoveToPosition(position);
    }

    private IEnumerator RespawnCoroutine(
        ulong clientId,
        Health deadPlayer)
    {
        // Despawn the dead player object
        deadPlayer.NetworkObject.Despawn();

        Debug.Log("Player despawned. Respawn in " + respawnTime + " seconds.");

        yield return new WaitForSeconds(respawnTime);

        // Create a new player object at the respawn point
        GameObject newPlayer = Instantiate(
            playerPrefab,
            respawnPoint.position,
            respawnPoint.rotation
        );

        NetworkObject networkObject =
            newPlayer.GetComponent<NetworkObject>();

        // Spawn the new player object as a player object for the specified client
        networkObject.SpawnAsPlayerObject(clientId);

        SetCameraTargetClientRpc(
            networkObject.NetworkObjectId,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            }
        );

        Debug.Log("Player " + clientId + " respawned.");
    }
}

