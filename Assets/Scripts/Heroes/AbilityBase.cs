using UnityEngine;

public abstract class AbilityBase : IAbility
{
    protected bool active;
    protected float timer;

    public bool IsActive()
    {
        return active;
    }

    public float RemainingTime => active ? timer : 0f;

    protected bool UpdateTimer()
    {
        if (!active)
            return false;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            timer = 0f;
            active = false;
            return true;
        }

        return false;
    }

    // Charge-related methods can be overridden by abilities that have a charge mechanic
    public virtual bool HasCharge() => false;
    public virtual int CurrentCharge() => 0;
    public virtual int MaxCharge() => 0;

    public virtual bool IsUsable()
    {
        return true;
    }

    public abstract bool Use();
    public abstract void Update();
}