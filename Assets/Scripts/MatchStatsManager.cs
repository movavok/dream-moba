using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class MatchStatsManager : NetworkBehaviour
{
    public static MatchStatsManager Instance { get; private set; }

    private readonly Dictionary<ulong, int> kills = new();
    private readonly Dictionary<ulong, int> deaths = new();

    public event System.Action<ulong> StatsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log(
            $"[MatchStatsManager] Spawned. " +
            $"IsServer={IsServer}, " +
            $"IsClient={IsClient}, " +
            $"IsSpawned={NetworkObject.IsSpawned}"
        );

        if (!IsServer)
            return;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            kills.TryAdd(clientId, 0);
            deaths.TryAdd(clientId, 0);
        }
    }

    public void AddKill(ulong clientId)
    {
        Debug.Log(
            $"[STATS TEST] AddKill called | " +
            $"IsServer={IsServer} | " +
            $"IsSpawned={IsSpawned}"
        );

        if (!IsServer)
            return;

        if (!kills.ContainsKey(clientId))
            kills[clientId] = 0;

        kills[clientId]++;

        Debug.Log(
            $"[MatchStatsManager] KILL: " +
            $"{clientId} = {kills[clientId]}"
        );

        // Серверный UI
        StatsChanged?.Invoke(clientId);

        // Синхронизируем только kills
        UpdateClientKillsClientRpc(
            clientId,
            kills[clientId]
        );
    }

    public void AddDeath(ulong clientId)
    {
        Debug.Log(
            $"[STATS TEST] AddDeath called | " +
            $"IsServer={IsServer} | " +
            $"IsSpawned={IsSpawned}"
        );

        if (!IsServer)
            return;

        if (!deaths.ContainsKey(clientId))
            deaths[clientId] = 0;

        deaths[clientId]++;

        Debug.Log(
            $"[MatchStatsManager] DEATH: " +
            $"{clientId} = {deaths[clientId]}"
        );

        // Серверный UI
        StatsChanged?.Invoke(clientId);

        // Синхронизируем только deaths
        UpdateClientDeathsClientRpc(
            clientId,
            deaths[clientId]
        );
    }

    public int GetKills(ulong clientId)
    {
        return kills.TryGetValue(clientId, out int value)
            ? value
            : 0;
    }

    public int GetDeaths(ulong clientId)
    {
        return deaths.TryGetValue(clientId, out int value)
            ? value
            : 0;
    }

    [ClientRpc]
    private void UpdateClientKillsClientRpc(
        ulong clientId,
        int newKills)
    {
        kills[clientId] = newKills;

        Debug.Log(
            $"[MatchStatsManager] CLIENT KILL UPDATE: " +
            $"{clientId} = {newKills}"
        );

        StatsChanged?.Invoke(clientId);
    }

    [ClientRpc]
    private void UpdateClientDeathsClientRpc(
        ulong clientId,
        int newDeaths)
    {
        deaths[clientId] = newDeaths;

        Debug.Log(
            $"[MatchStatsManager] CLIENT DEATH UPDATE: " +
            $"{clientId} = {newDeaths}"
        );

        StatsChanged?.Invoke(clientId);
    }
}