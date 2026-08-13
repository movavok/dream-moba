using UnityEngine;
using TMPro;

public class NetworkStarter : MonoBehaviour
{
    public GameObject MainMenu;

    [Header("Relay UI")]
    public TMP_InputField JoinCodeInput;
    public TMP_Text JoinCodeText;

    public async void StartHost()
    {
        string joinCode =
            await RelayManager.Instance.StartHost();

        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogError("Could not create Relay game.");
            return;
        }

        JoinCodeText.text = joinCode;

        MainMenu.SetActive(false);

        Debug.Log("Your Join Code: " + joinCode);
    }

    public async void StartClient()
    {
        string joinCode = JoinCodeInput.text.Trim();

        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogError("Enter Join Code.");
            return;
        }

        bool success =
            await RelayManager.Instance.StartClient(joinCode);

        if (!success)
        {
            Debug.LogError("Could not join Relay game.");
            return;
        }

        MainMenu.SetActive(false);
    }
}
