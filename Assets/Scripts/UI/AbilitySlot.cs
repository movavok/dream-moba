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
    private bool pointerInside;
    private TMP_Text placeholderText;
    private enum LabelMode { None, Seconds, Tenths, Charge }
    private LabelMode labelMode;
    private int labelValue = -1, labelMaximum = -1;

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

    public Sprite FrameSprite => frame != null ? frame.sprite : null;

    public void SetKeyColor(Color color)
    {
        if (keyText != null)
            keyText.color = color;
    }

    public TMP_Text CooldownTextTemplate => cooldownText;

    public void EnsureCooldownText(TMP_Text template)
    {
        if (cooldownText != null || cooldownOverlay == null || template == null)
            return;

        cooldownText = Instantiate(template, cooldownOverlay.transform, false);
        cooldownText.gameObject.name = "CooldownText";
        SetReady();
    }

    public void SetIcon(Sprite sprite)
    {
        if (icon == null)
            return;

        icon.sprite = sprite;
        if (sprite != null && icon.color.a == 0f)
        {
            Color color = icon.color;
            color.a = 1f;
            icon.color = color;
        }
        icon.gameObject.SetActive(sprite != null);
    }

    public void SetItemCharges(int current, int maximum)
    {
        if (icon == null) return;
        icon.type = maximum > 0 ? Image.Type.Filled : Image.Type.Simple;
        if (maximum > 0)
        {
            icon.fillMethod = Image.FillMethod.Vertical;
            icon.fillOrigin = (int)Image.OriginVertical.Bottom;
            icon.fillAmount = Mathf.Clamp01(current / (float)maximum);
        }
        else icon.fillAmount = 1f;
    }

    public void SetPlaceholder(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            if (placeholderText != null)
                placeholderText.gameObject.SetActive(false);
            return;
        }

        if (placeholderText == null)
        {
            GameObject label = new GameObject("Item Initial",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            placeholderText = label.GetComponent<TMP_Text>();
            placeholderText.font = TMP_Settings.defaultFontAsset;
            placeholderText.fontSize = 28f;
            placeholderText.alignment = TextAlignmentOptions.Center;
            placeholderText.raycastTarget = false;
        }

        placeholderText.text = text;
        placeholderText.gameObject.SetActive(true);
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

        SetTimerText(remaining);
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

        SetTimerText(remaining);
    }

    private void SetTimerText(float remaining)
    {
        if (cooldownText == null)
            return;
        cooldownText.gameObject.SetActive(true);
        LabelMode mode = remaining < 1f ? LabelMode.Tenths : LabelMode.Seconds;
        int value = mode == LabelMode.Tenths
            ? Mathf.RoundToInt(remaining * 10f) : Mathf.CeilToInt(remaining);
        if (labelMode == mode && labelValue == value)
            return;
        labelMode = mode;
        labelValue = value;
        if (mode == LabelMode.Tenths)
            cooldownText.SetText("{0:0.0}", value * 0.1f);
        else
            cooldownText.SetText("{0}", value);
    }

    public void SetReady()
    {
        labelMode = LabelMode.None;
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

            if (showText && (labelMode != LabelMode.Charge ||
                labelValue != current || labelMaximum != maximum))
            {
                labelMode = LabelMode.Charge;
                labelValue = current;
                labelMaximum = maximum;
                cooldownText.SetText("{0}/{1}", current, maximum);
            }
        }
    }

    public void SetBlocked()
    {
        labelMode = LabelMode.None;
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

        if (!pointerInside || HeroHUD.Instance == null)
            return;

        if (string.IsNullOrWhiteSpace(tooltipTitle))
            HeroHUD.Instance.HideTooltip();
        else
            HeroHUD.Instance.ShowTooltip(tooltipTitle, tooltipDescription);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        if (HeroHUD.Instance == null || string.IsNullOrWhiteSpace(tooltipTitle))
            return;

        HeroHUD.Instance.ShowTooltip(
            tooltipTitle,
            tooltipDescription
        );
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (HeroHUD.Instance == null)
            return;

        HeroHUD.Instance.HideTooltip();
    }

    private void OnDisable()
    {
        if (pointerInside && HeroHUD.Instance != null)
            HeroHUD.Instance.HideTooltip();

        pointerInside = false;
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
