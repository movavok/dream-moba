using System.Collections;
using UnityEngine;
using TMPro;

public class TeamHeader : MonoBehaviour
{
    [SerializeField] private Transform playersContent;
    [SerializeField] private PlayerRow playerRowPrefab;

    [SerializeField] private TMP_Text teamNameText;
    [SerializeField] private TMP_Text flagsText;

    private short teamId;

    public void Initialize(Team team)
    {
        teamId = team.TeamId.Value;

        if (teamNameText != null)
        {
            teamNameText.text = team.TeamName;
            teamNameText.color = team.TeamColor;
        }

        StartCoroutine(SubscribeToFlagsWhenReady());
    }

    private IEnumerator SubscribeToFlagsWhenReady()
    {
        float timeout = Time.time + 10f;

        while (MatchStatsManager.Instance == null &&
            Time.time < timeout)
        {
            yield return null;
        }

        if (MatchStatsManager.Instance == null)
            yield break;

        MatchStatsManager.Instance.FlagsChanged -= UpdateFlags;
        MatchStatsManager.Instance.FlagsChanged += UpdateFlags;

        UpdateFlags();
    }

    private void UpdateFlags()
    {
        if (flagsText == null)
            return;

        flagsText.text =
            MatchStatsManager.Instance != null
                ? MatchStatsManager.Instance
                    .GetFlagScore(teamId)
                    .ToString()
                : "0";
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
        return playersContent
            .GetComponentsInChildren<PlayerRow>(true)
            .Length > 0;
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

    private void OnDestroy()
    {
        if (MatchStatsManager.Instance != null)
            MatchStatsManager.Instance.FlagsChanged -= UpdateFlags;
    }
}