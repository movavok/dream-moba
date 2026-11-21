using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class RespawnManager : NetworkBehaviour
{
    public static RespawnManager Instance;

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform respawnPoint;

    [SerializeField] private float respawnTime = 5f;
    [SerializeField] private GameObject deathVisualPrefab;

    [Header("Respawn Reward")]
    [SerializeField] private ItemDefinition respawnItem;
    [SerializeField, Min(1)] private int respawnItemCount = 1;
    [SerializeField] private ContainerInventory[] teamBackpacks;

    private readonly System.Collections.Generic.Dictionary<ulong, NetworkObject> deathVisuals =
        new();

    private readonly System.Collections.Generic.Dictionary<ulong, ulong> spectatingTargets =
        new();
    private int cameraRequestId;
    private int grayscaleRequestId;

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

        Team deadPlayerTeam =
            deadPlayerNetwork.GetComponent<Team>();

        if (deadPlayerTeam == null)
        {
            Debug.LogError(
                "RespawnManager: Team not found on dead player!"
            );

            deathNetworkObject.Despawn();
            return;
        }

        deathVisualComponent.Initialize(
            deadHero,
            deadPlayerTeam.TeamId.Value
        );

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

        SetDeathGrayscaleClientRpc(true, clientRpcParams);

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
        int requestId = ++cameraRequestId;
        StartCoroutine(FollowCameraTargetWhenReady(targetNetworkObjectId, requestId));
    }

    private IEnumerator FollowCameraTargetWhenReady(ulong targetId, int requestId)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (requestId == cameraRequestId &&
               Time.realtimeSinceStartup < deadline &&
               NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsListening)
        {
            if (PlayerNetwork.LocalPlayer != null &&
                PlayerNetwork.LocalPlayer.IsSpawned &&
                PlayerNetwork.LocalPlayer.NetworkObjectId != targetId &&
                PlayerNetwork.LocalPlayer.GetComponent<Health>() is Health health &&
                health.IsAlive())
                yield break;

            if (PlayerCamera.Instance != null && PlayerCamera.Instance.IsReady &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects
                    .TryGetValue(targetId, out NetworkObject target))
            {
                PlayerCamera.Instance.TeleportToPlayer(target.transform);
                yield break;
            }

            yield return null;
        }
    }

    [ClientRpc]
    private void SetCameraPositionClientRpc(
        Vector3 position,
        ClientRpcParams clientRpcParams = default)
    {
        int requestId = ++cameraRequestId;
        StartCoroutine(MoveCameraWhenReady(position, requestId));
    }

    private IEnumerator MoveCameraWhenReady(Vector3 position, int requestId)
    {
        while (requestId == cameraRequestId &&
               (PlayerCamera.Instance == null || !PlayerCamera.Instance.IsReady))
            yield return null;

        if (requestId == cameraRequestId)
            PlayerCamera.Instance.MoveToPosition(position);
    }

    [ClientRpc]
    private void SetDeathGrayscaleClientRpc(
        bool dead,
        ClientRpcParams clientRpcParams = default)
    {
        int requestId = ++grayscaleRequestId;
        StartCoroutine(SetGrayscaleWhenReady(dead, requestId));
    }

    private IEnumerator SetGrayscaleWhenReady(bool dead, int requestId)
    {
        while (requestId == grayscaleRequestId &&
               (PlayerCamera.Instance == null || !PlayerCamera.Instance.IsReady))
            yield return null;

        if (requestId == grayscaleRequestId)
            PlayerCamera.Instance.SetDeathGrayscale(dead);
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
        Inventory oldInventory = deadPlayer != null
            ? deadPlayer.GetComponent<Inventory>() : null;
        InventorySlot[] savedInventory = oldInventory?.CapturePersistentSlots();
        int savedSelection = oldInventory != null
            ? oldInventory.SelectedSlot.Value : 0;
        Team oldTeam = deadPlayer != null ? deadPlayer.GetComponent<Team>() : null;
        short savedTeamId = oldTeam != null ? oldTeam.TeamId.Value : (short)0;

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
        GamePlayerSpawner spawner = GetComponent<GamePlayerSpawner>();
        Transform playerSpawnPoint = spawner != null
            ? spawner.GetSpawnPoint(clientId, savedTeamId) : respawnPoint;
        if (playerSpawnPoint == null)
        {
            Debug.LogError($"RespawnManager: No spawn point for client {clientId}.");
            yield break;
        }

        GameObject newPlayer = Instantiate(
            playerPrefab,
            playerSpawnPoint.position,
            playerSpawnPoint.rotation
        );

        NetworkObject networkObject =
            newPlayer.GetComponent<NetworkObject>();

        networkObject.SpawnAsPlayerObject(clientId);
        Inventory newInventory = newPlayer.GetComponent<Inventory>();
        newInventory?.RestoreSlots(savedInventory, savedSelection);
        GrantRespawnItem(newInventory, savedTeamId);

        spectatingTargets.Remove(clientId);

        ClientRpcParams respawnClientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };

        SetDeathGrayscaleClientRpc(false, respawnClientRpcParams);
        SetCameraTargetClientRpc(
            networkObject.NetworkObjectId,
            respawnClientRpcParams
        );

        Debug.Log(
            "Player " + clientId + " respawned."
        );
    }

    private void GrantRespawnItem(Inventory inventory, short teamId)
    {
        if (respawnItem == null || respawnItemCount <= 0)
            return;

        if (inventory != null && inventory.TryAdd(respawnItem, respawnItemCount))
            return;

        GetTeamBackpack(teamId)?.TryAdd(respawnItem, respawnItemCount);
    }


    // Grants an item to a connected client: tries the player's live inventory
    // first, then falls back to the team backpack. Reuses GetTeamBackpack.
    public void GrantItemToClient(ulong clientId, ItemDefinition item, int count)
    {
        if (!IsServer || item == null || count <= 0 ||
            NetworkManager.Singleton == null)
            return;

        Inventory inventory = null;
        NetworkObject playerObj =
            NetworkManager.Singleton.SpawnManager
                .GetPlayerNetworkObject(clientId);

        if (playerObj != null)
            inventory = playerObj.GetComponent<Inventory>();

        if (inventory != null && inventory.TryAdd(item, count))
            return;

        TeamManager teamMgr = GetComponent<TeamManager>();
        short teamId = teamMgr != null
            ? teamMgr.GetTeamId(clientId)
            : (short)0;

        GetTeamBackpack(teamId)?.TryAdd(item, count);
    }

    private ContainerInventory GetTeamBackpack(short teamId)
    {
        if (teamId <= 0 || teamBackpacks == null || teamBackpacks.Length == 0)
            return null;

        TeamManager teamManager = GetComponent<TeamManager>();
        if (teamManager == null || NetworkManager.Singleton == null)
            return null;

        List<short> activeTeamIds = new() { teamId };
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            short id = teamManager.GetTeamId(clientId);
            if (id > 0 && !activeTeamIds.Contains(id))
                activeTeamIds.Add(id);
        }

        activeTeamIds.Sort();
        int backpackIndex = activeTeamIds.IndexOf(teamId);
        return backpackIndex >= 0 && backpackIndex < teamBackpacks.Length
            ? teamBackpacks[backpackIndex]
            : null;
    }
}
