using UnityEngine;
using Unity.Netcode;

public class Team : NetworkBehaviour
{
    [SerializeField] private TeamDefinition teamDefinition;

    public NetworkVariable<short> TeamId =
        new NetworkVariable<short>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public bool IsEnemyFor(short attackerTeamId)
    {
        if (attackerTeamId < 1)
            return false;

        if (TeamId.Value == -1)
            return true;

        if (TeamId.Value == 0)
            return false;

        return TeamId.Value > 0 &&
            TeamId.Value != attackerTeamId;
    }

    public TeamDefinition.TeamData Data
    {
        get
        {
            if (teamDefinition == null)
                return null;

            return teamDefinition.GetTeam(TeamId.Value);
        }
    }

    public string TeamName
    {
        get
        {
            TeamManager manager = FindAnyObjectByType<TeamManager>();

            if (manager == null)
                return "Unknown Team";

            TeamDefinition.TeamData data =
                manager.GetTeamData(TeamId.Value);

            return data != null
                ? data.teamName
                : "Unknown Team";
        }
    }

    public Color TeamColor
    {
        get
        {
            TeamManager manager = FindAnyObjectByType<TeamManager>();

            if (manager == null)
                return Color.white;

            TeamDefinition.TeamData data =
                manager.GetTeamData(TeamId.Value);

            return data != null
                ? data.color
                : Color.white;
        }
    }

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