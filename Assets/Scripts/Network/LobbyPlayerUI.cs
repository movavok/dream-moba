using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text readyText;

    public void Setup(string playerName, bool ready)
    {
        playerNameText.text = playerName;
        readyText.text = ready ? "READY" : "NOT READY";
    }
}
