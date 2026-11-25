using System;
using Unity.Netcode;

public struct InventorySlot : INetworkSerializable, IEquatable<InventorySlot>
{
    public int itemIndex;
    public int count;
    public int charges;

    public InventorySlot(int itemIndex, int count, int charges = 0)
    {
        this.itemIndex = itemIndex;
        this.count = count;
        this.charges = charges;
    }

    public bool IsEmpty => itemIndex < 0 || count <= 0;

    public static InventorySlot Empty => new(-1, 0);

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref itemIndex);
        serializer.SerializeValue(ref count);
        serializer.SerializeValue(ref charges);
    }

    public bool Equals(InventorySlot other)
    {
        return itemIndex == other.itemIndex &&
            count == other.count && charges == other.charges;
    }

    public override bool Equals(object obj)
    {
        return obj is InventorySlot other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(itemIndex, count, charges);
    }
}
