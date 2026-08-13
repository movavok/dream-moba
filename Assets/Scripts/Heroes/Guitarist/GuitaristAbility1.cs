using UnityEngine;

public class GuitaristAbility1 : AbilityBase
{
    private PlayerNetwork player;
    private PlayerStreak streak;
    private HeroDefinition heroData;

    private float timer;

    public GuitaristAbility1(
        PlayerNetwork player,
        PlayerStreak streak,
        HeroDefinition heroData)
    {
        this.player = player;
        this.streak = streak;
        this.heroData = heroData;
    }

    public override void Use()
    {
        if (active)
            return;

        active = true;
        timer = heroData.ability1.duration;

        player.SetSpeedMultiplier(GetSpeedMultiplier());

        Debug.Log("Guitarist A1 on! Spd mult: " + GetSpeedMultiplier());
    }

    public override void Update()
    {
        if (!active)
            return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            active = false;

            player.SetSpeedMultiplier(1f);

            Debug.Log("Guitarist A1 ended!");
        }
    }

    private float GetSpeedMultiplier()
    {
        if (!active)
            return 1f;

        return 1f + streak.Streak * heroData.ability1.multiplier;
    }
}