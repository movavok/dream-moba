using UnityEngine;
using TMPro;

public class TeamHeader : MonoBehaviour
{
    [SerializeField] private Transform playersContent;
    [SerializeField] private PlayerRow playerRowPrefab;

    [SerializeField] private TMP_Text teamNameText;

    private short teamId;

    public void Initialize(Team team)
    {
        teamId = team.TeamId.Value;

        if (teamNameText != null)
        {
            teamNameText.text = team.TeamName;
            teamNameText.color = team.TeamColor;
        }
    }

    public void AddPlayer(PlayerNetwork player)
    {
        if (playerRowPrefab == null || playersContent == null)
            return;

        PlayerRow row =
            Instantiate(
                playerRowPrefab,
                playersContent
            );

        row.Setup(player);
    }

    public bool HasPlayers()
    {
        PlayerRow[] rows =
            playersContent.GetComponentsInChildren<PlayerRow>(true);

        Debug.Log(
            $"TEAM {teamId}: PlayerRows found = {rows.Length}"
        );

        foreach (PlayerRow row in rows)
        {
            if (row != null)
            {
                Debug.Log(
                    $"TEAM {teamId}: PlayerRow still exists, clientId = {row.ClientId}"
                );

                return true;
            }
        }

        Debug.Log($"TEAM {teamId}: NO PLAYERS");

        return false;
}

    public bool RemovePlayer(ulong clientId)
    {
        PlayerRow[] rows = playersContent.GetComponentsInChildren<PlayerRow>();

        foreach (PlayerRow row in rows)
        {
            if (row.ClientId == clientId)
            {
                Destroy(row.gameObject);
                return true;
            }
        }

        return false;
    }
}