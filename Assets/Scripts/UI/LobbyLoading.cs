using UnityEngine;

public class LobbyLoadingUI : MonoBehaviour
{
    [SerializeField] private GameObject overlay;

    private void Start()
    {
        if (NetworkSessionManager.Instance != null)
        {
            NetworkSessionManager.Instance.RegisterLoadingUI(this);
        }
    }

    public void Show()
    {
        overlay.SetActive(true);
    }

    public void Hide()
    {
        overlay.SetActive(false);
    }
}