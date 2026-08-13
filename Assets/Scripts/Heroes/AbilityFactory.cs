using UnityEngine;

public static class AbilityFactory
{
    public static IAbility Create(
        string abilityId,
        PlayerNetwork player,
        PlayerStreak streak,
        HeroDefinition heroData)
    {
        switch (abilityId)
        {
            // Guitarist Abilities
            case "guitaristAb1":
                return new GuitaristAbility1(player, streak, heroData);
            case "guitaristAb2":
                return new GuitaristAbility2(player, streak, heroData);

            default:
                Debug.LogError(
                    "Unknown ability ID: " + abilityId
                );

                return null;
        }
    }
}