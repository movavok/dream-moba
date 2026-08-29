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
}