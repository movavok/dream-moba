using Unity.Netcode;
using UnityEngine;

// PlayerNetwork's FixedUpdate owns velocity. Apply a temporary displacement there,
// rather than a competing force or a client-side teleport.
public sealed class BoarKnockback : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float duration = 0.28f;
    private float remaining;
    private float speed;
    private Vector2 direction;
    private Rigidbody2D body;

    private void Awake() { body = GetComponent<Rigidbody2D>(); }

    public void Push(Vector2 heading, float distance)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer ||
            body == null || heading.sqrMagnitude < 0.001f) return;
        direction = heading.normalized;
        speed = Mathf.Max(0f, distance) / duration;
        remaining = duration;
    }

    public Vector2 Apply(Vector2 inputVelocity)
    {
        if (remaining <= 0f) return inputVelocity;
        remaining = Mathf.Max(0f, remaining - Time.fixedDeltaTime);
        float strength = Mathf.SmoothStep(0f, 1f, remaining / duration);
        return inputVelocity * (1f - strength) + direction * speed * 2f * strength;
    }
}
