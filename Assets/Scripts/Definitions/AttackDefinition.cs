using System;
using UnityEngine;

public enum ProjectileSpawnMode
{
    Point,
    Direction
}

[Serializable]
public class AttackDefinition
{
    public Sprite icon;
    public string name;
    public string description;
    public GameObject projectilePrefab;

    public ProjectileSpawnMode spawnMode;

    [Tooltip("Only used if spawnMode is Point")]
    public Vector2 projectileSpawnOffset = Vector2.zero;

    public float projectileSpawnDistance = 0.5f;

    public int damage = 10;
    public float cooldown = 0.5f;
    public float range = 50f;
    public float projectileSpeed = 10f;
}