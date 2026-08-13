using UnityEngine;
using TMPro;

public class JoinRoomUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private LobbyUI lobbyUI;
    [SerializeField] private TMP_InputField nicknameInput;

    private bool isJoining;

    private void Awake()
    {
        if (lobbyUI == null)
            lobbyUI = FindAnyObjectByType<LobbyUI>();
    }

    public async void JoinRoom()
    {
        if (NetworkSessionManager.Instance == null)
        {
            Debug.LogError("JoinRoomUI: NetworkSessionManager is missing.");
            return;
        }

        if (NetworkSessionManager.Instance.IsSessionOperationInProgress)
            return;

        if (roomCodeInput == null)
        {
            Debug.LogError("JoinRoomUI: roomCodeInput is not assigned.");
            return;
        }

        string roomCode = roomCodeInput.text.Trim();

        if (string.IsNullOrEmpty(roomCode))
        {
            Debug.LogWarning("Room code is empty.");
            return;
        }

        bool success =
            await NetworkSessionManager.Instance.JoinRoom(roomCode);

        if (!success)
        {
            Debug.LogWarning("Could not join room.");
            return;
        }

        Debug.Log("Joined room!");

        string nickname = nicknameInput != null
            ? nicknameInput.text
            : "";

        await NetworkSessionManager.Instance.SetNickname(nickname);

        if (lobbyUI == null)
        {
            Debug.LogError("JoinRoomUI: lobbyUI is not assigned.");
            return;
        }

        lobbyUI.ShowLobby();
    }
}