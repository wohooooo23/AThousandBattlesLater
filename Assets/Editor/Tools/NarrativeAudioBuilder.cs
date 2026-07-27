#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors reusable dialogue/BGM prefabs and saves their instances into gameplay scenes.</summary>
public static class NarrativeAudioBuilder
{
    private const float DialogueOverlayScale = 0.58f;
    private const string DialoguePrefabPath = "Assets/Prefab/WorldDialogueBubble.prefab";
    private const string BgmPrefabPath = "Assets/Prefab/BgmPlayer.prefab";
    private const string BossBgmPath = "Assets/Audio/SFX/monume-tension-tension-music-547908.mp3";
    private const string DialogueFontPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private static readonly string[] GameplayScenePaths =
    {
        "Assets/Scenes/stage1.unity",
        "Assets/Scenes/stage1_full.unity",
        "Assets/Scenes/stage1 boss.unity"
    };

    [MenuItem("Tools/Narrative & Audio/Repair Dialogue and BGM")]
    public static void RepairAll()
    {
        EnsurePrefabs();
        foreach (string scenePath in GameplayScenePaths)
        {
            if (!File.Exists(scenePath))
                continue;
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            InstallIntoScene(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new InvalidOperationException("Failed to save " + scenePath);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        // Full campaign chapters own newer, scene-specific dialogue and illustrated cutscenes.
        // Re-apply them after the legacy dialogue/BGM repair so this tool cannot restore old text.
        StoryChapterBuilder.Build();
        ValidateAll();
        Debug.Log("NARRATIVE_AUDIO_REPAIR_OK: world dialogue and scene-authored BGM players saved in gameplay scenes.");
    }

    public static void InstallIntoActiveScene()
    {
        EnsurePrefabs();
        Scene scene = SceneManager.GetActiveScene();
        InstallIntoScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Tools/Narrative & Audio/Validate Dialogue and BGM")]
    public static void ValidateAll()
    {
        EnsurePrefabs();
        foreach (string scenePath in GameplayScenePaths)
        {
            if (!File.Exists(scenePath))
                continue;
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            BgmPlayer[] players = FindInScene<BgmPlayer>(scene);
            if (players.Length != 1 || players[0].GetComponent<AudioSource>() == null ||
                !players[0].GetComponent<AudioSource>().loop)
                throw new InvalidOperationException(scenePath + " must contain one looping prefab-authored BGM player.");

            GameObject hero = FindRootOrChild(scene, "Hero");
            if (hero == null)
                throw new InvalidOperationException(scenePath + " is missing Hero.");
            ValidateBubble(scenePath, hero.transform);

            GameObject boss = FindRootOrChild(scene, "Enemy");
            if (boss != null && boss.GetComponent<EnemyHealth>() != null)
                ValidateBubble(scenePath, boss.transform);

            StoryDialogueController story = FindInScene<StoryDialogueController>(scene).SingleOrDefault();
            bool isBossScene = boss != null && boss.GetComponent<EnemyHealth>() != null;
            bool fullChapter = scene.name == "stage1_full";
            int openingCount = fullChapter || !isBossScene ? 5 : 0;
            int encounterCount = fullChapter || !isBossScene ? 3 : 0;
            int introductionCount = isBossScene ? 6 : 0;
            int victoryCount = fullChapter ? 7 : isBossScene ? 6 : 0;
            if (story == null || story.OpeningLineCount != openingCount ||
                story.EncounterLineCount != encounterCount ||
                story.BossIntroductionLineCount != introductionCount ||
                story.BossVictoryLineCount != victoryCount)
                throw new InvalidOperationException(scenePath + " is missing its complete translated story sequence.");
            if (!isBossScene && FindInScene<StoryEncounterTrigger2D>(scene).Length != 1)
                throw new InvalidOperationException(scenePath + " needs one saved first-encounter story trigger.");
        }
        StoryChapterBuilder.Validate();
        Debug.Log("NARRATIVE_AUDIO_VALIDATE_OK: translated story, comics, triggers, dialogue visuals and BGM components are valid.");
    }

    private static void EnsurePrefabs()
    {
        BuildDialoguePrefab();
        BuildBgmPrefab();
    }

    [MenuItem("Tools/Narrative & Audio/Rebuild Dialogue Prefab Only")]
    public static void RebuildDialoguePrefabOnly()
    {
        BuildDialoguePrefab();
        AssetDatabase.SaveAssets();
        Debug.Log("DIALOGUE_PREFAB_REBUILT_OK");
    }

    private static void BuildDialoguePrefab()
    {
        GameObject root = new GameObject("World Dialogue Bubble", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(CanvasGroup), typeof(WorldDialogueBubble));
        try
        {
            RectTransform rootRect = (RectTransform)root.transform;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = WorldDialogueBubble.HighestDialogueSortingOrder;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 128f;

            GameObject bubbleRoot = new GameObject("Bubble Root", typeof(RectTransform));
            bubbleRoot.transform.SetParent(root.transform, false);
            RectTransform bubbleRect = (RectTransform)bubbleRoot.transform;
            bubbleRect.anchorMin = bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleRect.sizeDelta = new Vector2(960f, 320f);
            bubbleRect.localScale = Vector3.one * DialogueOverlayScale;

            GameObject background = new GameObject("White Background", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            background.transform.SetParent(bubbleRoot.transform, false);
            RectTransform backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = new Vector2(0f, 0.22f);
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = Color.white;
            backgroundImage.raycastTarget = false;
            Outline outline = background.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject label = new GameObject("Dialogue Text", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            label.transform.SetParent(background.transform, false);
            Stretch((RectTransform)label.transform, new Vector2(18f, 12f), new Vector2(-18f, -12f));
            TMP_FontAsset dialogueFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DialogueFontPath);
            if (dialogueFont == null)
                throw new InvalidOperationException("TMP Essential Resources are required at " + DialogueFontPath);
            TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
            text.font = dialogueFont;
            text.fontSize = 52f;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.color = Color.black;
            text.text = "...";
            text.raycastTarget = false;

            GameObject hint = new GameObject("Enter Skip Hint", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            hint.transform.SetParent(bubbleRoot.transform, false);
            RectTransform hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = new Vector2(1f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.anchoredPosition = Vector2.zero;
            hintRect.sizeDelta = new Vector2(360f, 56f);
            Image hintImage = hint.GetComponent<Image>();
            hintImage.color = Color.white;
            hintImage.raycastTarget = false;
            Outline hintOutline = hint.GetComponent<Outline>();
            hintOutline.effectColor = Color.black;
            hintOutline.effectDistance = new Vector2(2f, -2f);

            GameObject hintLabel = new GameObject("Hint Text", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            hintLabel.transform.SetParent(hint.transform, false);
            Stretch((RectTransform)hintLabel.transform, new Vector2(6f, 2f), new Vector2(-6f, -2f));
            TextMeshProUGUI hintText = hintLabel.GetComponent<TextMeshProUGUI>();
            hintText.font = dialogueFont;
            hintText.fontSize = 32f;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = Color.black;
            hintText.text = "Press Enter to skip";
            hintText.raycastTarget = false;
            hint.SetActive(false);

            SerializedObject bubbleData = new SerializedObject(root.GetComponent<WorldDialogueBubble>());
            bubbleData.FindProperty("bubbleRoot").objectReferenceValue = bubbleRect;
            bubbleData.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            bubbleData.FindProperty("dialogueText").objectReferenceValue = text;
            bubbleData.FindProperty("skipHintRoot").objectReferenceValue = hint;
            bubbleData.FindProperty("initialText").stringValue = "...";
            bubbleData.FindProperty("visibleOnAwake").boolValue = false;
            bubbleData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, DialoguePrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void BuildBgmPrefab()
    {
        // A designer may assign the final AudioClip or Resources path directly on this prefab.
        // Once it exists, never recreate it during a repair pass and wipe that authored choice.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BgmPrefabPath) != null)
            return;

        GameObject root = new GameObject("BGM Player", typeof(AudioSource), typeof(BgmPlayer));
        try
        {
            AudioSource source = root.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0.65f;
            PrefabUtility.SaveAsPrefabAsset(root, BgmPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void InstallIntoScene(Scene scene)
    {
        RemoveNamedRoot(scene, "Dialogue Bubbles");
        RemoveNamedRoot(scene, "BGM Player");
        RemoveNamedRoot(scene, "Story System");

        GameObject bgmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BgmPrefabPath);
        GameObject bgm = (GameObject)PrefabUtility.InstantiatePrefab(bgmPrefab, scene);
        bgm.name = "BGM Player";
        ConfigureSceneBgm(scene, bgm.GetComponent<BgmPlayer>());

        GameObject dialogueRoot = new GameObject("Dialogue Bubbles");
        SceneManager.MoveGameObjectToScene(dialogueRoot, scene);
        GameObject hero = FindRootOrChild(scene, "Hero");
        WorldDialogueBubble heroBubble = null;
        if (hero != null)
            heroBubble = CreateBubble(dialogueRoot.transform, hero.transform, "Hero Dialogue", CalculateOffset(hero.transform));

        GameObject boss = FindRootOrChild(scene, "Enemy");
        WorldDialogueBubble bossBubble = null;
        if (boss != null && boss.GetComponent<EnemyHealth>() != null)
            bossBubble = CreateBubble(dialogueRoot.transform, boss.transform, "Boss Dialogue", CalculateOffset(boss.transform));

        SetupStorySystem(scene, hero, boss, heroBubble, bossBubble);

        BossArenaController arena = FindInScene<BossArenaController>(scene).FirstOrDefault();
        if (arena != null)
        {
            SerializedObject arenaData = new SerializedObject(arena);
            arenaData.FindProperty("bgmPlayer").objectReferenceValue = bgm.GetComponent<BgmPlayer>();
            arenaData.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureSceneBgm(Scene scene, BgmPlayer player)
    {
        AudioClip tensionTrack = AssetDatabase.LoadAssetAtPath<AudioClip>(BossBgmPath);
        if (tensionTrack == null)
            throw new InvalidOperationException("Missing Boss BGM at " + BossBgmPath);

        bool combinedStage = scene.name == "stage1_full";
        bool bossOnlyStage = scene.name == "stage1 boss";
        SerializedObject data = new SerializedObject(player);
        data.FindProperty("explorationClip").objectReferenceValue = combinedStage || bossOnlyStage ? null : tensionTrack;
        data.FindProperty("explorationResourcesPath").stringValue = string.Empty;
        data.FindProperty("bossClip").objectReferenceValue = combinedStage || bossOnlyStage ? tensionTrack : null;
        data.FindProperty("bossResourcesPath").stringValue = string.Empty;
        data.FindProperty("startingTrack").enumValueIndex = bossOnlyStage ? (int)BgmTrack.Boss : (int)BgmTrack.Exploration;
        data.FindProperty("persistAcrossScenes").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static WorldDialogueBubble CreateBubble(Transform parent, Transform target, string name, Vector3 offset)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePrefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = name;
        SerializedObject data = new SerializedObject(instance.GetComponent<WorldDialogueBubble>());
        data.FindProperty("followTarget").objectReferenceValue = target;
        data.FindProperty("followOffset").vector3Value = offset;
        data.FindProperty("initialText").stringValue = "...";
        data.FindProperty("visibleOnAwake").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        return instance.GetComponent<WorldDialogueBubble>();
    }

    private static void SetupStorySystem(Scene scene, GameObject hero, GameObject boss,
        WorldDialogueBubble heroBubble, WorldDialogueBubble bossBubble)
    {
        bool bossScene = boss != null && boss.GetComponent<EnemyHealth>() != null;
        GameObject root = new GameObject("Story System", typeof(StoryDialogueController));
        SceneManager.MoveGameObjectToScene(root, scene);
        CanvasGroup fade = CreateFadeOverlay(root.transform, bossScene ? 0f : 1f);
        GameObject victoryOverlay = bossScene ? FindRootOrChild(scene, "Victory Overlay") : null;

        StoryDialogueController story = root.GetComponent<StoryDialogueController>();
        SerializedObject storyData = new SerializedObject(story);
        storyData.FindProperty("sceneMode").enumValueIndex = bossScene
            ? (int)StorySceneMode.Boss
            : (int)StorySceneMode.Exploration;
        storyData.FindProperty("heroBubble").objectReferenceValue = heroBubble;
        storyData.FindProperty("bossBubble").objectReferenceValue = bossBubble;
        storyData.FindProperty("bossVisualRoot").objectReferenceValue = boss != null ? boss.transform : null;
        storyData.FindProperty("fadeOverlay").objectReferenceValue = fade;
        storyData.FindProperty("victoryOverlay").objectReferenceValue = victoryOverlay;
        SetStoryLines(storyData.FindProperty("openingLines"), bossScene ? Array.Empty<(StorySpeaker, string)>() : OpeningLines());
        SetStoryLines(storyData.FindProperty("firstEncounterLines"), bossScene ? Array.Empty<(StorySpeaker, string)>() : EncounterLines());
        SetStoryLines(storyData.FindProperty("bossIntroductionLines"), bossScene ? BossIntroductionLines() : Array.Empty<(StorySpeaker, string)>());
        SetStoryLines(storyData.FindProperty("bossVictoryLines"), bossScene ? BossVictoryLines() : Array.Empty<(StorySpeaker, string)>());
        storyData.ApplyModifiedPropertiesWithoutUndo();

        if (bossScene)
        {
            SerializedObject healthData = new SerializedObject(boss.GetComponent<EnemyHealth>());
            healthData.FindProperty("storyController").objectReferenceValue = story;
            healthData.ApplyModifiedPropertiesWithoutUndo();
            BossHealthBarController bossBar = FindInScene<BossHealthBarController>(scene).SingleOrDefault();
            if (bossBar != null)
            {
                bossBar.ConfigureStory(story);
                EditorUtility.SetDirty(bossBar);
            }
        }
        else
        {
            GameObject triggerObject = new GameObject("First Encounter Story Trigger",
                typeof(BoxCollider2D), typeof(StoryEncounterTrigger2D));
            triggerObject.transform.SetParent(root.transform, false);
            triggerObject.transform.position = hero.transform.position + new Vector3(18f, 0f, 0f);
            BoxCollider2D trigger = triggerObject.GetComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(4f, 36f);
            SerializedObject triggerData = new SerializedObject(triggerObject.GetComponent<StoryEncounterTrigger2D>());
            triggerData.FindProperty("storyController").objectReferenceValue = story;
            triggerData.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (AbilityUnlockOrb2D orb in FindInScene<AbilityUnlockOrb2D>(scene))
            SetStoryReference(orb, story);
        foreach (DashUnlockOrb orb in FindInScene<DashUnlockOrb>(scene))
            SetStoryReference(orb, story);
    }

    private static CanvasGroup CreateFadeOverlay(Transform parent, float alpha)
    {
        GameObject canvasObject = new GameObject("Story Fade Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("Black Fade", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(canvasObject.transform, false);
        Stretch((RectTransform)panel.transform, Vector2.zero, Vector2.zero);
        Image image = panel.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        group.alpha = alpha;
        group.blocksRaycasts = false;
        group.interactable = false;
        return group;
    }

    private static void SetStoryReference(Component target, StoryDialogueController story)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty("storyController");
        if (property == null)
            throw new InvalidOperationException(target.name + " has no storyController field.");
        property.objectReferenceValue = story;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStoryLines(SerializedProperty property, (StorySpeaker speaker, string text)[] lines)
    {
        property.arraySize = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            SerializedProperty entry = property.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("speaker").enumValueIndex = (int)lines[i].speaker;
            entry.FindPropertyRelative("text").stringValue = lines[i].text;
        }
    }

    private static (StorySpeaker, string)[] OpeningLines() => new[]
    {
        (StorySpeaker.Samurai, "Decades have passed... and now I have returned."),
        (StorySpeaker.Samurai, "Since that day, I have lost count of the battles I have fought."),
        (StorySpeaker.Samurai, "Even on the quietest nights, I have never known a moment's rest."),
        (StorySpeaker.Samurai, "Today, I have finally made my choice."),
        (StorySpeaker.Samurai, "I will put the past to rest.")
    };

    private static (StorySpeaker, string)[] EncounterLines() => new[]
    {
        (StorySpeaker.Samurai, "These monsters again? How familiar."),
        (StorySpeaker.Samurai, "Time has rusted my blade—and weathered its wielder."),
        (StorySpeaker.Samurai, "I should find equipment worthy of the road ahead.")
    };

    private static (StorySpeaker, string)[] BossIntroductionLines() => new[]
    {
        (StorySpeaker.EvilWizard, "You...?"),
        (StorySpeaker.Samurai, "Me. I have come to settle an old debt."),
        (StorySpeaker.EvilWizard, "So you finally came. You never could let go of what happened."),
        (StorySpeaker.EvilWizard, "Your lord met a truly wretched end."),
        (StorySpeaker.Samurai, "Do not dare speak of him!"),
        (StorySpeaker.EvilWizard, "Ha! Whether I have the right is yours to prove in battle!")
    };

    private static (StorySpeaker, string)[] BossVictoryLines() => new[]
    {
        (StorySpeaker.EvilWizard, "You have grown stronger. All those years of battle..."),
        (StorySpeaker.Samurai, "You did not kill my lord! Who are you?"),
        (StorySpeaker.EvilWizard, "...and wiser, too. Your eyes have sharpened."),
        (StorySpeaker.EvilWizard, "You are right. It was not me. Take the crimson rune you found and seek the truth."),
        (StorySpeaker.Samurai, "..."),
        (StorySpeaker.Samurai, "Then today, at last, the truth will be revealed.")
    };

    private static Vector3 CalculateOffset(Transform target)
    {
        float top = target.position.y + 3f;
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            top = Mathf.Max(top, renderer.bounds.max.y);
        foreach (Collider2D collider in target.GetComponentsInChildren<Collider2D>(true))
            top = Mathf.Max(top, collider.bounds.max.y);
        return new Vector3(0f, top - target.position.y + 7.5f, 0f);
    }

    private static void ValidateBubble(string scenePath, Transform target)
    {
        WorldDialogueBubble bubble = FindInScene<WorldDialogueBubble>(target.gameObject.scene)
            .FirstOrDefault(candidate => candidate.FollowTarget == target);
        Canvas canvas = bubble != null ? bubble.GetComponent<Canvas>() : null;
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay ||
            canvas.sortingOrder != WorldDialogueBubble.HighestDialogueSortingOrder)
            throw new InvalidOperationException(scenePath + " needs a highest-order overlay dialogue bubble for " + target.name + ".");
        RectTransform layout = bubble.transform.Find("Bubble Root") as RectTransform;
        Image background = layout?.Find("White Background")?.GetComponent<Image>();
        TMP_Text text = layout?.Find("White Background/Dialogue Text")?.GetComponent<TMP_Text>();
        TMP_Text hint = layout?.Find("Enter Skip Hint/Hint Text")?.GetComponent<TMP_Text>();
        if (background == null || background.color != Color.white || text == null || text.color != Color.black ||
            text.font == null || !text.font.name.Contains("LiberationSans") || text.fontSize < 52f ||
            layout == null || layout.sizeDelta.x < 960f ||
            Mathf.Abs(layout.localScale.x - DialogueOverlayScale) > 0.0001f ||
            hint == null || hint.text != "Press Enter to skip" ||
            bubble.GetComponent<CanvasScaler>().dynamicPixelsPerUnit < 128f)
            throw new InvalidOperationException(scenePath + " dialogue must use a white background and black text.");
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }

    private static GameObject FindRootOrChild(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
            Transform child = FindDeepChild(root.transform, name);
            if (child != null)
                return child.gameObject;
        }
        return null;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            Transform nested = FindDeepChild(child, name);
            if (nested != null)
                return nested;
        }
        return null;
    }

    private static void RemoveNamedRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name)
                UnityEngine.Object.DestroyImmediate(root);
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
#endif
