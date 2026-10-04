using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "A Thousand Battles Later/WFC Dungeon Settings")]
public sealed class WfcDungeonSettings : ScriptableObject
{
    public const int MinimumRoomWidth = 100;
    public const int MaximumRoomWidth = 150;
    public const int MinimumRoomHeight = 50;
    public const int MaximumRoomHeight = 100;

    public static int NormalizeRoomWidth(int requested) => Mathf.Max(MinimumRoomWidth, requested);

    [Header("Current prototype: one winding room")]
    public bool singleRoom = true;
    public bool randomizeRoomSize = true;
    [Min(1)] public float routeMultiplier = 2f;
    public Vector2Int SizeForSeed(int seed, bool? randomOverride = null)
    {
        if (!singleRoom || !(randomOverride ?? randomizeRoomSize)) return new Vector2Int(singleRoom ? NormalizeRoomWidth(width) : width, height);
        var random = new System.Random(seed ^ 0x57fc12);
        return new Vector2Int(random.Next(MinimumRoomWidth, MaximumRoomWidth + 1), random.Next(MinimumRoomHeight, MaximumRoomHeight + 1));
    }
    [Range(40, 256)] public int width = 120, height = 80;
    private void OnValidate()
    {
        if (!singleRoom) return;
        width = Mathf.Clamp(width, MinimumRoomWidth, MaximumRoomWidth);
        height = Mathf.Clamp(height, MinimumRoomHeight, MaximumRoomHeight);
    }
    public float cellSize = 4.5f;
    [Range(0, 3)] public float enemyDensity = 1;
    public int solverBudget = 4096;
    public WfcRoomSettings art;
    [Tooltip("Bottom-left, bottom, bottom-right, left, centre, right, top-left, top, top-right")]
    public TileBase[] wallTiles = new TileBase[9];
    public GameObject orcPrefab, eyePrefab, mushroomPrefab, skeletonPrefab;
    public GameObject EnemyPrefab(DungeonEnemyKind species) => species switch
    {
        DungeonEnemyKind.Orc => orcPrefab,
        DungeonEnemyKind.FlyingEye => eyePrefab,
        DungeonEnemyKind.Mushroom => mushroomPrefab,
        DungeonEnemyKind.Skeleton => skeletonPrefab,
        _ => throw new System.ArgumentOutOfRangeException(nameof(species))
    };
    public Material wallMaterial;
    public DungeonLootTable loot;
}
