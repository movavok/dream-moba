using System;
using UnityEngine;
using UnityEngine.U2D.Animation;

[Serializable]
public class HeroVisualDefinition
{
    [Header("Sprite Library")]
    public SpriteLibraryAsset spriteLibrary;

    [Header("Idle")]
    public Sprite idleFront;
    public Sprite idleFrontLeft;
    public Sprite idleFrontRight;
    public Sprite idleBack;
    public Sprite idleBackLeft;
    public Sprite idleBackRight;
    public Sprite idleLeft;
    public Sprite idleRight;

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
    public float walkAnimationSpeed = 8f;
}