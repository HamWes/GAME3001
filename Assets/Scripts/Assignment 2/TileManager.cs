using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class TileManager : MonoBehaviour
{
    [SerializeField] private LayerMask tileLayer;
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Transform tileContainer;
    [SerializeField] ObjectPool tileObjectPool;
    [SerializeField] ObjectPool secondTileObjectPool;

    //private List<GameObject> tiles = new List<GameObject>();
    [SerializeField] Tile[,] tiles;
    public int tileWidth = 10;
    public int tileHeight = 10;

    [Header("Internal Cache")]
    Ray ray;
    Tile tile;
    SecondTile secondTile;

    private Camera mainCam;
    private Mouse mouse;

    private void Awake()
    {
        mainCam = Camera.main;
        mouse = Mouse.current;
    }

    private void Update()
    {
        if (mouse == null) mouse = Mouse.current;

        if (mouse == null || mainCam == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            TileClickCheck();
        }
        if (mouse.rightButton.wasPressedThisFrame)
        {
            Debug.Log(GetTile(new Vector2Int(4, 4)));
        }
    }

    private void TileClickCheck()
    {
        tile = null;
        secondTile = null;
        Vector2 screenPos = mouse.position.ReadValue();
        ray = mainCam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, tileLayer))
        {
            tile = hit.transform.GetComponent<Tile>();
            if (tile != null)
            {
                tile.ToggleHighlighted();
                List<Tile> neighbors = GetNeighbors(tile);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    Debug.Log(neighbors[i]);
                }
            }
        }
    }

    public Tile GetTile(Vector2Int pos)
    {
        return GetTile(pos.x, pos.y);
    }

    public Tile GetTile(int x, int z)
    {
        if (x < 0 || x >= tileWidth || z < 0 || z >= tileHeight) return null;

        return tiles[x, z];
    }

    public void SpawnTiles()
    {
        ClearTiles();
        Vector3 pos = new Vector3(0, 0, 0);
        tiles = new Tile[tileWidth, tileHeight];
        for (int x = 0; x < tileWidth; x++)
        {
            for (int z = 0; z < tileHeight; z++)
            {
                /*
                float ran = Random.Range(0f, 1f);

                if(ran < 0.5f)
                {
                    pool = secondTileObjectPool;
                }
                else
                {
                    pool = tileObjectPool;
                }
                */

                pos.x = x;
                pos.y = 0;
                pos.z = z;
                GameObject tile = tileObjectPool.Get(pos, Quaternion.identity, tileContainer);
                Tile t = tile.GetComponent<Tile>();
                t.Initialize(x, z);
                tiles[x, z] = t;
            }
        }
    }

    private void ClearTiles()
    {
        if (tiles == null) return;

        for (int x = 0; x < tiles.GetLength(0); x++)
        {
            for (int z = 0; z < tiles.GetLength(1); z++)
            {
                Tile tile = tiles[x, z];
                if (tile == null) continue;
                tileObjectPool.Return(tile.gameObject);
            }
        }
    }

    public List<Tile> GetNeighbors(Tile tile)
    {
        List<Tile> neighbors = new List<Tile>();
        if (tile == null || tiles == null) return neighbors;
        int x = tile.x_pos;
        int z = tile.z_pos;

        Tile north = GetTile(x, z + 1);
        if (north != null) neighbors.Add(north);

        Tile east = GetTile(x + 1, z);
        if (east != null) neighbors.Add(east);

        Tile south = GetTile(x, z - 1);
        if (south != null) neighbors.Add(south);

        Tile west = GetTile(x - 1, z);
        if (west != null) neighbors.Add(west);

        return neighbors;
    }
}
