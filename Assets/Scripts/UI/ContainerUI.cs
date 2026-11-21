using System.Collections;
using UnityEngine;

// Only local panel state lives here; authoritative slots live on ContainerInventory.
[RequireComponent(typeof(InventoryHUD))]
public class ContainerUI : MonoBehaviour
{
    public static ContainerUI Instance { get; private set; }

    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;

    private InventoryHUD playerHUD;
    private ContainerInventory opened;
    private ContainerPanelView panel;
    private Coroutine fadeRoutine;

    public bool IsOpen => opened != null && panel != null;

    private void Awake()
    {
        Instance = this;
        playerHUD = GetComponent<InventoryHUD>();
    }

    public void Open(ContainerInventory container)
    {
        if (opened == container && panel != null)
            return;

        if (container == null || container.PanelPrefab == null ||
            container.PanelPrefab.SlotCount != container.Slots.Count ||
            PlayerNetwork.LocalPlayer == null ||
            Vector2.Distance(PlayerNetwork.LocalPlayer.transform.position,
                container.transform.position) > container.InteractionRange)
        {
            container?.CloseForLocalPlayer();
            return;
        }

        Close();
        opened = container;
        panel = Instantiate(container.PanelPrefab, transform.parent, false);
        panel.Bind(container, this);
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null)
            group = panel.gameObject.AddComponent<CanvasGroup>();
        group.alpha = fadeInDuration > 0f ? 0f : 1f;
        if (fadeInDuration > 0f)
            fadeRoutine = StartCoroutine(FadeIn(group));

        for (int i = 0; i < playerHUD.Slots.Length; i++)
        {
            AbilitySlot slot = playerHUD.Slots[i];
            if (slot == null)
                continue;

            ContainerSlotPointer pointer = slot.GetComponent<ContainerSlotPointer>();
            if (pointer == null)
                pointer = slot.gameObject.AddComponent<ContainerSlotPointer>();
            pointer.Bind(this, i, false);
        }

        PlayerNetwork.LocalPlayer.CancelAttackForUI();
        FindAnyObjectByType<LeaveButtonUI>()?.HideButton();
    }

    public void Transfer(bool fromContainer, int slotIndex, bool wholeStack)
    {
        if (!IsOpen || PlayerNetwork.LocalPlayer == null)
            return;

        Inventory inventory = PlayerNetwork.LocalPlayer.GetComponent<Inventory>();
        if (inventory != null)
            opened.RequestTransfer(fromContainer, slotIndex, wholeStack,
                inventory.SelectedSlot.Value);
    }

    public bool TryClose()
    {
        if (!IsOpen)
            return false;

        Close();
        return true;
    }

    public void CloseIfCurrent(ContainerInventory container)
    {
        if (opened == container)
            Close(false);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        PlayerNetwork player = PlayerNetwork.LocalPlayer;
        if (player == null || !player.IsSpawned ||
            player.GetComponent<Health>() is not Health health || !health.IsAlive() ||
            Vector2.Distance(player.transform.position, opened.transform.position) >
                opened.InteractionRange)
            Close();
    }

    public void Close(bool informServer = true)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (informServer && opened != null)
            opened.CloseForLocalPlayer();
        opened = null;
        if (panel != null)
            Destroy(panel.gameObject);
        panel = null;
    }

    private IEnumerator FadeIn(CanvasGroup group)
    {
        float elapsed = 0f;
        while (elapsed < fadeInDuration && group != null)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        if (group != null)
            group.alpha = 1f;
        fadeRoutine = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        Close(false);
    }
}
