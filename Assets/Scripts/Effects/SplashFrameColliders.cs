using System;
using UnityEngine;

// Author one trigger collider per animation Sprite on the visual prefab.
// Only the server samples these shapes; clients never enable them.
public sealed class SplashFrameColliders : MonoBehaviour
{
    [Serializable]
    private struct Frame
    {
        public Sprite sprite;
        public Collider2D collider;
    }

    [SerializeField] private Frame[] frames;
    private Collider2D active;
    public bool HasFrames => frames != null && frames.Length > 0;

    private void Awake() => DisableAll();

    public void DisableAll()
    {
        if (frames == null) return;
        foreach (Frame frame in frames)
            if (frame.collider != null) frame.collider.enabled = false;
        active = null;
    }

    public Collider2D Select(Sprite currentSprite)
    {
        if (currentSprite == null || frames == null) return null;
        foreach (Frame frame in frames)
        {
            if (frame.sprite != currentSprite || frame.collider == null) continue;
            if (active != frame.collider)
            {
                DisableAll();
                active = frame.collider;
                active.enabled = true;
            }
            return active;
        }
        DisableAll();
        return null;
    }
}
