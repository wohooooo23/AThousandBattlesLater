using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "A Thousand Battles Later/WFC Dungeon Settings")]
public sealed class WfcDungeonSettings : ScriptableObject
{
    [Header("Current prototype: one winding room")]
    public bool singleRoom = true;
    public bool randomizeRoomSize = true;
    public Vector2Int SizeForSeed(int seed)
    {
        if (!singleRoom || !randomizeRoomSize) return new Vector2Int(width, height);
        var random = new System.Random(seed ^ 0x57fc12);
        return new Vector2Int(random.Next(50, 151), random.Next(50, 101));
    }
    [Range(40, 256)] public int width = 120, height = 80;
    public float cellSize = 4.5f;
    [Range(0, 3)] public float enemyDensity = 1;
    public int solverBudget = 4096;
    public WfcRoomSettings art;
    [Tooltip("Bottom-left, bottom, bottom-right, left, centre, right, top-left, top, top-right")]
    public TileBase[] wallTiles = new TileBase[9];
    public GameObject orcPrefab, eyePrefab;
    public Material wallMaterial;
}
