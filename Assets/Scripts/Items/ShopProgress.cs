using System.Collections.Generic;

// Server match state keyed by client, independent of the hero's inventory/lifetime.
public static class ShopProgress
{
    private static readonly Dictionary<ulong, int> tiers = new();
    public static void Reset() => tiers.Clear();
    public static void Remove(ulong clientId) => tiers.Remove(clientId);
    public static int Snapshot(ulong clientId) => tiers.TryGetValue(clientId, out int value) ? value : 0;
    public static int UnlockedTier(int snapshot, ShopCategory category) =>
        category == ShopCategory.Signature ? 1 :
        1 + ((snapshot >> ((int)category * 2)) & 3);

    public static int RecordPurchase(ulong clientId, ShopCategory category, int tier)
    {
        int snapshot = Snapshot(clientId);
        if (category == ShopCategory.Signature || tier >= 3) return snapshot;
        int shift = (int)category * 2;
        int current = (snapshot >> shift) & 3;
        if (tier > current)
            snapshot = (snapshot & ~(3 << shift)) | (tier << shift);
        tiers[clientId] = snapshot;
        return snapshot;
    }
}
