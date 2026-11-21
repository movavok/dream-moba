using Unity.Netcode;
using UnityEngine;

// Shared world interaction for backpacks and future containers.
[RequireComponent(typeof(NetworkObject), typeof(CircleCollider2D))]
public class ContainerInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionVerb = "Open";
    [SerializeField] private string interactionName = "backpack";
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private InteractionOutline outline;

    private NetworkObject networkObject;
    private ContainerInventory container;

    public string InteractionVerb => interactionVerb;
    public string InteractionName => interactionName;
    public Transform InteractionPromptTarget => transform;

    private void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        container = GetComponent<ContainerInventory>();
        if (GetComponent<ItemSorting>() == null)
            gameObject.AddComponent<ItemSorting>();
        if (outline != null && visual != null)
            outline.Configure(visual);
    }

    public bool CanInteract(PlayerNetwork player)
    {
        return networkObject != null && networkObject.IsSpawned &&
            player != null && player.IsSpawned;
    }

    public void SetHighlighted(bool highlighted)
    {
        outline?.SetHighlighted(highlighted);
    }

    public void Interact(PlayerNetwork player)
    {
        container?.Open(player);
    }

    private void OnDisable()
    {
        outline?.SetHighlighted(false);
    }
}
