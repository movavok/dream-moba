using Unity.Netcode;
using UnityEngine;

public class GuitaristProjectileLogic : NetworkBehaviour, IProjectileLogic
{
    private Vector3 startPosition;
    private float maxDistance;

    private bool initialized;

    private NetworkVariable<float> networkScale =
        new NetworkVariable<float>(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public void Initialize(ProjectileNetwork projectile)
    {
        if (!IsServer || initialized)
            return;

        startPosition = transform.position;
        maxDistance = projectile.Range;

        initialized = true;
    }

    private void Update()
    {
        if (IsServer)
        {
            UpdateScale();
        }

        ApplyScale(networkScale.Value);
    }

    private void UpdateScale()
    {
        if (!initialized || maxDistance <= 0f)
            return;

        float distance =
            Vector2.Distance(
                startPosition,
                transform.position
            );

        float progress =
            Mathf.Clamp01(
                distance / maxDistance
            );

        float scale =
            Mathf.Lerp(1f, 2f, progress);

        networkScale.Value = scale;
    }

    private void ApplyScale(float scale)
    {
        transform.localScale =
            Vector3.one * scale;
    }
}