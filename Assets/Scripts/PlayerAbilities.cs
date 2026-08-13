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

    private float ability1CooldownTimer;
    private float ability2CooldownTimer;

    private bool ability1OnCooldown;
    private bool ability2OnCooldown;

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

    [ServerRpc]
    private void UseAbility1ServerRpc()
    {
        if (ability1 == null)
            return;

        if (ability1OnCooldown)
            return;

        ability1.Use();
    }

    [ServerRpc]
    private void UseAbility2ServerRpc()
    {
        if (ability2 == null)
            return;

        if (ability2OnCooldown)
            return;

        ability2.Use();
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
                ability1OnCooldown = true;
                ability1CooldownTimer = heroData.ability1.cooldown;

                Debug.Log(
                    "Ab1 cooldown started: " +
                    heroData.ability1.cooldown
                );
            }

            ability1WasActive = isActive;

            if (ability1OnCooldown)
            {
                ability1CooldownTimer -= Time.deltaTime;

                if (ability1CooldownTimer <= 0f)
                {
                    ability1CooldownTimer = 0f;
                    ability1OnCooldown = false;

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
                ability2OnCooldown = true;
                ability2CooldownTimer = heroData.ability2.cooldown;

                Debug.Log(
                    "Ab2 cooldown started: " +
                    heroData.ability2.cooldown
                );
            }

            ability2WasActive = isActive;

            if (ability2OnCooldown)
            {
                ability2CooldownTimer -= Time.deltaTime;

                if (ability2CooldownTimer <= 0f)
                {
                    ability2CooldownTimer = 0f;
                    ability2OnCooldown = false;

                    Debug.Log("Ab2 cooldown finished!");
                }
            }
        }
    }

    private void Update()
    {
        if (!IsServer)
            return;

        ability1?.Update();
        ability2?.Update();

        UpdateAbilityCooldowns();
    }
}
