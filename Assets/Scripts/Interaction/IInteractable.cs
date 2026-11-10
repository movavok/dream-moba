using UnityEngine;

public interface IInteractable
{
    bool CanInteract(PlayerNetwork player);
    string InteractionVerb { get; }
    string InteractionName { get; }
    Transform InteractionPromptTarget { get; }
    void SetHighlighted(bool highlighted);
    void Interact(PlayerNetwork player);
}
