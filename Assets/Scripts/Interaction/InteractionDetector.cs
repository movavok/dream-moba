using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerNetwork))]
public class InteractionDetector : MonoBehaviour
{
    [SerializeField] private float interactionRadius = 1.5f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    private PlayerNetwork player;
    private InputAction interactAction;

    private IInteractable currentInteractable;
    private NetworkObject currentNetworkObject;
    private IInteractable currentHighlighted;
    private IInteractable currentPrompted;
    private readonly List<Collider2D> hits = new(16);
    private readonly List<MonoBehaviour> interactionComponents = new(16);

    // Use the same physics query as the local highlight/prompt when the server
    // validates an interaction or a container transfer.
    public bool IsWithinInteractionRange(NetworkObject target, IInteractable interactable)
    {
        if (target == null || interactable == null)
            return false;

        QueryNearbyColliders();
        foreach (Collider2D hit in hits)
            if (hit.GetComponentInParent<NetworkObject>() == target &&
                ReferenceEquals(FindInteractable(hit), interactable))
                return true;

        return false;
    }

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
        FindNearestInteractables(
            out IInteractable available,
            out NetworkObject availableNetworkObject,
            out IInteractable nearest);

        if (nearest != currentHighlighted)
        {
            currentHighlighted?.SetHighlighted(false);
            currentHighlighted = nearest;
            currentHighlighted?.SetHighlighted(true);
        }

        currentInteractable = available;
        currentNetworkObject = availableNetworkObject;

        if (available == null)
        {
            if (currentPrompted != null)
            {
                currentPrompted = null;
                InteractionPrompt.Hide();
            }

            return;
        }

        if (available == currentPrompted &&
            available is not IDynamicInteractionPrompt)
            return;

        currentPrompted = available;
        InteractionPrompt.Show(
            available,
            GetKeyboardBindingDisplayString()
        );
    }

    private void FindNearestInteractables(
        out IInteractable available,
        out NetworkObject availableNetworkObject,
        out IInteractable nearest)
    {
        available = null;
        availableNetworkObject = null;
        nearest = null;

        float availableDistance = float.MaxValue;
        float nearestDistance = float.MaxValue;

        QueryNearbyColliders();

        foreach (Collider2D hit in hits)
        {
            IInteractable interactable = FindInteractable(hit);
            if (interactable == null)
                continue;

            NetworkObject networkObject =
                hit.GetComponentInParent<NetworkObject>();
            if (networkObject == null)
                continue;

            float distance =
                (hit.transform.position - transform.position).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearest = interactable;
                nearestDistance = distance;
            }

            if (distance < availableDistance &&
                interactable.CanInteract(player))
            {
                available = interactable;
                availableNetworkObject = networkObject;
                availableDistance = distance;
            }
        }
    }

    private void QueryNearbyColliders()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(interactionLayers);
        filter.useTriggers = Physics2D.queriesHitTriggers;
        Physics2D.OverlapCircle(transform.position, interactionRadius, filter, hits);
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

    private IInteractable FindInteractable(Collider2D hit)
    {
        hit.GetComponentsInParent(false, interactionComponents);
        foreach (MonoBehaviour component in interactionComponents)
        {
            if (component is IInteractable interactable)
                return interactable;
        }

        return null;
    }

    private void OnDisable()
    {
        currentHighlighted?.SetHighlighted(false);
        currentHighlighted = null;
        currentInteractable = null;
        currentNetworkObject = null;
        currentPrompted = null;
        InteractionPrompt.Hide();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
