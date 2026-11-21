using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Temporary, server-owned stat sources. Each effect key can run only once.
[RequireComponent(typeof(StatController))]
public class TimedStatEffects : MonoBehaviour
{
    private readonly Dictionary<ScriptableObject, double> endsAt = new();
    private readonly List<ScriptableObject> expired = new();
    private StatController stats;
    private HeroEffectVisuals visuals;

    private void Awake()
    {
        stats = GetComponent<StatController>();
        visuals = GetComponent<HeroEffectVisuals>();
    }

    public bool IsActive(ScriptableObject source)
    {
        return source != null && endsAt.TryGetValue(source, out double end) &&
            Time.timeAsDouble < end;
    }

    public void Activate(ScriptableObject source, float duration, StatModifier modifier)
    {
        if (source == null || stats == null || NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer || IsActive(source))
            return;

        endsAt[source] = Time.timeAsDouble + Mathf.Max(0f, duration);
        stats.AddSource(source, new[] { modifier });
        visuals?.SetActive(source, true);
    }

    private void Update()
    {
        if (endsAt.Count == 0)
            return;

        expired.Clear();
        foreach (var effect in endsAt)
            if (Time.timeAsDouble >= effect.Value)
                expired.Add(effect.Key);

        foreach (ScriptableObject source in expired)
        {
            stats?.RemoveSource(source);
            visuals?.SetActive(source, false);
            endsAt.Remove(source);
        }
    }

    private void OnDestroy()
    {
        if (stats == null)
            return;

        foreach (ScriptableObject source in endsAt.Keys)
        {
            stats.RemoveSource(source);
            visuals?.SetActive(source, false);
        }
    }
}
