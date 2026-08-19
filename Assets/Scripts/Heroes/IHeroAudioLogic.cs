using UnityEngine;

public interface IHeroAudioLogic
{
    void PlayAttackHit(Vector2 position, int streak);

    void PlayAbility1(Vector2 position);

    void PlayAbility2(Vector2 position);
}