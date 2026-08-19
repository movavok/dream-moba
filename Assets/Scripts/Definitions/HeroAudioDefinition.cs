using UnityEngine;

[System.Serializable]
public class HeroAudioDefinition
{
    [Header("Basic Attack")]
    public AudioClip[] basicAttack;
    [Range(0f, 1f)]
    public float basicAttackVolume = 0.3f;

    [Header("Attack Hit")]
    public AudioClip[] attackHit;
    [Range(0f, 1f)]
    public float attackHitVolume = 1f;

    [Header("Abilities")]
    public AudioClip[] ability1;
    [Range(0f, 1f)]
    public float ability1Volume = 1f;

    public AudioClip[] ability2;
    [Range(0f, 1f)]
    public float ability2Volume = 1f;

    [Header("Hero")]

    public AudioClip[] death;
    [Range(0f, 1f)]
    public float deathVolume = 1f;

    public AudioClip[] respawn;
    [Range(0f, 1f)]
    public float respawnVolume = 1f;
}