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
        activeFlag.GetComponent<FlagObject>()?.SetHomePosition(flagPoint.position);

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

        if (!player.GetComponent<Inventory>().RemoveItem(flag.InventoryItem, 1))
            return;

        Team team = player.GetComponent<Team>();
        CompleteDelivery(
            player.OwnerClientId,
            team != null ? team.TeamId.Value : (short)1,
            team != null ? team.TeamColor : Color.white
        );
    }

    public void TryCompleteThrownDelivery(
        FlagObject flag,
        Vector3 previousPosition,
        Vector3 currentPosition)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer ||
            deliveryInProgress || flag == null || !flag.HasThrower ||
            flag.NetworkObject != activeFlag || activeFlag == null ||
            !activeFlag.IsSpawned || activeDeliveryZone == null ||
            !activeDeliveryZone.IsSpawned ||
            (flag.LastThrowerTeamId != 1 && flag.LastThrowerTeamId != 2))
            return;

        CircleCollider2D zone = activeDeliveryZone.GetComponent<CircleCollider2D>();
        CircleCollider2D flagCollider = flag.GetComponent<CircleCollider2D>();
        if (zone == null || flagCollider == null)
            return;

        Vector2 center = zone.transform.TransformPoint(zone.offset);
        Vector2 flagOffset = flag.transform.TransformVector(flagCollider.offset);
        Vector2 from = (Vector2)previousPosition + flagOffset;
        Vector2 to = (Vector2)currentPosition + flagOffset;
        Vector2 path = to - from;
        float t = path.sqrMagnitude > 0f
            ? Mathf.Clamp01(Vector2.Dot(center - from, path) / path.sqrMagnitude)
            : 0f;
        Vector2 closest = from + path * t;
        float zoneScale = Mathf.Max(
            Mathf.Abs(zone.transform.lossyScale.x),
            Mathf.Abs(zone.transform.lossyScale.y));
        float flagScale = Mathf.Max(
            Mathf.Abs(flag.transform.lossyScale.x),
            Mathf.Abs(flag.transform.lossyScale.y));
        float reach = zone.radius * zoneScale + flagCollider.radius * flagScale;

        if ((center - closest).sqrMagnitude > reach * reach)
            return;

        CompleteDelivery(
            flag.LastThrowerClientId,
            flag.LastThrowerTeamId,
            flag.LastThrowerTeamColor
        );
    }

    private void CompleteDelivery(ulong clientId, short teamId, Color celebrationColor)
    {
        deliveryInProgress = true;

        Debug.Log(
            $"[FlagGameManager] Flag delivered by client " +
            $"{clientId}."
        );

        MatchStatsManager.Instance?.AddFlagDelivery(
            clientId
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
        FlagDeliveryZone zone = activeDeliveryZone != null
            ? activeDeliveryZone.GetComponent<FlagDeliveryZone>() : null;
        yield return new WaitForSeconds(
            (zone != null ? zone.AnimationDuration : 0.8f) + 0.2f);

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
