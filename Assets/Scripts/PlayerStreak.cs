using UnityEngine;
using Unity.Netcode;
using System;

public class PlayerStreak : NetworkBehaviour
{
    private HeroDefinition heroData;
    private PlayerNetwork playerNetwork;

    private NetworkVariable<int> streak =
        new NetworkVariable<int>(0);

    private NetworkVariable<float> streakTimer =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public float StreakTimer => streakTimer.Value;

    public float StreakTimerMax =>
        heroData != null
            ? heroData.attack.cooldown *
            heroData.streaks.streakTimeoutMultiplier
            : 0f;

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

        streakTimer.Value =
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

        // Streak lives for 3x the attack cooldown
        streakTimer.Value = heroData.attack.cooldown * heroData.streaks.streakTimeoutMultiplier;

        OnStreakAdded?.Invoke();

        Debug.Log("Streak: " + streak.Value);
    }

    public event Action OnStreakDecreased;

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

        streakTimer.Value -= Time.deltaTime;

        if (streakTimer.Value <= 0f)
        {
            streak.Value--;

            if (streak.Value <= 0)
            {
                streak.Value = 0;
                streakTimer.Value = 0f;
                OnStreakReset?.Invoke();
                Debug.Log("Streak lost completely");
            }
            else
            {
                streakTimer.Value =
                    heroData.attack.cooldown *
                    heroData.streaks.streakTimeoutMultiplier;

                OnStreakDecreased?.Invoke();

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
        streakTimer.Value = 0f;
        OnStreakReset?.Invoke();
    }
}