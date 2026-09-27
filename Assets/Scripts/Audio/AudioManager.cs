using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource audioSourcePrefab;
    [SerializeField] private AudioLibrary globalLibrary;

    [Header("Pitch")]
    [SerializeField] private float minPitch = 0.95f;
    [SerializeField] private float maxPitch = 1.05f;

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
        float volume = 1f)
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

        source.spatialBlend = 1f;
        source.volume = volume;
        source.pitch = Random.Range(minPitch, maxPitch);
        source.clip = clip;

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

        PlayAtPosition(
            clip,
            position,
            globalLibrary.damageTakenVolume
        );
    }

    public void PlayBuildingTransition(int soundIndex)
    {
        if (globalLibrary == null)
            return;

        if (globalLibrary.buildingTransitions == null ||
            soundIndex < 0 ||
            soundIndex >= globalLibrary.buildingTransitions.Length)
            return;

        AudioClip clip =
            globalLibrary.buildingTransitions[soundIndex];

        PlayLocal(
            clip,
            globalLibrary.buildingTransitionVolume
        );
    }

    private void PlayLocal(
        AudioClip clip,
        float volume = 1f)
    {
        if (clip == null || audioSourcePrefab == null)
            return;

        AudioSource source =
            Instantiate(audioSourcePrefab);

        source.transform.position = Vector3.zero;

        source.spatialBlend = 0f;
        source.panStereo = 0f;
        source.volume = volume;
        source.pitch = Random.Range(minPitch, maxPitch);
        source.clip = clip;

        source.Play();

        Destroy(
            source.gameObject,
            clip.length + 0.1f
        );
    }
}