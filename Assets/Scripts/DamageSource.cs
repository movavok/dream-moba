public enum DamageSource
{
    Generic = 0,
    Projectile = 1,
    Reflect = 2
}

public readonly struct DamageEvent
{
    public readonly int Amount;
    public readonly ulong AttackerClientId;
    public readonly DamageSource Source;

    public DamageEvent(
        int amount,
        ulong attackerClientId,
        DamageSource source)
    {
        Amount = amount;
        AttackerClientId = attackerClientId;
        Source = source;
    }
}
