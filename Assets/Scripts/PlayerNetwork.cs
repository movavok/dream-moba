using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using Unity.Collections;
using System.Collections;
using Unity.Services.Authentication;

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

    public Vector2 LastMoveDirection => lastMoveDirection;

    private StatController statController;

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

    private NetworkVariable<FixedString64Bytes> playerName =
        new NetworkVariable<FixedString64Bytes>(
            "",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public string PlayerName => playerName.Value.ToString();

    public event System.Action PlayerNameChanged;

    private void OnPlayerNameChanged(
        FixedString64Bytes oldValue,
        FixedString64Bytes newValue)
    {
        PlayerNameChanged?.Invoke();
    }

    [ServerRpc]
    private void SetPlayerNameServerRpc(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            nickname = "Player";

        playerName.Value = nickname.Trim();
    }

    public static PlayerNetwork LocalPlayer { get; private set; }
    public static event System.Action<PlayerNetwork> LocalPlayerSpawned;

    public event System.Action Death;

    [ServerRpc]
    public void RequestInteractionServerRpc(
        NetworkObjectReference targetReference)
    {
        if (!IsServer || !IsSpawned || !IsAliveForInteraction())
            return;

        if (!targetReference.TryGet(out NetworkObject target))
            return;

        IInteractable interactable =
            FindInteractable(target.gameObject);

        if (interactable == null ||
            !interactable.CanInteract(this))
            return;

        if (Vector2.Distance(transform.position, target.transform.position) >
            1.75f)
            return;

        interactable.Interact(this);
    }

    private IInteractable FindInteractable(GameObject target)
    {
        MonoBehaviour[] components =
            target.GetComponentsInParent<MonoBehaviour>();

        foreach (MonoBehaviour component in components)
        {
            if (component is IInteractable interactable)
                return interactable;
        }

        return null;
    }

    private bool IsAliveForInteraction()
    {
        Health health = GetComponent<Health>();
        return health != null && health.IsAlive();
    }

    [ServerRpc]
    private void RequestThrowServerRpc(bool strongThrow)
    {
        if (!IsServer || !IsAliveForInteraction())
            return;

        Vector2 direction = lastMoveDirection;
        if (direction.sqrMagnitude < 0.01f)
            direction = Vector2.down;

        float distanceMultiplier = strongThrow ? 3f : 1f;

        Inventory inventory = GetComponent<Inventory>();
        if (inventory == null)
            return;

        int selected = inventory.SelectedSlot.Value;
        ItemDefinition selectedItem = inventory.GetItem(selected);
        if (selectedItem == null)
            return;

        foreach (NetworkObject networkObject in
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            MonoBehaviour[] components =
                networkObject.GetComponents<MonoBehaviour>();

            foreach (MonoBehaviour component in components)
            {
                if (component is not ICarriableItem carriable ||
                    !carriable.IsCarriedBy(this) ||
                    carriable.InventoryItem != selectedItem)
                    continue;

                carriable.Throw(
                    this,
                    direction.normalized,
                    distanceMultiplier
                );

                return;
            }
        }

        inventory.TryThrow(selected, direction, strongThrow);
    }

    private InputAction throwAction;
    private InputAction forceAction;

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

    private void OnThrowPerformed(InputAction.CallbackContext context)
    {
        if (!IsOwner || !IsSpawned)
            return;

        bool strongThrow =
            forceAction != null &&
            forceAction.IsPressed();

        RequestThrowServerRpc(strongThrow);
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

        if (input.sqrMagnitude > 0.01f)
            DisableSpawnProtection();
    }

    public void DisableSpawnProtection()
    {
        Health health = GetComponent<Health>();

        if (health != null)
            health.DisableSpawnProtection();
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

        projectileNetwork.SetAttackerStats(
            GetComponent<PlayerStreak>()
        );

        projectileNetwork.SetSpeedMultiplier(
            projectileSpeedMultiplier
        );

        var team = GetComponent<Team>();
        projectileNetwork.SetTeam(
            team.TeamId.Value
        );

        projectileNetwork.SetAttacker(this);

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
        playerName.OnValueChanged += OnPlayerNameChanged;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        rb = GetComponent<Rigidbody2D>();

        statController = GetComponent<StatController>();

        if (statController != null)
            statController.SetHeroData(heroData);

        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            throwAction = playerInput.actions.FindAction(
                "Player/Throw",
                false
            );
            forceAction = playerInput.actions.FindAction(
                "Player/Force",
                false
            );
        }

        if (rb == null)
        {
            Debug.LogError(
                $"PlayerNetwork on {gameObject.name} has no Rigidbody2D!"
            );
        }
    }

    private void OnEnable()
    {
        if (throwAction != null)
        {
            if (!throwAction.enabled)
                throwAction.Enable();

            throwAction.performed += OnThrowPerformed;
        }

        if (forceAction != null && !forceAction.enabled)
            forceAction.Enable();
    }

    private void OnDisable()
    {
        if (throwAction != null)
            throwAction.performed -= OnThrowPerformed;
    }

    private void FixedUpdate()
    {
        if (!IsServer)
            return;

        if (heroData == null || rb == null)
            return;

        float baseSpeed = statController != null
            ? statController.Get(StatType.MoveSpeed)
            : heroData.stats.moveSpeed;

        float speed = baseSpeed * speedMultiplier;

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

                DisableSpawnProtection();

                Health health = GetComponent<Health>();

                if (health != null)
                {
                    health.ResetRegenDelay();
                    health.DisableSpawnProtection();
                }

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

    private string GetLocalNickname()
    {
        string defaultNickname = "Player " + (OwnerClientId + 1);

        if (NetworkSessionManager.Instance == null)
            return defaultNickname;

        var session =
            NetworkSessionManager.Instance.CurrentSession;

        if (session == null || session.CurrentPlayer == null)
            return defaultNickname;

        if (session.CurrentPlayer.Properties.TryGetValue(
            "nickname",
            out var nicknameProperty))
        {
            if (!string.IsNullOrWhiteSpace(nicknameProperty.Value))
                return nicknameProperty.Value;
        }

        return defaultNickname;
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

            LocalPlayer = this;
            LocalPlayerSpawned?.Invoke(this);

            string nickname = GetLocalNickname();

            Debug.Log("My nickname: " + nickname);

            SetPlayerNameServerRpc(nickname);

            StartCoroutine(SetupHUD());
            StartCoroutine(SetupCamera());
        }

        if (IsServer)
        {
            lastPosition = rb.position;
        }

        Team team = GetComponent<Team>();
        HeroSorting sorting = GetComponentInChildren<HeroSorting>();

        if (team != null &&
            sorting != null &&
            team.TeamId.Value != 0)
        {
            sorting.SetTeamColor(team.TeamColor);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (LocalPlayer == this)
        {
            LocalPlayer = null;
        }

        GrassInteractionManager.Instance?.UnregisterPlayer(body);

        base.OnNetworkDespawn();
    }

    public event System.Action VolumePressed;

    public void OnVolume(InputValue value)
    {
        Debug.Log("VOLUME INPUT RECEIVED: " + value.isPressed);

        if (!IsOwner || !IsSpawned)
            return;

        if (!value.isPressed)
            return;

        VolumePressed?.Invoke();
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

        CinemachineImpulseSource impulseSource =
            GetComponentInChildren<CinemachineImpulseSource>();

        PlayerCamera.Instance.SetImpulseSource(impulseSource);
        PlayerCamera.Instance.FollowPlayer(transform);

        Debug.Log(
            $"[{OwnerClientId}] Camera attached to player " +
            $"NetworkObjectId={NetworkObjectId}"
        );
    }

    [ClientRpc]
    private void EnterBuildingClientRpc(
        Vector3 targetPosition,
        int soundIndex)
    {
        if (!IsOwner)
            return;

        StartCoroutine(
            BuildingTransition(
                targetPosition,
                soundIndex
            )
        );
    }

    [ServerRpc]
    private void TeleportToBuildingServerRpc(Vector3 targetPosition)
    {
        Teleport(targetPosition);

        BuildingTeleportedClientRpc(targetPosition);
    }

    private IEnumerator TeleportCameraAfterPhysics(
        Vector3 targetPosition)
    {
        yield return new WaitForFixedUpdate();
        yield return null;

        PlayerCamera.Instance.TeleportToPosition(targetPosition);

        yield return ScreenTransition.Instance.FadeOut();
    }

    [ClientRpc]
    private void BuildingTeleportedClientRpc(Vector3 targetPosition)
    {
        if (!IsOwner)
            return;

        StartCoroutine(
            TeleportCameraAfterPhysics(targetPosition)
        );
    }

    public void EnterBuilding(
        Vector3 targetPosition,
        int soundIndex)
    {
        if (!IsServer)
            return;

        EnterBuildingClientRpc(
            targetPosition,
            soundIndex
        );
    }

    private IEnumerator BuildingTransition(
        Vector3 targetPosition,
        int soundIndex)
    {
        yield return ScreenTransition.Instance.FadeIn();

        AudioManager.Instance.PlayBuildingTransition(
            soundIndex
        );

        TeleportToBuildingServerRpc(targetPosition);
    }

    public void Teleport(Vector3 position)
    {
        if (!IsServer)
            return;

        rb.linearVelocity = Vector2.zero;

        rb.position = position;
        transform.position = position;
    }
}
