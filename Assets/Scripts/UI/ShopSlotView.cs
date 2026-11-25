using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ShopSlotView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private Image frame;
    [SerializeField] private Color lockedColor = new(0.38f, 0.38f, 0.38f, 0.85f);
    [SerializeField] private Color availableColor = new(1f, 1f, 1f, 0.88f);
    [SerializeField] private Color selectedColor = new(1f, 0.86f, 0.25f, 1f);
    [SerializeField] private Color poorColor = new(0.62f, 0.62f, 0.62f, 0.85f);
    [SerializeField, Min(0.01f)] private float unlockDuration = 0.22f;
    [SerializeField, Min(0.1f)] private float pulsePeriod = 1.8f;
    [SerializeField, Range(0f, 0.4f)] private float pulseAmount = 0.1f;
    private Action onClick;
    private Coroutine transition;
    private Coroutine pulse;
    private bool pulsing;
    private Color target;
    private bool initialized;
    private bool available;

    public void Bind(Action clicked)
    {
        onClick = clicked;
        if (icon == null) icon = transform.Find("Icon")?.GetComponent<Image>();
        if (frame == null) frame = GetComponent<Image>();
        if (icon != null) icon.raycastTarget = false;
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) onClick?.Invoke();
    }
    public void Display(Sprite sprite, bool occupied, bool unlocked, bool selected, bool affordable)
    {
        if (icon == null || frame == null) return;
        icon.sprite = sprite;
        // Keep empty slot frames but make their icons fully transparent.
        icon.color = sprite == null ? Color.clear :
            unlocked ? Color.white : lockedColor;
        icon.enabled = sprite != null;
        bool active = occupied && unlocked;
        Color next = !occupied ? lockedColor : !unlocked ? lockedColor :
            selected ? selectedColor : affordable ? availableColor : poorColor;
        if (transition != null) StopCoroutine(transition);
        if (pulse != null) StopCoroutine(pulse);
        pulsing = false;
        bool unlockedNow = initialized && !available && active;
        initialized = true;
        available = active;
        target = next;
        if (unlockedNow)
            transition = StartCoroutine(Tint(unlockDuration));
        else
        {
            frame.color = target;
            transition = null;
        }
        if (active && !selected && affordable)
        {
            pulsing = true;
            pulse = StartCoroutine(Pulse());
        }
    }
    private IEnumerator Tint(float duration)
    {
        Color start = frame.color;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / duration);
            frame.color = Color.Lerp(start, target, p);
            if (icon.enabled) icon.color = Color.Lerp(lockedColor, Color.white, p);
            yield return null;
        }
        frame.color = target;
        if (icon.enabled && available) icon.color = Color.white;
        transition = null;
    }
    private IEnumerator Pulse()
    {
        while (true)
        {
            float p = (1f + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / pulsePeriod)) * 0.5f;
            Color c = target;
            c.a = Mathf.Clamp01(target.a + pulseAmount * p);
            if (transition == null && pulsing) frame.color = c;
            yield return null;
        }
    }
}
