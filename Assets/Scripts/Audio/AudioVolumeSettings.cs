using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AudioVolumeSettings",
    menuName = "Audio/Volume Settings"
)]
public class AudioVolumeSettings : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string key;
        [Range(0f, 1f)] public float volume;
    }

    [Serializable]
    public class Group
    {
        public string group;
        public Entry[] entries = Array.Empty<Entry>();
    }

    [SerializeField] private Group[] groups =
    {
        new Group
        {
            group = AudioGroups.Global,
            entries = new[]
            {
                new Entry { key = SoundKeys.DamageTaken, volume = 0.34f },
                new Entry { key = SoundKeys.CustomDamage, volume = 1f },
                new Entry { key = SoundKeys.BuildingTransition, volume = 1f }
            }
        },
        new Group
        {
            group = AudioGroups.Hero("Guitarist"),
            entries = new[]
            {
                new Entry { key = SoundKeys.BasicAttack, volume = 0.2f },
                new Entry { key = SoundKeys.AttackHit, volume = 0.9f },
                new Entry { key = SoundKeys.Ability1, volume = 0.4f },
                new Entry { key = SoundKeys.Ability2, volume = 0.5f },
                new Entry { key = SoundKeys.Death, volume = 0.6f },
                new Entry { key = SoundKeys.Respawn, volume = 1f },
                new Entry { key = SoundKeys.CustomDamage, volume = 1f }
            }
        },
        new Group
        {
            group = AudioGroups.Item("energy_drink"),
            entries = new[]
            {
                new Entry { key = SoundKeys.ItemUse, volume = 1f }
            }
        },
        new Group
        {
            group = AudioGroups.MinionBoss("Scarecrow"),
            entries = new[]
            {
                new Entry { key = SoundKeys.CustomDamage, volume = 1f },
                new Entry { key = SoundKeys.Death, volume = 1f }
            }
        },
        new Group
        {
            group = AudioGroups.Creep("Melee"),
            entries = new[]
            {
                new Entry { key = SoundKeys.CustomDamage, volume = 1f },
                new Entry { key = SoundKeys.Death, volume = 1f }
            }
        }
    };

    private Dictionary<string, float> lookup;

    public float GetVolume(string group, string key)
    {
        EnsureLookup();

        return lookup.TryGetValue(
            Combine(group, key),
            out float volume)
            ? volume
            : 1f;
    }

    private void EnsureLookup()
    {
        if (lookup != null)
            return;

        lookup = new Dictionary<string, float>();

        if (groups == null)
            return;

        foreach (Group group in groups)
        {
            if (group == null || group.entries == null)
                continue;

            foreach (Entry entry in group.entries)
                lookup[Combine(group.group, entry.key)] =
                    Mathf.Clamp01(entry.volume);
        }
    }

    private static string Combine(string group, string key)
    {
        return (group ?? string.Empty) + "/" + (key ?? string.Empty);
    }

    private void OnValidate()
    {
        lookup = null;

        if (groups == null)
            return;

        foreach (Group group in groups)
        {
            if (group == null || group.entries == null)
                continue;

            for (int i = 0; i < group.entries.Length; i++)
            {
                Entry entry = group.entries[i];
                entry.volume = Mathf.Clamp01(entry.volume);
                group.entries[i] = entry;
            }
        }
    }
}
