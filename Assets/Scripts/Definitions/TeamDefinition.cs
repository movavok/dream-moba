using UnityEngine;

[CreateAssetMenu(
    fileName = "TeamDefinition",
    menuName = "Game/Team Definition"
)]
public class TeamDefinition : ScriptableObject
{
    [System.Serializable]
    public class TeamData
    {
        public short id;
        public string teamName;
        public Color color;
    }

    public TeamData[] teams;

    public TeamData GetTeam(short teamId)
    {
        foreach (TeamData team in teams)
        {
            if (team.id == teamId)
                return team;
        }

        return null;
    }
}