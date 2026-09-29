using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "A Thousand Battles Later/WFC Test Room Settings")]
public sealed class WfcRoomSettings : ScriptableObject
{
    [Header("Interior cells; the enclosing shell occupies the next cell outside")]
    [Min(16)] public int width = 20;
    [Min(16)] public int height = 20;
    [Min(.1f)] public float cellSize = 4.5f;
    [Header("Module constraints")]
    [Range(2, 4)] public int wallCount = 3;
    [Min(2)] public int wallSeparation = 3;
    [Range(1f, 4f)] public float maximumPlatformShift = 3f;
    [Range(1, 32)] public int attempts = 12;
    [Min(32)] public int solverBudget = 2048;
    [Header("Conservative single-jump movement profile, in world units")]
    public float jumpSpeed = 64f;
    public float airSpeed = 31.5f;
    public float gravity = 147.15f;
    public Vector2 actorSize = new Vector2(2.2f, 5.496f);
    [Min(0f)] public float clearance = .25f;
    [Header("Reused castle art; collision semantics are explicit")]
    public TileBase groundFill;
    public TileBase groundTop;
    public TileBase platformLeft;
    public TileBase platformMiddle;
    public TileBase platformRight;
    public TileBase background;
    public Sprite bossDoor;

    public void ValidateSettings()
    {
        if (width < 16 || height < 16 || width > 64 || height > 64 || height % 2 != 0 || cellSize <= 0 ||
            wallCount < 2 || wallCount > 4 || wallSeparation < 2 || maximumPlatformShift <= 0 ||
            attempts < 1 || solverBudget < 1 || jumpSpeed <= 0 || airSpeed <= 0 || gravity <= 0 ||
            actorSize.x <= 0 || actorSize.y <= 0 || clearance < 0)
            throw new System.InvalidOperationException("Invalid WFC room dimensions, constraints or movement profile.");
        if (jumpSpeed * jumpSpeed / (2 * gravity) < cellSize * 2 + clearance)
            throw new System.InvalidOperationException("The movement profile cannot clear the two-cell platform rise.");
    }
}
