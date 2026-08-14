using UnityEngine;
using Unity.Cinemachine;

public class PlayerCamera : MonoBehaviour
{
    public static PlayerCamera Instance { get; private set; }

    [SerializeField] private CinemachineCamera virtualCamera;

    public bool IsReady => virtualCamera != null;

    private void Awake()
    {
        Instance = this;

        Debug.Log(
            $"PlayerCamera Awake: {gameObject.name}, " +
            $"VirtualCamera = {virtualCamera}"
        );
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