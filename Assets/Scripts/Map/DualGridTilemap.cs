using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using static TileType;

public enum TileType
{
    Empty,
    Grass
}

public class DualGridTilemap : MonoBehaviour
{
    private static readonly Vector3Int[] NEIGHBOURS =
    {
        new Vector3Int(0, 0, 0),
        new Vector3Int(1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(1, 1, 0)
    };

    private Dictionary<(TileType, TileType, TileType, TileType), Tile> neighbourTupleToTile;

    [Header("Tilemaps")]
    [SerializeField] private Tilemap placeholderTilemap;
    [SerializeField] private Tilemap displayTilemap;

    [Header("Grass Placeholder")]
    [SerializeField] private Tile grassPlaceholderTile;

    [Header("16 Dual Grid Tiles")]
    [SerializeField] private Tile[] tiles;

    private void Awake()
    {
        neighbourTupleToTile = new()
        {
            { (Grass, Grass, Grass, Grass), tiles[6] },

            { (Empty, Empty, Empty, Grass), tiles[13] },
            { (Empty, Empty, Grass, Empty), tiles[0] },
            { (Empty, Grass, Empty, Empty), tiles[8] },
            { (Grass, Empty, Empty, Empty), tiles[15] },

            { (Empty, Grass, Empty, Grass), tiles[1] },
            { (Grass, Empty, Grass, Empty), tiles[11] },

            { (Empty, Empty, Grass, Grass), tiles[3] },
            { (Grass, Grass, Empty, Empty), tiles[9] },

            { (Empty, Grass, Grass, Grass), tiles[5] },
            { (Grass, Empty, Grass, Grass), tiles[2] },
            { (Grass, Grass, Empty, Grass), tiles[10] },
            { (Grass, Grass, Grass, Empty), tiles[7] },

            { (Empty, Grass, Grass, Empty), tiles[14] },
            { (Grass, Empty, Empty, Grass), tiles[4] },

            { (Empty, Empty, Empty, Empty), tiles[12] }
        };
    }

    private void Start()
    {
        RefreshDisplayTilemap();
    }

    public void SetCell(Vector3Int coords, Tile tile)
    {
        placeholderTilemap.SetTile(coords, tile);
        SetDisplayTilesAround(coords);
    }

    private TileType GetTileTypeAt(Vector3Int coords)
    {
        if (placeholderTilemap.GetTile(coords) == grassPlaceholderTile)
            return Grass;

        return Empty;
    }

    private Tile CalculateDisplayTile(Vector3Int coords)
    {
        TileType topRight = GetTileTypeAt(coords - NEIGHBOURS[0]);
        TileType topLeft = GetTileTypeAt(coords - NEIGHBOURS[1]);
        TileType bottomRight = GetTileTypeAt(coords - NEIGHBOURS[2]);
        TileType bottomLeft = GetTileTypeAt(coords - NEIGHBOURS[3]);

        var neighbourTuple = (
            topLeft,
            topRight,
            bottomLeft,
            bottomRight
        );

        return neighbourTupleToTile[neighbourTuple];
    }

    private void SetDisplayTilesAround(Vector3Int pos)
    {
        for (int i = 0; i < NEIGHBOURS.Length; i++)
        {
            Vector3Int newPos = pos + NEIGHBOURS[i];

            Tile tile = CalculateDisplayTile(newPos);

            displayTilemap.SetTile(newPos, tile);
        }
    }

    public void RefreshDisplayTilemap()
    {
        for (int x = -53; x < 120; x++)
        {
            for (int y = -30; y < 135; y++)
            {
                SetDisplayTilesAround(new Vector3Int(x, y, 0));
            }
        }
    }
}