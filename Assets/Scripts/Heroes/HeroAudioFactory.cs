using UnityEngine;

public static class HeroAudioFactory
{
    public static IHeroAudioLogic Create(
        HeroAudio heroAudio,
        HeroDefinition heroData)
    {
        if (heroData == null)
            return null;

        switch (heroData.id)
        {
            case "Guitarist":
                return heroAudio.gameObject
                    .AddComponent<GuitaristAudioLogic>();

            default:
                DefaultHeroAudioLogic defaultLogic =
                    heroAudio.gameObject
                        .AddComponent<DefaultHeroAudioLogic>();

                defaultLogic.Initialize(heroAudio);

                return defaultLogic;
        }
    }
}