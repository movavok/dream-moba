using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class PlayerNetwork : NetworkBehaviour
{
    // Movement
    [SerializeField] private HeroDefinition heroData;

    [SerializeField] private HeroVisual heroVisual;

    [SerializeField] private Animator animator;

    public HeroDefinition HeroData => heroData;

    private Vector2 moveInput;

    private NetworkVariable<Vector2> networkMoveDirection =
    new NetworkVariable<Vector2>(
        Vector2.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> networkIsMoving =
    new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public void OnMove(InputValue value)
    {
        if (!IsOwner)
            return;

        moveInput = value.Get<Vector2>();

        SendMoveServerRpc(moveInput);
    }

    private float speedMultiplier = 1f;

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    private float attackCooldownMultiplier = 1f;

    public void SetAttackCooldownMultiplier(float multiplier)
    {
        attackCooldownMultiplier = multiplier;
    }

    // Combat input
    private bool isAttacking;
    private Vector2 aimDirection;

    public void OnAttack(InputValue value)
    {
        if (!IsOwner)
            return;

        isAttacking = value.isPressed;

        SetAttackingServerRpc(isAttacking);
    }

    // Aim input
    private Camera mainCamera;
    private Vector2 mousePosition;

    public void OnAim(InputValue value)
    {
        if (!IsOwner)
        return;

        mousePosition = value.Get<Vector2>();

        Camera cam = GetMainCamera();

        if (cam == null)
            return;

        SetAimDirectionServerRpc(GetAimDirection());
    }

    private NetworkVariable<bool> projectileTrailActive =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public void SetProjectileTrail(bool active)
    {
        if (IsServer)
        {
            projectileTrailActive.Value = active;
        }
        else
        {
            SetProjectileTrailServerRpc(active);
        }
    }

    [ServerRpc]
    private void SetProjectileTrailServerRpc(bool active)
    {
        projectileTrailActive.Value = active;
    }

    private Camera GetMainCamera()
    {
        if (mainCamera == null)
        {
        mainCamera = Camera.main;
        }

        return mainCamera;
    }

    private Vector3 GetProjectileSpawnPosition()
    {
        if (heroData.attack.spawnMode == ProjectileSpawnMode.Point)
        {
            Vector2 offset = heroData.attack.projectileSpawnOffset;

            return transform.position + (Vector3)offset;
        }

        return transform.position;
    }

    private Vector2 GetAimDirection()
    {
        Camera cam = GetMainCamera();

        if (cam == null)
            return Vector2.zero;

        Vector3 mouseWorldPosition =
            cam.ScreenToWorldPoint(mousePosition);

        Vector3 spawnPosition =
            GetProjectileSpawnPosition();

        Vector2 direction =
            (Vector2)mouseWorldPosition -
            (Vector2)spawnPosition;

        return direction.normalized;
    }

    [ServerRpc]
    private void SendMoveServerRpc(Vector2 input)
    {
        moveInput = input;
    }

    // Shared combat state
    

    [ServerRpc]
    private void SetAttackingServerRpc(bool attacking)
    {
        isAttacking = attacking;
    }

    [ServerRpc]
    private void SetAimDirectionServerRpc(Vector2 direction)
    {
        aimDirection = direction;
    }

    private float projectileSpeedMultiplier = 1f;

    public void SetProjectileSpeedMultiplier(float multiplier)
    {
        projectileSpeedMultiplier = multiplier;
    }

    // Projectile spawning
    private void ShootProjectile(Vector2 direction, Vector2 moveDirection)
    {
        if (heroData == null || heroData.attack == null)
            return;

        if (heroData.attack.projectilePrefab == null)
        {
            Debug.LogError(
                $"Hero {heroData.heroName} has no projectile prefab!"
            );
            return;
        }

        Vector3 spawnPosition;

        // Determine spawn position based on the spawn mode
        if (heroData.attack.spawnMode == ProjectileSpawnMode.Point)
        {
            Vector2 offset = heroData.attack.projectileSpawnOffset;

            spawnPosition = transform.TransformPoint(offset);
        }
        else
        {
            spawnPosition =
                transform.position +
                (Vector3)(
                    direction *
                    heroData.attack.projectileSpawnDistance
                );
        }

        GameObject projectile = Instantiate(
            heroData.attack.projectilePrefab,
            spawnPosition,
            Quaternion.identity
        );

        ProjectileNetwork projectileNetwork =
            projectile.GetComponent<ProjectileNetwork>();

        if (projectileNetwork == null)
        {
            Debug.LogError(
                $"Projectile prefab {heroData.attack.projectilePrefab.name} " +
                $"doesn't have ProjectileNetwork!"
            );

            Destroy(projectile);
            return;
        }

        projectileNetwork.SetOwnerStreak(
            GetComponent<PlayerStreak>()
        );

        projectileNetwork.SetSpeedMultiplier(
            projectileSpeedMultiplier
        );

        projectileNetwork.SetTeam(
            GetComponent<Team>().TeamId.Value
        );

        projectileNetwork.SetDirection(direction);

        projectile.GetComponent<NetworkObject>().Spawn();

        projectileNetwork.SetTrail(
            projectileTrailActive.Value
        );

        projectileNetwork.SetBehindPlayer(
            moveDirection.y > 0
        );
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        Vector2 direction = networkMoveDirection.Value;

        animator.SetFloat("MoveX", direction.x);
        animator.SetFloat("MoveY", direction.y);
        animator.SetBool("IsMoving", networkIsMoving.Value);
    }

    // Server simulation
    private float nextAttackTime;

    private void Update()
    {
        if (IsServer)
        {
            if (heroData == null)
                return;

            float speed = heroData.stats.moveSpeed * speedMultiplier;

            Vector2 moveDirection = moveInput;

            transform.position +=
                (Vector3)moveDirection * speed * Time.deltaTime;

            // Update network variables for clients
            networkMoveDirection.Value = moveDirection;
            networkIsMoving.Value = moveDirection.sqrMagnitude > 0.01f;

            if (isAttacking && Time.time >= nextAttackTime)
            {
                nextAttackTime =
                    Time.time +
                    heroData.attack.cooldown *
                    (1 / attackCooldownMultiplier);

                ShootProjectile(aimDirection, moveInput);
            }
        }

        UpdateAnimator();
    }
    
    // Network setup
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (heroVisual != null)
        {
            heroVisual.Initialize(heroData);
        }

        if (IsOwner)
        {
            Debug.Log("Its my player");
            StartCoroutine(SetupCamera());
        }
    }

    private System.Collections.IEnumerator SetupCamera()
    {
        if (!IsOwner)
            yield break;

        Debug.Log($"[{OwnerClientId}] SetupCamera started.");

        // Wait until PlayerCamera instance is available
        while (PlayerCamera.Instance == null)
        {
            yield return null;
        }

        // Wait until PlayerCamera is ready
        while (!PlayerCamera.Instance.IsReady)
        {
            yield return null;
        }

        PlayerCamera.Instance.FollowPlayer(transform);

        Debug.Log(
            $"[{OwnerClientId}] Camera attached to player " +
            $"NetworkObjectId={NetworkObjectId}"
        );
    }

}