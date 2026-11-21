using TMPro;
using Unity.Netcode;
using UnityEngine;

// Visual-only adapter: each container prefab lays out its own ordered slots.
public class ContainerPanelView : MonoBehaviour
{
    [SerializeField] private AbilitySlot[] slots;
    [SerializeField] private TMP_Text[] stackTexts;

    private ContainerInventory container;

    public int SlotCount => slots != null ? slots.Length : 0;

    public void Bind(ContainerInventory source, ContainerUI ui)
    {
        container = source;
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
                continue;

            ContainerSlotPointer pointer = slots[i].GetComponent<ContainerSlotPointer>();
            if (pointer == null)
                pointer = slots[i].gameObject.AddComponent<ContainerSlotPointer>();
            pointer.Bind(ui, i, true);
        }

        container.Slots.OnListChanged += OnSlotsChanged;
        Refresh();
    }

    private void OnSlotsChanged(NetworkListEvent<InventorySlot> change)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (container == null)
            return;

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
                continue;

            InventorySlot content = i < container.Slots.Count
                ? container.Slots[i] : InventorySlot.Empty;
            ItemDefinition item = container.Database != null
                ? container.Database.GetByIndex(content.itemIndex) : null;
            slots[i].SetIcon(item != null ? item.icon : null);
            slots[i].SetPlaceholder(item != null && item.icon == null &&
                !string.IsNullOrEmpty(item.id) ? item.id[..1].ToUpperInvariant() : null);
            slots[i].SetTooltipData(item != null ? item.displayName : null,
                item != null ? item.description : null);
            slots[i].SetKey("");
            slots[i].SetReady();

            if (stackTexts != null && i < stackTexts.Length && stackTexts[i] != null)
            {
                stackTexts[i].text = content.count > 1 ? content.count.ToString() : "";
                stackTexts[i].gameObject.SetActive(content.count > 1);
            }

        }
    }

    private void OnDestroy()
    {
        if (container != null)
            container.Slots.OnListChanged -= OnSlotsChanged;
    }
}
