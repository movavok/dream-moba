using System.Collections;
using UnityEngine;

// A local, one-shot animation at the prefab's authored scale.
public sealed class ExpandingSplashVisual : MonoBehaviour
{
    public void Play(float duration)
    {
        StartCoroutine(RemoveAfter(Mathf.Max(0.01f, duration)));
    }

    private IEnumerator RemoveAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        Destroy(gameObject);
    }
}
