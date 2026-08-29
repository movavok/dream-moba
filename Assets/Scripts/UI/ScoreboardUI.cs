using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class ScoreboardUI : MonoBehaviour
{

    [SerializeField] private Transform teamsContent;
    [SerializeField] private TeamHeader teamHeaderPrefab;

    private readonly Dictionary<short, TeamHeader> teamHeaders = new();

    private void OnEnable()
    {
        StartCoroutine(WaitForPlayers());
    }

    private IEnumerator WaitForPlayers()
    {
        while (FindObjectsByType<PlayerNetwork>().Length == 0)
        {
            yield return null;
        }

        Debug.Log("PLAYERS FOUND: " +
                  FindObjectsByType<PlayerNetwork>().Length);

        Refresh();
    }


    public void Refresh()
    {
        Debug.Log("SCOREBOARD REFRESH");

        Clear();

        PlayerNetwork[] players =
            FindObjectsByType<PlayerNetwork>();

        Debug.Log("PLAYERS FOUND: " + players.Length);

        foreach (PlayerNetwork player in players)
        {
            Team team = player.GetComponent<Team>();

            if (team == null)
            {
                Debug.LogWarning(
                    "Player has no Team: " + player.name
                );

                continue;
            }

            Debug.Log(
                $"Player: {player.PlayerName} | Team: {team.TeamId.Value}"
            );

            short teamId = team.TeamId.Value;

            if (!teamHeaders.TryGetValue(
                teamId,
                out TeamHeader teamHeader))
            {
                Debug.Log(
                    "Creating TeamHeader for team: " + teamId
                );

                teamHeader = CreateTeamHeader(team);
            }

            teamHeader.AddPlayer(player);
        }
    }

    private TeamHeader CreateTeamHeader(Team team)
    {
        short teamId = team.TeamId.Value;

        TeamHeader teamHeader =
            Instantiate(
                teamHeaderPrefab,
                teamsContent
            );

        teamHeader.Initialize(team);

        teamHeaders.Add(teamId, teamHeader);

        return teamHeader;
    }

    private void Clear()
    {
        foreach (TeamHeader teamHeader in teamHeaders.Values)
        {
            if (teamHeader != null)
                Destroy(teamHeader.gameObject);
        }

        teamHeaders.Clear();
    }
}