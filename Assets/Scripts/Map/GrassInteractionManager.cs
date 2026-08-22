using System.Collections.Generic;
using UnityEngine;

public class GrassInteractionManager : MonoBehaviour
{
    public static GrassInteractionManager Instance { get; private set; }

    [SerializeField] private Material grassMaterial;

    private const int MaxPlayers = 6;

    private readonly List<Transform> players = new();

    private static readonly int PlayerPositionsID =
        Shader.PropertyToID("_PlayerPositions");

    private readonly Vector4[] playerPositions =
        new Vector4[MaxPlayers];

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (i < players.Count && players[i] != null)
            {
                Vector3 p = players[i].position;

                playerPositions[i] =
                    new Vector4(p.x, p.y, 0f, 0f);
            }
            else
            {
                playerPositions[i] =
                    new Vector4(99999f, 99999f, 0f, 0f);
            }
        }

        Shader.SetGlobalVectorArray(
            PlayerPositionsID,
            playerPositions
        );
    }

    public void RegisterPlayer(Transform player)
    {
        if (player != null && !players.Contains(player))
            players.Add(player);
    }

    public void UnregisterPlayer(Transform player)
    {
        players.Remove(player);
    }
}