using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class NetworkSessionManager : MonoBehaviour
{
    public static NetworkSessionManager Instance { get; private set; }

    public ISession CurrentSession { get; private set; }
    public bool IsSessionOperationInProgress { get; private set; }

    public bool HostLeftGame { get; private set; }

    private bool isLeavingGame;

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        DontDestroyOnLoad(gameObject);

        await InitializeServices();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return;

        // Сервер обнаружил отключившегося игрока
        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log(
                $"Client disconnected from server: {clientId}"
            );

            if (RespawnManager.Instance != null)
            {
                RespawnManager.Instance.PlayerDisconnected(clientId);
            }

            ScoreboardUI scoreboard = FindFirstObjectByType<ScoreboardUI>();

            if (scoreboard != null)
            {
                scoreboard.OnPlayerLeft(clientId);
            }

            return;
        }

        // Клиент обновляет табло при выходе любого игрока
        ScoreboardUI clientScoreboard =
            FindFirstObjectByType<ScoreboardUI>();

        if (clientScoreboard != null)
        {
            clientScoreboard.OnPlayerLeft(clientId);
        }

        // Если отключился именно хост — возвращаемся в лобби
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        if (isLeavingGame)
            return;

        Debug.Log(
            "Host disconnected. Returning to LobbyScene."
        );

        HostLeftGame = true;
        CurrentSession = null;

        SceneManager.LoadScene("LobbyScene");
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private async Task InitializeServices()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log("Unity Services initialized.");
        }
        catch (Exception e)
        {
            Debug.LogError("Services initialization failed: " + e);
        }
    }

    public async Task<bool> CreateRoom()
    {
        if (IsSessionOperationInProgress)
        {
            Debug.LogWarning("CreateRoom ignored: session operation already in progress.");
            return false;
        }

        try
        {
            IsSessionOperationInProgress = true;

            SessionOptions options = new SessionOptions
            {
                MaxPlayers = 6
            }.WithRelayNetwork();

            CurrentSession =
                await MultiplayerService.Instance.CreateSessionAsync(options);

            Debug.Log("Room created!");
            Debug.Log("Unity Room Code: " + CurrentSession.Code);

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to create room: " + e);
            return false;
        }
        finally
        {
            IsSessionOperationInProgress = false;
        }
    }

    public async Task<bool> JoinRoom(string roomCode)
    {
        if (IsSessionOperationInProgress)
        {
            Debug.LogWarning("JoinRoom ignored: operation already in progress.");
            return false;
        }

        if (CurrentSession != null)
        {
            Debug.LogWarning("JoinRoom ignored: already in a session.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(roomCode))
        {
            Debug.LogWarning("JoinRoom: empty room code.");
            return false;
        }

        try
        {
            IsSessionOperationInProgress = true;

            roomCode = roomCode.Trim().ToUpper();

            JoinSessionOptions options = new JoinSessionOptions();

            CurrentSession =
                await MultiplayerService.Instance.JoinSessionByCodeAsync(
                    roomCode,
                    options
                );

            Debug.Log("Joined room!");
            Debug.Log("Room Code: " + CurrentSession.Code);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed to join room: " + e.Message);
            return false;
        }
        finally
        {
            IsSessionOperationInProgress = false;
        }
    }

    public async Task SetNickname(string nickname)
    {
        if (CurrentSession == null || CurrentSession.CurrentPlayer == null)
        {
            Debug.LogError("SetNickname: no active session or player.");
            return;
        }

        nickname = nickname.Trim();

        CurrentSession.CurrentPlayer.SetProperty(
            "nickname",
            new PlayerProperty(nickname)
        );

        try
        {
            await CurrentSession.SaveCurrentPlayerDataAsync();

            Debug.Log(
                string.IsNullOrEmpty(nickname)
                    ? "Nickname cleared."
                    : "Nickname saved: " + nickname
            );
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save nickname: " + e);
        }
    }

    public bool IsHost
    {
        get
        {
            return CurrentSession is IHostSession;
        }
    }

    public Task StartGame()
    {
        if (CurrentSession == null)
        {
            Debug.LogError("StartGame: no active session.");
            return Task.CompletedTask;
        }

        if (!IsHost)
        {
            Debug.LogWarning("Only host can start the game.");
            return Task.CompletedTask;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("StartGame: NetworkManager.Singleton is null.");
            return Task.CompletedTask;
        }

        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogError("StartGame: NetworkManager is not running as Host.");
            return Task.CompletedTask;
        }

        Debug.Log("Starting game...");

        NetworkManager.Singleton.SceneManager.LoadScene(
            "GameScene",
            LoadSceneMode.Single
        );

        return Task.CompletedTask;
    }

    public async Task LeaveRoom()
    {
        ISession session = CurrentSession;

        if (session == null)
        {
            Debug.LogWarning("LeaveRoom: no active session.");
            return;
        }

        string playerId = session.CurrentPlayer?.Id ?? "unknown";

        Debug.Log($"Leaving room... Player: {playerId}");

        try
        {
            await session.LeaveAsync();

            Debug.Log($"Successfully left room. Player: {playerId}");

            CurrentSession = null;
        }
        catch (SessionException e)
        {
            Debug.LogError($"Failed to leave room: {e.Message}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Unexpected error while leaving room: {e}");
        }
    }

    public async Task LeaveGame()
    {
        if (isLeavingGame)
            return;

        isLeavingGame = true;
        HostLeftGame = false;

        Debug.Log("Leaving game...");

        try
        {
            ISession session = CurrentSession;

            // Сначала отключаем Netcode.
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                Debug.Log("Shutting down NetworkManager...");

                NetworkManager.Singleton.Shutdown();

                // Даём Netcode закончить отключение.
                await Task.Yield();
            }

            // Затем покидаем Unity Multiplayer Session.
            if (session != null)
            {
                string playerId =
                    session.CurrentPlayer?.Id ?? "unknown";

                Debug.Log($"Leaving session. Player: {playerId}");

                await session.LeaveAsync();

                Debug.Log(
                    $"Successfully left game session. Player: {playerId}"
                );
            }

            CurrentSession = null;

            SceneManager.LoadScene("LobbyScene");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to leave game: {e}");

            CurrentSession = null;

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SceneManager.LoadScene("LobbyScene");
        }
        finally
        {
            isLeavingGame = false;
        }
    }


}
