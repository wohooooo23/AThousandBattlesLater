#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places the two "press F to open" prompt triggers, one beside each of the lower reward chests.
///
/// Deliberately additive, like StartMenuSettingsBuilder: it only adds what is missing and moves the
/// triggers onto their chests, so hand-authored level content is never destroyed. Idempotent —
/// running it twice changes nothing. Re-run it after a full-map rebuild.
///
/// Each trigger carries its own StoryBeat, so the prompt appears once at each chest rather than
/// only at whichever the player reaches first.
/// </summary>
public static class ChestHintTriggerBuilder
{
    private const string ScenePath = "Assets/Scenes/stage1_full.unity";
    private const string RootName = "Chest Hint Triggers";
    private const string CombatPrompt = "Press J to attack. Press I to throw a kunai.";
    private const string ChestPrompt = "Press F to open treasure chests.";
    private const string EquipmentPrompt = "Press B to open the backpack, N to open the forge.";
    private static readonly Vector2 TriggerSize = new Vector2(36f, 24f);
    private static readonly Vector3 TriggerOffset = new Vector3(0f, 6f, 0f);

    private static readonly (string ChestName, string TriggerName, StoryBeat Beat)[] Hints =
    {
        ("Double Jump Treasure Chest", "Double Jump Chest Hint Trigger", StoryBeat.ChestHintDoubleJump),
        ("Dash Treasure Chest", "Dash Chest Hint Trigger", StoryBeat.ChestHintDash)
    };

    [MenuItem("Tools/Story/Build Chest Hint Triggers")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        StoryDialogueController story = UnityEngine.Object.FindFirstObjectByType<StoryDialogueController>(FindObjectsInactive.Include);
        if (story == null)
            throw new InvalidOperationException(ScenePath + " is missing StoryDialogueController.");

        ApplyGuidancePrompts(story);

        Transform root = FindOrCreateRoot();
        foreach ((string chestName, string triggerName, StoryBeat beat) in Hints)
        {
            TreasureChest2D chest = FindChest(chestName);
            if (chest == null)
                throw new InvalidOperationException(ScenePath + " is missing " + chestName + ".");
            EnsureTrigger(root, triggerName, beat, story, chest.transform.position + TriggerOffset);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Failed to save " + ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("CHEST_HINT_TRIGGERS_OK: both chest prompt triggers placed; existing content preserved.");
    }

    [MenuItem("Tools/Story/Validate Chest Hint Triggers")]
    public static void Validate()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        StoryDialogueController story = UnityEngine.Object.FindFirstObjectByType<StoryDialogueController>(FindObjectsInactive.Include);
        if (story == null)
            throw new InvalidOperationException(ScenePath + " is missing StoryDialogueController.");
        SerializedObject data = new SerializedObject(story);
        if (data.FindProperty("combatPrompt").stringValue != CombatPrompt ||
            data.FindProperty("chestPrompt").stringValue != ChestPrompt ||
            data.FindProperty("equipmentPrompt").stringValue != EquipmentPrompt)
            throw new InvalidOperationException("The guidance prompts in the scene are out of date; run the build step.");

        StoryPromptTrigger2D[] triggers = UnityEngine.Object
            .FindObjectsByType<StoryPromptTrigger2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach ((string chestName, string triggerName, StoryBeat beat) in Hints)
        {
            StoryPromptTrigger2D trigger = triggers.FirstOrDefault(candidate => candidate.name == triggerName);
            if (trigger == null || trigger.StoryController == null)
                throw new InvalidOperationException(triggerName + " is missing or unwired.");
            if (trigger.Beat != beat)
                throw new InvalidOperationException(triggerName + " carries the wrong story beat.");

            TreasureChest2D chest = FindChest(chestName);
            if (chest == null)
                throw new InvalidOperationException(ScenePath + " is missing " + chestName + ".");
            Vector3 expected = chest.transform.position + TriggerOffset;
            if (Vector3.Distance(trigger.transform.position, expected) > 0.01f)
                throw new InvalidOperationException(triggerName + " has drifted away from " + chestName + ".");
        }
        Debug.Log("CHEST_HINT_TRIGGERS_VALIDATE_OK.");
    }

    /// <summary>
    /// The prompts were serialised into the scene before they were reworded, and a saved value wins
    /// over the field's default, so the canonical English is written back here. It doubles as the
    /// translation key, so a stale string would silently show English in Chinese.
    /// </summary>
    private static void ApplyGuidancePrompts(StoryDialogueController story)
    {
        SerializedObject data = new SerializedObject(story);
        data.FindProperty("combatPrompt").stringValue = CombatPrompt;
        data.FindProperty("chestPrompt").stringValue = ChestPrompt;
        data.FindProperty("equipmentPrompt").stringValue = EquipmentPrompt;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(story);
    }

    private static TreasureChest2D FindChest(string chestName)
    {
        return UnityEngine.Object
            .FindObjectsByType<TreasureChest2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(chest => chest.name == chestName);
    }

    private static Transform FindOrCreateRoot()
    {
        GameObject existing = GameObject.Find(RootName);
        return existing != null ? existing.transform : new GameObject(RootName).transform;
    }

    private static void EnsureTrigger(Transform root, string name, StoryBeat beat,
        StoryDialogueController story, Vector3 position)
    {
        Transform existing = root.Find(name);
        GameObject trigger = existing != null ? existing.gameObject : new GameObject(name);
        trigger.transform.SetParent(root, false);
        trigger.transform.position = position;

        BoxCollider2D box = trigger.GetComponent<BoxCollider2D>();
        if (box == null)
            box = trigger.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = TriggerSize;
        box.offset = Vector2.zero;
        box.enabled = true;

        StoryPromptTrigger2D prompt = trigger.GetComponent<StoryPromptTrigger2D>();
        if (prompt == null)
            prompt = trigger.AddComponent<StoryPromptTrigger2D>();

        SerializedObject data = new SerializedObject(prompt);
        data.FindProperty("storyController").objectReferenceValue = story;
        data.FindProperty("beat").enumValueIndex = (int)beat;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(prompt);
    }
}
#endif
