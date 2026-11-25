using UnityEngine;

// Local-only prefab visual. World-space points follow the hero's recent path,
// while the first vertex stays attached to the feet every frame.
[DisallowMultipleComponent]
public class PixelLightningVisual : MonoBehaviour
{
    [SerializeField] private Transform feet;
    [SerializeField, Range(2, 3)] private int lightningCount = 2;
    [SerializeField] private Color startColor = Color.white;
    [SerializeField] private Color endColor = Color.yellow;
    [SerializeField, Min(0.001f)] private float width = 0.0625f;
    [SerializeField, Min(0f)] private float minLength = 0.1875f;
    [SerializeField, Min(0f)] private float maxLength = 1.25f;
    [SerializeField, Min(0f)] private float lengthPerSpeed = 0.18f;
    [SerializeField, Range(2, 32)] private int segments = 8;
    [SerializeField, Min(0f)] private float deviation = 0.1875f;
    [SerializeField, Range(1f, 60f)] private float refreshRate = 14f;
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;
    [SerializeField] private Material material;
    [SerializeField] private string sortingLayer = "Effect";
    [SerializeField] private int orderInLayer = 1;

    private sealed class Bolt
    {
        public LineRenderer line;
        public Vector3[] points;
        public float[] offsets;
        public float nextRefresh;
    }

    private Bolt[] bolts;
    private const int HistoryCapacity = 256;
    private readonly Vector2[] history = new Vector2[HistoryCapacity];
    private Vector2[] path;
    private int historyHead;
    private int historyCount;
    private Transform actor;
    private Vector2 previousPosition;
    private Vector2 previousFeet;
    private Vector2 lastDirection = Vector2.down;
    private Vector2 drawnFeet;
    private float smoothedSpeed;
    private float drawnLength = -1f;
    private bool visible;

    public void Initialize(Transform movementSource)
    {
        actor = movementSource;
        if (feet == null)
            Debug.LogWarning("PixelLightningVisual needs a feet Transform in its prefab.", this);
        else
            SetVisible(true);
    }

    public void SetVisible(bool active)
    {
        if (visible == active)
            return;

        visible = active;
        if (!active)
        {
            if (bolts != null)
                foreach (Bolt bolt in bolts)
                    bolt.line.enabled = false;
            return;
        }

        if (feet == null)
        {
            visible = false;
            return;
        }

        if (bolts == null)
            CreateBolts();

        previousPosition = actor.position;
        previousFeet = feet.position;
        drawnFeet = previousFeet;
        historyCount = 0;
        smoothedSpeed = 0f;
        drawnLength = -1f;
        if (path == null || path.Length != segments + 1)
            path = new Vector2[segments + 1];
        for (int i = 0; i < bolts.Length; i++)
        {
            Bolt bolt = bolts[i];
            bolt.line.enabled = true;
            bolt.nextRefresh = Time.time + i / (refreshRate * bolts.Length);
            Randomize(bolt);
        }
        BuildPath(previousFeet, minLength);
        foreach (Bolt bolt in bolts)
            Draw(bolt);
        drawnLength = minLength;
    }

