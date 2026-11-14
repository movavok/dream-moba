using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemDatabase",
    menuName = "Game/Item Database"
)]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private ItemDefinition[] items;

    private Dictionary<string, int> indexById;
    private Dictionary<string, ItemDefinition> byId;

    public int Count => items != null ? items.Length : 0;

    public ItemDefinition GetByIndex(int index)
    {
        if (items == null || index < 0 || index >= items.Length)
            return null;

        return items[index];
    }

    public int GetIndex(ItemDefinition definition)
    {
        if (definition == null)
            return -1;

        int index = GetIndex(definition.id);
        return GetByIndex(index) == definition ? index : -1;
    }

    public int GetIndex(string id)
    {
        EnsureIndex();

        if (string.IsNullOrEmpty(id))
            return -1;

        return indexById.TryGetValue(id, out int index)
            ? index
            : -1;
    }

    public ItemDefinition Get(string id)
    {
        EnsureIndex();

        if (string.IsNullOrEmpty(id))
            return null;

        return byId.TryGetValue(id, out ItemDefinition definition)
            ? definition
            : null;
    }

    private void EnsureIndex()
    {
        if (indexById != null)
            return;

        indexById = new Dictionary<string, int>();
        byId = new Dictionary<string, ItemDefinition>();

        if (items == null)
            return;

        for (int i = 0; i < items.Length; i++)
        {
            ItemDefinition definition = items[i];

            if (definition == null ||
                string.IsNullOrEmpty(definition.id))
                continue;

            indexById[definition.id] = i;
            byId[definition.id] = definition;
        }
    }

    private void OnValidate()
    {
        indexById = null;
        byId = null;
    }
}
