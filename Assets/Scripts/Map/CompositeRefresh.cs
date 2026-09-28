using UnityEngine;
using System.Collections;

public class CompositeRefresh : MonoBehaviour
{
    private CompositeCollider2D composite;

    private void Awake()
    {
        composite = GetComponent<CompositeCollider2D>();
    }

    private void Start()
    {
        StartCoroutine(RefreshComposite());
    }

    private IEnumerator RefreshComposite()
    {
        yield return null;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();

        foreach (Collider2D collider in colliders)
        {
            if (collider is not CompositeCollider2D)
                collider.compositeOperation = Collider2D.CompositeOperation.None;
        }

        yield return null;

        foreach (Collider2D collider in colliders)
        {
            if (collider is not CompositeCollider2D)
                collider.compositeOperation = Collider2D.CompositeOperation.Difference;
        }
    }
}