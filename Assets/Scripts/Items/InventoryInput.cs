using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerNetwork), typeof(Inventory))]
public class InventoryInput : MonoBehaviour
{
    private PlayerNetwork player;
    private Inventory inventory;

    private void Awake()
    {
        player = GetComponent<PlayerNetwork>();
        inventory = GetComponent<Inventory>();
    }

    private void Update()
    {
        if (player == null || inventory == null || !player.IsOwner ||
            !player.IsSpawned || !inventory.IsSpawned || Keyboard.current == null)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            inventory.SelectSlot(0);
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            inventory.SelectSlot(1);
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            inventory.SelectSlot(2);
        else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
            inventory.SelectSlot(3);

        if (keyboard.spaceKey.wasPressedThisFrame)
            inventory.RequestUseServerRpc();
    }
}
