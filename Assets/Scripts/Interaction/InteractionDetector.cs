using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerNetwork))]
public class InteractionDetector : MonoBehaviour
{
    [SerializeField] private float interactionRadius = 1.5f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    private PlayerNetwork player;
    private InputAction interactAction;
    private IInteractable currentInteractable;
    private NetworkObject currentNetworkObject;

    private void Awake()
    {
        player = GetComponent<PlayerNetwork>();

        PlayerInput playerInput = GetComponent<PlayerInput>();

        if (playerInput != null)
        {
            interactAction =
                playerInput.actions.FindAction(
                    "Player/Interact",
                    true
                );
        }
    }

    private void Update()
    {
        if (player == null || !player.IsOwner || !player.IsSpawned)
            return;

        UpdateCurrentInteractable();

        if (currentInteractable != null &&
            currentNetworkObject != null &&
            interactAction != null &&
            interactAction.WasPressedThisFrame())
        {
            player.RequestInteractionServerRpc(currentNetworkObject);
        }
    }

    private void UpdateCurrentInteractable()
    {
        NetworkObject nearestNetworkObject;
        IInteractable nearest =
            FindNearestInteractable(out nearestNetworkObject);

        if (nearest == currentInteractable)
            return;

        currentInteractable?.SetHighlighted(false);
        currentInteractable = nearest;
        currentNetworkObject = nearestNetworkObject;
        currentInteractable?.SetHighlighted(true);

        if (currentInteractable == null)
            InteractionPrompt.Hide();
        else
        {
            string binding = GetKeyboardBindingDisplayString();

            InteractionPrompt.Show(
                currentInteractable,
                binding
            );
        }
    }

    private string GetKeyboardBindingDisplayString()
    {
        if (interactAction == null)
            return "F";

        for (int i = 0; i < interactAction.bindings.Count; i++)
        {
            InputBinding binding = interactAction.bindings[i];

            string path = binding.effectivePath;
            if (!path.Contains("<Keyboard>"))
                continue;

            string display = path;
            int separator = display.LastIndexOf('/');
            if (separator >= 0)
                display = display[(separator + 1)..];

            if (display.Length > 0)
                return display.ToUpperInvariant();

            display =
                interactAction.GetBindingDisplayString(i)
                    .Replace("Press ", string.Empty);
            if (!string.IsNullOrWhiteSpace(display))
                return display;
        }

        return "F";
    }

    private IInteractable FindNearestInteractable(
        out NetworkObject nearestNetworkObject)
    {
        nearestNetworkObject = null;
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            interactionRadius,
            interactionLayers
        );

        IInteractable nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            IInteractable interactable = FindInteractable(hit);
            NetworkObject networkObject =
                hit.GetComponentInParent<NetworkObject>();

            if (interactable == null ||
                networkObject == null ||
                !interactable.CanInteract(player))
                continue;

            float distance =
                (hit.transform.position - transform.position).sqrMagnitude;

            if (distance >= nearestDistance)
                continue;

            nearest = interactable;
            nearestNetworkObject = networkObject;
            nearestDistance = distance;
        }

        return nearest;
    }

    private IInteractable FindInteractable(Collider2D hit)
    {
        MonoBehaviour[] components =
            hit.GetComponentsInParent<MonoBehaviour>();

        foreach (MonoBehaviour component in components)
        {
            if (component is IInteractable interactable)
                return interactable;
        }

        return null;
    }

    private void OnDisable()
    {
        currentInteractable?.SetHighlighted(false);
        currentInteractable = null;
        currentNetworkObject = null;
        InteractionPrompt.Hide();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
