using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MatchTimerHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Vector2 anchoredPosition = new(0f, -45f);

    [Header("Mini Flag Indicators")]
    [SerializeField] private GameObject miniFlagPrefab;
    [SerializeField] private float baseDistance = 125f;
    [SerializeField] private float flagSpacing = 14f;
    [SerializeField] private Vector2 flagSize = new(32f, 32f);

    private RectTransform team1Container;
    private RectTransform team2Container;
    private readonly List<GameObject> team1Flags = new();
    private readonly List<GameObject> team2Flags = new();
    private int displayedTeam1Score = -1;
    private int displayedTeam2Score = -1;

    private void Awake()
    {
        if (timerText == null)
        {
            Debug.LogError(
                "[MatchTimerHUD] Timer text is not assigned. " +
                "Assign the MatchTimer TextMeshProUGUI child in the scene."
            );
        }

        EnsureContainers();
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

        SyncFlagIndicators(manager.Team1Flags, manager.Team2Flags);
    }

    private void SyncFlagIndicators(int team1Score, int team2Score)
    {
        if (team1Score != displayedTeam1Score)
        {
            UpdateTeamFlags(1, team1Score, team1Container, team1Flags, true);
            displayedTeam1Score = team1Score;
        }

        if (team2Score != displayedTeam2Score)
        {
            UpdateTeamFlags(2, team2Score, team2Container, team2Flags, false);
            displayedTeam2Score = team2Score;
        }
    }

    private void UpdateTeamFlags(
        short teamId,
        int targetCount,
        RectTransform container,
        List<GameObject> flagList,
        bool isLeft)
    {
        EnsureContainers();
        EnsurePrefab();

        if (miniFlagPrefab == null || container == null)
            return;

        Color teamColor = GetTeamColor(teamId);

        while (flagList.Count < targetCount)
        {
            int index = flagList.Count;
            GameObject flag = Instantiate(miniFlagPrefab, container);
            flag.name = $"MiniFlag_{teamId}_{index}";

            if (!flag.TryGetComponent<RectTransform>(out RectTransform rt))
                rt = flag.AddComponent<RectTransform>();

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = flagSize;
            rt.localScale = Vector3.one;

            float xPos = isLeft
                ? -baseDistance - (index * flagSpacing)
                : baseDistance + (index * flagSpacing);

            rt.anchoredPosition = new Vector2(xPos, 0f);

            MiniFlagIndicator indicator =
                flag.GetComponent<MiniFlagIndicator>() ??
                flag.AddComponent<MiniFlagIndicator>();

            indicator.Setup(teamColor, 100 + index);

            // Layer new flags on top of old flags
            flag.transform.SetAsLastSibling();
            flagList.Add(flag);
        }

        while (flagList.Count > targetCount)
        {
            int lastIndex = flagList.Count - 1;
            Destroy(flagList[lastIndex]);
            flagList.RemoveAt(lastIndex);
        }
    }

    private void EnsureContainers()
    {
        if (team1Container == null)
        {
            Transform t1 = transform.Find("Team1Flags");
            if (t1 == null)
            {
                GameObject go = new("Team1Flags", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                t1 = go.transform;
            }
            team1Container = t1.GetComponent<RectTransform>();
            ConfigureContainer(team1Container);
        }

        if (team2Container == null)
        {
            Transform t2 = transform.Find("Team2Flags");
            if (t2 == null)
            {
                GameObject go = new("Team2Flags", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                t2 = go.transform;
            }
            team2Container = t2.GetComponent<RectTransform>();
            ConfigureContainer(team2Container);
        }
    }

    private void ConfigureContainer(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private void EnsurePrefab()
    {
        if (miniFlagPrefab != null)
            return;

        miniFlagPrefab = Resources.Load<GameObject>("miniFlagIndicator");

#if UNITY_EDITOR
        if (miniFlagPrefab == null)
        {
            miniFlagPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/miniFlagIndicator.prefab"
            );
        }
#endif
    }

    private Color GetTeamColor(short teamId)
    {
        TeamManager teamManager = FindAnyObjectByType<TeamManager>();
        if (teamManager != null)
        {
            TeamDefinition.TeamData data = teamManager.GetTeamData(teamId);
            if (data != null)
                return data.color;
        }

        return teamId switch
        {
            1 => new Color(0.187f, 0.288f, 1f, 1f),
            2 => new Color(1f, 0.285f, 0.285f, 1f),
            3 => new Color(0.292f, 0.947f, 0.291f, 1f),
            4 => new Color(0.654f, 0.356f, 0.947f, 1f),
            _ => Color.white
        };
    }
}
