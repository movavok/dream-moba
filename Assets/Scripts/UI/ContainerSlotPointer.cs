using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Shared click and hover behavior for both hero and container HUD slots.
public class ContainerSlotPointer : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private ContainerUI ui;
    private int index;
    private bool fromContainer;
    private Image hover;

    public void Bind(ContainerUI owner, int slotIndex, bool isContainer)
    {
        ui = owner;
        index = slotIndex;
        fromContainer = isContainer;
        if (hover != null)
            return;

        GameObject overlay = new GameObject("Transfer Hover",
            typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)overlay.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        hover = overlay.GetComponent<Image>();
        hover.color = new Color(0f, 0f, 0f, 0.3f);
        hover.raycastTarget = false;
        hover.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hover != null && ui != null && ui.IsOpen)
            hover.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hover != null)
            hover.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || ui == null ||
            !ui.IsOpen)
            return;

        Keyboard keyboard = Keyboard.current;
        bool wholeStack = keyboard != null &&
            (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        ui.Transfer(fromContainer, index, wholeStack);
    }

    private void OnDisable()
    {
        if (hover != null)
            hover.enabled = false;
    }
}
