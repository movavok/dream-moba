using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopPanelView : MonoBehaviour
{
    // Order: Attack, Survival, Mobility & Control, Signature.
    [SerializeField] private Transform[] categories;
    [SerializeField] private Button buyButton;
    [SerializeField] private Image previewIcon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private ShopSlotView slotPrefab;
    [SerializeField] private UIReveal reveal;
    [SerializeField] private UICoinBurst purchaseEffect;
    private readonly List<Entry> slots = new();
    private ShopChest chest;
    private EconomyManager economy;
    private int progress;
    private Entry selected;

    private sealed class Entry
    {
        public ShopCategory category;
        public int tier, position;
        public ShopSlotView view;
        public ShopOffer offer;
    }

    public void Bind(ShopChest source, int snapshot)
    {
        chest = source;
        progress = snapshot;
        economy = EconomyManager.Instance;
        if (economy != null) economy.GoldChanged += OnGoldChanged;
        slots.Clear();
        for (int c = 0; categories != null && c < categories.Length && c < 4; c++)
        {
            if (categories[c] == null) continue;
            ShopCategory category = (ShopCategory)c;
            foreach (Transform child in categories[c].GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.StartsWith("Slot") ||
                    !int.TryParse(child.name.Substring(4), out int position)) continue;
                int tier = 1;
                if (category != ShopCategory.Signature)
                {
                    Transform parent = child.parent;
                    if (parent == null || !parent.name.StartsWith("lvl") ||
                        !int.TryParse(parent.name.Substring(3), out tier)) continue;
                }
                if (!ShopCatalog.ValidPosition(category, tier, position)) continue;
                if (slotPrefab == null) continue;
                // Existing artist-authored slots are layout anchors. A single shared
                // prefab owns every interactive frame/icon and its visual settings.
                Image anchorFrame = child.GetComponent<Image>();
                if (anchorFrame != null) anchorFrame.enabled = false;
                Transform anchorIcon = child.Find("Icon");
                if (anchorIcon != null && anchorIcon.TryGetComponent(out Image oldIcon))
                    oldIcon.enabled = false;
                ShopSlotView view = Instantiate(slotPrefab, child, false);
                Entry entry = new() { category = category, tier = tier,
                    position = position, view = view,
                    offer = chest.Catalog.Get(category, tier, position) };
                slots.Add(entry);
                view.Bind(() => Select(entry));
            }
        }
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(Buy);
            buyButton.transition = Selectable.Transition.None;
        }
        Refresh();
        reveal?.Show();
    }

    public void SetProgress(int snapshot) { progress = snapshot; Refresh(); }
    public void Purchased(int snapshot)
    {
        SetProgress(snapshot);
        purchaseEffect?.Play();
    }
    public bool TryHide()
    {
        if (reveal == null) return false;
        reveal.Hide();
        return true;
    }
    private void Select(Entry entry)
    {
        selected = entry;
        Refresh();
    }
    private void Refresh()
    {
        if (chest == null) return;
        ulong clientId = Unity.Netcode.NetworkManager.Singleton != null
            ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;
        int gold = economy != null ? economy.GetGold(clientId) : 0;
        foreach (Entry slot in slots)
        {
            bool unlocked = slot.tier <= ShopProgress.UnlockedTier(progress, slot.category);
            ShopOffer offer = slot.offer;
            slot.view.Display(offer != null && offer.item != null
                ? (offer.iconOverride != null ? offer.iconOverride : offer.item.icon) : null,
                offer != null && offer.item != null, unlocked,
                slot == selected, offer != null && gold >= offer.price);
        }
        ShopOffer current = selected?.offer;
        bool valid = current != null && current.item != null;
        bool canBuy = valid && selected.tier <= ShopProgress.UnlockedTier(progress, selected.category)
            && gold >= current.price;
        if (nameText != null) nameText.text = valid ? current.item.ColoredDisplayName : "";
        if (descriptionText != null) descriptionText.text = valid ? current.item.description : "";
        if (priceText != null) priceText.text = valid ? current.price.ToString() : "";
        if (previewIcon != null)
        {
            Sprite worldSprite = null;
            if (valid && current.item.worldPrefab != null &&
                current.item.worldPrefab.TryGetComponent(out WorldItem worldItem))
                worldSprite = worldItem.WorldSprite;
            previewIcon.sprite = valid ? (current.previewOverride != null
                ? current.previewOverride : worldSprite != null
                    ? worldSprite : current.item.icon) : null;
            previewIcon.color = previewIcon.sprite == null ? Color.clear : Color.white;
        }
        if (buyButton != null)
        {
            UIGroupTint tint = buyButton.GetComponent<UIGroupTint>();
            if (tint != null) tint.SetDisabled(!canBuy);
            buyButton.interactable = canBuy;
        }
    }
    private void Buy()
    {
        if (chest == null || selected == null || buyButton == null || !buyButton.interactable)
            return;
        buyButton.GetComponent<UIPress>()?.Play();
        chest.BuyServerRpc(selected.category, selected.tier, selected.position);
    }
    private void OnGoldChanged(ulong clientId)
    {
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            clientId == Unity.Netcode.NetworkManager.Singleton.LocalClientId) Refresh();
    }
    private void OnDestroy()
    {
        if (economy != null) economy.GoldChanged -= OnGoldChanged;
        if (buyButton != null) buyButton.onClick.RemoveListener(Buy);
    }
}
