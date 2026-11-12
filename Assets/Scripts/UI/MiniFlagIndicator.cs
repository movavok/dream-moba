using UnityEngine;
using UnityEngine.UI;

public class MiniFlagIndicator : MonoBehaviour
{
    [SerializeField] private SpriteRenderer branchRenderer;
    [SerializeField] private SpriteRenderer clothRenderer;
    [SerializeField] private Image branchImage;
    [SerializeField] private Image clothImage;

    private void Awake()
    {
        ResolveRenderers();
    }

    public void Setup(Color teamColor, int sortingOrder)
    {
        ResolveRenderers();

        Canvas canvas = GetComponentInParent<Canvas>();
        bool isCanvasUi = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay;

        if (branchImage != null || clothImage != null || isCanvasUi)
        {
            EnsureUiImage(ref branchImage, branchRenderer, "Branch", Color.white);
            EnsureUiImage(ref clothImage, clothRenderer, "Cloth", teamColor);

            if (branchRenderer != null)
                branchRenderer.enabled = false;
            if (clothRenderer != null)
                clothRenderer.enabled = false;
        }
        else
        {
            if (branchRenderer != null)
            {
                branchRenderer.enabled = true;
                branchRenderer.sortingOrder = sortingOrder;
            }

            if (clothRenderer != null)
            {
                clothRenderer.enabled = true;
                clothRenderer.color = teamColor;
                clothRenderer.sortingOrder = sortingOrder + 1;
            }
        }
    }

    private void ResolveRenderers()
    {
        if (branchRenderer == null || branchImage == null)
        {
            Transform branch = transform.Find("Branch");
            if (branch != null)
            {
                if (branchRenderer == null)
                    branchRenderer = branch.GetComponent<SpriteRenderer>();
                if (branchImage == null)
                    branchImage = branch.GetComponent<Image>();
            }
        }

        if (clothRenderer == null || clothImage == null)
        {
            Transform cloth = transform.Find("Cloth");
            if (cloth != null)
            {
                if (clothRenderer == null)
                    clothRenderer = cloth.GetComponent<SpriteRenderer>();
                if (clothImage == null)
                    clothImage = cloth.GetComponent<Image>();
            }
        }
    }

    private void EnsureUiImage(
        ref Image image,
        SpriteRenderer sr,
        string childName,
        Color tintColor)
    {
        Transform child = transform.Find(childName);
        if (child == null)
            return;

        GameObject childGo = child.gameObject;

        if (image == null)
            image = childGo.GetComponent<Image>();

        if (image == null)
        {
            Sprite sprite = sr != null ? sr.sprite : null;
            if (sr != null)
                sr.enabled = false;

            image = childGo.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;

            RectTransform rt = childGo.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
            }
        }

        image.color = tintColor;
        image.enabled = true;
    }
}
