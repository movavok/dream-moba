using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class RespawnManager : NetworkBehaviour
{
    public static RespawnManager Instance;

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform respawnPoint;

    [SerializeField] private float respawnTime = 5f;
    [SerializeField] private GameObject deathVisualPrefab;

    private readonly System.Collections.Generic.Dictionary<ulong, NetworkObject> deathVisuals =
        new();

    private readonly System.Collections.Generic.Dictionary<ulong, ulong> spectatingTargets =
        new();

    private void Awake()
    {
        Instance = this;
    }

    public void RespawnPlayer(Health deadPlayer)
    {
        if (!IsServer)
            return;

        Vector3 deathPosition = deadPlayer.transform.position;

        PlayerNetwork deadPlayerNetwork =
            deadPlayer.GetComponent<PlayerNetwork>();

        if (deadPlayerNetwork == null)
        {
            Debug.LogError("RespawnManager: PlayerNetwork not found!");
            return;
        }

        deadPlayerNetwork.PlayDeathAudio();

        HeroDefinition deadHero =
            deadPlayerNetwork.HeroData;

        GameObject deathVisual =
            Instantiate(
                deathVisualPrefab,
                deathPosition,
                Quaternion.identity
            );

        NetworkObject deathNetworkObject =
            deathVisual.GetComponent<NetworkObject>();

        DeathVisual deathVisualComponent =
            deathVisual.GetComponent<DeathVisual>();

        if (deathVisualComponent == null)
        {
            Debug.LogError(
                "DeathVisual prefab doesn't have DeathVisual component!"
            );

            Destroy(deathVisual);
            return;
        }

        deathNetworkObject.Spawn();

        deathVisualComponent.Initialize(deadHero);

        deathVisuals[deadPlayer.NetworkObject.OwnerClientId] =
            deathNetworkObject;

        Health nearestAlly = FindNearestAlly(deadPlayer); 
        
        ulong clientId = deadPlayer.NetworkObject.OwnerClientId;

        if (nearestAlly != null)
        {
            spectatingTargets[clientId] =
                nearestAlly.NetworkObject.NetworkObjectId;
        }
        else
        {
            spectatingTargets.Remove(clientId);
        }

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

        UpdateSpectators(deadPlayer);

        StartCoroutine(RespawnCoroutine(clientId, deadPlayer));
    }

    private void UpdateSpectators(Health deadPlayer)
    {
        ulong deadNetworkObjectId =
            deadPlayer.NetworkObject.NetworkObjectId;

        var spectatorsToUpdate =
            new System.Collections.Generic.List<ulong>();

        foreach (var pair in spectatingTargets)
        {
            if (pair.Key == deadPlayer.NetworkObject.OwnerClientId)
                continue;

            if (pair.Value == deadNetworkObjectId)
            {
                spectatorsToUpdate.Add(pair.Key);
            }
        }

        foreach (ulong spectatorClientId in spectatorsToUpdate)
        {
            Health newTarget = FindNearestAlly(deadPlayer);

            if (newTarget != null)
            {
                spectatingTargets[spectatorClientId] =
                    newTarget.NetworkObject.NetworkObjectId;

                SetCameraTargetClientRpc(
                    newTarget.NetworkObject.NetworkObjectId,
                    new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new[] { spectatorClientId }
                        }
                    }
                );

                Debug.Log(
                    $"Spectator {spectatorClientId} switched to " +
                    $"player {newTarget.NetworkObject.OwnerClientId}"
                );
            }
            else
            {
                spectatingTargets.Remove(spectatorClientId);

                Debug.Log(
                    $"Spectator {spectatorClientId}: " +
                    "no alive allies left."
                );
            }
        }
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

    public void PlayerDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        Debug.Log(
            $"RespawnManager: player {clientId} disconnected."
        );

        // Удаляем его spectating target
        spectatingTargets.Remove(clientId);

        // Удаляем death visual
        if (deathVisuals.TryGetValue(
            clientId,
            out NetworkObject deathVisual))
        {
            if (deathVisual != null &&
                deathVisual.IsSpawned)
            {
                deathVisual.Despawn();
            }

            deathVisuals.Remove(clientId);
        }
    }

    private IEnumerator RespawnCoroutine(
        ulong clientId,
        Health deadPlayer)
    {
        // Despawn the dead player object
        if (deadPlayer != null &&
            deadPlayer.NetworkObject != null &&
            deadPlayer.NetworkObject.IsSpawned)
        {
            deadPlayer.NetworkObject.Despawn();
        }

        Debug.Log(
            "Player despawned. Respawn in " +
            respawnTime +
            " seconds."
        );

        yield return new WaitForSeconds(respawnTime);

        // Игрок вышел во время смерти.
        // Не создаём ему нового PlayerObject.
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening ||
            !NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            Debug.Log(
                $"Respawn cancelled: client {clientId} is no longer connected."
            );

            if (deathVisuals.TryGetValue(
                clientId,
                out NetworkObject disconnectedDeathVisual))
            {
                if (disconnectedDeathVisual != null &&
                    disconnectedDeathVisual.IsSpawned)
                {
                    disconnectedDeathVisual.Despawn();
                }

                deathVisuals.Remove(clientId);
            }

            spectatingTargets.Remove(clientId);

            yield break;
        }

        // Удаляем death visual
        if (deathVisuals.TryGetValue(
            clientId,
            out NetworkObject deathVisual))
        {
            if (deathVisual != null &&
                deathVisual.IsSpawned)
            {
                deathVisual.Despawn();
            }

            deathVisuals.Remove(clientId);
        }

        if (!NetworkManager.Singleton.IsListening || !IsServer)
        {
            Debug.LogWarning(
                "RespawnManager: NetworkManager is no longer listening. " +
                "Respawn cancelled."
            );

            yield break;
        }

        // Create a new player object at the respawn point
        GameObject newPlayer = Instantiate(
            playerPrefab,
            respawnPoint.position,
            respawnPoint.rotation
        );

        NetworkObject networkObject =
            newPlayer.GetComponent<NetworkObject>();

        networkObject.SpawnAsPlayerObject(clientId);

        spectatingTargets.Remove(clientId);

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

        Debug.Log(
            "Player " + clientId + " respawned."
        );
    }
}

