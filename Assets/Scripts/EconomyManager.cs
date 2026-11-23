using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public struct EconomyGold : INetworkSerializable, IEquatable<EconomyGold>
{
    public ulong clientId;
    public int gold;

    public EconomyGold(ulong clientId, int gold)
    {
        this.clientId = clientId;
        this.gold = gold;
    }

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref gold);
    }

    public bool Equals(EconomyGold other)
    {
        return clientId == other.clientId && gold == other.gold;
    }

    public override bool Equals(object obj)
    {
        return obj is EconomyGold other && Equals(other);
    }

    public override int GetHashCode()
    {
        return clientId.GetHashCode() ^ (gold * 397);
    }
}

public class EconomyManager : NetworkBehaviour
{
    public static EconomyManager Instance { get; private set; }

    [Header("Income")]
    [SerializeField] private float startDelay = 10f;
    [SerializeField, Min(0.05f)] private float incomeTick = 0.5f;

    [Header("Piggy Bank Income")]
    [SerializeField] private float piggyBankIncomePerSecond = 3f;

    [Header("Flag rewards")]
    [SerializeField, Min(0)] private int flagTeamReward = 100;
    [SerializeField, Min(0)] private int flagDelivererReward = 150;

    [Header("Scarecrow rewards")]
    [SerializeField, Min(0)] private int scarecrowAssistReward = 100;

    [Header("HUD")]
    [SerializeField] private MonoBehaviour goldHUD;
    [SerializeField] private GoldFloatingText goldFloatingText;
    [SerializeField] private TMP_Text goldIncomeLabel;

    private readonly NetworkList<EconomyGold> goldEntries = new();
    private readonly Dictionary<ulong, float> pendingGold = new();
    private readonly Dictionary<ulong, int> lastGold = new();
    private readonly Dictionary<ulong, float> lastIncomeRate = new();

    private TeamManager teamManager;
    private float tickTimer;

    private readonly List<PiggyBank> registeredPiggyBanks = new();

    public event System.Action<ulong> GoldChanged;

    public float TeamIncome => piggyBankIncomePerSecond;
    public float StartDelay => startDelay;
    public float IncomeTick => incomeTick;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        goldEntries.OnListChanged -= OnGoldListChanged;
        goldEntries.OnListChanged += OnGoldListChanged;

        lastGold.Clear();
        lastIncomeRate.Clear();

        foreach (EconomyGold entry in goldEntries)
            lastGold[entry.clientId] = entry.gold;

        if (goldIncomeLabel != null)
            goldIncomeLabel.text = "+0/с";

