using System;
using UnityEngine;

[Serializable]
public class AbilityDefinition
{
    public Sprite icon;
    public string name;
    public string description;

    public float cost;
    public float multiplier;
    public float cooldown;
    public float duration;

    [Header("Quick Cast")]
    public bool quickCastEnabled;

    public string abilityId;

    [SerializeField] private GameObject visualEffectPrefab;
    public GameObject VisualEffectPrefab => visualEffectPrefab;
}