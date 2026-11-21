using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource audioSourcePrefab;
    [SerializeField] private AudioSource buildingTransitionSource;
    [SerializeField] private AudioLibrary globalLibrary;
    [SerializeField] private AudioVolumeSettings volumeSettings;

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

        if (buildingTransitionSource != null)
            buildingTransitionSource.spatialBlend = 0f;
    }

    public float GetVolume(string group, string sound)
    {
        return volumeSettings != null
            ? volumeSettings.GetVolume(group, sound)
            : 1f;
    }

    public AudioSource PlaySound(
        string group,
        string sound,
        AudioClip clip,
        Vector3 position)
    {
        return PlayAtPosition(
            clip,
            position,
            GetVolume(group, sound)
        );
    }

    public AudioSource PlaySoundFollowing(
        string group,
        string sound,
        AudioClip clip,
        Transform follow)
    {
        if (clip == null || follow == null)
            return null;

        if (audioSourcePrefab == null)
        {
            Debug.LogError(
                "AudioManager: AudioSource Prefab is not assigned!"
            );

            return null;
        }

        AudioSource source =
            Instantiate(audioSourcePrefab);

        source.transform.SetParent(follow, false);
        source.transform.localPosition = Vector3.zero;

        source.spatialBlend = 1f;
        source.volume = GetVolume(group, sound);
        source.pitch = Random.Range(minPitch, maxPitch);
        source.clip = clip;

        source.Play();

        Destroy(
            source.gameObject,
            clip.length + 0.1f
        );

        return source;
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

        PlayRandomClip(
            AudioGroups.Global,
            SoundKeys.DamageTaken,
            globalLibrary.damageTaken,
            position
        );
    }

    public void PlayRandomClip(
        string group,
        string sound,
        AudioClip[] clips,
        Vector3 position)
    {
        if (clips == null || clips.Length == 0)
            return;

        PlayAtPosition(
            clips[Random.Range(0, clips.Length)],
            position,
            GetVolume(group, sound)
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

        if (clip == null)
            return;

        float volume = GetVolume(
            AudioGroups.Global,
            SoundKeys.BuildingTransition
        );

        if (buildingTransitionSource != null)
        {
            buildingTransitionSource.pitch = Random.Range(minPitch, maxPitch);
            buildingTransitionSource.PlayOneShot(
                clip,
                volume
            );
            return;
        }

        PlayLocal(
            clip,
            volume
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
