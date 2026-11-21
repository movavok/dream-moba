using UnityEngine;

[RequireComponent(typeof(PiggyBank))]
public class PiggyBankVisual : MonoBehaviour
{
    [Header("Indicators")]
    [SerializeField] private SpriteRenderer lowerIndicator;
    [SerializeField] private SpriteRenderer upperIndicator;

    [Header("Animation")]
    [SerializeField] private float fillSpeed = 2f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem activeParticles;

    private PiggyBank piggyBank;
    private TeamManager teamManager;
    private MaterialPropertyBlock lowerProps;
    private MaterialPropertyBlock upperProps;

    private float lowerFill;
    private float upperFill;
    private PiggyBank.CaptureState lastState;

    private void Awake()
    {
        piggyBank = GetComponent<PiggyBank>();
        teamManager = FindAnyObjectByType<TeamManager>();

        lowerProps = new MaterialPropertyBlock();
        upperProps = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (piggyBank == null)
            return;

        if (piggyBank.State != lastState)
        {
            lastState = piggyBank.State;

            if (lastState == PiggyBank.CaptureState.PendingCapture)
                upperFill = 0f;
            else if (lastState == PiggyBank.CaptureState.Starting)
                lowerFill = piggyBank.Progress;
        }

        Color ownerColor = GetTeamColor(piggyBank.OwnerTeamId);
        Color pendingColor = GetTeamColor(piggyBank.PendingTeamId);
        float step = Time.deltaTime * fillSpeed;

        switch (piggyBank.State)
        {
            case PiggyBank.CaptureState.Starting:
                lowerFill = Mathf.MoveTowards(lowerFill, piggyBank.Progress, step);
                upperFill = 0f;
                ApplyIndicator(lowerIndicator, lowerProps, ownerColor, lowerFill);
                ApplyIndicator(upperIndicator, upperProps, ownerColor, upperFill);
                break;

            case PiggyBank.CaptureState.Active:
                lowerFill = 1f;
                upperFill = 0f;
                ApplyIndicator(lowerIndicator, lowerProps, ownerColor, lowerFill);
                ApplyIndicator(upperIndicator, upperProps, ownerColor, upperFill);
                break;

            case PiggyBank.CaptureState.PendingCapture:
                lowerFill = 1f;
                upperFill = Mathf.MoveTowards(upperFill, piggyBank.Progress, step);
                ApplyIndicator(lowerIndicator, lowerProps, ownerColor, lowerFill);
                ApplyIndicator(upperIndicator, upperProps, pendingColor, upperFill);
                break;

            default:
                lowerFill = 0f;
                upperFill = 0f;
                ApplyIndicator(lowerIndicator, lowerProps, ownerColor, lowerFill);
                ApplyIndicator(upperIndicator, upperProps, ownerColor, upperFill);
                break;
        }

        UpdateParticles();
    }

    private void UpdateParticles()
    {
        if (activeParticles == null)
            activeParticles = GetComponentInChildren<ParticleSystem>(true);

        if (activeParticles == null)
            return;

        if (piggyBank.IsEarning)
        {
            if (!activeParticles.isPlaying)
                activeParticles.Play();
        }
        else if (activeParticles.isPlaying)
        {
            activeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void OnDisable()
    {
        if (activeParticles != null)
            activeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void ApplyIndicator(
        SpriteRenderer renderer,
        MaterialPropertyBlock props,
        Color color,
        float fill)
    {
        if (renderer == null)
            return;

        props.SetColor("_Color", color);
        props.SetFloat("_FillAmount", Mathf.Clamp01(fill));
        props.SetFloat("_Clockwise", 1f);
        props.SetFloat("_StartAngle", 0f);
        renderer.SetPropertyBlock(props);
        renderer.enabled = fill > 0.01f;
    }

    private Color GetTeamColor(short teamId)
    {
        if (teamManager == null)
            teamManager = FindAnyObjectByType<TeamManager>();

        if (teamManager == null)
            return Color.white;

        TeamDefinition.TeamData data = teamManager.GetTeamData(teamId);
        return data != null ? data.color : Color.white;
    }
}
