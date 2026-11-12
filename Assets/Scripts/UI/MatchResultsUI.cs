using System.Collections;
using UnityEngine;

public class MatchResultsUI : MonoBehaviour
{
    [SerializeField] private MatchResultsView view;
    [SerializeField] private Color victoryColor = new(0.30f, 0.85f, 0.35f);
    [SerializeField] private Color defeatColor = new(0.92f, 0.25f, 0.25f);
    [SerializeField] private string victoryTitle = "<<VICTORY>>";
    [SerializeField] private string defeatTitle = "<<DEFEAT>>";
    [SerializeField] private string personalVictoryText = "Good\nboy!";
    [SerializeField] private string personalDefeatText = "Bad\nboy!";
    [SerializeField] private string noWinnerText = "NO WINNER";
    [SerializeField] private float countdownDuration = 10f;
    [SerializeField] private float fadeInDuration = 0.6f;

    private CanvasGroup group;
    private bool isShown;
    private bool transitionStarted;

    private void Awake()
    {
        if (view == null)
            view = GetComponent<MatchResultsView>();

        if (view == null)
            view = GetComponentInChildren<MatchResultsView>(true);

        group = GetComponent<CanvasGroup>();

        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();

        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenStatsReady());
    }

    private void OnDisable()
    {
        if (MatchStatsManager.Instance != null)
            MatchStatsManager.Instance.MatchEnded -= OnMatchEnded;
    }

    private IEnumerator SubscribeWhenStatsReady()
    {
        while (MatchStatsManager.Instance == null)
            yield return null;

        MatchStatsManager.Instance.MatchEnded -= OnMatchEnded;
        MatchStatsManager.Instance.MatchEnded += OnMatchEnded;

        if (!isShown && MatchStatsManager.Instance.HasMatchEnded)
            OnMatchEnded(MatchStatsManager.Instance.WinnerTeamId);
    }

    private void OnMatchEnded(short winnerTeamId)
    {
        if (isShown)
            return;

        isShown = true;

        if (view == null)
            return;

        group.blocksRaycasts = true;
        group.interactable = true;

        ApplyOutcome(winnerTeamId);
        view.FillAwards(noWinnerText);
        view.FillPersonalCard(GetPersonalResultText(winnerTeamId));

        StartCoroutine(FadeIn());
    }

    private void ApplyOutcome(short winnerTeamId)
    {
        bool isVictory = IsLocalPlayerOnTeam(winnerTeamId);
        Color outcome = isVictory ? victoryColor : defeatColor;
        string title = isVictory ? victoryTitle : defeatTitle;

        if (winnerTeamId <= 0)
        {
            outcome = Color.white;
            title = noWinnerText;
        }

        view.ApplyOutcome(outcome, title);
    }

    private string GetPersonalResultText(short winnerTeamId)
    {
        if (winnerTeamId <= 0)
            return personalDefeatText;

        return IsLocalPlayerOnTeam(winnerTeamId)
            ? personalVictoryText
            : personalDefeatText;
    }

    private void Update()
    {
        if (!isShown || transitionStarted)
            return;

        MatchStatsManager stats = MatchStatsManager.Instance;

        if (stats == null || stats.MatchEndTime < 0f)
            return;

        float remaining =
            (stats.MatchEndTime + countdownDuration) - stats.MatchTime;

        if (remaining <= 0f)
        {
            transitionStarted = true;

            if (NetworkSessionManager.Instance != null)
                _ = NetworkSessionManager.Instance.LeaveGame();
        }
    }

    private static bool IsLocalPlayerOnTeam(short teamId)
    {
        PlayerNetwork localPlayer = PlayerNetwork.LocalPlayer;

        if (localPlayer == null)
            return false;

        Team team = localPlayer.GetComponent<Team>();

        return team != null && team.TeamId.Value == teamId;
    }

    private IEnumerator FadeIn()
    {
        float time = 0f;

        while (time < fadeInDuration)
        {
            time += Time.unscaledDeltaTime;

            group.alpha = Mathf.Clamp01(time / fadeInDuration);

            yield return null;
        }

        group.alpha = 1f;
    }
}
