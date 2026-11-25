using UnityEngine;

// Optional hook into the existing swept item throw; regular items are unchanged.
public interface IThrownItemImpact
{
    void OnThrowStart(Transform thrower);
    bool Sweep(Vector2 from, Vector2 to);
    void Impact(Vector2 point);
}
