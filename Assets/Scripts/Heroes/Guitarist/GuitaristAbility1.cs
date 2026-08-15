using UnityEngine;

public class GuitaristAbility1 : AbilityBase
{
    private PlayerNetwork player;
    private PlayerStreak streak;
    private HeroDefinition heroData;

    public GuitaristAbility1(
        PlayerNetwork player,
        PlayerStreak streak,
        HeroDefinition heroData)
    {
        this.player = player;
        this.streak = streak;
        this.heroData = heroData;
    }

    public override bool IsUsable()
    {
        return !active && streak.Streak >= 1;;
    }

    public override bool Use()
    {
        if (!IsUsable())
            return false;

        active = true;
        timer = heroData.ability1.duration;

        player.SetSpeedMultiplier(GetSpeedMultiplier());

        Debug.Log(
            "Guitarist A1 on! Spd mult: " +
            GetSpeedMultiplier()
        );

        return true;
    }

    public override void Update()
    {
        bool ended = UpdateTimer();

        if (ended)
        {
            player.SetSpeedMultiplier(1f);
            Debug.Log("Guitarist A1 ended!");
            return;
        }

        if (active)
        {
            player.SetSpeedMultiplier(GetSpeedMultiplier());
        }
    }

    private float GetSpeedMultiplier()
    {
        if (!active)
            return 1f;

        return 1f + streak.Streak * heroData.ability1.multiplier;
    }
}