using UnityEngine;
using UnityEngine.UI;

// Includes TMP graphics, nested icons and decorative UI sprites.
public sealed class UIGroupTint : MonoBehaviour
{
    [SerializeField] private Color disabledTint = new(0.45f, 0.45f, 0.45f, 1f);
    private Graphic[] graphics;
    private Color[] original;
    private bool? disabled;

    public void SetDisabled(bool value)
    {
        if (graphics == null)
        {
            graphics = GetComponentsInChildren<Graphic>(true);
            original = new Color[graphics.Length];
            for (int i = 0; i < graphics.Length; i++) original[i] = graphics[i].color;
        }
        if (disabled == value) return;
        disabled = value;
        for (int i = 0; i < graphics.Length; i++)
        {
            Color c = original[i];
            graphics[i].color = value
                ? new Color(disabledTint.r, disabledTint.g, disabledTint.b, c.a * disabledTint.a)
                : c;
        }
    }
}
