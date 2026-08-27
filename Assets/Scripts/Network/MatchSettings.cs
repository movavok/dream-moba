using UnityEngine;

public class MatchSettings : MonoBehaviour
{
    public static MatchSettings Instance { get; private set; }

    public TeamAssignmentMode TeamAssignmentMode { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void SetTeamAssignmentMode(TeamAssignmentMode mode)
    {
        TeamAssignmentMode = mode;
    }
}