    private void CreateBolts()
    {
        bolts = new Bolt[lightningCount];
        for (int i = 0; i < bolts.Length; i++)
        {
            GameObject child = new GameObject("Pixel Lightning " + (i + 1));
            child.transform.SetParent(feet, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            line.startColor = startColor;
            line.endColor = endColor;
            line.startWidth = width;
            line.endWidth = width;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.sortingLayerName = sortingLayer;
            line.sortingOrder = orderInLayer;
            line.positionCount = segments * 2 + 1;
            bolts[i] = new Bolt
            {
                line = line,
                points = new Vector3[segments * 2 + 1],
                offsets = new float[segments + 1]
            };
        }
    }

    private void LateUpdate()
    {
        if (!visible || feet == null || actor == null)
            return;

        Vector2 current = actor.position;
        Vector2 currentFeet = feet.position;
        float dt = Time.deltaTime;
        Vector2 displacement = current - previousPosition;
        Vector2 velocity = dt > 0f ? displacement / dt : Vector2.zero;
        previousPosition = current;

        // Use rendered displacement on remote peers, not input/facing or their kinematic RB velocity.
        if (displacement.sqrMagnitude > maxLength * maxLength * 4f)
        {
            // A teleport should not drag a trail across the whole map.
            historyCount = 0;
            velocity = Vector2.zero;
            smoothedSpeed = 0f;
        }
        else if (velocity.sqrMagnitude > 0.01f)
        {
            lastDirection = velocity.normalized;
            PushHistory(previousFeet);
        }
        previousFeet = currentFeet;

        // FixedUpdate/server movement can span several render frames; smooth only
        // its magnitude so the length does not flicker between zero and a spike.
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, velocity.magnitude,
            1f - Mathf.Exp(-dt * 18f));
        float length = Mathf.Clamp(minLength + smoothedSpeed * lengthPerSpeed,
            minLength, Mathf.Max(minLength, maxLength));
        bool moved = currentFeet.x != drawnFeet.x || currentFeet.y != drawnFeet.y ||
            Mathf.Abs(length - drawnLength) > 0.001f;
        if (moved)
            BuildPath(currentFeet, length);
        for (int i = 0; i < bolts.Length; i++)
        {
            Bolt bolt = bolts[i];
            if (Time.time >= bolt.nextRefresh)
            {
                Randomize(bolt);
                bolt.nextRefresh = Time.time + 1f / refreshRate;
                Draw(bolt);
            }
            else if (moved)
                Draw(bolt);
        }
        drawnFeet = currentFeet;
        drawnLength = length;
    }

    private void Randomize(Bolt bolt)
    {
        bolt.offsets[0] = 0f;
        for (int i = 1; i < bolt.offsets.Length; i++)
            bolt.offsets[i] = Random.Range(-deviation, deviation);
    }

    private void PushHistory(Vector2 position)
    {
        float spacing = 0.5f / Mathf.Max(1, pixelsPerUnit);
        if (historyCount > 0 &&
            (position - history[historyHead]).sqrMagnitude < spacing * spacing)
            return;

        historyHead = (historyHead + 1) % HistoryCapacity;
        history[historyHead] = position;
        historyCount = Mathf.Min(historyCount + 1, HistoryCapacity);
    }

    private void BuildPath(Vector2 origin, float length)
    {
        path[0] = origin;
        Vector2 previous = origin;
        Vector2 extension = -lastDirection;
        float travelled = 0f;
        int sampleIndex = 0;
        for (int i = 1; i <= segments; i++)
        {
            float target = length * i / segments;
            while (sampleIndex < historyCount)
            {
                Vector2 sample = history[(historyHead - sampleIndex + HistoryCapacity) % HistoryCapacity];
                float distance = Vector2.Distance(previous, sample);
                if (travelled + distance >= target && distance > 0f)
                    break;
                if (distance > 0f)
                    extension = (sample - previous) / distance;
                travelled += distance;
                previous = sample;
                sampleIndex++;
            }

            if (sampleIndex < historyCount)
            {
                Vector2 sample = history[(historyHead - sampleIndex + HistoryCapacity) % HistoryCapacity];
                float distance = Vector2.Distance(previous, sample);
                path[i] = Vector2.Lerp(previous, sample, (target - travelled) / distance);
            }
            else
                path[i] = previous + extension * (target - travelled);
        }
    }

    private void Draw(Bolt bolt)
    {
        float ppu = Mathf.Max(1, pixelsPerUnit);
        Vector3[] points = bolt.points;
        points[0] = path[0]; // Exact feet position, even between shape refreshes.
        for (int i = 1; i <= segments; i++)
        {
            Vector2 tangent = path[i] - path[i - 1];
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = -lastDirection;
            tangent.Normalize();
            Vector2 side = new(-tangent.y, tangent.x);
            Vector2 target = path[i] + side * bolt.offsets[i];
            float x = Mathf.Round(target.x * ppu) / ppu;
            float y = Mathf.Round(target.y * ppu) / ppu;
            // Orthogonal steps only: no diagonal/smoothed segments between grid points.
            Vector3 previous = points[2 * i - 2];
            points[2 * i - 1] = new Vector3(x, previous.y, 0f);
            points[2 * i] = new Vector3(x, y, 0f);
        }
        bolt.line.SetPositions(points);
    }

}
