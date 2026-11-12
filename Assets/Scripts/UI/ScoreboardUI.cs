using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class ScoreboardUI : MonoBehaviour
{

    [SerializeField] private Transform teamsContent;
    [SerializeField] private TeamHeader teamHeaderPrefab;

    private readonly Dictionary<short, TeamHeader> teamHeaders = new();

    private NetworkManager networkManager;

    private void OnEnable()
    {
        networkManager = NetworkManager.Singleton;

        if (networkManager != null)
            networkManager.OnClientConnectedCallback += OnClientConnected;

        StartCoroutine(WaitForPlayers());
    }

    private void OnDisable()
    {
        if (networkManager != null)
            networkManager.OnClientConnectedCallback -= OnClientConnected;

        networkManager = null;
    }

    private void OnClientConnected(ulong clientId)
    {
        StartCoroutine(RefreshWhenPlayerSpawns(clientId));
    }

    private IEnumerator RefreshWhenPlayerSpawns(ulong clientId)
    {
        float timeout = Time.time + 10f;

        while (Time.time < timeout && !HasPlayer(clientId))
            yield return null;

        if (HasPlayer(clientId))
            Refresh();
    }

    private bool HasPlayer(ulong clientId)
    {
        foreach (PlayerNetwork player in FindObjectsByType<PlayerNetwork>())
        {
            if (player.OwnerClientId == clientId)
                return true;
        }

        return false;
    }

    private IEnumerator WaitForPlayers()
    {
        while (FindObjectsByType<PlayerNetwork>().Length == 0)
        {
            yield return null;
        }

        Refresh();
    }


    public void Refresh()
    {
        Clear();

        PlayerNetwork[] players = FindObjectsByType<PlayerNetwork>()
            .OrderBy(GetTeamId)
            .ThenByDescending(GetKillCount)
            .ThenBy(player => player.OwnerClientId)
            .ToArray();

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

            short teamId = team.TeamId.Value;

            if (!teamHeaders.TryGetValue(
                teamId,
                out TeamHeader teamHeader))
            {
                teamHeader = CreateTeamHeader(team);
            }

            teamHeader.AddPlayer(player);
        }
    }

    private static short GetTeamId(PlayerNetwork player)
    {
        Team team = player.GetComponent<Team>();
        return team != null ? team.TeamId.Value : (short)0;
    }

    private static int GetKillCount(PlayerNetwork player)
    {
        return MatchStatsManager.Instance != null
            ? MatchStatsManager.Instance.GetKills(player.OwnerClientId)
            : 0;
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


    public void OnPlayerLeft(ulong clientId)
    {
        foreach (var pair in teamHeaders)
        {
            if (pair.Value.RemovePlayer(clientId))
            {
                StartCoroutine(RemoveEmptyTeam(pair.Key, pair.Value));
                break;
            }
        }
    }

    private IEnumerator RemoveEmptyTeam(
        short teamId,
        TeamHeader teamHeader)
    {
        yield return null;

        if (!teamHeader.HasPlayers())
        {
            Destroy(teamHeader.gameObject);
            teamHeaders.Remove(teamId);
        }
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