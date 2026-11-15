using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.U2D.Animation;

public class MinibossCorpseVisual : NetworkBehaviour
{
    [System.Serializable]
    private struct LibraryEntry
    {
        public string id;
        public SpriteLibraryAsset library;
    }

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SpriteRenderer behindOutline;
    [SerializeField] private SpriteLibrary spriteLibrary;
    [SerializeField] private LibraryEntry[] libraries;

    private readonly NetworkVariable<FixedString64Bytes> visualId = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private static readonly int TeamColorId = Shader.PropertyToID("_TeamColor");
    private static readonly int PatternEnabledId = Shader.PropertyToID("_PatternEnabled");

    private void Awake()
    {
        if (behindOutline == null)
            return;

        var properties = new MaterialPropertyBlock();
        behindOutline.GetPropertyBlock(properties);
        properties.SetColor(TeamColorId, Color.yellow);
        properties.SetFloat(PatternEnabledId, 0f);
        behindOutline.SetPropertyBlock(properties);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        visualId.OnValueChanged += OnVisualIdChanged;
        ApplyLibrary();
    }

    public override void OnNetworkDespawn()
    {
        visualId.OnValueChanged -= OnVisualIdChanged;
        base.OnNetworkDespawn();
    }

    public void Initialize(string id)
    {
        if (!IsServer || string.IsNullOrEmpty(id))
            return;

        visualId.Value = id;
    }

    private void OnVisualIdChanged(FixedString64Bytes previous, FixedString64Bytes current)
    {
        ApplyLibrary();
    }

    private void ApplyLibrary()
    {
        if (spriteLibrary == null || visualId.Value.IsEmpty)
            return;

        string id = visualId.Value.ToString();
        if (libraries != null)
            foreach (LibraryEntry entry in libraries)
                if (entry.id == id && entry.library != null)
                {
                    spriteLibrary.spriteLibraryAsset = entry.library;
                    return;
                }

        Debug.LogError($"[MinibossCorpseVisual] No Sprite Library for '{id}'.", this);
    }

    private void LateUpdate()
    {
        if (spriteRenderer != null && behindOutline != null)
            behindOutline.sprite = spriteRenderer.sprite;
    }
}
