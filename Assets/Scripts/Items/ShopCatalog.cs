using System;
using UnityEngine;

public enum ShopCategory { Attack, Survival, MobilityControl, Signature }

[Serializable]
public class ShopOffer
{
    public ShopCategory category;
    [Range(1, 3)] public int tier = 1;
    [Range(1, 3)] public int position = 1;
    public ItemDefinition item;
    [Min(0)] public int price;
    public Sprite iconOverride;
    public Sprite previewOverride;
}

[CreateAssetMenu(menuName = "Game/Shop Catalog")]
public sealed class ShopCatalog : ScriptableObject
{
    [SerializeField] private ShopOffer[] offers;

    public ShopOffer Get(ShopCategory category, int tier, int position)
    {
        if (!ValidPosition(category, tier, position) || offers == null)
            return null;
        foreach (ShopOffer offer in offers)
            if (offer != null && offer.category == category &&
                offer.tier == tier && offer.position == position)
                return offer;
        return null;
    }

    public static bool ValidPosition(ShopCategory category, int tier, int position)
    {
        if (category == ShopCategory.Signature)
            return tier == 1 && position >= 1 && position <= 3;
        return category >= ShopCategory.Attack && category <= ShopCategory.MobilityControl &&
            tier >= 1 && tier <= 3 && position >= 1 &&
            position <= (tier == 2 ? 3 : 2);
    }

    private void OnValidate()
    {
        if (offers == null) return;
        for (int i = 0; i < offers.Length; i++)
        {
            ShopOffer offer = offers[i];
            if (offer == null) continue;
            if (!ValidPosition(offer.category, offer.tier, offer.position))
                Debug.LogWarning($"Invalid shop position at index {i}", this);
            for (int j = 0; j < i; j++)
                if (offers[j] != null && offers[j].category == offer.category &&
                    offers[j].tier == offer.tier && offers[j].position == offer.position)
                    Debug.LogWarning($"Duplicate shop position at index {i}", this);
        }
    }
}
