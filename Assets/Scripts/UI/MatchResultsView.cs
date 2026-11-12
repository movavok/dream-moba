using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchResultsView : MonoBehaviour
{
    public enum AwardStat
    {
        Kills,
        FlagsDelivered,
        Deaths,
        Counters
    }

    [Serializable]
    public class AwardCard
    {
        [SerializeField] private TextMeshProUGUI nicknameText;
        [SerializeField] private TextMeshProUGUI numberText;
        [SerializeField] private Color valueColor = Color.white;
        [SerializeField] private string statLabel = "";
        [SerializeField] private AwardStat stat;
        [SerializeField] private bool lowerIsBetter;

        [SerializeField] private Image cardBackground;

        [Header("Local Player Highlight")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.82f, 0.35f, 1f);
        [SerializeField, Range(0f, 1f)] private float highlightAmount = 0.25f;

        private Color originalBackgroundColor;
        private bool hasOriginalBackgroundColor;

        public AwardStat Stat => stat;
        public bool LowerIsBetter => lowerIsBetter;

        public void Show(
            string nickname,
            int value,
            string emptyLabel,
            bool isLocalWinner)
        {
            bool hasWinner = !string.IsNullOrWhiteSpace(nickname);

            if (nicknameText != null)
                nicknameText.text = hasWinner ? nickname : emptyLabel;

            if (numberText != null)
            {
                numberText.gameObject.SetActive(hasWinner);

                if (hasWinner)
                {
                    string color = ColorUtility.ToHtmlStringRGB(valueColor);

                    numberText.text =
                        statLabel +
                        "\n<color=#" + color + ">" +
                        value +
                        "</color>";
                }
            }

            if (cardBackground != null)
            {
                if (!hasOriginalBackgroundColor)
                {
                    originalBackgroundColor = cardBackground.color;
                    hasOriginalBackgroundColor = true;
                }

                Color targetColor = originalBackgroundColor;

                if (isLocalWinner)
                {
                    targetColor.r = Mathf.Lerp(
                        originalBackgroundColor.r,
                        highlightColor.r,
                        highlightAmount
                    );

                    targetColor.g = Mathf.Lerp(
                        originalBackgroundColor.g,
                        highlightColor.g,
                        highlightAmount
                    );

                    targetColor.b = Mathf.Lerp(
                        originalBackgroundColor.b,
                        highlightColor.b,
                        highlightAmount
                    );
                }

                cardBackground.color = targetColor;
            }
        }
    }

    [Header("Outcome")]
    [SerializeField] private Image tintOverlay;
    [SerializeField] private TextMeshProUGUI gameResultText;
    [SerializeField, Range(0f, 1f)] private float outcomeTintAmount = 0.18f;

    [Header("Backdrop")]
    [SerializeField] private Image backdropImage;
    [SerializeField] private Animator backdropAnimator;
    [SerializeField] private string backdropOpenParameter = "Open";

    [Header("Effects")]
    [SerializeField] private ParticleSystem resultParticles;

    [Header("Award Cards")]
    [SerializeField] private AwardCard killerCard;
    [SerializeField] private AwardCard carryCard;
    [SerializeField] private AwardCard survivorCard;
    [SerializeField] private AwardCard clutchCard;

    [Header("Personal Card")]
    [SerializeField] private TextMeshProUGUI personalTitleText;
    [SerializeField] private TextMeshProUGUI personalResultText;
    [SerializeField] private TextMeshProUGUI personalKdText;
    [SerializeField] private TextMeshProUGUI personalFlagsText;
    [SerializeField] private TextMeshProUGUI personalFlagControlText;
    [SerializeField] private TextMeshProUGUI personalDamageText;
    [SerializeField] private TextMeshProUGUI personalKillStreakText;

    public void ApplyOutcome(Color outcome, string resultText)
    {
        if (tintOverlay != null)
        {
            tintOverlay.color = Color.Lerp(
                tintOverlay.color,
                outcome,
                outcomeTintAmount
            );
        }

        if (backdropImage != null)
        {
            Color color = outcome;
            color.a = backdropImage.color.a;
            backdropImage.color = color;
        }

        if (backdropAnimator != null &&
            !string.IsNullOrEmpty(backdropOpenParameter))
        {
            backdropAnimator.SetBool(backdropOpenParameter, true);
        }

        if (gameResultText != null)
        {
            gameResultText.text = resultText;
            gameResultText.color = outcome;
        }

        PlayResultParticles();
    }

    private void PlayResultParticles()
    {
        if (resultParticles == null)
            resultParticles = GetComponentInChildren<ParticleSystem>(true);

        if (resultParticles == null)
            return;

        resultParticles.Clear(true);
        resultParticles.Play(true);
    }

    public void FillAwards(string noWinnerText)
    {
        MatchStatsManager stats = MatchStatsManager.Instance;

        if (stats == null)
            return;

        PlayerNetwork[] players = FindObjectsByType<PlayerNetwork>();

        FillCard(killerCard, players, stats, noWinnerText);
        FillCard(carryCard, players, stats, noWinnerText);
        FillCard(survivorCard, players, stats, noWinnerText);
        FillCard(clutchCard, players, stats, noWinnerText);
    }

    public void FillPersonalCard(string resultText)
    {
        PlayerNetwork localPlayer = PlayerNetwork.LocalPlayer;
        MatchStatsManager stats = MatchStatsManager.Instance;

        if (localPlayer == null || stats == null)
        {
            if (personalTitleText != null)
                personalTitleText.gameObject.SetActive(false);

            return;
        }

        if (personalTitleText != null)
            personalTitleText.gameObject.SetActive(true);

        ulong clientId = localPlayer.OwnerClientId;
        int kills = stats.GetKills(clientId);
        int deaths = stats.GetDeaths(clientId);

        if (personalResultText != null)
            personalResultText.text = resultText;

        if (personalKdText != null)
            personalKdText.text = $": {kills} / {deaths}";

        if (personalFlagsText != null)
        {
            personalFlagsText.text =
                $": {stats.GetFlagsDelivered(clientId)}";
        }

        if (personalFlagControlText != null)
        {
            personalFlagControlText.text =
                $": {GetFlagControlPercent(localPlayer, stats)}%";
        }

        if (personalDamageText != null)
            personalDamageText.text = $": {stats.GetDamage(clientId)}";

        if (personalKillStreakText != null)
        {
            personalKillStreakText.text =
                $": {GetBestStreak(localPlayer)}";
        }
    }

    private static int GetFlagControlPercent(
        PlayerNetwork localPlayer,
        MatchStatsManager stats)
    {
        Team team = localPlayer.GetComponent<Team>();
        short teamId = team != null ? team.TeamId.Value : (short)0;

        if (teamId <= 0)
            return 0;

        int teamFlags = stats.GetFlagScore(teamId);
        int totalFlags = stats.Team1Flags + stats.Team2Flags;

        if (totalFlags <= 0)
            return 0;

        float control = (float)teamFlags / totalFlags;

        return Mathf.RoundToInt(Mathf.Clamp01(control) * 100f);
    }

    private static int GetBestStreak(PlayerNetwork localPlayer)
    {
        PlayerStreak streak = localPlayer.GetComponent<PlayerStreak>();

        return streak != null ? streak.BestStreak : 0;
    }

    private static void FillCard(
        AwardCard card,
        PlayerNetwork[] players,
        MatchStatsManager stats,
        string emptyLabel)
    {
        if (card == null)
            return;

        List<PlayerNetwork> winners = FindWinners(
            players,
            stats,
            card.Stat,
            card.LowerIsBetter
        );

        if (winners.Count == 0 || winners.Count > 1)
        {
            card.Show("NO WINNERS", 0, emptyLabel, false);
            return;
        }

        PlayerNetwork localPlayer = PlayerNetwork.LocalPlayer;

        bool isLocalWinner =
            localPlayer != null &&
            winners.Exists(player =>
                player.OwnerClientId == localPlayer.OwnerClientId);

        List<string> names = new(winners.Count);

        foreach (PlayerNetwork player in winners)
            names.Add($"\"{GetDisplayName(player)}\"");

        int value = GetStat(stats, winners[0], card.Stat);

        card.Show(
            string.Join(", ", names),
            value,
            emptyLabel,
            isLocalWinner
        );
    }

    private static List<PlayerNetwork> FindWinners(
        PlayerNetwork[] players,
        MatchStatsManager stats,
        AwardStat stat,
        bool lowerIsBetter)
    {
        List<PlayerNetwork> winners = new();
        int bestValue = lowerIsBetter ? int.MaxValue : 0;

        foreach (PlayerNetwork player in players)
        {
            if (player == null)
                continue;

            int value = GetStat(stats, player, stat);

            if (!lowerIsBetter && value <= 0)
                continue;

            if (lowerIsBetter ? value < bestValue : value > bestValue)
            {
                bestValue = value;
                winners.Clear();
            }

            if (value == bestValue)
                winners.Add(player);
        }

        return winners;
    }

    private static int GetStat(
        MatchStatsManager stats,
        PlayerNetwork player,
        AwardStat stat)
    {
        ulong clientId = player.OwnerClientId;

        return stat switch
        {
            AwardStat.Kills => stats.GetKills(clientId),
            AwardStat.FlagsDelivered =>
                stats.GetFlagsDelivered(clientId),
            AwardStat.Deaths => stats.GetDeaths(clientId),
            AwardStat.Counters => stats.GetCounters(clientId),
            _ => 0
        };
    }

    private static string GetDisplayName(PlayerNetwork player)
    {
        return string.IsNullOrWhiteSpace(player.PlayerName)
            ? "Player " + (player.OwnerClientId + 1)
            : player.PlayerName;
    }
}
