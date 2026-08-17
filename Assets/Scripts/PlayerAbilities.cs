using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAbilities : NetworkBehaviour
{
    private PlayerNetwork playerNetwork;
    private PlayerStreak playerStreak;
    private HeroDefinition heroData;

    private IAbility ability1;
    private IAbility ability2;

    public IAbility Ability1 => ability1;
    public IAbility Ability2 => ability2;

    public event System.Action Ability1Used;
    public event System.Action Ability2Used;

    private bool ability1WasUsable;
    private bool ability2WasUsable;

    private GameObject ability1Visual;
    private GameObject ability2Visual;

    private NetworkVariable<bool> ability1QuickCast =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    private NetworkVariable<bool> ability2QuickCast =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    public bool Ability1QuickCast => ability1QuickCast.Value;
    public bool Ability2QuickCast => ability2QuickCast.Value;

    public void ToggleAbility1QuickCast()
    {
        if (!IsOwner)
            return;

        if (heroData == null || heroData.ability1 == null)
            return;

        if (!heroData.ability1.quickCastEnabled)
            return;

        ability1QuickCast.Value = !ability1QuickCast.Value;
    }

    public void ToggleAbility2QuickCast()
    {
        if (!IsOwner)
            return;

        if (heroData == null || heroData.ability2 == null)
            return;

        if (!heroData.ability2.quickCastEnabled)
            return;

        ability2QuickCast.Value = !ability2QuickCast.Value;
    }

    private NetworkVariable<float> ability1CooldownTimer =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<float> ability2CooldownTimer =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<bool> ability1OnCooldown =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<bool> ability2OnCooldown =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<float> ability1ActiveTimer =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<float> ability2ActiveTimer =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<int> ability2Charge =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private bool ability1WasActive;
    private bool ability2WasActive;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        playerNetwork = GetComponent<PlayerNetwork>();
        playerStreak = GetComponent<PlayerStreak>();

        heroData = playerNetwork.HeroData;

        if (heroData == null)
        {
            Debug.LogError("PlayerAbilities: HeroData is null!");
            return;
        }

        ability1 = AbilityFactory.Create(
            heroData.ability1.abilityId,
            playerNetwork,
            playerStreak,
            heroData
        );

        ability2 = AbilityFactory.Create(
            heroData.ability2.abilityId,
            playerNetwork,
            playerStreak,
            heroData
        );

        Debug.Log(
            $"Abilities created: " +
            $"{heroData.ability1.abilityId}, " +
            $"{heroData.ability2.abilityId}"
        );
    }

    public void OnAbility1(InputValue value)
    {
        if (!IsOwner)
            return;

        if (!value.isPressed)
            return;

        UseAbility1ServerRpc();
    }

    public void OnAbility2(InputValue value)
    {
        if (!IsOwner)
            return;

        if (!value.isPressed)
            return;

        UseAbility2ServerRpc();
    }

    [ClientRpc]
    private void StartAbilityVisualClientRpc(int abilityIndex, float power)
    {
        AbilityDefinition definition;

        if (abilityIndex == 1)
            definition = heroData.ability1;
        else
            definition = heroData.ability2;

        if (definition == null)
            return;

        if (definition.VisualEffectPrefab == null)
            return;

        GameObject effect = Instantiate(
            definition.VisualEffectPrefab,
            transform.position,
            Quaternion.identity,
            transform
        );

        IAbilityVisual visual =
            effect.GetComponent<IAbilityVisual>();

        if (visual != null)
        {
            visual.Initialize(transform, power);
        }

        if (abilityIndex == 1)
            ability1Visual = effect;
        else
            ability2Visual = effect;
    }

    private void StopAbilityVisual(int abilityIndex)
    {
        if (abilityIndex == 1 && ability1Visual != null)
        {
            Destroy(ability1Visual);
            ability1Visual = null;
        }
        else if (abilityIndex == 2 && ability2Visual != null)
        {
            Destroy(ability2Visual);
            ability2Visual = null;
        }
    }

    [ClientRpc]
    private void StopAbilityVisualClientRpc(int abilityIndex)
    {
        StopAbilityVisual(abilityIndex);
    }

    private void UseAbility1()
    {
        if (!IsServer)
            return;

        if (ability1 == null)
            return;

        if (ability1OnCooldown.Value)
            return;

        bool used = ability1.Use();

        if (used)
        {
            Ability1UsedClientRpc();
            StartAbilityVisualClientRpc(1, ability1.VisualPower);
        }
    }

    private void UseAbility2()
    {
        if (!IsServer)
            return;

        if (ability2 == null)
            return;

        if (ability2OnCooldown.Value)
            return;

        bool used = ability2.Use();

        if (used)
        {
            Ability2UsedClientRpc();
            StartAbilityVisualClientRpc(2, ability2.VisualPower);
        }
    }

    [ServerRpc]
    private void UseAbility1ServerRpc()
    {
        UseAbility1();
    }

    [ServerRpc]
    private void UseAbility2ServerRpc()
    {
        UseAbility2();
    }

    [ClientRpc]
    private void Ability1UsedClientRpc()
    {
        Ability1Used?.Invoke();
    }

    [ClientRpc]
    private void Ability2UsedClientRpc()
    {
        Ability2Used?.Invoke();
    }

    private void UpdateAbilityCooldowns()
    {
        // =========================
        // ABILITY 1
        // =========================

        if (ability1 != null)
        {
            bool isActive = ability1.IsActive();

            // Ability just finished
            if (ability1WasActive && !isActive)
            {
                ability1OnCooldown.Value = true;
                ability1CooldownTimer.Value = heroData.ability1.cooldown;

                StopAbilityVisualClientRpc(1);

                Debug.Log(
                    "Ab1 cooldown started: " +
                    heroData.ability1.cooldown
                );
            }

            ability1WasActive = isActive;

            if (ability1OnCooldown.Value)
            {
                ability1CooldownTimer.Value -= Time.deltaTime;

                if (ability1CooldownTimer.Value <= 0f)
                {
                    ability1CooldownTimer.Value = 0f;
                    ability1OnCooldown.Value = false;

                    Debug.Log("Ab1 cooldown finished!");
                }
            }
        }

        // =========================
        // ABILITY 2
        // =========================

        if (ability2 != null)
        {
            bool isActive = ability2.IsActive();

            // Ability just finished
            if (ability2WasActive && !isActive)
            {
                ability2OnCooldown.Value = true;
                ability2CooldownTimer.Value = heroData.ability2.cooldown;

                StopAbilityVisualClientRpc(2);

                Debug.Log(
                    "Ab2 cooldown started: " +
                    heroData.ability2.cooldown
                );
            }

            ability2WasActive = isActive;

            if (ability2OnCooldown.Value)
            {
                ability2CooldownTimer.Value -= Time.deltaTime;

                if (ability2CooldownTimer.Value <= 0f)
                {
                    ability2CooldownTimer.Value = 0f;
                    ability2OnCooldown.Value = false;

                    Debug.Log("Ab2 cooldown finished!");
                }
            }
        }
    }

    public float Ability1CooldownRemaining =>
        ability1OnCooldown.Value ? ability1CooldownTimer.Value : 0f;

    public float Ability2CooldownRemaining =>
        ability2OnCooldown.Value ? ability2CooldownTimer.Value : 0f;

    public float Ability1CooldownDuration =>
        heroData != null ? heroData.ability1.cooldown : 0f;

    public float Ability2CooldownDuration =>
        heroData != null ? heroData.ability2.cooldown : 0f;

    public float Ability1ActiveRemaining =>
        ability1ActiveTimer.Value;

    public float Ability2ActiveRemaining =>
        ability2ActiveTimer.Value;

    public int Ability2MaxCharge =>
        ability2 != null ? ability2.MaxCharge() : 0;

    public int Ability2CurrentCharge =>
        ability2Charge.Value;

    public bool Ability1IsUsable =>
        ability1 != null && ability1.IsUsable();

    public bool Ability2IsUsable =>
        ability2 != null && ability2.IsUsable();

    private void UpdateQuickCast()
    {
        // =========================
        // ABILITY 1
        // =========================

        if (ability1 != null && ability1QuickCast.Value)
        {
            bool isUsable = ability1.IsUsable();

            if (isUsable && !ability1WasUsable)
            {
                UseAbility1();
            }

            ability1WasUsable = isUsable;
        }
        else if (ability1 != null)
        {
            ability1WasUsable = ability1.IsUsable();
        }


        // =========================
        // ABILITY 2
        // =========================

        if (ability2 != null && ability2QuickCast.Value)
        {
            bool isUsable = ability2.IsUsable();

            if (isUsable && !ability2WasUsable)
            {
                UseAbility2();
            }

            ability2WasUsable = isUsable;
        }
        else if (ability2 != null)
        {
            ability2WasUsable = ability2.IsUsable();
        }
    }

    private void Update()
    {
        if (!IsServer)
            return;

        ability1?.Update();
        ability2?.Update();

        UpdateQuickCast();

        if (ability1 != null)
        {
            if (ability1.IsActive())
            {
                ability1ActiveTimer.Value = ability1.RemainingTime;
            }
            else
            {
                ability1ActiveTimer.Value = 0f;
            }
        }

        if (ability2 != null)
        {
            if (ability2.IsActive())
            {
                ability2ActiveTimer.Value = ability2.RemainingTime;
            }
            else
            {
                ability2ActiveTimer.Value = 0f;
            }
        }

        UpdateAbilityCooldowns();

        if (ability2 != null && ability2.HasCharge())
        {
            ability2Charge.Value = ability2.CurrentCharge();
        }
    }
}
