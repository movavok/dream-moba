using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject), typeof(Rigidbody2D), typeof(Health))]
public sealed class BoarAI : NetworkBehaviour
{
    public enum State : byte { Wander, Alert, Windup, Charge, Stunned, Retreat, Flee, Dead }

    [Header("Movement")]
    [SerializeField, Min(0f)] private float wanderSpeed = 1.7f;
    [SerializeField, Min(0f)] private float alertSpeed = 3f;
    [SerializeField, Min(0f)] private float fleeSpeed = 3f;
    [SerializeField, Min(0f)] private float wanderPauseMin = 1f;
    [SerializeField, Min(0f)] private float wanderPauseMax = 3f;
    [SerializeField, Min(0.5f)] private float chaseDistance = 6f;
    [SerializeField, Min(0.1f)] private float fleeDuration = 2f;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float detectionRadius = 3.5f;
    [SerializeField, Min(0.05f)] private float detectionInterval = 0.25f;
    [SerializeField, Min(0f)] private float windupDuration = 0.5f;
    [SerializeField, Min(0.1f)] private float chargeSpeed = 7f;
    [SerializeField, Min(0.1f)] private float chargeDistance = 7.5f;
    [SerializeField, Min(1)] private int chargeDamage = 35;
    [SerializeField, Min(0f)] private float knockbackDistance = 2f;
    [SerializeField, Min(0f)] private float stunDuration = 1.5f;
    [SerializeField, Min(0f)] private float retreatMinDistance = 3f;
    [SerializeField, Min(0f)] private float retreatMaxDistance = 5f;
    [SerializeField, Min(0f)] private float aggroCooldown = 8f;
    [SerializeField, Range(0.01f, 1f)] private float fleeThreshold = 0.25f;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private LayerMask chargeLayers;

    [Header("Health and rewards")]
    [SerializeField, Min(0f)] private float regenerationDelay = 6f;
    [SerializeField, Min(0f)] private float regenerationRate = 5f;
    [SerializeField, Min(0)] private int killGoldReward = 10;
    [SerializeField, Min(0)] private int rawMeatDropCount = 1;
    [SerializeField] private WorldItem rawMeatPrefab;

    [Header("Visuals (Animator state names are optional)")]
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private SpriteRenderer shadow;
    [SerializeField] private SpriteRenderer behindOutline;
    [SerializeField] private SpriteMask hitMask;
    [SerializeField] private HealthBar healthBar;
    [SerializeField, Min(0.1f)] private float dissolveDuration = 0.65f;
    [SerializeField, Min(0f)] private float dissolveDistance = 1f;
    [SerializeField] private Animator animator;
    [SerializeField] private string idleAnimation = "Idle";
    [SerializeField] private string walkAnimation = "Walk";
    [SerializeField] private string windupAnimation = "Windup";
    [SerializeField] private string chargeAnimation = "Charge";
    [SerializeField] private string stunnedAnimation = "Stunned";
    [SerializeField] private string blinkAnimation = "Blink";
    [SerializeField, Min(0f)] private float firstBlinkDelayMin = 0.25f;
    [SerializeField, Min(0f)] private float firstBlinkDelayMax = 0.55f;
    [SerializeField, Min(0.1f)] private float blinkPauseMin = 4f;
    [SerializeField, Min(0.1f)] private float blinkPauseMax = 9f;
    [SerializeField] private ParticleSystem dust;

    private readonly NetworkVariable<State> networkState = new(State.Wander);
    private readonly NetworkVariable<bool> moving = new(false);
    private readonly NetworkVariable<bool> facingLeft = new(false);
    private readonly NetworkVariable<bool> behindBuilding = new(false);
    private readonly Collider2D[] targetHits = new Collider2D[20];
    private readonly RaycastHit2D[] chargeHits = new RaycastHit2D[20];
    private BoarForest forest;
    private Rigidbody2D body;
    private Health health;
    private HeroSorting sorting;
    private Collider2D bodyCollider;
    private Collider2D[] colliders;
    private Health target;
    private Vector2 threatPosition;
    private Vector2 destination;
    private Vector2 chargeDirection;
    private float chargeRemaining;
    private float stateUntil;
    private float pauseUntil;
    private float cooldownUntil;
    private float nextDetection;
    private float nextRoute;
    private float detourUntil;
    private float nextRegenerationTime;
    private float stuckTime;
    private Vector2 previousPosition;
    private ulong lethalAttacker = ulong.MaxValue;
    private bool dying;
    private string playingAnimation;
    private float blinkEndTime;
    private float nextBlinkTime;
    private float blinkLength = 0.3f;
    private Sprite lastVisualSprite;
    private bool dissolving;

