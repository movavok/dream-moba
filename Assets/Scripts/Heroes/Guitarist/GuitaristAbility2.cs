using UnityEngine;

public class GuitaristAbility2 : AbilityBase
{
    private PlayerNetwork player;
    private PlayerStreak streak;
    private HeroDefinition heroData;

    private int requiredStreaks = 6;
    private int newStreaks;

    public override bool HasCharge() => true;

    public override int CurrentCharge() => newStreaks;

    public override int MaxCharge() => requiredStreaks; 

    public GuitaristAbility2(
        PlayerNetwork player,
        PlayerStreak streak,
        HeroDefinition heroData)
    {
        this.player = player;
        this.streak = streak;
        this.heroData = heroData;

        streak.OnStreakAdded += OnStreakAdded;
        streak.OnStreakReset += OnStreakReset;
    }

    private void OnStreakAdded()
    {
        if (active)
            return;

        newStreaks++;
    }

    private void OnStreakReset()
    {
        if (active)
            return;

        newStreaks = 0;

        Debug.Log($"[{Time.time}] Guitarist A2 charge reset!");
    }

    public override bool IsUsable()
    {
        return !active && newStreaks >= requiredStreaks;
    }

    public override bool Use()
    {
        if (!IsUsable())
            return false;

        newStreaks = 0;

        active = true;
        timer = heroData.ability2.duration;

        player.SetProjectileTrail(true);

        player.SetAttackCooldownMultiplier(
            heroData.ability2.multiplier
        );

        player.SetProjectileSpeedMultiplier(
            heroData.ability2.multiplier
        );

        streak.SetFrozen(true);
        streak.RefreshTimer();

        Debug.Log("Guitarist A2 on!");

        return true;
    }

    public override void Update()
    {
        bool ended = UpdateTimer();

        if (ended)
        {
            player.SetProjectileTrail(false);
            player.SetAttackCooldownMultiplier(1f);
            player.SetProjectileSpeedMultiplier(1f);
            streak.SetFrozen(false);

            Debug.Log("Guitarist A2 ended!");
            return;
        }
    }
}