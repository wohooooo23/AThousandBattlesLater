#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors the two-chapter English story and its three panel-by-panel comics.</summary>
public static class StoryChapterBuilder
{
    private const string Stage1Path = "Assets/Scenes/stage1_full.unity";
    private const string Stage2Path = "Assets/Scenes/stage2_full.unity";
    private const string ComicPrefabPath = "Assets/Prefab/StoryComicPanel.prefab";
    private const string DialoguePrefabPath = "Assets/Prefab/WorldDialogueBubble.prefab";
    private const string WizardPrefabPath = "Assets/Enemy/Bosses/EvilWizard/Boss_EvilWizard.prefab";
    private const string OrcPrefabPath = "Assets/Enemy/Mobs/Orc/Mob_Orc.prefab";
    private const string ProloguePath = "Assets/Resources/Story/Comic_Prologue.png";
    private const string BetrayalPath = "Assets/Resources/Story/Comic_Betrayal.png";
    private const string EpiloguePath = "Assets/Resources/Story/Comic_Epilogue.png";
    private const string Stage2EndScreenText =
        "VICTORY\nPress Space for Main Menu\n\nTEAM CONTRIBUTIONS\n" +
        "路子轩 · ZJU — Map Design / Art / Code\n" +
        "卢敏察 · ZJU — Assets / Art\n" +
        "孟祥铭 · SJTU — Code";
    private const string FontPath = "Assets/Resources/Fonts/BoldPixels SDF.asset";
    private const string ComicObjectName = "Story Comic Panel";
    private const string Stage2CastName = "Boss Introduction Cast";
    private const string WizardActorName = "Story Evil Wizard Idle_0";
    private const string OrcActorName = "Boss Companion Orc";

    [MenuItem("Tools/Narrative & Audio/Build Two-Chapter Story")]
    public static void Build()
    {
        ConfigureComicTexture(ProloguePath);
        ConfigureComicTexture(BetrayalPath);
        ConfigureComicTexture(EpiloguePath);
        BuildComicPrefab();
        ConfigureStage1();
        ConfigureStage2();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("STORY_CHAPTER_BUILD_OK: two-stage dialogue, three comics and final team credits are saved.");
    }

    [MenuItem("Tools/Narrative & Audio/Build Stage2 Boss Cast and Localization")]
    public static void BuildStage2BossCast()
    {
        ConfigureStage2();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("STAGE2_BOSS_CAST_BUILD_OK: Wizard, story-only Orc, speaker bubbles and King-only victory are saved.");
    }

    [MenuItem("Tools/Narrative & Audio/Validate Two-Chapter Story")]
    public static void Validate()
    {
        ValidateComicTexture(ProloguePath);
        ValidateComicTexture(BetrayalPath);
        ValidateComicTexture(EpiloguePath);
        ValidateStage(Stage1Path, StoryBeat.Opening, StoryBeat.BossIntroduction,
            5, 3, 6, 7, true, false, false);
        ValidateStage(Stage2Path, StoryBeat.Stage2Opening, StoryBeat.Stage2BossIntroduction,
            4, 0, 20, 3, false, true, true);
        ValidateTranslations();
        ValidateStage2BossCast();
        Debug.Log("STORY_CHAPTER_VALIDATE_OK: story text, comic insertion points and saved UI references are valid.");
    }

