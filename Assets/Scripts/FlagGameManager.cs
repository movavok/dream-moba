using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class FlagGameManager : MonoBehaviour
{
    [SerializeField] private GameObject flagPrefab;
    [SerializeField] private GameObject deliveryZonePrefab;
    [SerializeField] private Transform pierPointA;
    [SerializeField] private Transform pierPointB;

    private NetworkObject activeFlag;
    private NetworkObject activeDeliveryZone;
    private bool deliveryInProgress;

    public void StartInitialFlag()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsServer)
            return;

        if (deliveryInProgress ||
            (activeFlag != null && activeFlag.IsSpawned))
            return;

        if (flagPrefab == null)
        {
            Debug.LogError("[FlagGameManager] Flag prefab is not assigned.");
            return;
        }

        if (pierPointA == null || pierPointB == null)
        {
            Debug.LogError(
                "[FlagGameManager] Both pier points must be assigned."
            );
            return;
        }

        if (deliveryZonePrefab == null)
        {
            Debug.LogError(
                "[FlagGameManager] Delivery zone prefab is not assigned."
            );
            return;
        }

        bool spawnAtA = Random.value < 0.5f;
        Transform flagPoint = spawnAtA ? pierPointA : pierPointB;
        Transform deliveryPoint = spawnAtA ? pierPointB : pierPointA;

        GameObject flag = Instantiate(
            flagPrefab,
            flagPoint.position,
            Quaternion.identity
        );

        activeFlag = flag.GetComponent<NetworkObject>();

        if (activeFlag == null)
        {
            Debug.LogError(
                "[FlagGameManager] Flag prefab has no NetworkObject."
            );
            Destroy(flag);
            return;
        }

        activeFlag.Spawn();

        GameObject deliveryZone = Instantiate(
            deliveryZonePrefab,
            deliveryPoint.position,
            deliveryPoint.rotation
        );

        activeDeliveryZone = deliveryZone.GetComponent<NetworkObject>();

        if (activeDeliveryZone == null)
        {
            Debug.LogError(
                "[FlagGameManager] Delivery zone prefab has no NetworkObject."
            );
            Destroy(deliveryZone);
            return;
        }

        activeDeliveryZone.Spawn();

        Debug.Log(
            $"[FlagGameManager] Flag spawned at " +
            $"{flagPoint.name}; delivery zone at {deliveryPoint.name}."
        );
    }

    public void TryCompleteDelivery(PlayerNetwork player)
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null ||
            !networkManager.IsServer ||
            deliveryInProgress ||
            player == null ||
            activeFlag == null ||
            !activeFlag.IsSpawned)
        {
            return;
        }

        FlagObject flag = activeFlag.GetComponent<FlagObject>();
        if (flag == null || !flag.IsCarriedBy(player))
            return;

        deliveryInProgress = true;

        Debug.Log(
            $"[FlagGameManager] Flag delivered by client " +
            $"{player.OwnerClientId}."
        );

        Team team = player.GetComponent<Team>();
        Color celebrationColor =
            team != null ? team.TeamColor : Color.white;

        short teamId = team != null ? team.TeamId.Value : (short)1;
        MatchStatsManager.Instance?.AddFlagDelivery(
            player.OwnerClientId
        );
        MatchStatsManager.Instance?.AddFlagScore(teamId);

        FlagDeliveryZone deliveryZone =
            activeDeliveryZone != null
                ? activeDeliveryZone.GetComponent<FlagDeliveryZone>()
                : null;

        deliveryZone?.PlayDeliveryEffect(
            deliveryZone.transform.position,
            celebrationColor
        );

        if (activeFlag != null)
        {
            if (activeFlag.IsSpawned)
                activeFlag.Despawn(true);

            activeFlag = null;
        }

        StartCoroutine(DespawnDeliveryZoneAfterEffect());
        StartCoroutine(SpawnNextFlagAfterDelay());
    }

    private IEnumerator DespawnDeliveryZoneAfterEffect()
    {
        yield return new WaitForSeconds(0.8f);

        if (activeDeliveryZone != null)
        {
            if (activeDeliveryZone.IsSpawned)
                activeDeliveryZone.Despawn(true);

            activeDeliveryZone = null;
        }
    }

    private IEnumerator SpawnNextFlagAfterDelay()
    {
        yield return new WaitForSeconds(30f);

        deliveryInProgress = false;

        if (MatchStatsManager.Instance != null &&
            MatchStatsManager.Instance.HasMatchEnded)
            yield break;

        StartInitialFlag();
    }

    private void DespawnActiveObjects()
    {
        if (activeFlag != null)
        {
            if (activeFlag.IsSpawned)
                activeFlag.Despawn(true);

            activeFlag = null;
        }

        if (activeDeliveryZone != null)
        {
            if (activeDeliveryZone.IsSpawned)
                activeDeliveryZone.Despawn(true);

            activeDeliveryZone = null;
        }
    }
}
