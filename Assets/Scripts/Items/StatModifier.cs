using System;

public enum StatType
{
    MoveSpeed = 0,
    MaxHealth = 1,
    AttackDamage = 2,
    AttackSpeed = 3,
    HealthRegen = 4,
    DamageTaken = 5
}

public enum ModifierOp
{
    Flat = 0,
    Percent = 1
}

[Serializable]
public struct StatModifier
{
    public StatType stat;
    public ModifierOp op;
    public float value;

    public StatModifier(StatType stat, ModifierOp op, float value)
    {
        this.stat = stat;
        this.op = op;
        this.value = value;
    }
}
