using UnityEngine;
using TMPro;

public class LobbyCodeUI : MonoBehaviour
{
    [SerializeField] private TMP_Text roomCodeText;

    private void OnEnable()
    {
        UpdateCode();
    }

    public void UpdateCode()
    {
        if (NetworkSessionManager.Instance == null)
            return;

        if (NetworkSessionManager.Instance.CurrentSession == null)
            return;

        roomCodeText.text =
            NetworkSessionManager.Instance.CurrentSession.Code;
    }

    public void CopyCode()
    {
        if (NetworkSessionManager.Instance == null)
            return;

        if (NetworkSessionManager.Instance.CurrentSession == null)
            return;

        string code =
            NetworkSessionManager.Instance.CurrentSession.Code;

        GUIUtility.systemCopyBuffer = code;

        Debug.Log("Room code copied: " + code);
    }
}
