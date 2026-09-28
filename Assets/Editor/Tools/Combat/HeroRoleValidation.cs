#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Checks the consolidated Hero and preserved scene bindings without saving assets.</summary>
public static class HeroRoleValidation
{
    [MenuItem("Tools/Hero/Validate Role Components")]
    public static void Validate()
    {
        ValidateHero(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Hero.prefab"), false);
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string path in new[] {
                "Assets/Scenes/stage1_full.unity", "Assets/Scenes/stage2_full.unity",
                "Assets/Scenes/Legacy/stage1.unity", "Assets/Scenes/Legacy/stage1 boss.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Role role = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Role>(true)).Single();
                ValidateHero(role.gameObject, true);
                PlayerProgression progression = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PlayerProgression>(true)).Single();
                if (new SerializedObject(progression).FindProperty("playerCombat").objectReferenceValue != role)
                    throw new InvalidOperationException(path + " progression does not reference Role.");
                Debug.Log("HERO_ROLE_SCENE_OK: " + path + "; HP=" + role.MaximumHealth + "; melee radius=" + role.AttackRadius);
            }
        }
        finally
        {
            if (previous.Length > 0 && previous.Any(setup => setup.isLoaded && setup.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
        Debug.Log("HERO_ROLE_VALIDATE_OK: prefab, both campaign stages and both legacy Hero scenes.");
    }

    private static void ValidateHero(GameObject hero, bool requireHud)
    {
        if (hero == null || hero.GetComponents<Role>().Length != 1 || hero.GetComponent<Entity_Combat>() != null ||
            hero.GetComponent<CombatHealth>() != null)
            throw new InvalidOperationException("Hero must have one Role and no separate combat/health component.");
        foreach (Transform child in hero.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                throw new InvalidOperationException("Hero has a missing script: " + child.name);
        Role role = hero.GetComponent<Role>();
        SerializedObject data = new SerializedObject(role);
        foreach (string field in new[] { "targetCheck", "kunaiItem", "projectilePrefab" })
            if (data.FindProperty(field).objectReferenceValue == null)
                throw new InvalidOperationException("Role is missing " + field);
        GameObject projectile = (GameObject)data.FindProperty("projectilePrefab").objectReferenceValue;
        if (role.MaximumHealth <= 0f || role.AttackRadius <= 0f || projectile.GetComponent<FlyingEyeProjectile2D>() == null ||
            data.FindProperty("projectileSpeed").floatValue <= 0f || data.FindProperty("kunaiDamage").floatValue < 0f)
            throw new InvalidOperationException("Role has invalid health, melee or projectile settings.");
        if (requireHud && (data.FindProperty("healthBar").objectReferenceValue == null ||
            data.FindProperty("defeatedOverlay").objectReferenceValue == null))
            throw new InvalidOperationException("Role is missing scene HUD references.");
    }
}
#endif
