using UnityEngine;
using Unity.Netcode;

public class Team : NetworkBehaviour
{
    public NetworkVariable<short> TeamId =
        new NetworkVariable<short>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
            return;

        TeamManager teamManager =
            FindAnyObjectByType<TeamManager>();

        if (teamManager == null)
        {
            Debug.LogError("TeamManager not found!");
            return;
        }

        teamManager.AssignTeam(this);
    }
}