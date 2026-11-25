using UnityEngine;

[CreateAssetMenu(menuName = "Game/Items/Lighter Attack Config")]
public sealed class LighterAttackConfig : ScriptableObject
{
    [Tooltip("Cosmetic local particle prefab attached to each igniting basic projectile.")]
    [SerializeField] private ParticleSystem attackFirePrefab;

    public ParticleSystem AttackFirePrefab => attackFirePrefab;
}
