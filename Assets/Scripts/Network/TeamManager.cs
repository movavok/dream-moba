using UnityEngine;
using Unity.Netcode;

public enum TeamAssignmentMode
{
    Alternate,
    AllTeam1,
    AllDifferent
}

public class TeamManager : NetworkBehaviour
{
    [SerializeField] private TeamDefinition teamDefinition;

    public TeamAssignmentMode AssignmentMode =>
        MatchSettings.Instance.TeamAssignmentMode;

    public TeamDefinition.TeamData GetTeamData(short teamId)
    {
        if (teamDefinition == null)
        {
            Debug.LogError("TeamManager: TeamDefinition is not assigned!");
            return null;
        }

        return teamDefinition.GetTeam(teamId);
    }

    public short GetTeamId(ulong clientId)
    {
        switch (AssignmentMode)
        {
            case TeamAssignmentMode.AllTeam1:
                return 1;

            case TeamAssignmentMode.Alternate:
                return (short)((clientId % 2) + 1);

            case TeamAssignmentMode.AllDifferent:
                return (short)(clientId + 1);

            default:
                return 1;
        }
    }

    public void AssignTeam(Team team)
    {
        if (!IsServer || team == null)
            return;

        short teamId = GetTeamId(team.OwnerClientId);

        Debug.Log(
            $"[TEAM ASSIGN] " +
            $"ClientId={team.OwnerClientId} | " +
            $"Mode={AssignmentMode} | " +
            $"CalculatedTeam={teamId}"
        );

        team.TeamId.Value = teamId;

        Debug.Log(
            $"[TEAM ASSIGN] " +
            $"ClientId={team.OwnerClientId} | " +
            $"FinalTeam={team.TeamId.Value}"
        );
    }
}