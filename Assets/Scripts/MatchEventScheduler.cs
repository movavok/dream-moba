using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public enum MatchEventType
{
    FlagSpawn,
    ScarecrowSpawn
}

[Serializable]
public class MatchEventDefinition
{
    public string eventName = "Flag Spawn";
    public MatchEventType eventType = MatchEventType.FlagSpawn;
    [Min(0f)] public float triggerTime;
    public bool enabled = true;
    public UnityEvent onTriggered;
}

public class MatchEventScheduler : MonoBehaviour
{
    [SerializeField] private List<MatchEventDefinition> events = new();

    private readonly HashSet<int> triggeredEvents = new();
    private MatchStatsManager matchStatsManager;

    private void Update()
    {
        if (!IsServer())
            return;

        if (matchStatsManager == null)
            matchStatsManager = MatchStatsManager.Instance;

        if (matchStatsManager == null || !matchStatsManager.IsSpawned)
            return;

        for (int index = 0; index < events.Count; index++)
        {
            MatchEventDefinition matchEvent = events[index];

            if (!matchEvent.enabled ||
                triggeredEvents.Contains(index) ||
                matchStatsManager.MatchTime < matchEvent.triggerTime)
                continue;

            triggeredEvents.Add(index);

            Debug.Log(
                $"[MatchEventScheduler] Triggered " +
                $"'{matchEvent.eventName}' at " +
                $"{matchStatsManager.MatchTime:0.0}s."
            );

            matchEvent.onTriggered?.Invoke();
        }
    }

    private bool IsServer()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager != null && networkManager.IsServer;
    }
}
