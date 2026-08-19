using UnityEngine;

public class GuitaristAudioLogic : MonoBehaviour, IHeroAudioLogic
{
    private HeroAudio heroAudio;
    private PlayerAbilities playerAbilities;

    private int ability2FragmentIndex;

    private AudioSource currentFragmentSource;

    private void Awake()
    {
        heroAudio = GetComponent<HeroAudio>();
        playerAbilities = GetComponentInParent<PlayerAbilities>();
    }

    public void PlayAttackHit(
        Vector2 position,
        int streak)
    {
        if (heroAudio == null)
            return;

        if (playerAbilities != null &&
            playerAbilities.Ability2ActiveRemaining > 0f)
        {
            PlayAbility2Fragment(position);
            return;
        }

        AudioClip[] clips =
            heroAudio.GetAttackHitClips();

        if (clips == null || clips.Length == 0)
            return;

        int index =
            streak % clips.Length;

        heroAudio.PlayClipAtPosition(
            clips[index],
            position,
            heroAudio.AttackHitVolume
        );
    }

    public void PlayAbility1(Vector2 position)
    {
        if (heroAudio == null)
            return;

        AudioClip[] clips =
            heroAudio.GetAbility1Clips();

        if (clips == null || clips.Length == 0)
            return;

        AudioClip melody = clips[0];

        heroAudio.PlayClipFromHero(
            melody,
            heroAudio.Ability1Volume
        );
    }

    public void PlayAbility2(Vector2 position)
    {
        ability2FragmentIndex = 0;
        currentFragmentSource = null;
    }

    private void PlayAbility2Fragment(Vector2 position)
    {
        AudioClip[] clips =
            heroAudio.GetAbility2Clips();

        if (clips == null || clips.Length == 0)
            return;

        if (currentFragmentSource != null &&
            currentFragmentSource.isPlaying)
        {
            return;
        }

        if (ability2FragmentIndex >= clips.Length)
            ability2FragmentIndex = 0;

        AudioClip clip =
            clips[ability2FragmentIndex];

        currentFragmentSource =
            heroAudio.PlayClipAtPosition(
                clip,
                position,
                heroAudio.Ability2Volume
            );

        if (currentFragmentSource == null)
            return;

        ability2FragmentIndex++;

        if (ability2FragmentIndex >= clips.Length)
            ability2FragmentIndex = 0;
    }
}