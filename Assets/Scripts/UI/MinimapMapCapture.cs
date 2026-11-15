using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class MinimapMapCapture : MonoBehaviour
{
    [SerializeField] private Transform mapRoot;
    [SerializeField] private Collider2D cameraBounds;
    [SerializeField, Min(16)] private int maxResolution = 256;

    private Texture2D snapshot;
    private Sprite snapshotSprite;

    public Bounds WorldBounds => cameraBounds != null ? cameraBounds.bounds : default;

    private IEnumerator Start()
    {
        if (mapRoot == null || cameraBounds == null ||
            cameraBounds.bounds.size.x <= 0f || cameraBounds.bounds.size.y <= 0f)
        {
            Debug.LogError("MinimapMapCapture: Grid and camera bounds must be assigned.", this);
            yield break;
        }

        // The Grid shares Unity layers with players and UI. Temporarily put only
        // its renderers on an unused layer for one camera frame, then restore them.
        const int captureLayer = 31;
        if (!string.IsNullOrEmpty(LayerMask.LayerToName(captureLayer)))
        {
            Debug.LogError("MinimapMapCapture: layer 31 must be unused.", this);
            yield break;
        }

        yield return null; // Wait for the tilemaps to initialize.

        Bounds bounds = cameraBounds.bounds;
        int width = Mathf.Max(16, Mathf.RoundToInt(maxResolution *
            bounds.size.x / Mathf.Max(bounds.size.x, bounds.size.y)));
        int height = Mathf.Max(16, Mathf.RoundToInt(maxResolution *
            bounds.size.y / Mathf.Max(bounds.size.x, bounds.size.y)));

        Renderer[] renderers = mapRoot.GetComponentsInChildren<Renderer>();
        GameObject[] objects = new GameObject[renderers.Length];
        int[] originalLayers = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            objects[i] = renderers[i].gameObject;
            originalLayers[i] = objects[i].layer;
        }

        GameObject cameraObject = new GameObject("Minimap Capture Camera");
        Camera capture = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<UniversalAdditionalCameraData>();
        RenderTexture target = new RenderTexture(width, height, 16);
        target.filterMode = FilterMode.Point;

        capture.enabled = false;
        capture.orthographic = true;
        capture.orthographicSize = bounds.extents.y;
        capture.aspect = bounds.size.x / bounds.size.y;
        capture.transform.position = new Vector3(bounds.center.x, bounds.center.y, -100f);
        capture.cullingMask = 1 << captureLayer;
        capture.clearFlags = CameraClearFlags.SolidColor;
        Camera gameCamera = Camera.main;
        capture.backgroundColor = gameCamera != null
            ? gameCamera.backgroundColor : Color.black;
        capture.nearClipPlane = 0.1f;
        capture.farClipPlane = 200f;
        capture.depth = 100f;
        capture.targetTexture = target;

        try
        {
            for (int i = 0; i < objects.Length; i++)
                objects[i].layer = captureLayer;

            capture.enabled = true;
            yield return new WaitForEndOfFrame();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            try
            {
                snapshot = new Texture2D(width, height, TextureFormat.RGBA32, false);
                snapshot.filterMode = FilterMode.Point;
                snapshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                snapshot.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
            }

            Image image = GetComponent<Image>();
            snapshotSprite = Sprite.Create(snapshot,
                new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
            image.sprite = snapshotSprite;
        }
        finally
        {
            capture.enabled = false;
            for (int i = 0; i < objects.Length; i++)
                if (objects[i] != null)
                    objects[i].layer = originalLayers[i];

            capture.targetTexture = null;
            Destroy(cameraObject);
            Destroy(target);
        }
    }

    private void OnDestroy()
    {
        if (snapshotSprite != null)
            Destroy(snapshotSprite);
        if (snapshot != null)
            Destroy(snapshot);
    }
}
