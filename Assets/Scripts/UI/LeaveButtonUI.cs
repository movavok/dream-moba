using UnityEngine;
using UnityEngine.InputSystem;

public class LeaveButtonUI : MonoBehaviour
{
    [SerializeField] private GameObject leaveButton;
    [SerializeField] private InputActionReference escapeAction;

    private void OnEnable()
    {
        escapeAction.action.performed += OnEscape;
        escapeAction.action.Enable();
    }

    private void OnDisable()
    {
        escapeAction.action.performed -= OnEscape;
        escapeAction.action.Disable();
    }

    private void Start()
    {
        leaveButton.SetActive(false);
    }

    private void OnEscape(InputAction.CallbackContext context)
    {
        leaveButton.SetActive(!leaveButton.activeSelf);
    }

    public async void LeaveGame()
    {
        leaveButton.SetActive(false);

        if (NetworkSessionManager.Instance == null)
        {
            Debug.LogError("LeaveButtonUI: NetworkSessionManager not found!");
            return;
        }

        await NetworkSessionManager.Instance.LeaveGame();
    }
}