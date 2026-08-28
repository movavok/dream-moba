using UnityEngine;
using UnityEngine.SceneManagement;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Cursors")]
    [SerializeField] private Texture2D lobbyCursor;
    [SerializeField] private Texture2D gameCursor;

    [Header("Hotspots")]
    [SerializeField] private Vector2 lobbyHotspot;
    [SerializeField] private Vector2 gameHotspot;

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "Game";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ApplyCursor(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyCursor(scene.name);
    }

    private void ApplyCursor(string sceneName)
    {
        if (sceneName == gameSceneName)
        {
            SetGameCursor();
        }
        else
        {
            SetLobbyCursor();
        }
    }

    private void SetLobbyCursor()
    {
        Cursor.SetCursor(
            lobbyCursor,
            lobbyHotspot,
            CursorMode.Auto
        );

        Cursor.visible = true;
    }

    private void SetGameCursor()
    {
        Cursor.SetCursor(
            gameCursor,
            gameHotspot,
            CursorMode.Auto
        );

        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}