using TMPro;
using UnityEngine;

public class InventoryHUD : MonoBehaviour
{
    [Header("Slots (1-4)")]
    [SerializeField] private AbilitySlot[] slots = new AbilitySlot[Inventory.SlotCount];
    [SerializeField] private TMP_Text[] stackTexts = new TMP_Text[Inventory.SlotCount];

    [Header("Selection appearance")]
    [SerializeField] private Sprite normalFrame;
    [SerializeField] private Sprite selectedFrame;
    [SerializeField] private Color normalKeyColor = Color.white;
    [SerializeField] private Color selectedKeyColor = Color.yellow;

    private Inventory inventory;
    private Sprite[] originalFrames;

    private void Awake()
    {
        originalFrames = new Sprite[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            if (i < stackTexts.Length && stackTexts[i] == null)
            {
                foreach (TMP_Text text in slots[i].GetComponentsInChildren<TMP_Text>(true))
                    if (text.gameObject.name == "StackText")
                    {
                        stackTexts[i] = text;
                        break;
                    }
            }

            originalFrames[i] = slots[i].FrameSprite;
            slots[i].SetKey((i + 1).ToString());
            slots[i].SetReady();
        }

        // A hand-drawn scene slot can omit the prefab's cooldown label.
        TMP_Text template = null;
        foreach (AbilitySlot slot in slots)
            if (slot != null && slot.CooldownTextTemplate != null)
            {
                template = slot.CooldownTextTemplate;
                break;
            }

        foreach (AbilitySlot slot in slots)
            if (slot != null)
                slot.EnsureCooldownText(template);

        Refresh();
    }

    private void OnEnable()
    {
        PlayerNetwork.LocalPlayerSpawned += OnLocalPlayerSpawned;
        if (PlayerNetwork.LocalPlayer != null)
            OnLocalPlayerSpawned(PlayerNetwork.LocalPlayer);
    }

    private void OnDisable()
    {
        PlayerNetwork.LocalPlayerSpawned -= OnLocalPlayerSpawned;
        SetInventory(null);
    }

    private void OnLocalPlayerSpawned(PlayerNetwork player)
    {
        SetInventory(player.GetComponent<Inventory>());
    }

    public void SetInventory(Inventory value)
    {
        if (inventory != null)
        {
            inventory.InventoryChanged -= Refresh;
            inventory.SelectionChanged -= Refresh;
            inventory.ItemUsed -= OnItemUsed;
        }

        inventory = value;

        if (inventory != null)
        {
            inventory.InventoryChanged += Refresh;
            inventory.SelectionChanged += Refresh;
            inventory.ItemUsed += OnItemUsed;
        }

        Refresh();
    }

    private void OnItemUsed(int index)
    {
        if (index >= 0 && index < slots.Length && slots[index] != null)
            slots[index].PlayPress();

        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            AbilitySlot slot = slots[i];
            if (slot == null)
                continue;

            ItemDefinition item = inventory != null ? inventory.GetItem(i) : null;
            int count = inventory != null ? inventory.GetSlot(i).count : 0;
            bool selected = inventory != null && inventory.SelectedSlot.Value == i;

            slot.SetIcon(item != null ? item.icon : null);
            slot.SetTooltipData(
                item != null ? item.displayName : null,
                item != null ? item.description : null
            );
            slot.SetFrame(selected && selectedFrame != null
                ? selectedFrame : normalFrame != null ? normalFrame : originalFrames[i]);
            slot.SetKeyColor(selected ? selectedKeyColor : normalKeyColor);

            if (i < stackTexts.Length && stackTexts[i] != null)
            {
                stackTexts[i].text = count > 1 ? count.ToString() : "";
                stackTexts[i].gameObject.SetActive(count > 1);
            }
        }
    }

    private void Update()
    {
        if (inventory != null && !inventory.IsSpawned)
            SetInventory(null);

        for (int i = 0; i < slots.Length; i++)
        {
            AbilitySlot slot = slots[i];
            if (slot == null)
                continue;

            ItemDefinition item = inventory != null ? inventory.GetItem(i) : null;
            if (item != null && item.useAction != null && item.useCooldown > 0f)
                slot.SetCooldown(inventory.GetCooldownRemaining(i), item.useCooldown);
            else
                slot.SetReady();
        }
    }
}
