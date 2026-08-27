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

    [SerializeField] private Transform body;
    public Transform Body => body;

    public HeroDefinition HeroData => heroData;

    private Rigidbody2D rb;

    private Vector2 moveInput;
    private Vector2 lastPosition;

    private Vector2 lastMoveDirection = Vector2.down;

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

    public event System.Action Death;

    public void PlayDeathAudio()
    {
        if (!IsServer)
            return;

        PlayDeathAudioClientRpc();
    }

    [ClientRpc]
    private void PlayDeathAudioClientRpc()
    {
        Death?.Invoke();
    }

    public void OnMove(InputValue value)
    {
        if (!IsOwner || !IsSpawned)
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
    public event System.Action AttackUsed;
    [ClientRpc]
    private void AttackUsedClientRpc()
    {
        AttackUsed?.Invoke();
    }

    private bool isAttacking;
    private Vector2 aimDirection;

    public void OnAttack(InputValue value)
    {
        if (!IsOwner || !IsSpawned)
            return;

        isAttacking = value.isPressed;

        SetAttackingServerRpc(isAttacking);
    }

    // Aim input
    private Camera mainCamera;
    private Vector2 mousePosition;

    public void OnAim(InputValue value)
    {
        if (!IsOwner || !IsSpawned)
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
        if (!IsSpawned)
            return;

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

        projectileNetwork.InitializeVisual();

        projectileNetwork.SetTrail(
            projectileTrailActive.Value
        );

        projectileNetwork.SetBehindPlayer(
            lastMoveDirection.y > 0
        );
    }

        private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        rb = GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogError(
                $"PlayerNetwork on {gameObject.name} has no Rigidbody2D!"
            );
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer)
            return;

        if (heroData == null || rb == null)
            return;

        float speed =
            heroData.stats.moveSpeed *
            speedMultiplier;

        Vector2 moveDirection = moveInput;

        rb.linearVelocity = moveDirection * speed;

        Vector2 actualMovement =
            rb.position - lastPosition;

        bool isMoving =
            actualMovement.sqrMagnitude > 0.000001f;

        if (moveInput.sqrMagnitude > 0.01f)
        {
            lastMoveDirection = moveInput;
        }

        networkMoveDirection.Value = lastMoveDirection;
        networkIsMoving.Value = isMoving;

        lastPosition = rb.position;
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        Vector2 direction = networkMoveDirection.Value;

        animator.SetBool("IsMoving", networkIsMoving.Value);

        animator.SetFloat("MoveX", direction.x);
        animator.SetFloat("MoveY", direction.y);
    }

    // Server simulation
    private float nextAttackTime;

    private NetworkVariable<float> attackCooldownRemaining =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public float AttackCooldownRemaining =>
        attackCooldownRemaining.Value;

    public float AttackCooldownDuration =>
        heroData != null ? heroData.attack.cooldown : 0f;

    private void Update()
    {
        if (IsServer)
        {
            if (heroData == null)
                return;

            if (isAttacking && Time.time >= nextAttackTime)
            {
                nextAttackTime =
                    Time.time +
                    heroData.attack.cooldown *
                    (1 / attackCooldownMultiplier);

                ShootProjectile(aimDirection, moveInput);

                AttackUsedClientRpc();
            }

            attackCooldownRemaining.Value =
                Mathf.Max(
                    0f,
                    nextAttackTime - Time.time
                );
        }

        UpdateAnimator();
    }
    
    // Network setup
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        GrassInteractionManager.Instance?.RegisterPlayer(body);

        if (heroVisual != null)
        {
            heroVisual.Initialize(heroData);
        }

        if (IsOwner)
        {
            Debug.Log("Its my player");
            StartCoroutine(SetupHUD());
            StartCoroutine(SetupCamera());
        }

        if (IsServer)
        {
            lastPosition = rb.position;
        }
    }

    public override void OnNetworkDespawn()
    {
        GrassInteractionManager.Instance?.UnregisterPlayer(body);

        base.OnNetworkDespawn();
    }

    private System.Collections.IEnumerator SetupHUD()
    {
        while (HeroHUD.Instance == null)
        {
            yield return null;
        }

        Debug.Log("PlayerNetwork: HeroHUD found, initializing");

        HeroHUD.Instance.Initialize(heroData);
        HeroHUD.Instance.SetPlayerNetwork(this);

        PlayerAbilities abilities = GetComponent<PlayerAbilities>();

        if (abilities == null)
        {
            Debug.LogError("PlayerNetwork: PlayerAbilities not found!");
            yield break;
        }

        HeroHUD.Instance.SetAbilities(abilities);
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