using UnityEngine;
using Unity.Netcode;
using System;

public class PlayerStreak : NetworkBehaviour
{
    private HeroDefinition heroData;
    private PlayerNetwork playerNetwork;

    private NetworkVariable<int> streak =
        new NetworkVariable<int>(0);

    private float streakTimer;

    public int Streak => streak.Value;

    private bool frozen;

    public void SetFrozen(bool value)
    {
        if (!IsServer)
            return;

        frozen = value;
    }

    public void RefreshTimer()
    {
        if (!IsServer)
            return;

        if (heroData == null)
            return;

        streakTimer =
            heroData.attack.cooldown *
            heroData.streaks.streakTimeoutMultiplier;
    }

    public float DamageMultiplier =>
        1f + streak.Value * heroData.streaks.streakDamageBonus;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        playerNetwork = GetComponent<PlayerNetwork>();

        if (playerNetwork != null)
        {
            heroData = playerNetwork.HeroData;
        }
    }

    public event Action OnStreakAdded;
    public void AddStreak()
    {
        if (!IsServer)
            return;

        if (heroData == null)
            return;

        streak.Value++;

        // Streak lives for 2x the attack cooldown
        streakTimer = heroData.attack.cooldown * heroData.streaks.streakTimeoutMultiplier;

        OnStreakAdded?.Invoke();

        Debug.Log("Streak: " + streak.Value);
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (heroData == null)
            return;

        if (streak.Value <= 0)
            return;

        if (frozen)
            return;

        streakTimer -= Time.deltaTime;

        if (streakTimer <= 0f)
        {
            streak.Value--;

            if (streak.Value <= 0)
            {
                streak.Value = 0;
                OnStreakReset?.Invoke();
                Debug.Log("Streak lost completely");
            }
            else
            {
                streakTimer =
                    heroData.attack.cooldown *
                    heroData.streaks.streakTimeoutMultiplier;

                Debug.Log("Streak decreased: " + streak.Value);
            }
        }
    }

    public event Action OnStreakReset;

    public void ResetStreak()
    {
        if (!IsServer)
            return;

        streak.Value = 0;
        streakTimer = 0f;
        OnStreakReset?.Invoke();
    }
}