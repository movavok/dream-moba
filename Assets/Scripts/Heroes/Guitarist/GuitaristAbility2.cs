using UnityEngine;

public class GuitaristAbility2 : AbilityBase
{
    private PlayerNetwork player;
    private PlayerStreak streak;
    private HeroDefinition heroData;

    private float timer;

    private int requiredStreaks = 8;
    private int newStreaks;

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

    public override void Use()
    {
        if (active)
            return;

        if (newStreaks < requiredStreaks)
            return;

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

        Debug.Log(
            "Guitarist A2 on! " +
            "Atk cooldown mult: " + heroData.ability2.multiplier +
            ", proj spd mult: " + heroData.ability2.multiplier
        );
    }

    public override void Update()
    {
        if (!active)
            return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            active = false;

            player.SetProjectileTrail(false);

            player.SetAttackCooldownMultiplier(1f);
            player.SetProjectileSpeedMultiplier(1f);

            streak.SetFrozen(false);

            Debug.Log("Guitarist A2 ended!");
        }
    }
}