using UnityEngine;
using Unity.Netcode;

public enum TeamAssignmentMode
{
    Alternate,
    AllTeam1,
    AllDifferent
}

public class Team : NetworkBehaviour
{
    private TeamAssignmentMode assignmentMode = TeamAssignmentMode.Alternate;

    public NetworkVariable<short> TeamId =
        new NetworkVariable<short>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        TeamId.Value = GetTeamId();

        Debug.Log(
            "Player " + OwnerClientId +
            " Team: " + TeamId.Value
        );
    }
    private short GetTeamId()
    {
        switch (assignmentMode)
        {
            case TeamAssignmentMode.AllTeam1:
                return 1;

            case TeamAssignmentMode.Alternate:
                return (short)((OwnerClientId % 2) + 1);

            case TeamAssignmentMode.AllDifferent:
                return (short)(OwnerClientId + 1);

            default:
                return 1;
        }
    }
}