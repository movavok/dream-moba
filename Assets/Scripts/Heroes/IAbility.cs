public interface IAbility
{
    bool Use();
    void Update();
    bool IsActive();
    float RemainingTime { get; }

    bool HasCharge();
    int CurrentCharge();
    int MaxCharge();
    
    bool IsUsable();
}