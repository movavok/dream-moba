using UnityEngine;

[CreateAssetMenu(
    fileName = "AudioLibrary",
    menuName = "Audio/Audio Library"
)]
public class AudioLibrary : ScriptableObject
{
    [Header("Damage")]
    public AudioClip[] damageTaken;
    [Range(0f, 1f)]
    public float damageTakenVolume = 1f;

    [Header("Building")]
    public AudioClip[] buildingTransitions;

    [Range(0f, 1f)]
    public float buildingTransitionVolume = 1f;
}