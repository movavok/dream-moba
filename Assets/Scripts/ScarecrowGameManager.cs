using System.Collections;
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

    private NetworkObject activeScarecrow;
    private NetworkObject activeCorpse;
    private Health activeScarecrowHealth;
    private int currentMaxHealth;
    private Coroutine respawnCoroutine;

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

        activeScarecrow.Spawn();
        RemoveCorpse();

        activeScarecrowHealth = instance.GetComponent<Health>();

        if (activeScarecrowHealth != null)
        {
            activeScarecrowHealth.SetMaxHealth(currentMaxHealth);
            activeScarecrowHealth.Died += OnScarecrowDied;
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

    private void OnScarecrowDied()
    {
        if (!IsServer)
            return;

        if (activeScarecrowHealth != null)
            activeScarecrowHealth.Died -= OnScarecrowDied;

        activeScarecrowHealth = null;

        if (activeScarecrow != null)
        {
            Vector3 deathPosition = activeScarecrow.transform.position;
            Quaternion deathRotation = activeScarecrow.transform.rotation;
            string corpseVisualId = activeScarecrow
                .GetComponent<MinibossCorpseSource>()?.CorpseVisualId;

            if (activeScarecrow.IsSpawned)
                activeScarecrow.Despawn(true);

            activeScarecrow = null;
            SpawnCorpse(corpseVisualId, deathPosition, deathRotation);
        }

        currentMaxHealth += maxHealthIncrement;

        if (respawnCoroutine != null)
            StopCoroutine(respawnCoroutine);

        respawnCoroutine = StartCoroutine(RespawnRoutine());
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
            activeScarecrowHealth.Died -= OnScarecrowDied;

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