    public BoarForest Forest => forest;
    public bool IsLiving => !dying && health != null && health.IsAlive();
    public State CurrentState => networkState.Value;

    public void SetForest(BoarForest value) { forest = value; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        sorting = GetComponent<HeroSorting>();
        bodyCollider = GetComponent<Collider2D>();
        colliders = GetComponentsInChildren<Collider2D>();
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (healthBar == null) healthBar = GetComponentInChildren<HealthBar>(true);
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == "once-blink") blinkLength = Mathf.Max(0.1f, clip.length);
        }
    }

    public override void OnNetworkSpawn()
    {
        networkState.OnValueChanged += OnStateChanged;
        moving.OnValueChanged += OnMovingChanged;
        facingLeft.OnValueChanged += OnFacingChanged;
        behindBuilding.OnValueChanged += OnBehindChanged;
        OnFacingChanged(false, facingLeft.Value);
        OnBehindChanged(false, behindBuilding.Value);
        OnStateChanged(State.Dead, networkState.Value);
        if (!IsServer)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = false;
            return;
        }
        if (forest == null || bodyCollider == null)
            Debug.LogError("[BoarAI] Spawn using BoarSpawner with a forest and body collider.", this);
        health.Damaged += OnDamaged;
        health.Died += OnDied;
        previousPosition = body.position;
        ResetRegeneration();
        pauseUntil = Time.time + Random.Range(wanderPauseMin, wanderPauseMax);
    }

    public override void OnNetworkDespawn()
    {
        networkState.OnValueChanged -= OnStateChanged;
        moving.OnValueChanged -= OnMovingChanged;
        facingLeft.OnValueChanged -= OnFacingChanged;
        behindBuilding.OnValueChanged -= OnBehindChanged;
        if (IsServer)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
    }

    private void OnStateChanged(State oldState, State newState)
    {
        if (newState == State.Dead)
        {
            BeginDissolve();
            return;
        }
        ApplyAnimation();
        if (dust != null)
        {
            if (newState == State.Charge) dust.Play();
            else dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void OnMovingChanged(bool previous, bool current) { ApplyAnimation(); }

    public void SetBehind(bool behind)
    {
        if (IsServer) behindBuilding.Value = behind;
    }

    private void OnBehindChanged(bool previous, bool current)
    {
        if (sorting != null) sorting.SetBehind(current);
    }

    private void OnFacingChanged(bool previous, bool current)
    {
        if (visual != null) visual.flipX = current;
        if (behindOutline != null) behindOutline.flipX = current;
        if (hitMask != null)
        {
            Vector3 scale = hitMask.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (current ? -1f : 1f);
            hitMask.transform.localScale = scale;
        }
    }

    private void LateUpdate()
    {
        if (visual == null || visual.sprite == lastVisualSprite) return;
        lastVisualSprite = visual.sprite;
        if (behindOutline != null) behindOutline.sprite = lastVisualSprite;
        if (hitMask != null) hitMask.sprite = lastVisualSprite;
    }

    private void SetFacing(float horizontal)
    {
        if (Mathf.Abs(horizontal) > 0.05f)
            facingLeft.Value = horizontal < 0f;
    }

    private bool CanBlink => networkState.Value == State.Wander && !moving.Value &&
        (health == null || !health.IsStunned);

    private void PlayAnimation(string state)
    {
        if (animator == null || animator.runtimeAnimatorController == null ||
            string.IsNullOrEmpty(state) || playingAnimation == state) return;
        playingAnimation = state;
        animator.Play(state, 0, 0f);
    }

    private void ApplyAnimation()
    {
        string state = networkState.Value switch
        {
            State.Wander => moving.Value ? walkAnimation : idleAnimation,
            State.Alert or State.Retreat or State.Flee => walkAnimation,
            State.Windup => windupAnimation,
            State.Charge => chargeAnimation,
            State.Stunned => stunnedAnimation,
            _ => null
        };
        if (!CanBlink || state != idleAnimation) blinkEndTime = 0f;
        if (state == idleAnimation && playingAnimation != idleAnimation && blinkEndTime == 0f)
            nextBlinkTime = Time.time + Random.Range(firstBlinkDelayMin,
                Mathf.Max(firstBlinkDelayMin, firstBlinkDelayMax));
        PlayAnimation(state);
    }

    private void UpdateVisual()
    {
        if (!CanBlink || animator == null || animator.runtimeAnimatorController == null) return;
        if (blinkEndTime > 0f)
        {
            if (Time.time < blinkEndTime) return;
            blinkEndTime = 0f;
            PlayAnimation(idleAnimation);
            nextBlinkTime = Time.time + Random.Range(blinkPauseMin, Mathf.Max(blinkPauseMin, blinkPauseMax));
        }
        else if (Time.time >= nextBlinkTime && playingAnimation == idleAnimation)
        {
            PlayAnimation(blinkAnimation);
            blinkEndTime = Time.time + blinkLength;
        }
    }

    private void SetState(State state)
    {
        if (networkState.Value == state) return;
        networkState.Value = state;
    }

    private void Update()
    {
        if (IsSpawned && IsClient) UpdateVisual();
        if (!IsServer || !IsSpawned || forest == null || dying || !health.IsAlive()) return;
        Regenerate();
        if (health.IsStunned)
        {
            if (networkState.Value == State.Charge) StopCharge();
            if (networkState.Value == State.Charge || networkState.Value == State.Windup ||
                networkState.Value == State.Alert)
                EnterWander();
            return;
        }
        if (health.HasSightBlockingStatus)
        {
            if (networkState.Value == State.Charge) StopCharge();
            if (target != null || networkState.Value == State.Alert ||
                networkState.Value == State.Windup || networkState.Value == State.Charge)
                EnterWander();
        }
        if (health.CurrentHealth < health.MaxHealth * fleeThreshold &&
            networkState.Value != State.Flee && networkState.Value != State.Dead &&
            networkState.Value != State.Stunned)
        {
            EnterFlee();
        }

        if (!health.HasSightBlockingStatus && networkState.Value == State.Wander && Time.time >= nextDetection &&
            Time.time >= cooldownUntil && health.CurrentHealth >= health.MaxHealth * fleeThreshold)
        {
            nextDetection = Time.time + detectionInterval;
            target = FindTarget();
            if (target != null) SetState(State.Alert);
        }

        if (networkState.Value == State.Windup && Time.time >= stateUntil)
        {
            if (!ValidTarget(target) || !CanSee(target.transform.position)) { EnterWander(); return; }
            chargeDirection = ((Vector2)target.transform.position - body.position).normalized;
            if (chargeDirection.sqrMagnitude < 0.01f) { EnterWander(); return; }
            SetFacing(chargeDirection.x);
            chargeRemaining = chargeDistance;
            ResetRegeneration();
            SetState(State.Charge);
        }
        else if (networkState.Value == State.Stunned && Time.time >= stateUntil) EnterRetreat();
        else if (networkState.Value == State.Flee && Time.time >= stateUntil &&
            health.CurrentHealth >= health.MaxHealth * fleeThreshold) EnterWander();
    }

    private void FixedUpdate()
    {
        if (!IsServer || !IsSpawned || forest == null || dying || !health.IsAlive()) return;
        if (health.IsStunned)
        {
            Stop();
            if (moving.Value) moving.Value = false;
            return;
        }
        if (health.HasSightBlockingStatus &&
            (networkState.Value == State.Alert || networkState.Value == State.Windup ||
             networkState.Value == State.Charge))
        {
            if (networkState.Value == State.Charge) StopCharge();
            EnterWander();
        }
        switch (networkState.Value)
        {
            case State.Wander:
                if (Time.time < pauseUntil) { Stop(); break; }
                if (destination == Vector2.zero || Vector2.Distance(body.position, destination) < 0.22f)
                {
                    if (!forest.TryRandomPoint(out destination, body.position, 1.2f))
                        pauseUntil = Time.time + 1f;
                }
                MoveTo(destination, wanderSpeed);
                if (Vector2.Distance(body.position, destination) < 0.22f)
                {
                    destination = Vector2.zero;
                    pauseUntil = Time.time + Random.Range(wanderPauseMin, wanderPauseMax);
                }
                break;
            case State.Alert:
                if (Time.time >= cooldownUntil && ValidTarget(target) &&
                    Vector2.Distance(body.position, target.transform.position) <= chaseDistance &&
                    forest.Contains(target.transform.position) && CanSee(target.transform.position))
                {
                    float distance = Vector2.Distance(body.position, target.transform.position);
                    if (distance <= chargeDistance + 0.4f)
                    {
                        Stop();
                        SetFacing(target.transform.position.x - body.position.x);
                        SetState(State.Windup);
                        stateUntil = Time.time + windupDuration;
                    }
                    else MoveTo(Time.time < detourUntil ? destination :
                        (Vector2)target.transform.position, alertSpeed);
                }
                else EnterWander();
                break;
            case State.Charge: ChargeStep(); break;
            case State.Retreat:
                if (Time.time >= stateUntil || Vector2.Distance(body.position, destination) < 0.2f)
                    EnterWander();
                else MoveTo(destination, alertSpeed);
                break;
            case State.Flee:
                if (Time.time >= nextRoute || Vector2.Distance(body.position, destination) < 0.2f)
                {
                    nextRoute = Time.time + 0.7f;
                    PickAwayPoint();
                }
                MoveTo(destination, fleeSpeed);
                break;
            default: Stop(); break;
        }
        if (networkState.Value == State.Windup && target != null)
            SetFacing(target.transform.position.x - body.position.x);
        else if (networkState.Value == State.Charge)
            SetFacing(chargeDirection.x);
        else if (body.linearVelocity.sqrMagnitude > 0.02f)
            SetFacing(body.linearVelocity.x);
        bool activeMovement = networkState.Value == State.Charge ||
            body.linearVelocity.sqrMagnitude > 0.02f;
        if (moving.Value != activeMovement) moving.Value = activeMovement;
    }

    private void Stop() { body.linearVelocity = Vector2.zero; }

    private void MoveTo(Vector2 point, float speed)
    {
        Vector2 delta = point - body.position;
        if (delta.sqrMagnitude < 0.02f) { Stop(); return; }
        Vector2 direction = delta.normalized;
        Vector2 step = body.position + direction * (speed * Time.fixedDeltaTime + 0.02f);
        if (!forest.ClearPath(body.position, step))
        {
            Stop();
            if (Time.time >= nextRoute)
            {
                nextRoute = Time.time + 0.4f;
                // Bounded local detour; abandon unreachable routes instead of pushing into a wall.
                Vector2 side = new Vector2(-direction.y, direction.x);
                Vector2 a = body.position + (direction + side).normalized * 1.2f;
                Vector2 b = body.position + (direction - side).normalized * 1.2f;
                if (forest.ClearPath(body.position, a)) { destination = a; detourUntil = Time.time + 0.8f; }
                else if (forest.ClearPath(body.position, b)) { destination = b; detourUntil = Time.time + 0.8f; }
                else if (networkState.Value == State.Alert) EnterWander();
                else if (networkState.Value == State.Wander) { destination = Vector2.zero; pauseUntil = Time.time + 1f; }
                else PickAwayPoint();
            }
            return;
        }
        body.linearVelocity = direction * speed;
        if (Vector2.Distance(previousPosition, body.position) < 0.003f) stuckTime += Time.fixedDeltaTime;
        else stuckTime = 0f;
        previousPosition = body.position;
        if (stuckTime > 0.8f)
        {
            stuckTime = 0f;
            if (networkState.Value == State.Alert) EnterWander();
            else if (networkState.Value == State.Wander) destination = Vector2.zero;
            else PickAwayPoint();
        }
    }

    private void ChargeStep()
    {
        float step = Mathf.Min(chargeRemaining, chargeSpeed * Time.fixedDeltaTime);
        // Sweep against walls AND target bodies/hitboxes; never rely on tunneling-prone trigger events.
        ContactFilter2D chargeFilter = new ContactFilter2D();
        chargeFilter.SetLayerMask(chargeLayers);
        chargeFilter.useTriggers = true;
        int hits = Physics2D.CircleCast(body.position, forest.BodyRadius,
            chargeDirection, chargeFilter, chargeHits, step + 0.02f);
        RaycastHit2D nearest = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits; i++)
        {
            Collider2D collider = chargeHits[i].collider;
            if (collider == null || collider.transform.IsChildOf(transform) ||
                (collider.isTrigger && collider.GetComponentInParent<Health>() == null) ||
                collider.GetComponentInParent<BoarAI>() != null) continue;
            if (chargeHits[i].distance < nearestDistance)
            {
                nearest = chargeHits[i];
                nearestDistance = nearest.distance;
            }
        }

        float allowed = nearest.collider != null ? Mathf.Max(0f, nearestDistance - 0.02f) : step;
        Vector2 end = body.position + chargeDirection * allowed;
        if (!forest.ContainsSegment(body.position, end))
        {
            StopCharge();
            EnterRetreat();
            return;
        }
        body.MovePosition(end);
        chargeRemaining -= allowed;
        if (nearest.collider != null && nearestDistance <= step + 0.02f)
        {
            Health victim = nearest.collider.GetComponentInParent<Health>();
            PlayerNetwork player = nearest.collider.GetComponentInParent<PlayerNetwork>();
            if (victim != null && victim.IsAlive() && player != null)
            {
                bool protectedNow = victim.IsSpawnProtected;
                victim.ShowDamageHit(nearest.point, chargeDamage);
                victim.TakeDamage(chargeDamage, ulong.MaxValue);
                if (!protectedNow && victim.IsAlive())
                    player.GetComponent<BoarKnockback>()?.Push(chargeDirection, knockbackDistance);
                StopCharge(); EnterRetreat(); return;
            }
            DamageReflector reflector = nearest.collider.GetComponentInParent<DamageReflector>();
            if (victim != null && victim.IsAlive() && reflector != null)
            {
                reflector.SetNextNonPlayerAttacker(health);
                victim.ShowDamageHit(nearest.point, chargeDamage);
                victim.TakeDamage(chargeDamage, ulong.MaxValue);
                if (reflector != null) reflector.ClearNextNonPlayerAttacker(health);
                if (!IsLiving) return;
                StopCharge(); EnterRetreat(); return;
            }
            StopCharge();
            if ((obstacleLayers.value & (1 << nearest.collider.gameObject.layer)) != 0)
            {
                SetState(State.Stunned);
                stateUntil = Time.time + stunDuration;
            }
            else EnterRetreat();
            return;
        }
        if (chargeRemaining <= 0.001f) { StopCharge(); EnterRetreat(); }
    }

    private void StopCharge()
    {
        Stop();
        cooldownUntil = Time.time + aggroCooldown;
        ResetRegeneration();
    }

    private void EnterWander()
    {
        target = null;
        destination = Vector2.zero;
        Stop();
        pauseUntil = Time.time + Random.Range(wanderPauseMin, wanderPauseMax);
        SetState(State.Wander);
    }

    private void EnterRetreat()
    {
        if (target != null) threatPosition = target.transform.position;
        target = null;
        PickAwayPoint();
        stateUntil = Time.time + Mathf.Max(1f, retreatMaxDistance / Mathf.Max(0.1f, alertSpeed));
        SetState(State.Retreat);
    }

    private void EnterFlee()
    {
        if (networkState.Value == State.Charge) StopCharge();
        if (target != null) threatPosition = target.transform.position;
        target = null;
        Stop();
        stateUntil = Time.time + fleeDuration;
        nextRoute = 0f;
        PickAwayPoint();
        SetState(State.Flee);
    }

    private void PickAwayPoint()
    {
        float best = float.NegativeInfinity;
        Vector2 bestPoint = body.position;
        Vector2 away = body.position - threatPosition;
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle.normalized;
        float length = Random.Range(retreatMinDistance, Mathf.Max(retreatMinDistance, retreatMaxDistance));
        for (int range = 0; range < 4; range++)
        {
            for (int i = 0; i < 16; i++)
            {
                Vector2 dir = Quaternion.Euler(0f, 0f, i * 22.5f) * away.normalized;
                Vector2 candidate = body.position + dir * (length / (range + 1));
                if (!forest.ClearPath(body.position, candidate)) continue;
                float score = Vector2.Distance(candidate, threatPosition);
                if (score > best) { best = score; bestPoint = candidate; }
            }
            if (best > float.NegativeInfinity) break;
        }
        destination = bestPoint;
    }

    private bool ValidTarget(Health candidate)
    {
        return candidate != null && candidate.IsSpawned && candidate.IsAlive() &&
            (candidate.GetComponent<PlayerNetwork>() != null || candidate.GetComponent<DamageReflector>() != null);
    }

    private bool CanSee(Vector2 point)
    {
        Vector2 delta = point - body.position;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleLayers);
        filter.useTriggers = false;
        return Physics2D.Raycast(body.position, delta.normalized, filter,
            chargeHits, delta.magnitude) == 0;
    }

    private Health FindTarget()
    {
        ContactFilter2D targetFilter = new ContactFilter2D();
        targetFilter.SetLayerMask(targetLayers);
        targetFilter.useTriggers = true;
        int count = Physics2D.OverlapCircle(body.position, detectionRadius, targetFilter, targetHits);
        Health closest = null;
        float best = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Health candidate = targetHits[i].GetComponentInParent<Health>();
            if (!ValidTarget(candidate) || !forest.Contains(candidate.transform.position)) continue;
            float distance = Vector2.Distance(body.position, candidate.transform.position);
            if (distance < best && CanSee(candidate.transform.position))
            { best = distance; closest = candidate; }
        }
        return closest;
    }

    private void OnDamaged(DamageEvent hit)
    {
        lethalAttacker = hit.Source == DamageSource.Reflect ? ulong.MaxValue : hit.AttackerClientId;
        ResetRegeneration();
        if (!health.IsAlive() || networkState.Value == State.Stunned) return;
        if (health.HasSightBlockingStatus) return;
        if (networkState.Value == State.Charge &&
            health.CurrentHealth >= health.MaxHealth * fleeThreshold) return;
        if (hit.AttackerClientId != ulong.MaxValue && NetworkManager.ConnectedClients.TryGetValue(hit.AttackerClientId, out NetworkClient client) && client.PlayerObject != null)
        {
            threatPosition = client.PlayerObject.transform.position;
            Health attacker = client.PlayerObject.GetComponent<Health>();
            if (Time.time >= cooldownUntil && health.CurrentHealth >= health.MaxHealth * fleeThreshold &&
                CanSee(threatPosition) && forest.Contains(threatPosition) && ValidTarget(attacker))
            { target = attacker; SetState(State.Alert); return; }
        }
        EnterFlee();
    }

    private void ResetRegeneration()
    {
        nextRegenerationTime = Time.time + regenerationDelay;
    }

    private void Regenerate()
    {
        if (Time.time < nextRegenerationTime || health.CurrentHealth >= health.MaxHealth) return;
        health.Heal(Mathf.RoundToInt(regenerationRate), true);
        nextRegenerationTime = Time.time + 1f;
    }

    private void OnDied()
    {
        if (!IsServer || dying) return;
        dying = true;
        Stop();
        SetState(State.Dead);
        if (rawMeatPrefab != null && rawMeatPrefab.Definition != null)
            WorldItem.DropAndThrow(rawMeatPrefab.Definition, rawMeatDropCount, transform);
        if (lethalAttacker != ulong.MaxValue && killGoldReward > 0 &&
            NetworkManager.ConnectedClients.ContainsKey(lethalAttacker))
            EconomyManager.Instance?.AddGold(lethalAttacker, killGoldReward, "Кабан");
        StartCoroutine(DespawnAfterDissolve());
    }

    private void BeginDissolve()
    {
        if (dissolving) return;
        dissolving = true;
        if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (healthBar != null) healthBar.gameObject.SetActive(false);
        foreach (Collider2D collider in colliders) collider.enabled = false;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }
        PlayAnimation(walkAnimation);
        StartCoroutine(DissolveVisuals());
    }

    private IEnumerator DissolveVisuals()
    {
        Vector3 start = visual != null ? visual.transform.localPosition : Vector3.zero;
        float direction = facingLeft.Value ? -1f : 1f;
        Color spriteColor = visual != null ? visual.color : Color.white;
        Color shadowColor = shadow != null ? shadow.color : Color.white;
        Color outlineColor = behindOutline != null ? behindOutline.color : Color.white;
        float elapsed = 0f;
        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / dissolveDuration);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);
            if (visual != null)
            {
                visual.transform.localPosition = start + Vector3.right * (direction * dissolveDistance * progress);
                spriteColor.a = alpha;
                visual.color = spriteColor;
            }
            if (shadow != null) { shadowColor.a = alpha; shadow.color = shadowColor; }
            if (behindOutline != null) { outlineColor.a = alpha; behindOutline.color = outlineColor; }
            yield return null;
        }
    }

    private IEnumerator DespawnAfterDissolve()
    {
        yield return new WaitForSeconds(dissolveDuration);
        if (IsSpawned) NetworkObject.Despawn(true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.cyan;
        if (destination != Vector2.zero) Gizmos.DrawLine(transform.position, destination);
        Gizmos.color = Color.red;
        if (target != null) Gizmos.DrawLine(transform.position, target.transform.position);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)chargeDirection * chargeDistance);
    }
}
