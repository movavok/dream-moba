using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Multiplayer;
using Unity.Netcode;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject joinRoomPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Lobby Players")]
    [SerializeField] private Transform playersContent;
    [SerializeField] private LobbyPlayerUI playerPrefab;

    [Header("Lobby Code")]
    [SerializeField] private LobbyCodeUI lobbyCodeUI;

    [Header("Ready")]
    [SerializeField] private Button readyButton;
    [SerializeField] private TMPro.TMP_Text readyButtonText;

    [Header("Start")]
    [SerializeField] private GameObject startButtonObject;
    [SerializeField] private UnityEngine.UI.Button startButton;

    [Header("Player Name")]
    [SerializeField] private TMPro.TMP_InputField playerNameInput;

    private readonly List<LobbyPlayerUI> playerItems = new();

    private ISession subscribedSession;

    [SerializeField] private TMP_Dropdown teamModeDropdown;

    private const string TEAM_MODE_PROPERTY = "teamMode";

    public async Task SaveTeamMode()
    {
        ISession session =
            NetworkSessionManager.Instance?.CurrentSession;

        if (session == null)
        {
            Debug.LogError("SaveTeamMode: session is null!");
            return;
        }

        if (!session.IsHost)
        {
            Debug.LogWarning("Only host can change team mode.");
            return;
        }

        TeamAssignmentMode mode =
            (TeamAssignmentMode)teamModeDropdown.value;

        MatchSettings.Instance.SetTeamAssignmentMode(mode);

        try
        {
            IHostSession hostSession = session.AsHost();

            hostSession.SetProperty(
                TEAM_MODE_PROPERTY,
                new SessionProperty(
                    mode.ToString()
                )
            );

            await hostSession.SavePropertiesAsync();

            Debug.Log(
                $"Team mode saved to lobby: {mode}"
            );
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                $"Failed to save team mode: {e}"
            );
        }
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");

        Application.Quit();

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    private void UpdateTeamModeDropdown()
    {
        ISession session =
            NetworkSessionManager.Instance?.CurrentSession;

        if (session == null)
            return;

        teamModeDropdown.interactable = session.IsHost;

        if (!session.Properties.TryGetValue(
            TEAM_MODE_PROPERTY,
            out SessionProperty property))
        {
            return;
        }

        if (!System.Enum.TryParse(
            property.Value,
            out TeamAssignmentMode mode))
        {
            return;
        }

        teamModeDropdown.SetValueWithoutNotify(
            (int)mode
        );

        MatchSettings.Instance.SetTeamAssignmentMode(mode);

        Debug.Log(
            $"TEAM MODE FROM SESSION: {property.Value}"
        );
    }

    public async void OnTeamModeChanged(int value)
    {
        ISession session =
            NetworkSessionManager.Instance?.CurrentSession;

        if (session == null)
            return;

        if (!session.IsHost)
            return;

        TeamAssignmentMode mode =
            (TeamAssignmentMode)value;

        Debug.Log($"Dropdown changed by HOST: {mode}");

        MatchSettings.Instance.SetTeamAssignmentMode(mode);

        try
        {
            IHostSession hostSession = session.AsHost();

            hostSession.SetProperty(
                TEAM_MODE_PROPERTY,
                new SessionProperty(mode.ToString())
            );

            await hostSession.SavePropertiesAsync();

            Debug.Log($"TEAM MODE SAVED TO SESSION: {mode}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save team mode: {e}");
        }
    }

    private void Start()
    {
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        UnsubscribeFromSession();

        mainMenu.SetActive(true);
        joinRoomPanel.SetActive(false);
        lobbyPanel.SetActive(false);

        if (startButtonObject != null)
            startButtonObject.SetActive(false);
    }

    public void ShowJoinRoom()
    {
        UnsubscribeFromSession();

        mainMenu.SetActive(false);
        joinRoomPanel.SetActive(true);
        lobbyPanel.SetActive(false);
    }

    public void ShowLobby()
    {
        mainMenu.SetActive(false);
        joinRoomPanel.SetActive(false);
        lobbyPanel.SetActive(true);

        ISession session =
            NetworkSessionManager.Instance?.CurrentSession;

        if (session == null)
        {
            Debug.LogError("LobbyUI: CurrentSession is null!");
            return;
        }

        SubscribeToSession(session);

        RefreshPlayers();
        UpdateOwnReadyButton();
        UpdateTeamModeDropdown();

        if (lobbyCodeUI != null)
            lobbyCodeUI.UpdateCode();
    }

    private void SubscribeToSession(ISession session)
    {
        UnsubscribeFromSession();

        subscribedSession = session;

        subscribedSession.PlayerJoined += OnPlayerJoined;
        subscribedSession.PlayerHasLeft += OnPlayerLeft;
        subscribedSession.PlayerPropertiesChanged += OnPlayerPropertiesChanged;
        subscribedSession.SessionPropertiesChanged += OnSessionPropertiesChanged;

        Debug.Log("LobbyUI subscribed to session events.");
    }

    private void UnsubscribeFromSession()
    {
        if (subscribedSession == null)
            return;

        subscribedSession.PlayerJoined -= OnPlayerJoined;
        subscribedSession.PlayerHasLeft -= OnPlayerLeft;
        subscribedSession.PlayerPropertiesChanged -= OnPlayerPropertiesChanged;
        subscribedSession.SessionPropertiesChanged -= OnSessionPropertiesChanged;

        subscribedSession = null;
    }

    private void OnSessionPropertiesChanged()
    {
        Debug.Log("Session properties changed.");

        UpdateTeamModeDropdown();
    }

    private void OnPlayerJoined(string playerId)
    {
        Debug.Log("Player joined lobby: " + playerId);

        RefreshPlayers();
    }

    private void OnPlayerLeft(string playerId)
    {
        Debug.Log("Player left lobby: " + playerId);

        RefreshPlayers();
    }

    private void OnPlayerPropertiesChanged()
    {
        Debug.Log("Player properties changed.");

        RefreshPlayers();
        UpdateOwnReadyButton();
    }

    public void RefreshPlayers()
    {
        foreach (LobbyPlayerUI item in playerItems)
        {
            Destroy(item.gameObject);
        }

        playerItems.Clear();

        ISession session = NetworkSessionManager.Instance.CurrentSession;

        if (session == null)
        {
            Debug.LogError("No current session!");
            return;
        }

        Debug.Log("Players count: " + session.Players.Count);
        Debug.Log("playersContent: " + playersContent);
        Debug.Log("playerPrefab: " + playerPrefab);

        int playerNumber = 1;

        foreach (IReadOnlyPlayer player in session.Players)
        {
            LobbyPlayerUI item = Instantiate(
                playerPrefab,
                playersContent
            );

            Debug.Log(
                "CREATED: " +
                item.name +
                " | parent: " +
                item.transform.parent.name
            );

            string playerName;

            if (player.Properties.TryGetValue(
                "nickname",
                out PlayerProperty nicknameProperty) &&
                !string.IsNullOrWhiteSpace(nicknameProperty.Value))
            {
                playerName = nicknameProperty.Value;
            }
            else
            {
                playerName = "Player " + playerNumber;
            }

            bool ready = false;

            if (player.Properties.TryGetValue(
                "ready",
                out PlayerProperty readyProperty))
            {
                ready = readyProperty.Value == "true";
            }

            item.Setup(playerName, ready);

            playerItems.Add(item);

            playerNumber++;
        }

        UpdateStartButton();
        Debug.Log("Lobby players refreshed: " + playerItems.Count);
    }

    private void OnDestroy()
    {
        UnsubscribeFromSession();
    }

    public async void ToggleReady()
    {
        ISession session = NetworkSessionManager.Instance?.CurrentSession;

        if (session == null)
        {
            Debug.LogError("ToggleReady: no current session!");
            return;
        }

        if (session.CurrentPlayer == null)
        {
            Debug.LogError("ToggleReady: CurrentPlayer is null!");
            return;
        }

        bool currentReady = false;

        if (session.CurrentPlayer.Properties.TryGetValue(
            "ready",
            out PlayerProperty readyProperty))
        {
            currentReady = readyProperty.Value == "true";
        }

        bool newReady = !currentReady;

        session.CurrentPlayer.SetProperty(
            "ready",
            new PlayerProperty(newReady.ToString().ToLower())
        );

        try
        {
            await session.SaveCurrentPlayerDataAsync();

            Debug.Log("Ready changed to: " + newReady);

            UpdateReadyButton(newReady);
            UpdateStartButton();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to save ready state: " + e);
        }
    }

    private void UpdateReadyButton(bool ready)
    {
        if (readyButtonText != null)
        {
            readyButtonText.text = ready ? "UNREADY" : "READY";
        }
    }

    private void UpdateOwnReadyButton()
    {
        ISession session = NetworkSessionManager.Instance?.CurrentSession;

        if (session == null || session.CurrentPlayer == null)
            return;

        bool ready = false;

        if (session.CurrentPlayer.Properties.TryGetValue(
            "ready",
            out PlayerProperty readyProperty))
        {
            ready = readyProperty.Value == "true";
        }

        UpdateReadyButton(ready);
    }

    private bool AreAllPlayersReady()
    {
        ISession session = NetworkSessionManager.Instance?.CurrentSession;

        if (session == null || session.Players.Count == 0)
            return false;

        foreach (IReadOnlyPlayer player in session.Players)
        {
            if (!player.Properties.TryGetValue(
                "ready",
                out PlayerProperty readyProperty))
            {
                return false;
            }

            if (readyProperty.Value != "true")
                return false;
        }

        return true;
    }

    private void UpdateStartButton()
    {
        if (startButtonObject == null || startButton == null)
            return;

        ISession session = NetworkSessionManager.Instance?.CurrentSession;

        if (session == null)
        {
            startButtonObject.SetActive(false);
            return;
        }

        // Only the host can see the start button
        bool isHost = session.IsHost;

        startButtonObject.SetActive(isHost);

        // Enable the start button only if all players are ready
        if (isHost)
        {
            startButton.interactable = AreAllPlayersReady();
        }
    }

    public async void SaveNickname()
    {
        if (playerNameInput == null)
            return;

        await NetworkSessionManager.Instance.SetNickname(playerNameInput.text);
    }

    public async void StartGame()
    {
        if (!AreAllPlayersReady())
        {
            Debug.LogWarning("Cannot start: not all players are ready.");
            return;
        }

        if (NetworkSessionManager.Instance == null)
        {
            Debug.LogError("NetworkSessionManager is missing.");
            return;
        }

        Debug.Log("NetworkManager exists: " + (NetworkManager.Singleton != null));

        if (NetworkManager.Singleton != null)
        {
            Debug.Log("IsHost: " + NetworkManager.Singleton.IsHost);
            Debug.Log("IsClient: " + NetworkManager.Singleton.IsClient);
            Debug.Log("IsServer: " + NetworkManager.Singleton.IsServer);
        }

        await SaveTeamMode();

        await NetworkSessionManager.Instance.StartGame();
    }

    public async void LeaveLobby()
    {
        Debug.Log("Leave lobby button pressed.");

        if (NetworkSessionManager.Instance == null)
        {
            Debug.LogError("LeaveLobby: NetworkSessionManager.Instance is null!");
            return;
        }

        await NetworkSessionManager.Instance.LeaveRoom();

        Debug.Log("Lobby left. Returning to main menu.");

        ShowMainMenu();
    }
}
