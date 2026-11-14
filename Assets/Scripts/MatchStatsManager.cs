using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class MatchStatsManager : NetworkBehaviour
{
    public static MatchStatsManager Instance { get; private set; }

    [SerializeField] private int flagsToWin = 3;

    private readonly Dictionary<ulong, int> kills = new();
    private readonly Dictionary<ulong, int> deaths = new();
    private readonly Dictionary<ulong, int> currentKillStreak = new();
    private readonly Dictionary<ulong, int> bestKillStreak = new();
    private readonly Dictionary<ulong, int> flagsDelivered = new();
    private readonly Dictionary<ulong, int> counters = new();
    private readonly Dictionary<ulong, int> damageDealt = new();
    private NetworkVariable<float> matchTime =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<int> team1Flags =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<int> team2Flags =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<short> winnerTeamId =
        new NetworkVariable<short>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<float> matchEndTime =
        new NetworkVariable<float>(
            -1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private float syncTimer;

    public float MatchTime => matchTime.Value;
    public int Team1Flags => team1Flags.Value;
    public int Team2Flags => team2Flags.Value;
    public int FlagsToWin => flagsToWin;
    public short WinnerTeamId => winnerTeamId.Value;
    public bool HasMatchEnded => winnerTeamId.Value > 0;
    public float MatchEndTime => matchEndTime.Value;

    public event System.Action<ulong> StatsChanged;
    public event System.Action FlagsChanged;
    public event System.Action<short> MatchEnded;

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

        team1Flags.OnValueChanged -= OnTeamFlagsChanged;
        team2Flags.OnValueChanged -= OnTeamFlagsChanged;
        team1Flags.OnValueChanged += OnTeamFlagsChanged;
        team2Flags.OnValueChanged += OnTeamFlagsChanged;

        winnerTeamId.OnValueChanged -= OnWinnerTeamChanged;
        winnerTeamId.OnValueChanged += OnWinnerTeamChanged;

        if (!IsServer)
            return;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            kills.TryAdd(clientId, 0);
            deaths.TryAdd(clientId, 0);
            currentKillStreak.TryAdd(clientId, 0);
            bestKillStreak.TryAdd(clientId, 0);
            flagsDelivered.TryAdd(clientId, 0);
            counters.TryAdd(clientId, 0);
            damageDealt.TryAdd(clientId, 0);
        }
    }

    private void OnWinnerTeamChanged(short oldValue, short newValue)
    {
        MatchEnded?.Invoke(newValue);
    }

    private void Update()
    {
        if (!IsServer)
            return;

        syncTimer += Time.deltaTime;

        if (syncTimer < 0.1f)
            return;

        matchTime.Value += syncTimer;
        syncTimer = 0f;
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

        if (!currentKillStreak.ContainsKey(clientId))
            currentKillStreak[clientId] = 0;

        currentKillStreak[clientId]++;

        if (!bestKillStreak.ContainsKey(clientId))
            bestKillStreak[clientId] = 0;

        if (currentKillStreak[clientId] > bestKillStreak[clientId])
            bestKillStreak[clientId] = currentKillStreak[clientId];

        Debug.Log(
            $"[MatchStatsManager] KILL: " +
            $"{clientId} = {kills[clientId]} | " +
            $"KillStreak = {currentKillStreak[clientId]} | " +
            $"BestKillStreak = {bestKillStreak[clientId]}"
        );

        // Серверный UI
        StatsChanged?.Invoke(clientId);

        // Синхронизируем kills и kill streak
        UpdateClientKillsClientRpc(
            clientId,
            kills[clientId]
        );

        UpdateClientKillStreakClientRpc(
            clientId,
            currentKillStreak[clientId],
            bestKillStreak[clientId]
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

        if (!currentKillStreak.ContainsKey(clientId))
            currentKillStreak[clientId] = 0;

        currentKillStreak[clientId] = 0;

        if (!bestKillStreak.ContainsKey(clientId))
            bestKillStreak[clientId] = 0;

        Debug.Log(
            $"[MatchStatsManager] DEATH: " +
            $"{clientId} = {deaths[clientId]} | " +
            $"KillStreak reset. BestKillStreak = " +
            $"{bestKillStreak[clientId]}"
        );

        // Серверный UI
        StatsChanged?.Invoke(clientId);

        // Синхронизируем deaths и сброс kill streak
        UpdateClientDeathsClientRpc(
            clientId,
            deaths[clientId]
        );

        UpdateClientKillStreakClientRpc(
            clientId,
            currentKillStreak[clientId],
            bestKillStreak[clientId]
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

    public int GetCurrentKillStreak(ulong clientId)
    {
        return currentKillStreak.TryGetValue(clientId, out int value)
            ? value
            : 0;
    }

    public int GetBestKillStreak(ulong clientId)
    {
        return bestKillStreak.TryGetValue(clientId, out int value)
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

    [ClientRpc]
    private void UpdateClientKillStreakClientRpc(
        ulong clientId,
        int current,
        int best)
    {
        currentKillStreak[clientId] = current;
        bestKillStreak[clientId] = best;

        StatsChanged?.Invoke(clientId);
    }

    public void AddFlagDelivery(ulong clientId)
    {
        if (!IsServer || HasMatchEnded)
            return;

        if (!flagsDelivered.ContainsKey(clientId))
            flagsDelivered[clientId] = 0;

        flagsDelivered[clientId]++;

        UpdateClientFlagDeliveriesClientRpc(
            clientId,
            flagsDelivered[clientId]
        );
    }

    public int GetFlagsDelivered(ulong clientId)
    {
        return flagsDelivered.TryGetValue(clientId, out int value)
            ? value
            : 0;
    }

    [ClientRpc]
    private void UpdateClientFlagDeliveriesClientRpc(
        ulong clientId,
        int newValue)
    {
        flagsDelivered[clientId] = newValue;

        StatsChanged?.Invoke(clientId);
    }

    public void AddCounter(ulong clientId)
    {
        if (!IsServer || HasMatchEnded)
            return;

        if (!counters.ContainsKey(clientId))
            counters[clientId] = 0;

        counters[clientId]++;

        StatsChanged?.Invoke(clientId);

        UpdateClientCountersClientRpc(
            clientId,
            counters[clientId]
        );
    }

    public int GetCounters(ulong clientId)
    {
        return counters.TryGetValue(clientId, out int value)
            ? value
            : 0;
    }

    [ClientRpc]
    private void UpdateClientCountersClientRpc(
        ulong clientId,
        int newValue)
    {
        counters[clientId] = newValue;

        StatsChanged?.Invoke(clientId);
    }

    public void AddDamage(ulong clientId, int amount)
    {
        if (!IsServer || HasMatchEnded || amount <= 0)
            return;

        if (!damageDealt.ContainsKey(clientId))
            damageDealt[clientId] = 0;

        damageDealt[clientId] += amount;

        StatsChanged?.Invoke(clientId);

        UpdateClientDamageClientRpc(
            clientId,
            damageDealt[clientId]
        );
    }

    public int GetDamage(ulong clientId)
    {
        return damageDealt.TryGetValue(clientId, out int value)
            ? value
            : 0;
    }

    [ClientRpc]
    private void UpdateClientDamageClientRpc(
        ulong clientId,
        int newValue)
    {
        damageDealt[clientId] = newValue;

        StatsChanged?.Invoke(clientId);
    }

    public void AddFlagScore(short teamId)
    {
        if (!IsServer || HasMatchEnded)
            return;

        if (teamId == 1)
            team1Flags.Value++;
        else if (teamId == 2)
            team2Flags.Value++;

        Debug.Log(
            $"[MatchStatsManager] FLAG DELIVERED for Team {teamId}. " +
            $"Score: Team1={team1Flags.Value}, Team2={team2Flags.Value}"
        );

        if (winnerTeamId.Value > 0)
            return;

        if (team1Flags.Value >= flagsToWin)
            EndMatch(1);
        else if (team2Flags.Value >= flagsToWin)
            EndMatch(2);
    }

    private void EndMatch(short winnerId)
    {
        if (!IsServer || winnerTeamId.Value > 0)
            return;

        matchEndTime.Value = matchTime.Value;
        winnerTeamId.Value = winnerId;

        Debug.Log(
            $"[MatchStatsManager] MATCH ENDED. " +
            $"Winner: Team {winnerId} at {matchEndTime.Value:0.0}s."
        );
    }

    public int GetFlagScore(short teamId)
    {
        if (teamId == 1) return team1Flags.Value;
        if (teamId == 2) return team2Flags.Value;
        return 0;
    }

    private void OnTeamFlagsChanged(int oldValue, int newValue)
    {
        FlagsChanged?.Invoke();
    }
}