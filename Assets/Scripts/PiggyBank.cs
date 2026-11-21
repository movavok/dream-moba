using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject), typeof(CircleCollider2D))]
public class PiggyBank : NetworkBehaviour, IInteractable
{
    public enum CaptureState
    {
        Inactive,
        Starting,
        Active,
        PendingCapture
    }

    [Header("Settings")]
    [SerializeField] private short initialTeamId = 1;
    [SerializeField] private float incomePerSecond = 3f;
    [SerializeField] private float startDelay = 10f;
    [SerializeField] private float cooldownDuration = 60f;

    [Header("Interaction")]
    [SerializeField] private InteractionOutline outline;
    [SerializeField] private SpriteRenderer visual;

    private readonly NetworkVariable<short> ownerTeamId = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<short> pendingTeamId = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<CaptureState> state = new(
        CaptureState.Inactive,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<float> progress = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<float> stateTimer = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<float> cooldownRemaining = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private EconomyManager economyManager;
    private MatchStatsManager matchStatsManager;
    private float lastOwnerChangeTime;
    private float pendingWaitDuration;

    public float IncomePerSecond => incomePerSecond;
    public short OwnerTeamId => ownerTeamId.Value;
    public short PendingTeamId => pendingTeamId.Value;
    public CaptureState State => state.Value;
    public float Progress => progress.Value;

    public bool IsEarning =>
        state.Value == CaptureState.Active ||
        state.Value == CaptureState.PendingCapture;

    public string InteractionVerb => "Capture";
    public string InteractionName => "piggy bank";
    public Transform InteractionPromptTarget => transform;

    private void Awake()
    {
        if (outline != null && visual != null)
            outline.Configure(visual);
    }

    private void OnDisable()
    {
        outline?.SetHighlighted(false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        economyManager = EconomyManager.Instance;
        matchStatsManager = MatchStatsManager.Instance;

        if (!IsServer)
            return;

        economyManager?.RegisterPiggyBank(this);

        if (initialTeamId > 0)
            ownerTeamId.Value = initialTeamId;

        lastOwnerChangeTime = Time.time - cooldownDuration;
        stateTimer.Value = startDelay;
        state.Value = CaptureState.Starting;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
            economyManager?.UnregisterPiggyBank(this);

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (matchStatsManager != null && matchStatsManager.HasMatchEnded)
            return;

        float dt = Time.deltaTime;

        switch (state.Value)
        {
            case CaptureState.Starting:
                stateTimer.Value -= dt;
                progress.Value = 1f - (stateTimer.Value / startDelay);

                if (stateTimer.Value <= 0f)
                {
                    stateTimer.Value = 0f;
                    progress.Value = 1f;
                    state.Value = CaptureState.Active;
                }
                break;

            case CaptureState.PendingCapture:
                float remaining = cooldownDuration - (Time.time - lastOwnerChangeTime);
                cooldownRemaining.Value = Mathf.Max(0f, remaining);
                progress.Value = pendingWaitDuration > 0f
                    ? Mathf.Clamp01(1f - (cooldownRemaining.Value / pendingWaitDuration))
                    : 1f;

                if (remaining <= 0f && pendingTeamId.Value > 0)
                    ApplyOwnerChange(pendingTeamId.Value);
                break;
        }
    }

    public bool CanInteract(PlayerNetwork player)
    {
        if (!IsSpawned || player == null || !player.IsSpawned)
            return false;

        if (state.Value != CaptureState.Active)
            return false;

        Team team = player.GetComponent<Team>();
        return team != null && team.TeamId.Value != ownerTeamId.Value;
    }

    public void SetHighlighted(bool highlighted)
    {
        outline?.SetHighlighted(highlighted);
    }

    public void Interact(PlayerNetwork player)
    {
        if (!IsServer || !CanInteract(player))
            return;

        Team team = player.GetComponent<Team>();
        if (team == null)
            return;

        short capturerTeamId = team.TeamId.Value;
        float remaining = cooldownDuration - (Time.time - lastOwnerChangeTime);

        if (remaining <= 0f)
        {
            ApplyOwnerChange(capturerTeamId);
            return;
        }

        pendingTeamId.Value = capturerTeamId;
        pendingWaitDuration = remaining;
        cooldownRemaining.Value = remaining;
        progress.Value = 0f;
        state.Value = CaptureState.PendingCapture;
    }

    private void ApplyOwnerChange(short newOwnerTeamId)
    {
        ownerTeamId.Value = newOwnerTeamId;
        pendingTeamId.Value = -1;
        lastOwnerChangeTime = Time.time;
        cooldownRemaining.Value = 0f;
        progress.Value = 1f;
        state.Value = CaptureState.Active;
    }
}
