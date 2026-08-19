using UnityEngine;

public class DefaultHeroAudioLogic : MonoBehaviour, IHeroAudioLogic
{
    private HeroAudio heroAudio;

    public void Initialize(HeroAudio audio)
    {
        heroAudio = audio;
    }

    public void PlayAttackHit(Vector2 position, int streak)
    {
        AudioClip[] clips = heroAudio.GetAttackHitClips();

        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip =
            clips[Random.Range(0, clips.Length)];

        heroAudio.PlayClipAtPosition(
            clip,
            position,
            heroAudio.AttackHitVolume
        );
    }

    public void PlayAbility1(Vector2 position)
    {
        AudioClip[] clips =
            heroAudio.GetAbility1Clips();

        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip =
            clips[Random.Range(0, clips.Length)];

        heroAudio.PlayClipAtPosition(
            clip,
            position,
            heroAudio.Ability1Volume
        );
    }

    public void PlayAbility2(Vector2 position)
    {
        AudioClip[] clips =
            heroAudio.GetAbility2Clips();

        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip =
            clips[Random.Range(0, clips.Length)];

        heroAudio.PlayClipAtPosition(
            clip,
            position,
            heroAudio.Ability2Volume
        );
    }
}