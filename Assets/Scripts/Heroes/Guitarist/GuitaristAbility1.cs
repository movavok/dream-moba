using UnityEngine;

public class GuitaristAbility1 : AbilityBase
{
    private PlayerNetwork player;
    private PlayerStreak streak;
    private HeroDefinition heroData;

    private float activeSpeedMultiplier;
    public float ActiveSpeedMultiplier => activeSpeedMultiplier;

    public override float VisualPower => activeSpeedMultiplier;

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
        return !active && streak.Streak >= 1;
    }

    public override bool Use()
    {
        if (!IsUsable())
            return false;

        active = true;
        timer = heroData.ability1.duration;

        activeSpeedMultiplier = GetSpeedMultiplier();
        player.SetSpeedMultiplier(activeSpeedMultiplier);

        Debug.Log(
            "Guitarist A1 on! Spd mult: " +
            activeSpeedMultiplier
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
    }

    private float GetSpeedMultiplier()
    {
        if (!active)
            return 1f;

        return 1f + streak.Streak * heroData.ability1.multiplier;
    }
}