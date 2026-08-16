using UnityEngine;

public class HeroHUD : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] private HUDDefinition hudDefinition;

    [Header("Slots")]
    [SerializeField] private AbilitySlot attackSlot;
    [SerializeField] private AbilitySlot passiveSlot;
    [SerializeField] private AbilitySlot ability1Slot;
    [SerializeField] private AbilitySlot ability2Slot;

    [Header("Tooltip")]
    [SerializeField] private AbilityTooltip abilityTooltip;

    private const string AttackColor = "#FFB86B";
    private const string PassiveColor = "#a2ebe5";
    private const string AbilityColor = "#C8A2FF";

    private PlayerAbilities playerAbilities;
    private HeroDefinition heroData;
    private PlayerNetwork playerNetwork;

    public static HeroHUD Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnAttackUsed()
    {
        attackSlot.PlayPress();
    }

    private void OnAbility1Used()
    {
        ability1Slot.PlayPress();
    }

    private void OnAbility2Used()
    {
        ability2Slot.PlayPress();
    }

    public void ToggleQuickCast(AbilitySlot slot)
    {
        if (playerAbilities == null)
            return;

        if (!slot.QuickCastAvailable)
            return;

        if (slot == ability1Slot)
        {
            playerAbilities.ToggleAbility1QuickCast();
            ability1Slot.PlayPress();
        }
        else if (slot == ability2Slot)
        {
            playerAbilities.ToggleAbility2QuickCast();
            ability2Slot.PlayPress();
        }
    }

    public void SetAbilities(PlayerAbilities abilities)
    {
        if (playerAbilities != null)
        {
            playerAbilities.Ability1Used -= OnAbility1Used;
            playerAbilities.Ability2Used -= OnAbility2Used;
        }

        playerAbilities = abilities;

        if (playerAbilities != null)
        {
            playerAbilities.Ability1Used += OnAbility1Used;
            playerAbilities.Ability2Used += OnAbility2Used;
        }
    }

    public void SetPlayerNetwork(PlayerNetwork player)
    {
        if (playerNetwork != null)
        {
            playerNetwork.AttackUsed -= OnAttackUsed;
        }

        playerNetwork = player;

        if (playerNetwork != null)
        {
            playerNetwork.AttackUsed += OnAttackUsed;
        }
    }

    public void Initialize(HeroDefinition heroData)
    {
        Debug.Log(
            "HeroHUD Initialize: " +
            (heroData != null ? heroData.heroName : "NULL")
            );

        if (heroData == null)
        {
            Debug.LogError("HeroHUD: HeroData is null!");
            return;
        }

        this.heroData = heroData;

        if (hudDefinition == null)
        {
            Debug.LogError("HeroHUD: HUDDefinition is not assigned!");
            return;
        }

        // Basic attack
        attackSlot.SetIcon(heroData.attack.icon);
        attackSlot.SetFrame(hudDefinition.attackFrame);
        attackSlot.SetKey("LMB");

        attackSlot.SetTooltipData(
            $"<color={AttackColor}>{heroData.attack.name}</color>",
            heroData.attack.description
        );

        // Passive
        passiveSlot.SetIcon(heroData.passive.icon);
        passiveSlot.SetFrame(hudDefinition.passiveFrame);
        passiveSlot.SetKey("");

        passiveSlot.SetTooltipData(
            $"<color={PassiveColor}>{heroData.passive.name}</color>",
            heroData.passive.description
        );

        // Ability 1
        ability1Slot.SetIcon(heroData.ability1.icon);
        ability1Slot.SetFrames(
            hudDefinition.abilityFrame,
            hudDefinition.quickCastFrame
        );
        ability1Slot.SetKey("Q");

        ability1Slot.SetQuickCastAvailable(
            heroData.ability1.quickCastEnabled
        );

        ability1Slot.SetTooltipData(
            $"<color={AbilityColor}>{heroData.ability1.name}</color>",
            heroData.ability1.description
        );

        // Ability 2
        ability2Slot.SetIcon(heroData.ability2.icon);
        ability2Slot.SetFrames(
            hudDefinition.abilityFrame,
            hudDefinition.quickCastFrame
        );
        ability2Slot.SetKey("E");

        ability2Slot.SetQuickCastAvailable(
            heroData.ability2.quickCastEnabled
        );

        ability2Slot.SetTooltipData(
            $"<color={AbilityColor}>{heroData.ability2.name}</color>",
            heroData.ability2.description
        );
    }

    private void Update()
    {
        if (playerNetwork != null)
        {
            // Basic attack
            attackSlot.SetCooldown(
                playerNetwork.AttackCooldownRemaining,
                playerNetwork.AttackCooldownDuration
            );
        }

        if (playerAbilities == null)
            return;

        ability1Slot.SetQuickCast(
            playerAbilities.Ability1QuickCast
        );

        ability2Slot.SetQuickCast(
            playerAbilities.Ability2QuickCast
        );

        // Ability 1
        if (playerAbilities.Ability1ActiveRemaining > 0f)
        {
            ability1Slot.SetActiveTime(
                playerAbilities.Ability1ActiveRemaining,
                heroData.ability1.duration
            );
        }
        else if (playerAbilities.Ability1CooldownRemaining > 0f)
        {
            ability1Slot.SetCooldown(
                playerAbilities.Ability1CooldownRemaining,
                playerAbilities.Ability1CooldownDuration
            );
        }
        else if (!playerAbilities.Ability1IsUsable)
        {
            ability1Slot.SetBlocked();
        }
        else
        {
            ability1Slot.SetReady();
        }

        // Ability 2
        if (playerAbilities.Ability2ActiveRemaining > 0f)
        {
            ability2Slot.SetActiveTime(
                playerAbilities.Ability2ActiveRemaining,
                heroData.ability2.duration
            );
        }
        else if (playerAbilities.Ability2CooldownRemaining > 0f)
        {
            ability2Slot.SetCooldown(
                playerAbilities.Ability2CooldownRemaining,
                playerAbilities.Ability2CooldownDuration
            );
        }
        else if (playerAbilities.Ability2MaxCharge > 0)
        {
            ability2Slot.SetCharge(
                playerAbilities.Ability2CurrentCharge,
                playerAbilities.Ability2MaxCharge
            );
        }
        else
        {
            ability2Slot.SetReady();
        }
    }

    public void ShowTooltip(string title, string description)
    {
        if (abilityTooltip == null)
            return;

        abilityTooltip.Show(title, description);
    }

    public void HideTooltip()
    {
        if (abilityTooltip == null)
            return;

        abilityTooltip.Hide();
    }
}