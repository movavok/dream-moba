using UnityEngine;

public class HostUI : MonoBehaviour
{
    [SerializeField] private LobbyUI lobbyUI;

    [SerializeField] private TMPro.TMP_InputField nicknameInput;

    private void Awake()
    {
        if (lobbyUI == null)
            lobbyUI = FindAnyObjectByType<LobbyUI>();
    }

    public async void StartHost()
    {
        if (NetworkSessionManager.Instance != null &&
            NetworkSessionManager.Instance.IsSessionOperationInProgress)
        {
            return;
        }

        if (NetworkSessionManager.Instance == null)
        {
            Debug.LogError("HostUI: NetworkSessionManager is missing.");
            return;
        }

        bool success =
            await NetworkSessionManager.Instance.CreateRoom();

        if (!success)
        {
            if (NetworkSessionManager.Instance.IsSessionOperationInProgress)
            {
                return;
            }

            Debug.LogError("Failed to create room.");
            return;
        }

        if (lobbyUI == null)
        {
            Debug.LogError("HostUI: lobbyUI is not assigned.");
            return;
        }

        string nickname = nicknameInput != null
            ? nicknameInput.text
            : "";

        await NetworkSessionManager.Instance.SetNickname(nickname);

        lobbyUI.ShowLobby();
    }
}
