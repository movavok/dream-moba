using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(
    fileName = "GrassDetailTile",
    menuName = "Tiles/Grass Detail Tile"
)]
public class GrassDetailTile : Tile
{
    [Header("Grass Details")]
    public Sprite[] sprites;

    [Range(0f, 1f)]
    public float spawnChance = 0.7f;
}