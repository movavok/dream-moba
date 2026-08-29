using UnityEngine;
using UnityEngine.InputSystem;

public class ScoreboardInput : MonoBehaviour
{
    [SerializeField] private InputActionReference openAction;
    [SerializeField] private Animator animator;

    private void OnEnable()
    {
        openAction.action.performed += OnTab;
        openAction.action.Enable();
    }

    private void OnDisable()
    {
        openAction.action.performed -= OnTab;
        openAction.action.Disable();
    }

    private void OnTab(InputAction.CallbackContext context)
    {
        bool isOpen = animator.GetBool("Open");
        animator.SetBool("Open", !isOpen);
    }
}