    private static void ConfigureStage1()
    {
        StoryDialogueController story = OpenStory(Stage1Path, out Scene scene);
        story.gameObject.SetActive(true);
        EditorUtility.SetDirty(story.gameObject);
        StoryComicPanel panel = EnsureComicPanel(scene);
        SerializedObject data = new SerializedObject(story);
        SetObject(data, "comicPanel", panel);
        SetObject(data, "openingComic", AssetDatabase.LoadAssetAtPath<Texture2D>(ProloguePath));
        SetObject(data, "bossIntroductionComic", null);
        SetObject(data, "endingComic", null);
        data.FindProperty("bossIntroductionComicAfterLine").intValue = -1;
        data.FindProperty("openingProgressBeat").enumValueIndex = (int)StoryBeat.Opening;
        data.FindProperty("bossIntroductionProgressBeat").enumValueIndex =
            (int)StoryBeat.BossIntroduction;
        data.FindProperty("keepLastVictoryLineVisible").boolValue = true;
        SetLines(data.FindProperty("openingLines"), Stage1Opening());
        SetLines(data.FindProperty("firstEncounterLines"), Stage1Encounter());
        SetLines(data.FindProperty("bossIntroductionLines"), Stage1BossIntroduction());
        SetLines(data.FindProperty("bossVictoryLines"), Stage1BossVictory());
        data.ApplyModifiedPropertiesWithoutUndo();
        Save(scene, Stage1Path);
    }

    private static void ConfigureStage2()
    {
        StoryDialogueController story = OpenStory(Stage2Path, out Scene scene);
        story.gameObject.SetActive(true);
        EditorUtility.SetDirty(story.gameObject);
        StoryComicPanel panel = EnsureComicPanel(scene);
        SerializedObject data = new SerializedObject(story);
        SetObject(data, "comicPanel", panel);
        SetObject(data, "openingComic", null);
        SetObject(data, "bossIntroductionComic", AssetDatabase.LoadAssetAtPath<Texture2D>(BetrayalPath));
        SetObject(data, "endingComic", AssetDatabase.LoadAssetAtPath<Texture2D>(EpiloguePath));
        data.FindProperty("endingFadeToBlackDuration").floatValue = 1.15f;
        data.FindProperty("bossIntroductionComicAfterLine").intValue = 6;
        data.FindProperty("openingProgressBeat").enumValueIndex = (int)StoryBeat.Stage2Opening;
        data.FindProperty("bossIntroductionProgressBeat").enumValueIndex =
            (int)StoryBeat.Stage2BossIntroduction;
        data.FindProperty("keepLastVictoryLineVisible").boolValue = false;
        SetLines(data.FindProperty("openingLines"), Stage2Opening());
        SetLines(data.FindProperty("firstEncounterLines"), Array.Empty<(StorySpeaker, string)>());
        SetLines(data.FindProperty("bossIntroductionLines"), Stage2BossIntroduction());
        SetLines(data.FindProperty("bossVictoryLines"), Stage2BossVictory());
        ConfigureStage2BossCast(scene, story, data);
        ConfigureStage2EndScreen(scene);
        data.ApplyModifiedPropertiesWithoutUndo();
        Save(scene, Stage2Path);
    }

    private static void ConfigureStage2BossCast(Scene scene, StoryDialogueController story,
        SerializedObject storyData)
    {
        Transform oldCast = FindInScene<Transform>(scene).FirstOrDefault(candidate =>
            candidate.parent == null && candidate.name == Stage2CastName);
        if (oldCast != null)
            UnityEngine.Object.DestroyImmediate(oldCast.gameObject);

        EnemyHealth boss = FindInScene<EnemyHealth>(scene).SingleOrDefault() ??
            throw new MissingReferenceException("stage2 requires exactly one King EnemyHealth.");
        Collider2D bossCollider = boss.GetComponent<Collider2D>();
        float groundY = bossCollider != null ? bossCollider.bounds.min.y : boss.transform.position.y;

        GameObject castRoot = new GameObject(Stage2CastName);
        SceneManager.MoveGameObjectToScene(castRoot, scene);

        GameObject wizard = CreateWizardStoryActor(castRoot.transform, boss.transform.position, groundY);
        GameObject orc = CreateCompanionOrc(scene, castRoot.transform, boss.transform.position, groundY,
            out _);
        WorldDialogueBubble wizardBubble = CreateStoryBubble(scene, wizard.transform,
            "Wizard Story Dialogue", new Vector3(0f, 12f, 0f));
        WorldDialogueBubble orcBubble = CreateStoryBubble(scene, orc.transform,
            "Orc Story Dialogue", new Vector3(0f, 9f, 0f));

        SetSpeakerBindings(storyData.FindProperty("additionalSpeakerBubbles"),
            (StorySpeaker.EvilWizard, wizardBubble), (StorySpeaker.Monster, orcBubble));
        SetActorCues(storyData.FindProperty("bossIntroductionActorCues"),
            (3, wizard, true), (11, wizard, false), (11, orc, true));
        SetObjectArray(storyData.FindProperty("actorsHiddenAfterBossIntroduction"), wizard, orc);
        SetObjectArray(storyData.FindProperty("actorsActiveAfterBossIntroduction"));

        SerializedObject bossData = new SerializedObject(boss);
        SetObject(bossData, "victoryObjective", null);
        bossData.ApplyModifiedPropertiesWithoutUndo();

        wizard.SetActive(false);
        orc.SetActive(false);
        EditorUtility.SetDirty(story);
    }

