using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ScarecrowGameManager : NetworkBehaviour
{
    [SerializeField] private GameObject scarecrowPrefab;
    [SerializeField] private GameObject corpsePrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private string spawnPointName = "ScareCrowPoint";

    [Header("Timings")]
    [SerializeField, Min(0f)] private float respawnDelay = 120f;

    [Header("Health Scaling")]
    [SerializeField, Min(1)] private int initialMaxHealth = 100;
    [SerializeField, Min(0)] private int maxHealthIncrement = 50;

    [Header("Audio")]
    [SerializeField] private string soundGroup =
        AudioGroups.MinionBoss("Scarecrow");
    [SerializeField] private AudioClip deathClip;

    [Header("Rewards")]
    [SerializeField] private ItemDefinition killRewardItem;
    [SerializeField, Min(1)] private int killRewardItemCount = 1;
    [SerializeField, Min(1f)] private float participationWindow = 10f;

    private NetworkObject activeScarecrow;
    private NetworkObject activeCorpse;
    private Health activeScarecrowHealth;
    private int currentMaxHealth;
    private Coroutine respawnCoroutine;

    // clientId → server Time.time of their last hit on the active scarecrow.
    private readonly Dictionary<ulong, float> lastHitTime = new();
    private ulong lastAttackerClientId = ulong.MaxValue;

    public void SpawnScarecrow()
    {
        if (!IsServer)
            return;

        if (respawnCoroutine != null)
            return;

        if (activeScarecrow != null && activeScarecrow.IsSpawned)
            return;

        if (scarecrowPrefab == null)
        {
            Debug.LogError(
                "[ScarecrowGameManager] Scarecrow prefab is not assigned."
            );

            return;
        }

        Transform point = ResolveSpawnPoint();

        if (point == null)
        {
            Debug.LogError(
                "[ScarecrowGameManager] Spawn point not found."
            );

            return;
        }

        if (currentMaxHealth <= 0)
            currentMaxHealth = initialMaxHealth;

        GameObject instance = Instantiate(
            scarecrowPrefab,
            point.position,
            point.rotation
        );

        activeScarecrow = instance.GetComponent<NetworkObject>();

        if (activeScarecrow == null)
        {
            Debug.LogError(
                "[ScarecrowGameManager] Scarecrow prefab has no NetworkObject."
            );

            Destroy(instance);
            return;
        }

        lastHitTime.Clear();
        lastAttackerClientId = ulong.MaxValue;
        activeScarecrow.Spawn();
        RemoveCorpse();

        activeScarecrowHealth = instance.GetComponent<Health>();

        if (activeScarecrowHealth != null)
        {
            activeScarecrowHealth.SetMaxHealth(currentMaxHealth);
            activeScarecrowHealth.Died += OnScarecrowDied;
            activeScarecrowHealth.Damaged += OnScarecrowDamaged;
        }
        else
        {
            Debug.LogWarning(
                "[ScarecrowGameManager] Scarecrow has no Health component."
            );
        }

        Debug.Log(
            $"[ScarecrowGameManager] Scarecrow spawned with " +
            $"{currentMaxHealth} HP."
        );
    }

    private void OnScarecrowDamaged(DamageEvent damageEvent)
    {
        // Health raises Damaged before Died, including for the lethal hit.
        // Keep an unowned final hit unowned rather than crediting an older hit.
        lastAttackerClientId = damageEvent.AttackerClientId;
        // Record the timestamp for every attacker; ulong.MaxValue is "no owner".
        if (damageEvent.AttackerClientId != ulong.MaxValue)
            lastHitTime[damageEvent.AttackerClientId] = Time.time;
    }

    private void OnScarecrowDied()
    {
        if (!IsServer)
            return;

        if (activeScarecrowHealth != null)
        {
            activeScarecrowHealth.Died -= OnScarecrowDied;
            activeScarecrowHealth.Damaged -= OnScarecrowDamaged;
        }

        activeScarecrowHealth = null;

        if (activeScarecrow != null)
        {
            Vector3 deathPosition = activeScarecrow.transform.position;
            Quaternion deathRotation = activeScarecrow.transform.rotation;
            string corpseVisualId = activeScarecrow
                .GetComponent<MinibossCorpseSource>()?.CorpseVisualId;

            PlayDeathSoundClientRpc(deathPosition);

            if (activeScarecrow.IsSpawned)
                activeScarecrow.Despawn(true);

            activeScarecrow = null;
            SpawnCorpse(corpseVisualId, deathPosition, deathRotation);
        }

        GrantKillRewards();
        lastHitTime.Clear();
        lastAttackerClientId = ulong.MaxValue;

        currentMaxHealth += maxHealthIncrement;

        if (respawnCoroutine != null)
            StopCoroutine(respawnCoroutine);

        respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    private void GrantKillRewards()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (!IsServer || network == null || lastAttackerClientId == ulong.MaxValue)
            return;

        TeamManager teams = GetComponent<TeamManager>();
        if (teams == null)
            teams = FindAnyObjectByType<TeamManager>();
        if (teams == null)
            return;

        short winningTeamId = teams.GetTeamId(lastAttackerClientId);
        if (winningTeamId <= 0)
            return;

        // Only recent attackers from the last-hitter's team receive rewards.
        float cutoff = Time.time - participationWindow;
        var participants = new List<ulong>();

        foreach (ulong clientId in
            network.ConnectedClientsIds)
        {
            if (teams.GetTeamId(clientId) == winningTeamId &&
                lastHitTime.TryGetValue(clientId, out float t) && t >= cutoff)
                participants.Add(clientId);
        }

        if (participants.Count == 0)
            return;

        // Gold reward for all participants (no kill-bonus distinction).
        EconomyManager.Instance?.RewardScarecrowKill(participants);

        // Item reward: one killRewardItem per participant into their inventory.
        if (killRewardItem == null || killRewardItemCount <= 0 ||
            RespawnManager.Instance == null)
            return;

        foreach (ulong clientId in participants)
            RespawnManager.Instance.GrantItemToClient(
                clientId,
                killRewardItem,
                killRewardItemCount);
    }

    [ClientRpc]
    private void PlayDeathSoundClientRpc(Vector3 position)
    {
        if (deathClip == null || AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlaySound(
            soundGroup,
            SoundKeys.Death,
            deathClip,
            position
        );
    }

    private void SpawnCorpse(
        string corpseVisualId,
        Vector3 position,
        Quaternion rotation)
    {
        if (corpsePrefab == null)
        {
            Debug.LogError("[ScarecrowGameManager] Corpse prefab is not assigned.");
            return;
        }

        if (string.IsNullOrEmpty(corpseVisualId))
        {
            Debug.LogError("[ScarecrowGameManager] Scarecrow has no corpse visual ID.");
            return;
        }

        GameObject corpse = Instantiate(corpsePrefab, position, rotation);
        activeCorpse = corpse.GetComponent<NetworkObject>();
        if (activeCorpse == null)
        {
            Debug.LogError("[ScarecrowGameManager] Corpse prefab has no NetworkObject.");
            Destroy(corpse);
            return;
        }

        MinibossCorpseVisual visual = corpse.GetComponent<MinibossCorpseVisual>();
        if (visual == null)
        {
            Debug.LogError("[ScarecrowGameManager] Corpse prefab has no MinibossCorpseVisual.");
            Destroy(corpse);
            activeCorpse = null;
            return;
        }

        activeCorpse.Spawn();
        visual.Initialize(corpseVisualId);
    }

    private void RemoveCorpse()
    {
        if (activeCorpse == null)
            return;

        if (activeCorpse.IsSpawned)
            activeCorpse.Despawn(true);

        activeCorpse = null;
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        respawnCoroutine = null;

        if (MatchStatsManager.Instance != null &&
            MatchStatsManager.Instance.HasMatchEnded)
        {
            yield break;
        }

        SpawnScarecrow();
    }

    private Transform ResolveSpawnPoint()
    {
        if (spawnPoint != null)
            return spawnPoint;

        if (string.IsNullOrEmpty(spawnPointName))
            return null;

        GameObject found = GameObject.Find(spawnPointName);

        return found != null ? found.transform : null;
    }

    public override void OnNetworkDespawn()
    {
        if (activeScarecrowHealth != null)
        {
            activeScarecrowHealth.Died -= OnScarecrowDied;
            activeScarecrowHealth.Damaged -= OnScarecrowDamaged;
        }

        if (IsServer)
        {
            if (respawnCoroutine != null)
            {
                StopCoroutine(respawnCoroutine);
                respawnCoroutine = null;
            }

            RemoveCorpse();
        }

        base.OnNetworkDespawn();
    }
}
