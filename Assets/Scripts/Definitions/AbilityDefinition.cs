using System;
using UnityEngine;

[Serializable]
public class AbilityDefinition
{
    public string name;
    public string description;

    public float cost;
    public float multiplier;
    public float cooldown;
    public float duration;

    public string abilityId;
}