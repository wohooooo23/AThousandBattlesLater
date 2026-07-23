using UnityEngine;

/// <summary>
/// Shared procedural sprite/mesh utility (no imported textures needed).
/// Exposes: EnsureSprites(), SquareSprite / CircleSprite / ArrowSprite,
///          AddSprite(), CreateChildSprite(), CreateDisc(), CreateRing(), CreateArc().
/// Used by: Enemy/EnemyAttack/* (warning + bullet visuals) and UI/EnemyHealthBar.
/// Kept in Common because it is shared across the enemy and ui layers.
/// </summary>
public static class SceneArt
{
    public static Sprite SquareSprite { get; private set; }
    public static Sprite CircleSprite { get; private set; }
    public static Sprite ArrowSprite { get; private set; }

    public static void EnsureSprites()
    {
        if (SquareSprite != null) return;
        SquareSprite = MakeSprite("Square", (x, y) => true);
        CircleSprite = MakeSprite("Circle", (x, y) => x * x + y * y <= 0.245f);
        ArrowSprite = MakeSprite("Arrow", (x, y) =>
            (y > 0f && Mathf.Abs(x) <= (0.5f - y) * 1.05f) ||
            (y <= 0.05f && y >= -0.5f && Mathf.Abs(x) <= 0.14f));
    }

    private static Sprite MakeSprite(string name, System.Func<float, float, bool> filled)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = name + " Texture";
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float px = (x + 0.5f) / size - 0.5f;
            float py = (y + 0.5f) / size - 0.5f;
            pixels[y * size + x] = filled(px, py) ? Color.white : Color.clear;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
    }

    /// <summary>
    /// Sorting layer for every runtime attack telegraph/effect. Runtime-created renderers default to
    /// the "Default" sorting layer, which is the BOTTOM layer in this project (Default → background →
    /// ground → enemy → secret → Hero), so warnings and strikes were being drawn behind the map.
    /// All SceneArt visuals belong to enemy attacks, so they share the "enemy" sorting layer.
    /// </summary>
    public const string EffectSortingLayer = "enemy";

    /// <summary>
    /// Moves an instantiated effect (hitbox prefab, projectile) onto the effect sorting layer.
    /// The authored prefabs sit on "Default", which renders behind the map tilemaps.
    /// </summary>
    public static void ApplyEffectSorting(GameObject effect)
    {
        if (effect == null)
            return;
        foreach (Renderer renderer in effect.GetComponentsInChildren<Renderer>(true))
            renderer.sortingLayerName = EffectSortingLayer;
    }

    /// <summary>Sorting layer for pickups, chests, coins and ability orbs: above the terrain, below combat.</summary>
    public const string ItemSortingLayer = "item";

    /// <summary>
    /// Moves a world prop onto the item sorting layer. Same problem as ApplyEffectSorting: the
    /// authored prefabs sit on "Default", which is now the parallax backdrop layer and therefore
    /// renders behind the whole map. World-space Canvases (ability orb labels) are handled too.
    /// </summary>
    public static void ApplyItemSorting(GameObject prop)
    {
        if (prop == null)
            return;
        foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>(true))
            renderer.sortingLayerName = ItemSortingLayer;
        foreach (Canvas canvas in prop.GetComponentsInChildren<Canvas>(true))
            canvas.sortingLayerName = ItemSortingLayer;
    }

    public static SpriteRenderer AddSprite(GameObject target, Sprite sprite, Color color, int order)
    {
        SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = EffectSortingLayer;
        renderer.sortingOrder = order;
        return renderer;
    }

    public static GameObject CreateChildSprite(Transform parent, string name, Sprite sprite, Color color, int order)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        AddSprite(child, sprite, color, order);
        return child;
    }

    public static GameObject CreateDisc(string name, Vector2 position, float diameter, Color color, int order)
    {
        GameObject disc = new GameObject(name);
        disc.transform.position = position;
        disc.transform.localScale = Vector3.one * diameter;
        AddSprite(disc, CircleSprite, color, order);
        return disc;
    }

    public static LineRenderer CreateRing(Transform parent, float radius, float width, Color color, int order, int segments)
    {
        return CreateArc(parent, radius, width, color, order, 0f, 360f, segments);
    }

    public static LineRenderer CreateArc(Transform parent, float radius, float width, Color color, int order, float startAngle, float endAngle, int segments)
    {
        GameObject lineObject = new GameObject("Arc");
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = Mathf.Approximately(endAngle - startAngle, 360f);
        line.positionCount = segments + (line.loop ? 0 : 1);
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.sortingLayerName = EffectSortingLayer;
        line.sortingOrder = order;
        line.material = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < line.positionCount; i++)
        {
            float t = line.loop ? i / (float)segments : i / (float)(line.positionCount - 1);
            float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
        return line;
    }
}
