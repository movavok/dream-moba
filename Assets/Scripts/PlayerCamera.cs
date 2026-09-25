using UnityEngine;
using Unity.Cinemachine;

public class PlayerCamera : MonoBehaviour
{
    public static PlayerCamera Instance { get; private set; }

    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private CinemachineImpulseSource impulseSource;

    public bool IsReady => virtualCamera != null;

    private void Awake()
    {
        Instance = this;

        Debug.Log(
            $"PlayerCamera Awake: {gameObject.name}, " +
            $"VirtualCamera = {virtualCamera}"
        );
    }

    public void Shake(float damage)
    {
        if (impulseSource == null)
            return;

        float strength = Mathf.Clamp(damage / 100f, 0.005f, 0.025f);

        float angle = Random.Range(0f, Mathf.PI * 2f);

        Vector3 direction = new Vector3(
            Mathf.Cos(angle),
            Mathf.Sin(angle),
            0f
        );

        impulseSource.GenerateImpulse(direction * strength);
}

    public void SetImpulseSource(CinemachineImpulseSource source)
    {
        impulseSource = source;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void FollowPlayer(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("PlayerCamera: target is null!");
            return;
        }

        if (virtualCamera == null)
        {
            Debug.LogError(
                "PlayerCamera: CinemachineCamera is not assigned!"
            );
            return;
        }

        virtualCamera.Follow = target;

        Debug.Log(
            $"Camera attached to: {target.name}"
        );
    }

    public void MoveToPosition(Vector3 position)
    {
        if (virtualCamera == null)
        {
            Debug.LogError(
                "PlayerCamera: CinemachineCamera is not assigned!"
            );
            return;
        }

        virtualCamera.Follow = null;

        Vector3 currentPosition =
            virtualCamera.transform.position;

        virtualCamera.transform.position =
            new Vector3(
                position.x,
                position.y,
                currentPosition.z
            );
    }
}