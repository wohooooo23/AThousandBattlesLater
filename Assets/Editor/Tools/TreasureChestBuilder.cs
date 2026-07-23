#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authors and validates the reusable treasure chest prefab.</summary>
public static class TreasureChestBuilder
{
    private const string ChestPath = "Assets/Resources/Prefabs/TreasureChest.prefab";
    private const string DropPath = "Assets/Prefab/DemoItemPickup.prefab";
    private const string PromptName = "InteractionPrompt";
    private const string SpawnName = "DropSpawnPoint";

    [MenuItem("Tools/Items/Repair Treasure Chest")]
    public static void Repair()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ChestPath);
        try
        {
            TreasureChest2D chest = root.GetComponent<TreasureChest2D>();
            if (chest == null)
                chest = root.AddComponent<TreasureChest2D>();

            Animator animator = root.GetComponent<Animator>();
            if (animator == null)
                throw new MissingReferenceException("TreasureChest.prefab is missing its imported Animator.");
            animator.enabled = false;

            BoxCollider2D trigger = root.GetComponent<BoxCollider2D>();
            if (trigger == null)
                trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            // The imported Sprite is 100 PPU and the prefab root is scaled 20x. A local 8x6
            // trigger covered most of a room; this local size follows the visible chest instead.
            trigger.size = new Vector2(0.42f, 0.3f);

            Transform previousPrompt = root.transform.Find(PromptName);
            if (previousPrompt != null)
                Object.DestroyImmediate(previousPrompt.gameObject);
            GameObject prompt = BuildPrompt(root.transform);

            Transform spawn = root.transform.Find(SpawnName);
            if (spawn == null)
            {
                GameObject spawnObject = new GameObject(SpawnName);
                spawnObject.transform.SetParent(root.transform, false);
                spawn = spawnObject.transform;
            }
            spawn.localPosition = new Vector3(0f, 0.24f, 0f);

            GameObject drop = AssetDatabase.LoadAssetAtPath<GameObject>(DropPath);
            if (drop == null || drop.GetComponent<ItemPickup>() == null)
                throw new MissingReferenceException(DropPath + " must contain ItemPickup.");
            ConfigureDropPhysics();
            drop = AssetDatabase.LoadAssetAtPath<GameObject>(DropPath);

            SerializedObject data = new SerializedObject(chest);
            data.FindProperty("interactionUI").objectReferenceValue = prompt;
            data.FindProperty("animator").objectReferenceValue = animator;
            data.FindProperty("openStateName").stringValue = "Chest_Open_Animation";
            data.FindProperty("spawnPoint").objectReferenceValue = spawn;
            data.FindProperty("dropPickupDelay").floatValue = 1f;
            data.FindProperty("applyPopForce").boolValue = true;
            data.FindProperty("upwardForce").floatValue = 7f;
            data.FindProperty("outwardForce").floatValue = 5f;
            SerializedProperty drops = data.FindProperty("itemPrefabs");
            drops.arraySize = 1;
            drops.GetArrayElementAtIndex(0).objectReferenceValue = drop;
            data.ApplyModifiedPropertiesWithoutUndo();

            // Authored on "Default", the backdrop layer, so it hid behind the map.
            SceneArt.ApplyItemSorting(root);
            PrefabUtility.SaveAsPrefabAsset(root, ChestPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("TREASURE_CHEST_OK: F interaction, world prompt, animation gate and ItemPickup drop are authored.");
    }

    private static void ConfigureDropPhysics()
    {
        GameObject dropRoot = PrefabUtility.LoadPrefabContents(DropPath);
        try
        {
            Rigidbody2D body = dropRoot.GetComponent<Rigidbody2D>();
            if (body == null)
                body = dropRoot.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 3f;
            body.linearDamping = 0.45f;
            body.angularDamping = 0.15f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D trigger = null;
            CircleCollider2D solid = null;
            foreach (CircleCollider2D collider in dropRoot.GetComponents<CircleCollider2D>())
            {
                if (collider.isTrigger && trigger == null)
                    trigger = collider;
                else if (!collider.isTrigger && solid == null)
                    solid = collider;
            }
            if (trigger == null)
                trigger = dropRoot.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.62f;
            if (solid == null)
                solid = dropRoot.AddComponent<CircleCollider2D>();
            solid.isTrigger = false;
            solid.radius = 0.45f;

            // Authored on "Default", the backdrop layer, so it hid behind the map.
            SceneArt.ApplyItemSorting(dropRoot);
            PrefabUtility.SaveAsPrefabAsset(dropRoot, DropPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(dropRoot);
        }
    }

    private static GameObject BuildPrompt(Transform parent)
    {
        GameObject prompt = new GameObject(PromptName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        prompt.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)prompt.transform;
        rect.localPosition = new Vector3(0f, 0.32f, 0f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.00125f;
        rect.sizeDelta = new Vector2(240f, 52f);

        Canvas canvas = prompt.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = SceneArt.ItemSortingLayer;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = prompt.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Text), typeof(Outline));
        labelObject.transform.SetParent(prompt.transform, false);
        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = "按F打开";
        label.fontSize = 30;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        Outline outline = labelObject.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        prompt.SetActive(false);
        return prompt;
    }
}
#endif
