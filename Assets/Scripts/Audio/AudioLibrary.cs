using UnityEngine;

[CreateAssetMenu(
    fileName = "AudioLibrary",
    menuName = "Audio/Audio Library"
)]
public class AudioLibrary : ScriptableObject
{
    [Header("Damage")]
    public AudioClip[] damageTaken;

    [Header("Building")]
    public AudioClip[] buildingTransitions;
}
