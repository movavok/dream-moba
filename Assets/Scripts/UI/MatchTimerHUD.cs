using TMPro;
using UnityEngine;

public class MatchTimerHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Vector2 anchoredPosition = new(0f, -45f);

    private void Awake()
    {
        if (timerText != null)
            return;

        Debug.LogError(
            "[MatchTimerHUD] Timer text is not assigned. " +
            "Assign the MatchTimer TextMeshProUGUI child in the scene."
        );
    }

    private void Update()
    {
        if (timerText == null)
            return;

        MatchStatsManager manager = MatchStatsManager.Instance;

        if (manager == null || !manager.IsSpawned)
        {
            timerText.text = "00:00";
            return;
        }

        int totalSeconds = Mathf.Max(
            0,
            Mathf.FloorToInt(manager.MatchTime)
        );

        timerText.text = string.Format(
            "{0:00}:{1:00}",
            totalSeconds / 60,
            totalSeconds % 60
        );
    }
}