        UpdateHUD(NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClientId
            : 0UL);
    }

    public override void OnNetworkDespawn()
    {
        goldEntries.OnListChanged -= OnGoldListChanged;
        base.OnNetworkDespawn();
    }

    private void OnGoldListChanged(NetworkListEvent<EconomyGold> change)
    {
        EconomyGold entry = goldEntries[change.Index];

        if (lastGold.TryGetValue(entry.clientId, out int previous) &&
            previous == entry.gold)
            return;

        lastGold[entry.clientId] = entry.gold;
        GoldChanged?.Invoke(entry.clientId);
        UpdateHUD(entry.clientId);
    }

    private void UpdateHUD(ulong clientId)
    {
        if (goldHUD == null || NetworkManager.Singleton == null)
            return;

        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        int gold = GetGold(clientId);

        if (goldHUD is IEconomyHUD hud)
            hud.SetGold(gold);
        else if (goldHUD is TMP_Text text)
            text.text = gold.ToString();
    }

    private void Update()
    {
        if (!IsServer)
            return;

        MatchStatsManager stats = MatchStatsManager.Instance;

        if (stats == null || stats.HasMatchEnded)
            return;

        if (stats.MatchTime < startDelay)
            return;

        tickTimer += Time.deltaTime;

        if (tickTimer < incomeTick)
            return;

        float delta = tickTimer;
        tickTimer = 0f;
        DistributeIncome(delta);
    }

    private TeamManager GetTeamManager()
    {
        if (teamManager != null)
            return teamManager;

        teamManager = GetComponent<TeamManager>();

        if (teamManager == null)
            teamManager = FindAnyObjectByType<TeamManager>();

        return teamManager;
    }

    private void DistributeIncome(float delta)
    {
        EnsureClientEntries();

        TeamManager manager = GetTeamManager();

        if (manager == null || NetworkManager.Singleton == null)
            return;

        var aliveClients = new HashSet<ulong>();

        foreach (NetworkObject networkObject in
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            if (!networkObject.IsPlayerObject)
                continue;

            PlayerNetwork player =
                networkObject.GetComponent<PlayerNetwork>();

            if (player == null || !player.IsSpawned)
                continue;

            Health health = player.GetComponent<Health>();

            if (health == null || !health.IsAlive())
                continue;

            aliveClients.Add(player.OwnerClientId);
        }

        var teamMembers = new Dictionary<short, List<ulong>>();

        foreach (ulong clientId in
            NetworkManager.Singleton.ConnectedClientsIds)
        {
            short teamId = manager.GetTeamId(clientId);

            if (teamId <= 0)
                continue;

            if (!teamMembers.TryGetValue(
                    teamId,
                    out List<ulong> members))
            {
                members = new List<ulong>();
                teamMembers[teamId] = members;
            }

            members.Add(clientId);
        }

        var teamIncomeRates = new Dictionary<short, float>();

        foreach (var piggyBank in registeredPiggyBanks)
        {
            if (piggyBank == null || !piggyBank.IsSpawned)
                continue;

            if (!piggyBank.IsEarning)
                continue;

            short teamId = piggyBank.OwnerTeamId;
            if (teamId <= 0)
                continue;

            if (!teamIncomeRates.TryGetValue(teamId, out float currentRate))
                currentRate = 0f;

            currentRate += piggyBankIncomePerSecond;
            teamIncomeRates[teamId] = currentRate;
        }

        foreach (var pair in teamMembers)
        {
            short teamId = pair.Key;
            float teamTotalIncome = teamIncomeRates.TryGetValue(teamId, out float rate) ? rate : 0f;

            int aliveCount = 0;

            foreach (ulong clientId in pair.Value)
            {
                if (aliveClients.Contains(clientId))
                    aliveCount++;
            }

            float playerRate = aliveCount > 0
                ? teamTotalIncome / aliveCount
                : 0f;

            foreach (ulong clientId in pair.Value)
            {
                bool isAlive = aliveClients.Contains(clientId);
                float playerActualRate = isAlive ? playerRate : 0f;

                UpdateIncomeRate(clientId, playerActualRate);

                if (!isAlive)
                    continue;

                if (!pendingGold.TryGetValue(
                        clientId,
                        out float pending))
                    pending = 0f;

                pending += playerActualRate * delta;
                int whole = (int)Mathf.Floor(pending);

                if (whole > 0)
                {
                    pending -= whole;
                    AddGold(clientId, whole);
                }

                pendingGold[clientId] = pending;
            }
        }
    }

    private void UpdateIncomeRate(ulong clientId, float rate)
    {
        if (lastIncomeRate.TryGetValue(
                clientId,
                out float previous) &&
            Mathf.Approximately(previous, rate))
            return;

        lastIncomeRate[clientId] = rate;
        SetIncomeRateClientRpc(clientId, rate);
    }

    [ClientRpc]
    private void SetIncomeRateClientRpc(
        ulong clientId,
        float ratePerSeconds)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        if (goldIncomeLabel == null)
            return;

        string rate = ratePerSeconds.ToString(
            "0.#",
            System.Globalization.CultureInfo.InvariantCulture);

        goldIncomeLabel.text = $"+{rate}/с";
    }

    [ClientRpc]
    private void ShowGoldFloatingTextClientRpc(
        ulong clientId,
        int amount,
        string source)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        goldFloatingText?.Show(amount, source);
    }

    public void RewardTeamFlagDelivery(
        short teamId,
        ulong delivererId)
    {
        if (!IsServer || NetworkManager.Singleton == null)
            return;

        TeamManager manager = GetTeamManager();

        if (manager == null)
            return;

        EnsureClientEntries();

        foreach (ulong clientId in
            NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (manager.GetTeamId(clientId) != teamId)
                continue;

            int reward = clientId == delivererId
                ? flagDelivererReward
                : flagTeamReward;

            AddGold(clientId, reward, "Прапор");
        }
    }

    // Rewards every client in assisters with scarecrow gold.
    // assisters: connected clients from the killer's team who dealt damage
    // within the participation window, regardless of who landed the kill.
    public void RewardScarecrowKill(IEnumerable<ulong> assisters)
    {
        if (!IsServer)
            return;

        EnsureClientEntries();

        foreach (ulong clientId in assisters)
            AddGold(clientId, scarecrowAssistReward, "Опудало");
    }

    public void RegisterPiggyBank(PiggyBank piggyBank)
    {
        if (piggyBank == null)
            return;

        if (!registeredPiggyBanks.Contains(piggyBank))
            registeredPiggyBanks.Add(piggyBank);
    }

    public void UnregisterPiggyBank(PiggyBank piggyBank)
    {
        if (piggyBank == null)
            return;

        registeredPiggyBanks.Remove(piggyBank);
    }

    private void EnsureClientEntries()
    {
        if (NetworkManager.Singleton == null)
            return;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (HasEntry(clientId))
                continue;

            goldEntries.Add(new EconomyGold(clientId, 0));
            pendingGold.TryAdd(clientId, 0f);
        }
    }

    private bool HasEntry(ulong clientId)
    {
        for (int i = 0; i < goldEntries.Count; i++)
            if (goldEntries[i].clientId == clientId)
                return true;

        return false;
    }

    private int GetEntryIndex(ulong clientId)
    {
        for (int i = 0; i < goldEntries.Count; i++)
            if (goldEntries[i].clientId == clientId)
                return i;

        return -1;
    }

    public int GetGold(ulong clientId)
    {
        int index = GetEntryIndex(clientId);
        return index >= 0 ? goldEntries[index].gold : 0;
    }

    public void AddGold(
        ulong clientId,
        int amount,
        string source = null)
    {
        if (!IsServer || amount <= 0)
            return;

        int index = GetEntryIndex(clientId);
        if (index < 0)
            return;

        goldEntries[index] = new EconomyGold(
            clientId,
            goldEntries[index].gold + amount
        );

        if (!string.IsNullOrEmpty(source))
            ShowGoldFloatingTextClientRpc(
                clientId,
                amount,
                source
            );
    }

    public bool TrySpendGold(
        ulong clientId,
        int amount,
        string source = null)
    {
        if (!IsServer || amount <= 0)
            return false;

        int index = GetEntryIndex(clientId);
        if (index < 0)
            return false;

        int current = goldEntries[index].gold;
        if (current < amount)
            return false;

        goldEntries[index] = new EconomyGold(
            clientId,
            current - amount
        );

        if (!string.IsNullOrEmpty(source))
            ShowGoldFloatingTextClientRpc(
                clientId,
                -amount,
                source
            );

        return true;
    }
}
