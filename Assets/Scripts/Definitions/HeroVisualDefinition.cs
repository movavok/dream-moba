using System;
using UnityEngine;
using UnityEngine.U2D.Animation;

[Serializable]
public class HeroVisualDefinition
{
    [Header("Sprite Library")]
    public SpriteLibraryAsset spriteLibrary;

    [Header("Idle")]
    public Sprite[] idleFront;
    public Sprite[] idleFrontRight;
    public Sprite[] idleRight;
    public Sprite[] idleBackRight;
    public Sprite[] idleBack;
    public Sprite[] idleBackLeft;
    public Sprite[] idleLeft;
    public Sprite[] idleFrontLeft;

    [Header("Walk")]
    public Sprite[] walkFront;
    public Sprite[] walkFrontLeft;
    public Sprite[] walkFrontRight;
    public Sprite[] walkBack;
    public Sprite[] walkBackLeft;
    public Sprite[] walkBackRight;
    public Sprite[] walkLeft;
    public Sprite[] walkRight;

    [Header("Animation")]
    public float idleAnimationSpeed = 8f;
    public float walkAnimationSpeed = 8f;
}