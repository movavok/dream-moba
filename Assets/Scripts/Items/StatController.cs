using System.Collections.Generic;
using UnityEngine;

public class StatController : MonoBehaviour
{
    [SerializeField] private HeroDefinition heroData;

    private readonly Dictionary<StatType, float> baseStats = new();
    private readonly Dictionary<object, List<StatModifier>> sources = new();

    public event System.Action StatsChanged;

    public HeroDefinition HeroData => heroData;

    private void Awake()
    {
        if (heroData == null)
        {
            PlayerNetwork player = GetComponent<PlayerNetwork>();

            if (player != null)
                heroData = player.HeroData;
        }

        RebuildBaseStats();
    }

    public void SetHeroData(HeroDefinition definition)
    {
        heroData = definition;
        RebuildBaseStats();
    }

    private void RebuildBaseStats()
    {
        baseStats.Clear();

        if (heroData == null || heroData.stats == null)
            return;

        baseStats[StatType.MaxHealth] = heroData.stats.maxHealth;
        baseStats[StatType.MoveSpeed] = heroData.stats.moveSpeed;
        StatsChanged?.Invoke();
    }

    public float GetBase(StatType stat)
    {
        return baseStats.TryGetValue(stat, out float value)
            ? value
            : 0f;
    }

    public void AddSource(object key, IReadOnlyList<StatModifier> modifiers)
    {
        if (key == null || modifiers == null || modifiers.Count == 0)
            return;

        sources[key] = new List<StatModifier>(modifiers);
        StatsChanged?.Invoke();
    }

    public void RemoveSource(object key)
    {
        if (key == null)
            return;

        if (sources.Remove(key))
            StatsChanged?.Invoke();
    }

    public void ClearSources()
    {
        if (sources.Count == 0)
            return;

        sources.Clear();
        StatsChanged?.Invoke();
    }

    public float Get(StatType stat)
    {
        float flat = 0f;
        float percent = 0f;

        foreach (List<StatModifier> list in sources.Values)
        {
            for (int i = 0; i < list.Count; i++)
            {
                StatModifier modifier = list[i];

                if (modifier.stat != stat)
                    continue;

                if (modifier.op == ModifierOp.Flat)
                    flat += modifier.value;
                else
                    percent += modifier.value;
            }
        }

        return (GetBase(stat) + flat) * (1f + percent);
    }
}
