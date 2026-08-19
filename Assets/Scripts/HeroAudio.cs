using UnityEngine;

public class HeroAudio : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    private PlayerNetwork playerNetwork;
    private HeroDefinition heroData;

    private IHeroAudioLogic audioLogic;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        playerNetwork = GetComponentInParent<PlayerNetwork>();

        if (playerNetwork == null)
        {
            Debug.LogError(
                "HeroAudio: PlayerNetwork not found!"
            );

        }
    }

    private void Start()
    {
        if (playerNetwork != null)
        {
            heroData = playerNetwork.HeroData;
            CreateAudioLogic();
        }
    }

    private void CreateAudioLogic()
    {
        if (heroData == null)
            return;

        audioLogic =
            HeroAudioFactory.Create(
                this,
                heroData
            );
    }

    private void OnEnable()
    {
        if (playerNetwork != null)
            playerNetwork.AttackUsed += OnAttackUsed;
    }

    private void OnDisable()
    {
        if (playerNetwork != null)
            playerNetwork.AttackUsed -= OnAttackUsed;
    }

    private void OnAttackUsed()
    {
        PlayRandom(
            heroData != null
                ? heroData.audio.basicAttack
                : null,
            heroData != null
                ? heroData.audio.basicAttackVolume
                : 1f
        );
    }

    private void PlayRandom(AudioClip[] clips, float volume)
    {
        if (audioSource == null)
            return;

        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip =
            clips[Random.Range(0, clips.Length)];

        audioSource.pitch =
            Random.Range(0.95f, 1.05f);

        audioSource.PlayOneShot(clip, volume);
    }

    public void PlayAttackHit(
    Vector2 position,
    int streak)
    {
        if (audioLogic == null)
            return;

        audioLogic.PlayAttackHit(
            position,
            streak
        );
    }

    private void PlayRandomAtPosition(
        AudioClip[] clips,
        Vector2 position,
        float volume)
    {
        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip =
            clips[Random.Range(0, clips.Length)];

        AudioManager.Instance.PlayAtPosition(
            clip,
            position,
            volume
        );
    }

    public AudioClip[] GetAttackHitClips()
    {
        if (heroData == null || heroData.audio == null)
            return null;

        return heroData.audio.attackHit;
    }

    public float AttackHitVolume
    {
        get
        {
            if (heroData == null || heroData.audio == null)
                return 1f;

            return heroData.audio.attackHitVolume;
        }
    }

    public AudioSource PlayClipAtPosition(
        AudioClip clip,
        Vector2 position,
        float volume)
    {
        if (clip == null)
            return null;

        return AudioManager.Instance.PlayAtPosition(
            clip,
            position,
            volume
        );
    }

    public void PlayClipFromHero(
        AudioClip clip,
        float volume)
    {
        if (audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip, volume);
    }

    public void PlayAbility1(Vector2 position)
    {
        if (audioLogic == null)
            return;

        audioLogic.PlayAbility1(position);
    }

    public void PlayAbility2(Vector2 position)
    {
        if (audioLogic == null)
            return;

        audioLogic.PlayAbility2(position);
    }

    public AudioClip[] GetAbility1Clips()
    {
        if (heroData == null || heroData.audio == null)
            return null;

        return heroData.audio.ability1;
    }

    public float Ability1Volume
    {
        get
        {
            if (heroData == null || heroData.audio == null)
                return 1f;

            return heroData.audio.ability1Volume;
        }
    }

    public AudioClip[] GetAbility2Clips()
    {
        if (heroData == null || heroData.audio == null)
            return null;

        return heroData.audio.ability2;
    }

    public float Ability2Volume
    {
        get
        {
            if (heroData == null || heroData.audio == null)
                return 1f;

            return heroData.audio.ability2Volume;
        }
    }
}