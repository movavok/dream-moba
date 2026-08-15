using UnityEngine;

[CreateAssetMenu(
    fileName = "HUD Definition",
    menuName = "Game/HUD Definition"
)]
public class HUDDefinition : ScriptableObject
{
    [Header("Frames")]
    public Sprite attackFrame;
    public Sprite passiveFrame;
    public Sprite abilityFrame;
    public Sprite quickCastFrame;
}