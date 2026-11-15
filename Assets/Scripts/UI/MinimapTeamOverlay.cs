using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(MinimapMapCapture))]
public class MinimapTeamOverlay : MonoBehaviour
{
    [SerializeField, Min(1f)] private float dotSize = 14f;
    [SerializeField, Min(1f)] private float frameThickness = 2f;
    [SerializeField] private Color ownFrameColor = Color.yellow;
    [SerializeField] private Color allyFrameColor = Color.white;

    private class Marker
    {
        public RectTransform dot;
        public Image dotImage;
        public RectTransform[] edges = new RectTransform[4];
        public Vector2 lastOutdoorPosition;
        public bool hasOutdoorPosition;
    }

    private readonly Dictionary<ulong, Marker> markers = new();
    private RectTransform mapRect;
    private MinimapMapCapture map;
    private TeamManager teams;
    private Texture2D circleTexture;
    private Sprite circleSprite;

    public Sprite CircleSprite => circleSprite;

    private void Awake()
    {
        mapRect = (RectTransform)transform;
        map = GetComponent<MinimapMapCapture>();

        circleTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        circleTexture.filterMode = FilterMode.Point;
        Color32[] pixels = new Color32[16 * 16];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                pixels[y * 16 + x] = (x - 7.5f) * (x - 7.5f) +
                    (y - 7.5f) * (y - 7.5f) <= 49f
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
        circleTexture.SetPixels32(pixels);
        circleTexture.Apply();
        circleSprite = Sprite.Create(circleTexture,
            new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
    }

    private void OnDestroy()
    {
        if (circleSprite != null)
            Destroy(circleSprite);
        if (circleTexture != null)
            Destroy(circleTexture);
    }

    private void LateUpdate()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsListening || map.WorldBounds.size.x <= 0f)
            return;

        if (teams == null)
            teams = FindAnyObjectByType<TeamManager>();
        if (teams == null)
            return;

        ulong localId = network.LocalClientId;
        short localTeam = teams.GetTeamId(localId);
        foreach (Marker marker in markers.Values)
        {
            marker.dot.gameObject.SetActive(false);
            foreach (RectTransform edge in marker.edges)
                edge.gameObject.SetActive(false);
        }

        TeamCameraViews views = TeamCameraViews.Instance;
        if (views != null)
        {
            ShowFrame(localId, ownFrameColor, views);
            foreach (ulong clientId in views.Reporters)
                if (clientId != localId && teams.GetTeamId(clientId) == localTeam)
                    ShowFrame(clientId, allyFrameColor, views);
        }

        foreach (PlayerNetwork player in FindObjectsByType<PlayerNetwork>())
        {
            if (!player.IsSpawned || player.GetComponent<Team>() is not Team team)
                continue;

            bool ally = team.TeamId.Value == localTeam;
            if (!ally)
            {
                SpriteRenderer visual = player.GetComponent<HeroSorting>()?.HeroRenderer;
                if (visual == null || views == null ||
                    !views.IsVisible(visual.bounds))
                    continue;
            }

            Marker marker = GetMarker(player.OwnerClientId);
            Vector2 position = player.transform.position;
            bool inside = ally && CameraZone.BoundsAt(position) != null;
            if (!inside)
            {
                marker.lastOutdoorPosition = position;
                marker.hasOutdoorPosition = true;
            }

            marker.dot.gameObject.SetActive(true);
            marker.dot.anchoredPosition = WorldToMap(inside && marker.hasOutdoorPosition
                ? marker.lastOutdoorPosition : position);
            marker.dotImage.sprite = inside ? null : circleSprite;
            marker.dotImage.color = team.TeamColor;
        }
    }

    private void ShowFrame(ulong clientId, Color color, TeamCameraViews views)
    {
        if (!views.TryGetView(clientId, out Rect view, out bool insideBuilding) ||
            insideBuilding)
            return;

        Bounds bounds = map.WorldBounds;
        if (view.xMax < bounds.min.x || view.xMin > bounds.max.x ||
            view.yMax < bounds.min.y || view.yMin > bounds.max.y)
            return;

        Vector2 min = WorldToMap(new Vector2(view.xMin, view.yMin));
        Vector2 max = WorldToMap(new Vector2(view.xMax, view.yMax));
        float t = frameThickness;
        Marker marker = GetMarker(clientId);
        SetEdge(marker.edges[0], new Vector2(min.x, min.y), new Vector2(max.x - min.x, t), color);
        SetEdge(marker.edges[1], new Vector2(min.x, max.y - t), new Vector2(max.x - min.x, t), color);
        SetEdge(marker.edges[2], new Vector2(min.x, min.y), new Vector2(t, max.y - min.y), color);
        SetEdge(marker.edges[3], new Vector2(max.x - t, min.y), new Vector2(t, max.y - min.y), color);
    }

    public Vector2 WorldToMap(Vector2 world)
    {
        Bounds bounds = map.WorldBounds;
        return new Vector2(
            Mathf.InverseLerp(bounds.min.x, bounds.max.x, world.x) * mapRect.rect.width,
            Mathf.InverseLerp(bounds.min.y, bounds.max.y, world.y) * mapRect.rect.height);
    }

    private Marker GetMarker(ulong clientId)
    {
        if (markers.TryGetValue(clientId, out Marker marker))
            return marker;

        marker = new Marker();
        for (int i = 0; i < marker.edges.Length; i++)
            marker.edges[i] = CreateImage("Camera View Edge", Color.white);

        marker.dot = CreateImage("Player Dot", Color.white);
        marker.dot.pivot = new Vector2(0.5f, 0.5f);
        marker.dot.sizeDelta = new Vector2(dotSize, dotSize);
        marker.dotImage = marker.dot.GetComponent<Image>();
        markers.Add(clientId, marker);
        return marker;
    }

    private RectTransform CreateImage(string name, Color color)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(mapRect, false);
        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = (RectTransform)child.transform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        child.SetActive(false);
        return rect;
    }

    private static void SetEdge(RectTransform edge, Vector2 position, Vector2 size, Color color)
    {
        edge.gameObject.SetActive(true);
        edge.anchoredPosition = position;
        edge.sizeDelta = size;
        edge.GetComponent<Image>().color = color;
    }
}