    private static GameObject CreateWizardStoryActor(Transform parent, Vector3 bossPosition, float groundY)
    {
        GameObject wizardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WizardPrefabPath) ??
            throw new MissingReferenceException("Missing " + WizardPrefabPath);
        BossSpriteAnimator sourceAnimator = wizardPrefab.GetComponentInChildren<BossSpriteAnimator>(true);
        Sprite idle0 = sourceAnimator != null && sourceAnimator.idle.frames.Length > 0
            ? sourceAnimator.idle.frames[0]
            : null;
        if (idle0 == null)
            throw new MissingReferenceException("The Evil Wizard prefab has no Idle_0 sprite.");

        GameObject wizard = new GameObject(WizardActorName, typeof(SpriteRenderer));
        wizard.transform.SetParent(parent, false);
        SpriteRenderer renderer = wizard.GetComponent<SpriteRenderer>();
        renderer.sprite = idle0;
        renderer.sortingLayerName = SceneArt.EffectSortingLayer;
        renderer.sortingOrder = 10;
        float scale = 10f / Mathf.Max(0.01f, idle0.bounds.size.y);
        wizard.transform.localScale = Vector3.one * scale;
        wizard.transform.position = new Vector3(bossPosition.x - 18f,
            groundY - idle0.bounds.min.y * scale, bossPosition.z);
        return wizard;
    }

    private static GameObject CreateCompanionOrc(Scene scene, Transform parent, Vector3 bossPosition,
        float groundY, out Enemy_Health health)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrcPrefabPath) ??
            throw new MissingReferenceException("Missing " + OrcPrefabPath);
        GameObject orc = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        orc.name = OrcActorName;
        orc.transform.SetParent(parent, true);
        orc.transform.position = new Vector3(bossPosition.x + 14f, bossPosition.y, bossPosition.z);
        Physics2D.SyncTransforms();
        Collider2D collider = orc.GetComponent<Collider2D>();
        if (collider != null)
            orc.transform.position += Vector3.up * (groundY - collider.bounds.min.y);
        health = orc.GetComponent<Enemy_Health>() ??
            throw new MissingReferenceException("The companion Orc prefab has no Enemy_Health.");
        return orc;
    }

    private static WorldDialogueBubble CreateStoryBubble(Scene scene, Transform target, string name,
        Vector3 offset)
    {
        Transform dialogueRoot = FindInScene<Transform>(scene).FirstOrDefault(candidate =>
            candidate.parent == null && candidate.name == "Dialogue Bubbles") ??
            throw new MissingReferenceException("stage2 requires the saved Dialogue Bubbles root.");
        Transform old = dialogueRoot.Find(name);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePrefabPath) ??
            throw new MissingReferenceException("Missing " + DialoguePrefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, dialogueRoot);
        instance.name = name;
        WorldDialogueBubble bubble = instance.GetComponent<WorldDialogueBubble>();
        SerializedObject bubbleData = new SerializedObject(bubble);
        SetObject(bubbleData, "followTarget", target);
        bubbleData.FindProperty("followOffset").vector3Value = offset;
        bubbleData.FindProperty("initialText").stringValue = "...";
        bubbleData.FindProperty("visibleOnAwake").boolValue = false;
        bubbleData.ApplyModifiedPropertiesWithoutUndo();
        instance.transform.position = target.position + offset;
        return bubble;
    }

    private static StoryDialogueController OpenStory(string path, out Scene scene)
    {
        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        StoryDialogueController story = FindInScene<StoryDialogueController>(scene).SingleOrDefault();
        if (story == null)
            throw new MissingReferenceException(path + " requires exactly one StoryDialogueController.");
        return story;
    }

    private static void ConfigureStage2EndScreen(Scene scene)
    {
        Transform overlay = FindInScene<Transform>(scene).SingleOrDefault(candidate =>
            candidate.name == "Victory Overlay");
        Text label = overlay != null ? overlay.GetComponentInChildren<Text>(true) : null;
        if (label == null)
            throw new MissingReferenceException("stage2 requires the saved Victory Overlay text.");

        label.text = Stage2EndScreenText;
        label.fontSize = 38;
        label.resizeTextForBestFit = false;
        label.alignment = TextAnchor.MiddleCenter;
        RectTransform rect = label.rectTransform;
        rect.sizeDelta = new Vector2(1500f, 650f);
        rect.anchoredPosition = Vector2.zero;
        EditorUtility.SetDirty(label);
        EditorUtility.SetDirty(rect);
    }

    private static StoryComicPanel EnsureComicPanel(Scene scene)
    {
        StoryComicPanel[] existing = FindInScene<StoryComicPanel>(scene);
        if (existing.Length > 1)
            throw new InvalidOperationException(scene.path + " contains duplicate comic panels.");
        if (existing.Length == 1)
        {
            existing[0].gameObject.SetActive(true);
            EditorUtility.SetDirty(existing[0].gameObject);
            return existing[0];
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ComicPrefabPath);
        if (prefab == null)
            throw new MissingReferenceException("Missing comic panel prefab at " + ComicPrefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = ComicObjectName;
        return instance.GetComponent<StoryComicPanel>();
    }

    private static void BuildComicPrefab()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            throw new MissingReferenceException("Missing dialogue font at " + FontPath);

        GameObject root = new GameObject(ComicObjectName, typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(CanvasGroup), typeof(StoryComicPanel));
        try
        {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject shade = Child(root.transform, "Black Backdrop", typeof(Image));
            Stretch((RectTransform)shade.transform, Vector2.zero, Vector2.zero);
            Image shadeImage = shade.GetComponent<Image>();
            shadeImage.color = new Color(0f, 0f, 0f, 0.94f);
            shadeImage.raycastTarget = false;

            GameObject frame = Child(root.transform, "Comic Panel", typeof(RawImage), typeof(Outline));
            RectTransform frameRect = (RectTransform)frame.transform;
            frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0.5f, 0.5f);
            // Each source sheet is a square 2x2 grid, so every cropped quadrant is square too.
            // Keep the authored UI square to avoid stretching the coarse pixels horizontally.
            frameRect.sizeDelta = new Vector2(900f, 900f);
            frameRect.anchoredPosition = Vector2.zero;
            RawImage rawImage = frame.GetComponent<RawImage>();
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
            Outline outline = frame.GetComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(4f, -4f);

            GameObject hint = Child(root.transform, "Enter Hint", typeof(Image), typeof(Outline));
            RectTransform hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(1f, 0f);
            hintRect.sizeDelta = new Vector2(430f, 64f);
            hintRect.anchoredPosition = new Vector2(-42f, 28f);
            hint.GetComponent<Image>().color = Color.white;
            Outline hintOutline = hint.GetComponent<Outline>();
            hintOutline.effectColor = Color.black;
            hintOutline.effectDistance = new Vector2(3f, -3f);

            GameObject hintTextObject = Child(hint.transform, "Hint Text", typeof(TextMeshProUGUI));
            Stretch((RectTransform)hintTextObject.transform, new Vector2(8f, 3f), new Vector2(-8f, -3f));
            TextMeshProUGUI hintText = hintTextObject.GetComponent<TextMeshProUGUI>();
            hintText.font = font;
            hintText.fontSize = 34f;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = Color.black;
            hintText.text = "Press Enter to continue";
            hintText.raycastTarget = false;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            SerializedObject data = new SerializedObject(root.GetComponent<StoryComicPanel>());
            data.FindProperty("canvasGroup").objectReferenceValue = group;
            data.FindProperty("panelImage").objectReferenceValue = rawImage;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ComicPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureComicTexture(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new MissingReferenceException("Missing comic texture at " + path);
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static void ValidateComicTexture(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (texture == null || importer == null || importer.filterMode != FilterMode.Point ||
            importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
            throw new InvalidOperationException(path + " must be a crisp, uncompressed comic texture.");
    }

    private static void ValidateStage(string path, StoryBeat openingBeat, StoryBeat bossIntroBeat, int openingCount,
        int encounterCount, int bossIntroCount, int victoryCount, bool hasOpeningComic, bool hasBossComic,
        bool hasEndingComic)
    {
        StoryDialogueController story = OpenStory(path, out Scene scene);
        if (!story.gameObject.activeSelf)
            throw new InvalidOperationException(path + " must save its Story System active.");
        StoryComicPanel[] panels = FindInScene<StoryComicPanel>(scene);
        if (panels.Length != 1 || story.ComicPanel != panels[0] || !panels[0].gameObject.activeSelf)
            throw new InvalidOperationException(path + " must save one active, referenced StoryComicPanel prefab instance.");
        if (story.OpeningProgressBeat != openingBeat ||
            story.BossIntroductionProgressBeat != bossIntroBeat || story.OpeningLineCount != openingCount ||
            story.EncounterLineCount != encounterCount || story.BossIntroductionLineCount != bossIntroCount ||
            story.BossVictoryLineCount != victoryCount)
            throw new InvalidOperationException(path + " has incomplete chapter dialogue.");
        if ((story.OpeningComic != null) != hasOpeningComic ||
            (story.BossIntroductionComic != null) != hasBossComic ||
            (story.EndingComic != null) != hasEndingComic ||
            story.BossIntroductionComicAfterLine != (hasBossComic ? 6 : -1))
            throw new InvalidOperationException(path + " has the wrong comic or insertion point.");
        Canvas canvas = panels[0].GetComponent<Canvas>();
        RawImage raw = panels[0].GetComponentInChildren<RawImage>(true);
        TMP_Text hint = panels[0].GetComponentInChildren<TMP_Text>(true);
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay || canvas.sortingOrder != 1100 ||
            raw == null || ((RectTransform)raw.transform).sizeDelta != new Vector2(900f, 900f) ||
            hint == null || hint.text != "Press Enter to continue")
            throw new InvalidOperationException(path + " comic presentation prefab is incomplete.");
        if (hasEndingComic)
        {
            Transform overlay = FindInScene<Transform>(scene).SingleOrDefault(candidate =>
                candidate.name == "Victory Overlay");
            Text label = overlay != null ? overlay.GetComponentInChildren<Text>(true) : null;
            if (label == null || label.text != Stage2EndScreenText || label.fontSize != 38 ||
                label.rectTransform.sizeDelta != new Vector2(1500f, 650f) ||
                Mathf.Abs(story.EndingFadeToBlackDuration - 1.15f) > 0.001f)
                throw new InvalidOperationException("stage2 ending comic and team credits are incomplete.");
        }
    }

    private static void ValidateTranslations()
    {
        (StorySpeaker speaker, string text)[][] groups =
        {
            Stage1Opening(), Stage1Encounter(), Stage1BossIntroduction(), Stage1BossVictory(),
            Stage2Opening(), Stage2BossIntroduction(), Stage2BossVictory()
        };
        int count = 0;
        foreach ((StorySpeaker speaker, string text)[] group in groups)
        foreach ((StorySpeaker speaker, string text) line in group)
        {
            count++;
            if (!LocalizationTable.TryGetChinese(line.text, out string chinese) ||
                string.IsNullOrWhiteSpace(chinese) || chinese == line.text)
                throw new InvalidOperationException("Missing Chinese story translation for: " + line.text);
        }
        if (count != 48)
            throw new InvalidOperationException("The current two chapters must contain exactly 48 dialogue entries.");
    }

    private static void ValidateStage2BossCast()
    {
        StoryDialogueController story = OpenStory(Stage2Path, out Scene scene);
        Transform cast = FindInScene<Transform>(scene).SingleOrDefault(candidate =>
            candidate.parent == null && candidate.name == Stage2CastName);
        if (cast == null || !cast.gameObject.activeSelf)
            throw new InvalidOperationException("stage2 must save one active Boss Introduction Cast root.");
        Transform wizard = cast.Find(WizardActorName);
        Transform orc = cast.Find(OrcActorName);
        if (wizard == null || orc == null || wizard.gameObject.activeSelf || orc.gameObject.activeSelf)
            throw new InvalidOperationException("The Wizard and companion Orc must be saved dormant under the cast root.");

        GameObject wizardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WizardPrefabPath);
        Sprite expectedIdle0 = wizardPrefab.GetComponentInChildren<BossSpriteAnimator>(true).idle.frames[0];
        SpriteRenderer wizardRenderer = wizard.GetComponent<SpriteRenderer>();
        if (wizardRenderer == null || wizardRenderer.sprite != expectedIdle0 ||
            wizard.GetComponent<Collider2D>() != null || wizard.GetComponent<CombatHealth>() != null)
            throw new InvalidOperationException("The story Wizard must be the exact Idle_0 sprite without combat components.");

        Enemy_Health orcHealth = orc.GetComponent<Enemy_Health>();
        if (orcHealth == null || orc.GetComponent<Enemy_Orc>() == null)
            throw new InvalidOperationException("The stage2 companion must preserve the complete combat Orc prefab.");
        WorldDialogueBubble wizardBubble = story.GetBubbleForSpeaker(StorySpeaker.EvilWizard);
        WorldDialogueBubble orcBubble = story.GetBubbleForSpeaker(StorySpeaker.Monster);
        if (wizardBubble == null || orcBubble == null || wizardBubble == orcBubble ||
            wizardBubble.FollowTarget != wizard || orcBubble.FollowTarget != orc)
            throw new InvalidOperationException("Wizard and Monster dialogue must route to their own saved bubbles.");

        SerializedObject storyData = new SerializedObject(story);
        SerializedProperty cues = storyData.FindProperty("bossIntroductionActorCues");
        if (story.AdditionalSpeakerBubbleCount != 2 || story.BossIntroductionActorCueCount != 3 ||
            cues.GetArrayElementAtIndex(0).FindPropertyRelative("beforeLineIndex").intValue != 3 ||
            cues.GetArrayElementAtIndex(1).FindPropertyRelative("beforeLineIndex").intValue != 11 ||
            cues.GetArrayElementAtIndex(2).FindPropertyRelative("beforeLineIndex").intValue != 11)
            throw new InvalidOperationException("stage2 actor reveal/hide cues do not match the authored dialogue.");

        EnemyHealth boss = FindInScene<EnemyHealth>(scene).Single();
        SerializedObject storyDataForActors = new SerializedObject(story);
        SerializedProperty hiddenActors = storyDataForActors.FindProperty("actorsHiddenAfterBossIntroduction");
        SerializedProperty activeActors = storyDataForActors.FindProperty("actorsActiveAfterBossIntroduction");
        bool orcHiddenAtCombatStart = Enumerable.Range(0, hiddenActors.arraySize)
            .Any(index => hiddenActors.GetArrayElementAtIndex(index).objectReferenceValue == orc.gameObject);
        if (!orcHiddenAtCombatStart || activeActors.arraySize != 0 ||
            cast.GetComponent<BossEncounterObjective>() != null ||
            ReferencedObject(boss, "victoryObjective") != null)
            throw new InvalidOperationException("The story Orc must disappear at combat start and King death must end the encounter.");
    }

    private static (StorySpeaker, string)[] Stage1Opening() => new[]
    {
        (StorySpeaker.Samurai, "Decades have passed... and now I have returned."),
        (StorySpeaker.Samurai, "Since that day, I have lost count of the battles I have fought."),
        (StorySpeaker.Samurai, "Even on the quietest nights, my heart has never known a moment's rest."),
        (StorySpeaker.Samurai, "Today, I have finally made my choice—"),
        (StorySpeaker.Samurai, "I swear to defend justice and demand the truth for my lord!")
    };

    private static (StorySpeaker, string)[] Stage1Encounter() => new[]
    {
        (StorySpeaker.Samurai, "These monsters again... It feels all too familiar."),
        (StorySpeaker.Samurai, "Time has rusted my blade—and weathered its wielder."),
        (StorySpeaker.Samurai, "I should find equipment worthy of the road ahead.")
    };

    private static (StorySpeaker, string)[] Stage1BossIntroduction() => new[]
    {
        (StorySpeaker.EvilWizard, "You...?"),
        (StorySpeaker.Samurai, "Yes. I have come to demand the truth."),
        (StorySpeaker.EvilWizard, "So you finally came. You never could let go of that day."),
        (StorySpeaker.EvilWizard, "Your precious lord met a truly wretched end."),
        (StorySpeaker.Samurai, "Do not dare speak of him!"),
        (StorySpeaker.EvilWizard, "Ha! If you would silence me, prove your right in battle!")
    };

    private static (StorySpeaker, string)[] Stage1BossVictory() => new[]
    {
        (StorySpeaker.EvilWizard, "You have grown stronger through all those years of battle..."),
        (StorySpeaker.Samurai, "That technique... You were not the one who killed my lord! What happened that day?"),
        (StorySpeaker.EvilWizard, "And wiser, too. Age has sharpened your eyes."),
        (StorySpeaker.EvilWizard, "You are right. It was not me. Take the crimson rune you found—and seek the truth yourself."),
        (StorySpeaker.Samurai, "..."),
        (StorySpeaker.Samurai, "Then today, at last, the truth will be revealed."),
        (StorySpeaker.Samurai, "Wait... why is the crimson rune glowing?")
    };

    private static (StorySpeaker, string)[] Stage2Opening() => new[]
    {
        (StorySpeaker.Samurai, "This rune... it brought me back to—"),
        (StorySpeaker.Samurai, "The wizard's castle, on the very day my lord died."),
        (StorySpeaker.Samurai, "Then I can finally see it with my own eyes..."),
        (StorySpeaker.Samurai, "Whoever killed my lord will repay that blood a hundredfold.")
    };

    private static (StorySpeaker, string)[] Stage2BossIntroduction() => new[]
    {
        (StorySpeaker.Samurai, "My lord!"),
        (StorySpeaker.Samurai, "I was fighting elsewhere that day. I could not save you."),
        (StorySpeaker.Samurai, "This time, I will keep my oath. I will defend you with my life!"),
        (StorySpeaker.EvilWizard, "He is unworthy of your oath."),
        (StorySpeaker.Samurai, "What? Why are you here—alive?"),
        (StorySpeaker.EvilWizard, "First, see the truth for yourself."),
        (StorySpeaker.Samurai, "What... my lord was behind it all?"),
        (StorySpeaker.Samurai, "But why should I believe you?"),
        (StorySpeaker.EvilWizard, "You will, once you see your lord with your own eyes."),
        (StorySpeaker.EvilWizard, "I forged the crimson and verdant runes. The green rune can return you to the future."),
        (StorySpeaker.EvilWizard, "My part is finished. How amusing..."),
        (StorySpeaker.King, "On your next raid, take this village. I will leave enough 'food' for you."),
        (StorySpeaker.Monster, "Graaagh!"),
        (StorySpeaker.King, "In return, you will bring me the gold we agreed upon."),
        (StorySpeaker.King, "Wait. Who is there?"),
        (StorySpeaker.Samurai, "What is this? Why is my lord bargaining with the enemy?"),
        (StorySpeaker.Samurai, "Was the man I followed nothing but a hypocrite?"),
        (StorySpeaker.Samurai, "What have I done...?"),
        (StorySpeaker.Samurai, "But it is not too late. I will still honor my oath—"),
        (StorySpeaker.Samurai, "I will defend justice!")
    };

    private static (StorySpeaker, string)[] Stage2BossVictory() => new[]
    {
        (StorySpeaker.Samurai, "To think I killed my own lord to keep the very oath I made to him..."),
        (StorySpeaker.Samurai, "At last, everything I came here to do is done."),
        (StorySpeaker.Samurai, "I will return to the future with one vow intact: justice, no matter the cost.")
    };

    private static void SetLines(SerializedProperty property, (StorySpeaker speaker, string text)[] lines)
    {
        property.arraySize = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            SerializedProperty line = property.GetArrayElementAtIndex(i);
            line.FindPropertyRelative("speaker").enumValueIndex = (int)lines[i].speaker;
            line.FindPropertyRelative("text").stringValue = lines[i].text;
        }
    }

    private static void SetSpeakerBindings(SerializedProperty property,
        params (StorySpeaker speaker, WorldDialogueBubble bubble)[] bindings)
    {
        property.arraySize = bindings.Length;
        for (int i = 0; i < bindings.Length; i++)
        {
            SerializedProperty binding = property.GetArrayElementAtIndex(i);
            binding.FindPropertyRelative("speaker").enumValueIndex = (int)bindings[i].speaker;
            binding.FindPropertyRelative("bubble").objectReferenceValue = bindings[i].bubble;
        }
    }

    private static void SetActorCues(SerializedProperty property,
        params (int line, GameObject actor, bool active)[] cues)
    {
        property.arraySize = cues.Length;
        for (int i = 0; i < cues.Length; i++)
        {
            SerializedProperty cue = property.GetArrayElementAtIndex(i);
            cue.FindPropertyRelative("beforeLineIndex").intValue = cues[i].line;
            cue.FindPropertyRelative("actor").objectReferenceValue = cues[i].actor;
            cue.FindPropertyRelative("active").boolValue = cues[i].active;
        }
    }

    private static void SetObjectArray(SerializedProperty property, params UnityEngine.Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static UnityEngine.Object ReferencedObject(UnityEngine.Object target, string property) =>
        new SerializedObject(target).FindProperty(property)?.objectReferenceValue;

    private static void SetObject(SerializedObject data, string property, UnityEngine.Object value) =>
        data.FindProperty(property).objectReferenceValue = value;

    private static GameObject Child(Transform parent, string name, params Type[] components)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        foreach (Type component in components)
            if (component != typeof(RectTransform))
                child.AddComponent(component);
        return child;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static void Save(Scene scene, string path)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, path))
            throw new InvalidOperationException("Failed to save " + path);
    }
}
#endif
