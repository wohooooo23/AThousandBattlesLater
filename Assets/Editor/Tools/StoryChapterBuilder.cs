#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors the two-chapter English story and its panel-by-panel comic presentation.</summary>
public static class StoryChapterBuilder
{
    private const string Stage1Path = "Assets/Scenes/stage1_full.unity";
    private const string Stage2Path = "Assets/Scenes/stage2_full.unity";
    private const string ComicPrefabPath = "Assets/Prefab/StoryComicPanel.prefab";
    private const string ProloguePath = "Assets/Resources/Story/Comic_Prologue.png";
    private const string BetrayalPath = "Assets/Resources/Story/Comic_Betrayal.png";
    private const string FontPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string ComicObjectName = "Story Comic Panel";

    [MenuItem("Tools/Narrative & Audio/Build Two-Chapter Story")]
    public static void Build()
    {
        ConfigureComicTexture(ProloguePath);
        ConfigureComicTexture(BetrayalPath);
        BuildComicPrefab();
        ConfigureStage1();
        ConfigureStage2();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("STORY_CHAPTER_BUILD_OK: polished two-stage dialogue and two panel-by-panel comics are saved.");
    }

    [MenuItem("Tools/Narrative & Audio/Validate Two-Chapter Story")]
    public static void Validate()
    {
        ValidateComicTexture(ProloguePath);
        ValidateComicTexture(BetrayalPath);
        ValidateStage(Stage1Path, StoryBeat.Opening, 5, 3, 6, 7, true, false);
        ValidateStage(Stage2Path, StoryBeat.Stage2Opening, 4, 0, 20, 3, false, true);
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
        data.FindProperty("bossIntroductionComicAfterLine").intValue = -1;
        data.FindProperty("openingProgressBeat").enumValueIndex = (int)StoryBeat.Opening;
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
        data.FindProperty("bossIntroductionComicAfterLine").intValue = 6;
        data.FindProperty("openingProgressBeat").enumValueIndex = (int)StoryBeat.Stage2Opening;
        data.FindProperty("keepLastVictoryLineVisible").boolValue = false;
        SetLines(data.FindProperty("openingLines"), Stage2Opening());
        SetLines(data.FindProperty("firstEncounterLines"), Array.Empty<(StorySpeaker, string)>());
        SetLines(data.FindProperty("bossIntroductionLines"), Stage2BossIntroduction());
        SetLines(data.FindProperty("bossVictoryLines"), Stage2BossVictory());
        data.ApplyModifiedPropertiesWithoutUndo();
        Save(scene, Stage2Path);
    }

    private static StoryDialogueController OpenStory(string path, out Scene scene)
    {
        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        StoryDialogueController story = FindInScene<StoryDialogueController>(scene).SingleOrDefault();
        if (story == null)
            throw new MissingReferenceException(path + " requires exactly one StoryDialogueController.");
        return story;
    }

    private static StoryComicPanel EnsureComicPanel(Scene scene)
    {
        StoryComicPanel[] existing = FindInScene<StoryComicPanel>(scene);
        if (existing.Length > 1)
            throw new InvalidOperationException(scene.path + " contains duplicate comic panels.");
        if (existing.Length == 1)
            return existing[0];

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
            frameRect.sizeDelta = new Vector2(1600f, 900f);
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

    private static void ValidateStage(string path, StoryBeat openingBeat, int openingCount,
        int encounterCount, int bossIntroCount, int victoryCount, bool hasOpeningComic, bool hasBossComic)
    {
        StoryDialogueController story = OpenStory(path, out Scene scene);
        if (!story.gameObject.activeSelf)
            throw new InvalidOperationException(path + " must save its Story System active.");
        StoryComicPanel[] panels = FindInScene<StoryComicPanel>(scene);
        if (panels.Length != 1 || story.ComicPanel != panels[0])
            throw new InvalidOperationException(path + " must save one referenced StoryComicPanel prefab instance.");
        if (story.OpeningProgressBeat != openingBeat || story.OpeningLineCount != openingCount ||
            story.EncounterLineCount != encounterCount || story.BossIntroductionLineCount != bossIntroCount ||
            story.BossVictoryLineCount != victoryCount)
            throw new InvalidOperationException(path + " has incomplete chapter dialogue.");
        if ((story.OpeningComic != null) != hasOpeningComic ||
            (story.BossIntroductionComic != null) != hasBossComic ||
            story.BossIntroductionComicAfterLine != (hasBossComic ? 6 : -1))
            throw new InvalidOperationException(path + " has the wrong comic or insertion point.");
        Canvas canvas = panels[0].GetComponent<Canvas>();
        RawImage raw = panels[0].GetComponentInChildren<RawImage>(true);
        TMP_Text hint = panels[0].GetComponentInChildren<TMP_Text>(true);
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay || canvas.sortingOrder != 1100 ||
            raw == null || hint == null || hint.text != "Press Enter to continue")
            throw new InvalidOperationException(path + " comic presentation prefab is incomplete.");
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
