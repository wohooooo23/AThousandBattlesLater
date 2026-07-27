#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finalises the imported Orc model on Enemy_Orc.prefab: removes the green-circle placeholder child
/// (the animated sprite renderer is the only model) and switches its Entity_Combat to the forward-fan
/// (sector) attack with a matching fan-shaped warning. Idempotent.
/// </summary>
public static class OrcModelBuilder
{
    private const string OrcPrefabPath = "Assets/Enemy/Mobs/Orc/Mob_Orc.prefab";

    [MenuItem("Tools/Enemy/Apply Orc Model And Fan Attack")]
    public static void ApplyOrcModel()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(OrcPrefabPath);
        try
        {
            Transform placeholder = FindDeep(root.transform, "Green Circle Model");
            if (placeholder != null)
                Object.DestroyImmediate(placeholder.gameObject);

            // The green circle was the only rendering placeholder; the animated Orc SpriteRenderer
            // was left disabled. Enable it so the imported Orc model actually shows. The package's
            // material is missing from this project (a broken material draws the sprite's
            // transparent pixels as black quads), so fall back to the built-in sprite material.
            Material spriteDefault = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.enabled = true;
                if (spriteDefault != null && renderer.sharedMaterial == null)
                    renderer.sharedMaterial = spriteDefault;
            }

            Entity_Combat combat = root.GetComponent<Entity_Combat>();
            Enemy controller = root.GetComponent<Enemy>();
            if (combat == null || controller == null)
                throw new MissingReferenceException("Enemy_Orc.prefab is missing Entity_Combat or Enemy.");

            SerializedObject serialized = new SerializedObject(combat);
            serialized.FindProperty("attackMode").enumValueIndex = (int)EntityAttackMode.ForwardFan;
            SetFloat(serialized, "fanRadius", 7f);
            SetFloat(serialized, "fanHalfAngle", 45f);
            SetFloat(serialized, "fanWarningDuration", 0.95f);
            SetFloat(serialized, "fanStrikeDuration", 0.22f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject controllerData = new SerializedObject(controller);
            SetFloat(controllerData, "attackInterval", 1.35f);
            controllerData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, OrcPrefabPath);
            Debug.Log("<color=lime>ORC_MODEL_OK: green-circle placeholder removed; Orc now uses the forward-fan attack.</color>");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetFloat(SerializedObject serialized, string field, float value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property != null)
            property.floatValue = value;
    }

    private static Transform FindDeep(Transform root, string childName)
    {
        if (root.name == childName)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }
}
#endif
