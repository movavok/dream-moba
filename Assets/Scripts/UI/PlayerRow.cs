using UnityEngine;
using TMPro;

public class PlayerRow : MonoBehaviour
{
    private ulong clientId;

    [Header("Texts")]
    [SerializeField] private TMP_Text heroNameText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text killsDeathsText;

    public void Setup(PlayerNetwork player)
    {
        if (player == null)
            return;
        clientId = player.OwnerClientId;

        UpdatePlayerInfo(player);
        SubscribeToStats();
        UpdateKillsDeaths();
    }

    private void SubscribeToStats()
    {
        if (MatchStatsManager.Instance == null)
            return;

        MatchStatsManager.Instance.StatsChanged -= OnStatsChanged;
        MatchStatsManager.Instance.StatsChanged += OnStatsChanged;
    }

    private void UpdatePlayerInfo(PlayerNetwork player)
    {
        if (player == null)
            return;

        if (heroNameText != null)
        {
            heroNameText.text =
                player.HeroData != null
                    ? player.HeroData.heroName
                    : "Unknown";
        }

        if (playerNameText != null)
        {
            playerNameText.text =
                string.IsNullOrWhiteSpace(player.PlayerName)
                    ? "Player " + (clientId + 1)
                    : player.PlayerName;
        }
    }

    private void OnStatsChanged(ulong changedClientId)
    {
        if (changedClientId != clientId)
            return;

        UpdateKillsDeaths();
    }

    private void UpdateKillsDeaths()
    {
        if (killsDeathsText == null ||
            MatchStatsManager.Instance == null)
            return;

        int kills =
            MatchStatsManager.Instance.GetKills(clientId);

        int deaths =
            MatchStatsManager.Instance.GetDeaths(clientId);

        killsDeathsText.text =
            kills + " / " + deaths;
    }

    private void OnDestroy()
    {
        if (MatchStatsManager.Instance != null)
        {
            MatchStatsManager.Instance.StatsChanged -= OnStatsChanged;
        }
    }
}