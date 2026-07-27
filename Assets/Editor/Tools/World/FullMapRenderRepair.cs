using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Repairs imported map prefabs whose renderers arrive with material and Sorting Layer references
/// that belong to the source package rather than this project.
///
/// Two failure modes keep recurring on import:
/// 1. A dangling material GUID resolves to nothing, so Unity falls back to the error shader and the
///    map draws magenta. Every map renderer is forced onto the built-in Sprites-Default material.
/// 2. Foreign Sorting Layer IDs are meaningless here, so tiles draw in an arbitrary order. Each
///    renderer is reassigned from the role table below.
/// </summary>
public static class FullMapRenderRepair
{
    /// <summary>Draw order of one imported map. Roles are matched by GameObject name.</summary>
    private sealed class MapRenderProfile
    {
        public string prefabPath;
        public string scenePath;       // null: the prefab has no scene of its own yet
        public string decorationLayer; // sorting layer for loose decoration SpriteRenderers
        public string previewName;     // null: skip the render capture
    }

    private static readonly MapRenderProfile[] Maps =
    {
        new()
        {
            prefabPath = "Assets/Prefab/Grid.prefab",
            scenePath = "Assets/Scenes/stage1_full.unity",
            // Flags, doors and bookshelves are wall dressing and belong behind the terrain.
            decorationLayer = "background",
            previewName = "stage1_full_render.png"
        },
        new()
        {
            prefabPath = "Assets/Prefab/Grid1.prefab",
            // The Boss arena is instanced into stage1_full, whose camera the Grid profile above
            // already repairs — no scene of its own to fix.
            scenePath = null,
            // Crates and bags sit on the floor, so they draw in front of Ground and Platform.
            decorationLayer = "ground",
            previewName = null
        }
    };

    /// <summary>Sorting layer and order for each named Tilemap role.</summary>
    private static readonly Dictionary<string, (string layer, int order)> TilemapRoles = new()
    {
        { "Background", ("background", 2) },
        { "Ground", ("ground", 0) },
        { "Platform", ("ground", 1) },
        { "Secret", ("secret", 0) }
    };

    private const int DecorationSortingOrder = 3;

    // Secret is deliberately absent: not every imported map has a hidden-area layer.
    private static readonly string[] RequiredTilemapNames = { "Ground", "Platform", "Background" };

