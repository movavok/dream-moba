public abstract class AbilityBase : IAbility
{
    protected bool active;

    public bool IsActive()
    {
        return active;
    }

    public abstract void Use();
    public abstract void Update();
}