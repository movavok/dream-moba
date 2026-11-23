using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Lives on the match NetworkObject so a dead player's spectator camera can
// continue contributing its actual viewport after the player despawns.
public class TeamCameraViews : NetworkBehaviour
{
    public static TeamCameraViews Instance { get; private set; }

    [SerializeField] private Collider2D mapBounds;
    [SerializeField, Min(0.05f)] private float reportInterval = 0.15f;

    private readonly Dictionary<ulong, Rect> views = new();
    private readonly Dictionary<ulong, bool> insideBuildings = new();
    private readonly Dictionary<ulong, float> lastSeen = new();
    private float nextReport;
    private bool hadLocalPlayer;
    private TeamManager teams;
    private readonly List<ulong> recipients = new(6);

    public IEnumerable<ulong> Reporters => views.Keys;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        base.OnNetworkSpawn();
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
        views.Clear();
        insideBuildings.Clear();
        lastSeen.Clear();
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (PlayerNetwork.LocalPlayer != null)
            hadLocalPlayer = true;

        if (!hadLocalPlayer || !IsSpawned || Time.unscaledTime < nextReport ||
            PlayerCamera.Instance == null ||
            !PlayerCamera.Instance.TryGetWorldViewRect(out Rect view))
            return;

        nextReport = Time.unscaledTime + reportInterval;
        SendViewRpc(new Vector4(view.xMin, view.yMin, view.xMax, view.yMax),
            PlayerCamera.Instance.IsInBuilding);
    }

    public bool TryGetView(ulong clientId, out Rect view, out bool insideBuilding)
    {
        if (NetworkManager != null && clientId == NetworkManager.LocalClientId &&
            PlayerCamera.Instance != null)
        {
            insideBuilding = PlayerCamera.Instance.IsInBuilding;
            return PlayerCamera.Instance.TryGetWorldViewRect(out view);
        }

        if (views.TryGetValue(clientId, out view) &&
            Time.unscaledTime - lastSeen[clientId] < 2f)
        {
            insideBuilding = insideBuildings[clientId];
            return true;
        }

        view = default;
        insideBuilding = false;
        return false;
    }

    // Shared minimap/indicator visibility: even a partial sprite overlap counts.
    public bool IsVisible(Bounds visualBounds)
    {
        if (NetworkManager == null)
            return false;

        if (TryGetView(NetworkManager.LocalClientId, out Rect local, out bool inside) &&
            !inside && Overlaps(local, visualBounds))
            return true;

        foreach (var entry in views)
            if (Time.unscaledTime - lastSeen[entry.Key] < 2f &&
                !insideBuildings[entry.Key] && Overlaps(entry.Value, visualBounds))
                return true;

        return false;
    }

    private static bool Overlaps(Rect view, Bounds bounds)
    {
        return view.xMin < bounds.max.x && view.xMax > bounds.min.x &&
            view.yMin < bounds.max.y && view.yMax > bounds.min.y;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SendViewRpc(Vector4 corners, bool insideBuilding,
        RpcParams rpc = default)
    {
        if (mapBounds == null || !IsFinite(corners.x) ||
            !IsFinite(corners.y) || !IsFinite(corners.z) ||
            !IsFinite(corners.w) || corners.z <= corners.x ||
            corners.w <= corners.y)
            return;

        Bounds bounds = mapBounds.bounds;
        if (corners.z - corners.x > 100f || corners.w - corners.y > 100f ||
            corners.x < bounds.min.x - 50f || corners.z > bounds.max.x + 50f ||
            corners.y < bounds.min.y - 50f || corners.w > bounds.max.y + 50f)
            return;

        ulong sender = rpc.Receive.SenderClientId;
        if (teams == null)
            teams = FindAnyObjectByType<TeamManager>();
        if (teams == null)
            return;

        short teamId = teams.GetTeamId(sender);
        recipients.Clear();
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            if (clientId != sender && teams.GetTeamId(clientId) == teamId)
                recipients.Add(clientId);

        if (recipients.Count == 0)
            return;

        ReceiveViewClientRpc(sender, corners, insideBuilding, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = recipients }
        });
    }

    [ClientRpc]
    private void ReceiveViewClientRpc(ulong clientId, Vector4 corners, bool insideBuilding,
        ClientRpcParams rpc = default)
    {
        views[clientId] = Rect.MinMaxRect(corners.x, corners.y, corners.z, corners.w);
        insideBuildings[clientId] = insideBuilding;
        lastSeen[clientId] = Time.unscaledTime;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