    [MenuItem("Tools/A Thousand Battles Later/Repair Full Map Rendering")]
    public static void Repair()
    {
        foreach (MapRenderProfile profile in Maps)
        {
            RepairMapPrefab(profile);
            if (profile.scenePath != null)
                RepairSceneCamera(profile.scenePath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log($"Map rendering repaired for {Maps.Length} map(s): materials, draw order and 2D cameras are valid.");
    }

    [MenuItem("Tools/A Thousand Battles Later/Validate Full Map Rendering")]
    public static void Validate()
    {
        foreach (MapRenderProfile profile in Maps)
        {
            ValidateMapPrefab(profile);
            if (profile.scenePath != null)
                ValidateSceneCamera(profile.scenePath);
        }
        Debug.Log($"Map rendering validation passed for {Maps.Length} map(s).");
    }

    // Batch-mode entry point used by project verification.
    public static void RepairValidateAndCapture()
    {
        Repair();
        foreach (MapRenderProfile profile in Maps)
            if (profile.scenePath != null && profile.previewName != null)
                CapturePreview(profile.scenePath, profile.previewName);
    }

    /// <summary>
    /// Resolves a sorting layer by name. NameToID returns 0 both for "Default" and for names that do
    /// not exist, so the layer list is checked instead of trusting the returned id.
    /// </summary>
    private static int ResolveSortingLayer(string layerName)
    {
        if (!SortingLayer.layers.Any(layer => layer.name == layerName))
            throw new InvalidOperationException(
                $"Sorting Layer '{layerName}' is not defined in this project. Add it in Tags and Layers first.");
        return SortingLayer.NameToID(layerName);
    }

    private static (string layer, int order) GetTilemapRole(string tilemapName)
    {
        if (!TilemapRoles.TryGetValue(tilemapName, out (string layer, int order) role))
            throw new InvalidOperationException(
                $"Tilemap '{tilemapName}' has no draw-order role. Add it to TilemapRoles so its layer is deliberate.");
        return role;
    }

    private static void RepairMapPrefab(MapRenderProfile profile)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(profile.prefabPath);
        try
        {
            Material spriteMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (spriteMaterial == null)
                throw new InvalidOperationException("Unity built-in Sprites-Default material is unavailable.");

            int decorationLayerId = ResolveSortingLayer(profile.decorationLayer);
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.sharedMaterial = spriteMaterial;
                renderer.sortingLayerID = decorationLayerId;
                renderer.sortingOrder = DecorationSortingOrder;
                EditorUtility.SetDirty(renderer);
            }

            foreach (TilemapRenderer renderer in root.GetComponentsInChildren<TilemapRenderer>(true))
            {
                (string layer, int order) role = GetTilemapRole(renderer.gameObject.name);
                renderer.sharedMaterial = spriteMaterial;
                renderer.sortingLayerID = ResolveSortingLayer(role.layer);
                renderer.sortingOrder = role.order;
                EditorUtility.SetDirty(renderer);
            }

            PrefabUtility.SaveAsPrefabAsset(root, profile.prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RepairSceneCamera(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Camera camera = FindMainCamera(scene, scenePath);
        camera.orthographic = true;
        camera.cullingMask = ~0;
        EditorUtility.SetDirty(camera);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ValidateMapPrefab(MapRenderProfile profile)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(profile.prefabPath);
        string mapName = Path.GetFileName(profile.prefabPath);
        try
        {
            TilemapRenderer[] renderers = root.GetComponentsInChildren<TilemapRenderer>(true);
            foreach (string requiredName in RequiredTilemapNames)
            {
                int matches = renderers.Count(renderer => renderer.gameObject.name == requiredName);
                if (matches != 1)
                    throw new InvalidOperationException($"{mapName} needs exactly one {requiredName} TilemapRenderer, found {matches}.");
            }

            foreach (TilemapRenderer renderer in renderers)
            {
                (string layer, int order) role = GetTilemapRole(renderer.gameObject.name);
                RequireUsableMaterial(renderer.sharedMaterial, mapName, renderer.name);
                if (renderer.sortingLayerID != ResolveSortingLayer(role.layer))
                    throw new InvalidOperationException(
                        $"{mapName}: {renderer.name} must draw on the '{role.layer}' Sorting Layer.");
                if (renderer.sortingOrder != role.order)
                    throw new InvalidOperationException(
                        $"{mapName}: {renderer.name} must use sorting order {role.order}, found {renderer.sortingOrder}.");
            }

            Tilemap[] tilemaps = root.GetComponentsInChildren<Tilemap>(true);
            if (tilemaps.Length != renderers.Length)
                throw new InvalidOperationException($"{mapName} has mismatched Tilemap and TilemapRenderer counts.");

            foreach (Tilemap tilemap in tilemaps)
            {
                foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin)
                {
                    TileBase tile = tilemap.GetTile(position);
                    if (tile != null && tilemap.GetSprite(position) == null)
                        throw new InvalidOperationException($"{mapName}: {tilemap.name} has a tile without a sprite at {position}.");
                }
            }

            int decorationLayerId = ResolveSortingLayer(profile.decorationLayer);
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null)
                    throw new InvalidOperationException($"{mapName}: decoration {renderer.name} has no sprite.");
                RequireUsableMaterial(renderer.sharedMaterial, mapName, "decoration " + renderer.name);
                if (renderer.sortingLayerID != decorationLayerId)
                    throw new InvalidOperationException(
                        $"{mapName}: decoration {renderer.name} must draw on the '{profile.decorationLayer}' Sorting Layer.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Catches the magenta case: a dangling material GUID resolves to null, and a package shader that
    /// this render pipeline cannot compile reports isSupported == false.
    /// </summary>
    private static void RequireUsableMaterial(Material material, string mapName, string rendererName)
    {
        if (material == null || material.shader == null || !material.shader.isSupported)
            throw new InvalidOperationException(
                $"{mapName}: {rendererName} has a missing or unsupported material/shader (this is what draws magenta).");
    }

    private static void ValidateSceneCamera(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Camera camera = FindMainCamera(scene, scenePath);
        if (!camera.orthographic)
            throw new InvalidOperationException($"{scenePath} Main Camera must be orthographic.");
        if (camera.cullingMask == 0)
            throw new InvalidOperationException($"{scenePath} Main Camera culls every layer.");
    }

    private static Camera FindMainCamera(Scene scene, string scenePath)
    {
        Camera camera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(candidate => candidate.CompareTag("MainCamera"));
        if (camera == null)
            throw new InvalidOperationException($"{scenePath} has no MainCamera-tagged Camera.");
        return camera;
    }

    private static void CapturePreview(string scenePath, string previewName)
    {
        // Unity 6000.5 can crash in native TilemapRenderer code when Camera.Render is
        // forced on the null graphics device. Resource validation still runs in CI;
        // preview capture is intentionally limited to a real graphics device.
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.Log($"Skipped {previewName} capture because Unity is running with -nographics.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Camera camera = FindMainCamera(scene, scenePath);
        const int width = 1280;
        const int height = 720;
        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();

            string outputDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestResults");
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllBytes(Path.Combine(outputDirectory, previewName), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
