using UnityEngine;

// Local lifetime/range gate; authoritative purchase lives on ShopChest.
public sealed class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }
    public bool IsOpen => this != null && chest != null;

    private ShopChest chest;
    private ShopPanelView view;

    public static void Open(ShopChest source, GameObject panelPrefab, int progress)
    {
        PlayerNetwork player = PlayerNetwork.LocalPlayer;
        if (source == null || panelPrefab == null || player == null ||
            ContainerUI.Instance == null ||
            !source.IsPlayerInInteractionRange(player))
            return;

        if (Instance != null && Instance.chest == source)
        {
            Instance.view?.SetProgress(progress);
            return;
        }

        Instance?.Close();
        ContainerUI.Instance.Close();
        GameObject panel = Instantiate(panelPrefab,
            ContainerUI.Instance.transform.parent, false);
        ShopUI ui = panel.GetComponent<ShopUI>();
        if (ui == null)
            ui = panel.AddComponent<ShopUI>();
        ui.chest = source;
        ui.view = panel.GetComponent<ShopPanelView>();
        if (ui.view == null || source.Catalog == null)
        {
            Destroy(panel);
            return;
        }
        Instance = ui;
        ui.view.Bind(source, progress);
        player.CancelAttackForUI();
        Object.FindAnyObjectByType<LeaveButtonUI>()?.HideButton();
    }

    public static void Purchased(ShopChest source, int progress)
    {
        if (Instance != null && Instance.chest == source)
            Instance.view?.Purchased(progress);
    }

    public static void CloseIfCurrent(ShopChest source)
    {
        if (Instance != null && Instance.chest == source)
            Instance.Close();
    }

    public bool TryClose()
    {
        if (!IsOpen)
            return false;

        Close();
        return true;
    }

    private void Update()
    {
        PlayerNetwork player = PlayerNetwork.LocalPlayer;
        if (player == null || !player.IsSpawned ||
            player.GetComponent<Health>() is not Health health || !health.IsAlive() ||
            chest == null || !chest.IsPlayerInInteractionRange(player))
            Close();
    }

    private void Close()
    {
        if (Instance == this)
            Instance = null;
        chest = null;
        enabled = false;
        if (view != null && view.TryHide()) return;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
