using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(MinimapTeamOverlay))]
public class MinimapObjectMarkers : MonoBehaviour
{
    [SerializeField] private Color itemColor = Color.yellow;
    [SerializeField] private Color bossColor = new Color(1f, 0.48f, 0.04f);
    [SerializeField, Min(1f)] private float itemSize = 14f;
    [SerializeField, Min(1f)] private float flagSize = 24f;
    [SerializeField, Min(1f)] private float bossSize = 18f;

    private class Icon
    {
        public NetworkObject source;
        public RectTransform rect;
        public Image image;
    }

    private readonly Dictionary<ulong, Icon> icons = new();
    private readonly List<ulong> expired = new();
    private MinimapTeamOverlay map;
    private Sprite triangle;
    private Sprite cross;
    private Texture2D triangleTexture;
    private Texture2D crossTexture;

    private void Awake()
    {
        map = GetComponent<MinimapTeamOverlay>();
        triangle = CreateShape(true, out triangleTexture);
        cross = CreateShape(false, out crossTexture);
    }

    private void OnDestroy()
    {
        if (triangle != null) Destroy(triangle);
        if (cross != null) Destroy(cross);
        if (triangleTexture != null) Destroy(triangleTexture);
        if (crossTexture != null) Destroy(crossTexture);
    }

    private void LateUpdate()
    {
        foreach (Icon icon in icons.Values)
            icon.rect.gameObject.SetActive(false);

        TeamCameraViews vision = TeamCameraViews.Instance;
        NetworkManager network = NetworkManager.Singleton;
        if (vision != null && network != null && network.IsListening)
        {
            foreach (WorldItem item in FindObjectsByType<WorldItem>())
                if (item.IsSpawned && TryGetVisualBounds(item, out Bounds bounds) &&
                    vision.IsVisible(bounds))
                    Show(item.NetworkObject, item.transform.position,
                        map.CircleSprite, itemColor, itemSize);

            foreach (MinibossCorpseSource boss in FindObjectsByType<MinibossCorpseSource>())
            {
                NetworkObject source = boss.GetComponent<NetworkObject>();
                Health health = boss.GetComponent<Health>();
                if (source != null && source.IsSpawned && health != null &&
                    health.IsAlive() && TryGetVisualBounds(boss, out Bounds bounds) &&
                    vision.IsVisible(bounds))
                    Show(source, boss.transform.position, cross, bossColor, bossSize);
            }

            foreach (FlagObject flag in FindObjectsByType<FlagObject>())
            {
                if (!flag.IsSpawned)
                    continue;

                if (!flag.IsCarried)
                {
                    if (TryGetVisualBounds(flag, out Bounds bounds) && vision.IsVisible(bounds))
                        Show(flag.NetworkObject, flag.transform.position,
                            triangle, itemColor, flagSize);
                    continue;
                }

                foreach (PlayerNetwork player in FindObjectsByType<PlayerNetwork>())
                {
                    if (!player.IsSpawned || player.OwnerClientId != flag.CarrierClientId)
                        continue;

                    Team team = player.GetComponent<Team>();
                    Team localTeam = PlayerNetwork.LocalPlayer != null
                        ? PlayerNetwork.LocalPlayer.GetComponent<Team>() : null;
                    TeamManager teams = FindAnyObjectByType<TeamManager>();
                    short ownId = teams != null ? teams.GetTeamId(network.LocalClientId)
                        : localTeam != null ? localTeam.TeamId.Value : (short)0;
                    bool ally = team != null && team.TeamId.Value == ownId;
                    if (ally || (TryGetVisualBounds(player, out Bounds bounds) &&
                                 vision.IsVisible(bounds)))
                        Show(flag.NetworkObject, player.transform.position,
                            triangle, flag.CarrierTeamColor, flagSize);
                    break;
                }
            }
        }

        expired.Clear();
        foreach (var pair in icons)
            if (pair.Value.source == null || !pair.Value.source.IsSpawned)
                expired.Add(pair.Key);
        foreach (ulong id in expired)
        {
            if (icons[id].rect != null)
                Destroy(icons[id].rect.gameObject);
            icons.Remove(id);
        }
    }

    private void Show(NetworkObject source, Vector2 world, Sprite sprite, Color color, float size)
    {
        if (sprite == null)
            return;

        if (!icons.TryGetValue(source.NetworkObjectId, out Icon icon) || icon.source != source)
        {
            if (icon != null && icon.rect != null)
                Destroy(icon.rect.gameObject);

            GameObject child = new GameObject("Map Object", typeof(RectTransform), typeof(Image));
            child.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)child.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            Image image = child.GetComponent<Image>();
            image.raycastTarget = false;
            icon = new Icon { source = source, rect = rect, image = image };
            icons[source.NetworkObjectId] = icon;
        }

        icon.rect.gameObject.SetActive(true);
        icon.rect.anchoredPosition = map.WorldToMap(world);
        icon.rect.sizeDelta = new Vector2(size, size);
        icon.image.sprite = sprite;
        icon.image.color = color;
    }

    private static bool TryGetVisualBounds(Component source, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (SpriteRenderer renderer in source.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!renderer.enabled || renderer.sprite == null)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
                bounds.Encapsulate(renderer.bounds);
        }

        return found;
    }

    private static Sprite CreateShape(bool isTriangle, out Texture2D texture)
    {
        const int size = 20;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool filled = isTriangle
                    ? y >= 2 && y <= 18 && Mathf.Abs(x - 9.5f) <= (18 - y) * 0.52f
                    : Mathf.Abs(x - y) <= 1.5f || Mathf.Abs(x + y - 19) <= 1.5f;
                pixels[y * size + x] = new Color32(255, 255, 255, filled ? (byte)255 : (byte)0);
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
