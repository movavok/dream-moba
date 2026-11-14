using System;
using Unity.Netcode;

public struct InventorySlot : INetworkSerializable, IEquatable<InventorySlot>
{
    public int itemIndex;
    public int count;

    public InventorySlot(int itemIndex, int count)
    {
        this.itemIndex = itemIndex;
        this.count = count;
    }

    public bool IsEmpty => itemIndex < 0 || count <= 0;

    public static InventorySlot Empty => new(-1, 0);

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref itemIndex);
        serializer.SerializeValue(ref count);
    }

    public bool Equals(InventorySlot other)
    {
        return itemIndex == other.itemIndex &&
            count == other.count;
    }

    public override bool Equals(object obj)
    {
        return obj is InventorySlot other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(itemIndex, count);
    }
}
