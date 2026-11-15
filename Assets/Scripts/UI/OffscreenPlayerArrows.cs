using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Local HUD arrows. Assign a sprite pointing UPPER LEFT; only team-spotted enemies qualify.
public class OffscreenPlayerArrows : MonoBehaviour
{
    [SerializeField] private Sprite arrowSprite;
    [SerializeField] private RectTransform container;
    [SerializeField] private CanvasGroup screenFade;
    [SerializeField] private Vector2 arrowSize = new Vector2(36f, 36f);
    [SerializeField, Min(0f)] private float edgePaddingPixels = 32f;
    [SerializeField, Range(0f, 1f)] private float opacity = 0.65f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float followSpeed = 16f;
    [SerializeField] private float spriteAngleOffset = -135f;

    private class Arrow
    {
        public RectTransform rect;
        public Image image;
        public float alpha;
        public bool visible;
        public bool positioned;
        public Color color;
    }

    private readonly Dictionary<ulong, Arrow> arrows = new();
    private Canvas canvas;
    private TeamManager teams;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (container == null)
            container = (RectTransform)transform;
    }

    private void LateUpdate()
    {
        foreach (Arrow arrow in arrows.Values)
            arrow.visible = false;

        PlayerCamera playerCamera = PlayerCamera.Instance;
        NetworkManager network = NetworkManager.Singleton;
        Camera camera = playerCamera != null ? playerCamera.GetComponent<Camera>() : null;
        if (arrowSprite == null || camera == null || camera.pixelWidth <= 0 ||
            camera.pixelHeight <= 0 || network == null || !network.IsListening ||
            screenFade != null && screenFade.alpha > 0.01f)
        {
            foreach (Arrow arrow in arrows.Values)
            {
                arrow.alpha = 0f;
                arrow.positioned = false;
                arrow.rect.gameObject.SetActive(false);
            }
            return;
        }

        if (teams == null)
            teams = FindAnyObjectByType<TeamManager>();
        TeamCameraViews vision = TeamCameraViews.Instance;
        if (teams == null || !playerCamera.TryGetWorldViewRect(out Rect ownView))
            return;

        short localTeam = teams.GetTeamId(network.LocalClientId);
        foreach (PlayerNetwork player in FindObjectsByType<PlayerNetwork>())
        {
            if (!player.IsSpawned || player.OwnerClientId == network.LocalClientId ||
                player.GetComponent<Team>() is not Team team)
                continue;

            SpriteRenderer visual = player.GetComponent<HeroSorting>()?.HeroRenderer;
            if (visual == null)
                continue;

            Bounds bounds = visual.bounds;
            bool ally = team.TeamId.Value == localTeam;
            if (!ally && (vision == null || !vision.IsVisible(bounds)))
                continue;

            // A partially visible hero is on screen, not an off-screen target.
            if (ownView.xMin < bounds.max.x && ownView.xMax > bounds.min.x &&
                ownView.yMin < bounds.max.y && ownView.yMax > bounds.min.y)
                continue;

            Vector3 viewport = camera.WorldToViewportPoint(bounds.center);
            if (viewport.z <= 0f)
                continue;

            Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            float margin = edgePaddingPixels +
                Mathf.Max(arrowSize.x, arrowSize.y) *
                (canvas != null ? canvas.scaleFactor : 1f) * 0.5f;
            float halfWidth = Mathf.Max(0.05f, 0.5f - margin / camera.pixelWidth);
            float halfHeight = Mathf.Max(0.05f, 0.5f - margin / camera.pixelHeight);
            float reach = Mathf.Min(
                halfWidth / Mathf.Max(Mathf.Abs(direction.x), 0.0001f),
                halfHeight / Mathf.Max(Mathf.Abs(direction.y), 0.0001f));
            Vector3 screen = camera.ViewportToScreenPoint(
                new Vector3(0.5f + direction.x * reach,
                    0.5f + direction.y * reach, 0f));
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                container, screen, uiCamera, out Vector2 localPoint))
                continue;

            Arrow arrow = GetArrow(player.OwnerClientId);
            arrow.visible = true;
            arrow.color = team.TeamColor;
            if (!arrow.positioned)
            {
                arrow.rect.anchoredPosition = localPoint;
                arrow.positioned = true;
            }
            else
                arrow.rect.anchoredPosition = Vector2.Lerp(arrow.rect.anchoredPosition,
                    localPoint, 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));

            Vector2 screenDirection = new Vector2(
                direction.x * camera.pixelWidth, direction.y * camera.pixelHeight);
            arrow.rect.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg +
                spriteAngleOffset);
        }

        foreach (Arrow arrow in arrows.Values)
        {
            // Existing arrows must respond to Inspector size/sprite changes too.
            arrow.rect.sizeDelta = arrowSize;
            arrow.image.sprite = arrowSprite;
            float step = fadeDuration > 0f
                ? Time.unscaledDeltaTime * opacity / fadeDuration : 1f;
            arrow.alpha = Mathf.MoveTowards(arrow.alpha,
                arrow.visible ? opacity : 0f, step);
            arrow.rect.gameObject.SetActive(arrow.alpha > 0.001f);
            if (arrow.alpha <= 0f)
                arrow.positioned = false;
            Color color = arrow.color;
            color.a = arrow.alpha;
            arrow.image.color = color;
        }
    }

    private Arrow GetArrow(ulong clientId)
    {
        if (arrows.TryGetValue(clientId, out Arrow arrow))
            return arrow;

        GameObject child = new GameObject("Offscreen Player Arrow",
            typeof(RectTransform), typeof(Image));
        child.transform.SetParent(container, false);
        RectTransform rect = (RectTransform)child.transform;
        // LocalPointInRectangle is relative to the parent's pivot.
        rect.anchorMin = rect.anchorMax = container.pivot;
        rect.sizeDelta = arrowSize;
        Image image = child.GetComponent<Image>();
        image.sprite = arrowSprite;
        image.raycastTarget = false;
        arrow = new Arrow { rect = rect, image = image };
        arrows.Add(clientId, arrow);
        return arrow;
    }
}
