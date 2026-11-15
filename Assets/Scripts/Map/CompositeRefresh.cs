using UnityEngine;
using System.Collections;

public class CompositeRefresh : MonoBehaviour
{
    private CompositeCollider2D composite;

    private void Awake()
    {
        composite = GetComponent<CompositeCollider2D>();

        // Runs during Awake, before any player spawns, instead of two frames later.
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();

        foreach (Collider2D collider in colliders)
        {
            if (collider is not CompositeCollider2D)
                collider.compositeOperation = Collider2D.CompositeOperation.None;
        }

        foreach (Collider2D collider in colliders)
        {
            if (collider is not CompositeCollider2D)
                collider.compositeOperation = Collider2D.CompositeOperation.Difference;
        }
    }
}