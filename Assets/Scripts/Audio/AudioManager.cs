using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource audioSourcePrefab;
    [SerializeField] private AudioLibrary globalLibrary;

    [Header("Damage")]
    [SerializeField] private float damageMinPitch = 0.9f;
    [SerializeField] private float damageMaxPitch = 1.1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public AudioSource PlayAtPosition(
    AudioClip clip,
    Vector3 position,
    float volume = 1f,
    float pitch = 1f)
    {
        if (clip == null)
            return null;

        if (audioSourcePrefab == null)
        {
            Debug.LogError(
                "AudioManager: AudioSource Prefab is not assigned!"
            );

            return null;
        }

        AudioSource source =
            Instantiate(
                audioSourcePrefab,
                position,
                Quaternion.identity
            );

        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();

        Destroy(
            source.gameObject,
            clip.length + 0.1f
        );

        return source;
    }

    public void PlayDamageTaken(Vector2 position)
    {
        if (globalLibrary == null)
            return;

        if (globalLibrary.damageTaken == null ||
            globalLibrary.damageTaken.Length == 0)
            return;

        AudioClip clip =
            globalLibrary.damageTaken[
                Random.Range(
                    0,
                    globalLibrary.damageTaken.Length
                )
            ];

        float pitch = Random.Range(
            damageMinPitch,
            damageMaxPitch
        );

        PlayAtPosition(
            clip,
            position,
            globalLibrary.damageTakenVolume,
            pitch
        );
    }
}