using UnityEngine;

[CreateAssetMenu(fileName = "EnergyDrinkUse", menuName = "Game/Items/Energy Drink Use")]
public class EnergyDrinkUseAction : ItemUseAction
{
    [SerializeField, Range(0f, 1f)] private float speedBonus = 0.15f;
    [SerializeField, Min(0f)] private float duration = 15f;
    [SerializeField] private AudioClip[] useSounds;

    public override void PlayUseSound(Transform user, string group)
    {
        if (user == null || AudioManager.Instance == null)
            return;

        if (useSounds == null || useSounds.Length == 0)
            return;

        AudioClip clip =
            useSounds[Random.Range(0, useSounds.Length)];

        if (clip == null)
            return;

        AudioManager.Instance.PlaySoundFollowing(
            group,
            SoundKeys.ItemUse,
            clip,
            user
        );
    }

    public override bool CanUse(PlayerNetwork user, Inventory inventory, int slotIndex)
    {
        if (user == null || !user.IsServer || !user.IsSpawned ||
            inventory == null || inventory.GetItem(slotIndex)?.useAction != this ||
            inventory.GetSlot(slotIndex).IsEmpty ||
            user.GetComponent<StatController>() == null)
            return false;

        TimedStatEffects effects = user.GetComponent<TimedStatEffects>();
        return effects == null || !effects.IsActive(this);
    }

    public override void Use(PlayerNetwork user, Inventory inventory, int slotIndex)
    {
        if (!CanUse(user, inventory, slotIndex) || !inventory.TryRemove(slotIndex, 1))
            return;

        TimedStatEffects effects = user.GetComponent<TimedStatEffects>();
        if (effects == null)
            effects = user.gameObject.AddComponent<TimedStatEffects>();

        effects.Activate(this, duration,
            new StatModifier(StatType.MoveSpeed, ModifierOp.Percent, speedBonus));
    }
}
