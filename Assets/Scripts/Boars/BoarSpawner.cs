using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Scene component; runs only on the server, based on MatchStatsManager's match clock.
public sealed class BoarSpawner : MonoBehaviour
{
    [SerializeField] private GameObject boarPrefab;
    [SerializeField] private BoarForest[] forests = new BoarForest[2];
    [SerializeField, Min(0.1f)] private float respawnInterval = 120f;
    [SerializeField, Min(0)] private int initialBoarsPerForest = 3;
    [SerializeField, Min(1)] private int maxBoarsPerForest = 3;

    private readonly List<BoarAI> alive = new();
    private bool started;
    private int nextCheck = 1;

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer ||
            MatchStatsManager.Instance == null || !MatchStatsManager.Instance.IsSpawned ||
            MatchStatsManager.Instance.HasMatchEnded) return;

        if (!started)
        {
            started = true;
            nextCheck = Mathf.FloorToInt(MatchStatsManager.Instance.MatchTime / respawnInterval) + 1;
            if (boarPrefab == null || boarPrefab.GetComponent<BoarAI>() == null ||
                boarPrefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError("[BoarSpawner] Assign a prefab with BoarAI and NetworkObject.", this);
                return;
            }
            foreach (BoarForest forest in forests)
                for (int i = 0; i < Mathf.Min(initialBoarsPerForest, maxBoarsPerForest); i++) Spawn(forest);
        }

        int check = Mathf.FloorToInt(MatchStatsManager.Instance.MatchTime / respawnInterval);
        if (check < nextCheck) return;
        // One check per interval, never backfill missed spawns.
        nextCheck = check + 1;
        foreach (BoarForest forest in forests)
            if (Count(forest) < maxBoarsPerForest) Spawn(forest);
    }

    private int Count(BoarForest forest)
    {
        int count = 0;
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] == null || !alive[i].IsSpawned || !alive[i].IsLiving)
                alive.RemoveAt(i);
            else if (alive[i].Forest == forest) count++;
        }
        return count;
    }

    private void Spawn(BoarForest forest)
    {
        if (forest == null || boarPrefab == null || Count(forest) >= maxBoarsPerForest) return;
        if (!forest.TryRandomPoint(out Vector2 point, forest.transform.position))
        {
            Debug.LogWarning("[BoarSpawner] No free spawn position in " + forest.name, forest);
            return;
        }
        GameObject instance = Instantiate(boarPrefab, point, Quaternion.identity);
        BoarAI boar = instance.GetComponent<BoarAI>();
        boar.SetForest(forest); // Set before NetworkObject.Spawn (server-side reference).
        instance.GetComponent<NetworkObject>().Spawn();
        alive.Add(boar);
    }
}
