#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Read-only checks for the two campaign Boss Animator bindings and legacy scene loading.</summary>
public static class BossAnimatorValidation
{
    [MenuItem("Tools/Boss/Validate Campaign Animators")]
    public static void Validate()
    {
        ValidatePrefab();
        ValidateStage("Assets/Scenes/stage1_full.unity", "Assets/Animations/Boss/Wizard.controller");
        ValidateStage("Assets/Scenes/stage2_full.unity", "Assets/Animations/Boss/King.controller");
        Scene legacy = EditorSceneManager.OpenScene("Assets/Scenes/Legacy/stage1 boss.unity", OpenSceneMode.Single);
        if (!legacy.isLoaded || legacy.rootCount == 0)
            throw new InvalidOperationException("Legacy stage1 boss scene cannot be loaded.");
        Debug.Log("BOSS_ANIMATOR_VALIDATE_OK: Wizard prefab, both campaign arenas and legacy scene loaded.");
    }

    private static void ValidatePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Enemy/Bosses/EvilWizard/Boss_EvilWizard.prefab");
        ValidateBoss(prefab, "Assets/Animations/Boss/Wizard.controller");
    }

    private static void ValidateStage(string scenePath, string controllerPath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        BossArenaController arena = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<BossArenaController>(true)).Single();
        ValidateBoss(arena.BossRoot, controllerPath);
    }

    private static void ValidateBoss(GameObject boss, string controllerPath)
    {
        if (boss == null)
            throw new InvalidOperationException("Missing Boss root for " + controllerPath);
        BossStateMachine machine = boss.GetComponent<BossStateMachine>();
        Animator animator = boss.GetComponentInChildren<Animator>(true);
        SpriteRenderer renderer = animator != null ? animator.GetComponent<SpriteRenderer>() : null;
        if (machine == null || animator == null || renderer == null ||
            machine.VisualAnimator != animator ||
            AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) != controllerPath ||
            boss.GetComponentInChildren<BossSpriteAnimator>(true) != null)
            throw new InvalidOperationException(boss.name + " has an incomplete Animator migration.");

        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        string[] states = controller.layers[0].stateMachine.states
            .Select(child => child.state.name).OrderBy(name => name).ToArray();
        string[] required = { "Attack1", "Attack2", "Attack3", "Death", "Hurt", "Idle", "Run" };
        if (!states.SequenceEqual(required) || controller.layers[0].stateMachine.states
                .Any(child => child.state.motion == null))
            throw new InvalidOperationException(controllerPath + " is missing an animation state or clip.");
    }
}
#endif
