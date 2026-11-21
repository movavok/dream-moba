using UnityEngine;

[System.Serializable]
public class HeroAudioDefinition
{
    [Header("Basic Attack")]
    public AudioClip[] basicAttack;

    [Header("Attack Hit")]
    public AudioClip[] attackHit;

    [Header("Abilities")]
    public AudioClip[] ability1;
    public AudioClip[] ability2;

    [Header("Hero")]
    public AudioClip[] death;
    public AudioClip[] respawn;
}
