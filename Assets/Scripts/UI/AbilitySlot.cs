using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class AbilitySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private Image frame;
    [SerializeField] private Image icon;
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private TMP_Text keyText;

    [Header("Press Animation")]
    [SerializeField] private RectTransform slotVisual;
    [SerializeField] private float pressScale = 0.9f;
    [SerializeField] private float pressDuration = 0.1f;

    [Header("Tooltip")]
    private string tooltipTitle;
    private string tooltipDescription;  

    private Sprite normalFrame;
    private Sprite quickCastFrame;

    private bool quickCastAvailable;
    public bool QuickCastAvailable => quickCastAvailable;

    private void Awake()
    {
        SetReady();
    }

    private Coroutine pressCoroutine;

    public void PlayPress()
    {
        if (slotVisual == null)
            return;

        if (pressCoroutine != null)
            StopCoroutine(pressCoroutine);

        pressCoroutine = StartCoroutine(PressAnimation());
    }

    private System.Collections.IEnumerator PressAnimation()
    {
        slotVisual.localScale = Vector3.one * pressScale;

        yield return new WaitForSeconds(pressDuration);

        slotVisual.localScale = Vector3.one;

        pressCoroutine = null;
    }

    public void SetFrame(Sprite sprite)
    {
        if (frame == null)
            return;

        frame.sprite = sprite;
    }

    public void SetIcon(Sprite sprite)
    {
        if (icon == null)
            return;

        icon.sprite = sprite;
    }

    public void SetKey(string key)
    {
        if (keyText == null)
            return;

        keyText.text = key;
        keyText.gameObject.SetActive(!string.IsNullOrEmpty(key));
    }

    public void SetCooldown(float remaining, float duration)
    {
        if (cooldownOverlay == null)
            return;

        if (remaining <= 0f || duration <= 0f)
        {
            SetReady();
            return;
        }

        float progress = Mathf.Clamp01(remaining / duration);

        cooldownOverlay.gameObject.SetActive(true);
        cooldownOverlay.fillAmount = progress;

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(true);

            if (remaining < 1f)
                cooldownText.text = remaining.ToString("0.0");
            else
                cooldownText.text = Mathf.CeilToInt(remaining).ToString();
        }
    }

    public void SetActiveTime(float remaining, float duration)
    {
        if (cooldownOverlay == null)
            return;

        if (remaining <= 0f || duration <= 0f)
        {
            SetReady();
            return;
        }

        float progress = Mathf.Clamp01(
            1f - remaining / duration
        );

        cooldownOverlay.gameObject.SetActive(true);
        cooldownOverlay.fillAmount = progress;

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(true);

            if (remaining < 1f)
                cooldownText.text = remaining.ToString("0.0");
            else
                cooldownText.text = Mathf.CeilToInt(remaining).ToString();
        }
    }

    public void SetReady()
    {
        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = 0f;
            cooldownOverlay.gameObject.SetActive(false);
        }

        if (cooldownText != null)
        {
            cooldownText.text = "";
            cooldownText.gameObject.SetActive(false);
        }
    }

    public void SetCharge(int current, int maximum, bool showText = true)
    {
        if (maximum <= 0 || current >= maximum)
        {
            SetReady();
            return;
        }

        current = Mathf.Clamp(current, 0, maximum);

        if (cooldownOverlay != null)
        {
            cooldownOverlay.gameObject.SetActive(true);

            cooldownOverlay.fillAmount =
                1f - (float)current / maximum;
        }

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(showText);

            if (showText)
                cooldownText.text = $"{current}/{maximum}";
        }
    }

    public void SetBlocked()
    {
        if (cooldownOverlay != null)
        {
            cooldownOverlay.gameObject.SetActive(true);
            cooldownOverlay.fillAmount = 1f;
        }

        if (cooldownText != null)
        {
            cooldownText.text = "";
            cooldownText.gameObject.SetActive(false);
        }
    }

    public void SetFrames(Sprite normal, Sprite quickCast)
    {
        normalFrame = normal;
        quickCastFrame = quickCast;

        if (frame != null)
            frame.sprite = normalFrame;
    }

    public void SetQuickCastAvailable(bool available)
    {
        quickCastAvailable = available;

        if (!available)
        {
            SetQuickCast(false);
        }
    }

    public void SetQuickCast(bool active)
    {
        if (frame == null || !quickCastAvailable)
            return;

        frame.sprite = active
            ? quickCastFrame
            : normalFrame;
    }

    public void SetTooltipData(string title, string description)
    {
        tooltipTitle = title;
        tooltipDescription = description;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (HeroHUD.Instance == null)
            return;

        HeroHUD.Instance.ShowTooltip(
            tooltipTitle,
            tooltipDescription
        );
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (HeroHUD.Instance == null)
            return;

        HeroHUD.Instance.HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!quickCastAvailable)
            return;

        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (HeroHUD.Instance == null)
            return;

        HeroHUD.Instance.ToggleQuickCast(this);
    }
}