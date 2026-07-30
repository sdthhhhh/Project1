#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SetupStartScene
{
    private const string ScenePath = "Assets/Scenes/StartScene.unity";

    [MenuItem("BlindGame/Setup StartScene Menu")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Stop Play Mode first.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera + dark atmosphere
        GameObject camGo = new GameObject("Main Camera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.045f, 0.055f, 1f);
        camGo.AddComponent<AudioListener>();
        camGo.tag = "MainCamera";

        GameObject lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.2f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // EventSystem
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        Color textColor = new Color(0.92f, 0.93f, 0.95f, 1f);
        Color muted = new Color(0.72f, 0.74f, 0.78f, 1f);
        Color panelBg = new Color(0f, 0f, 0f, 0.72f);
        Color btnBg = new Color(0.08f, 0.09f, 0.11f, 0.92f);

        // Canvas
        GameObject canvasGo = new GameObject("StartMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup rootGroup = canvasGo.AddComponent<CanvasGroup>();

        // Soft vignette / atmosphere wash behind UI
        Image wash = CreateUiObject("Atmosphere", canvasGo.transform).AddComponent<Image>();
        StretchFull(wash.rectTransform);
        wash.color = new Color(0.06f, 0.07f, 0.09f, 1f);
        wash.raycastTarget = false;

        Image vignette = CreateUiObject("Vignette", canvasGo.transform).AddComponent<Image>();
        StretchFull(vignette.rectTransform);
        vignette.color = new Color(0f, 0f, 0f, 0.35f);
        vignette.raycastTarget = false;

        // MAIN
        GameObject main = CreateUiObject("MainPanel", canvasGo.transform);
        StretchFull(main.GetComponent<RectTransform>());

        TextMeshProUGUI brand = CreateText(main.transform, "BrandTitle", "DUALITY", font, 96f, textColor);
        RectTransform brandRt = brand.rectTransform;
        brandRt.anchorMin = new Vector2(0.5f, 0.55f);
        brandRt.anchorMax = new Vector2(0.5f, 0.55f);
        brandRt.pivot = new Vector2(0.5f, 0.5f);
        brandRt.anchoredPosition = new Vector2(0f, 80f);
        brandRt.sizeDelta = new Vector2(900f, 120f);
        brand.alignment = TextAlignmentOptions.Center;
        brand.fontStyle = FontStyles.Bold;
        brand.characterSpacing = 12f;

        TextMeshProUGUI tag = CreateText(main.transform, "Tagline", "put the pieces back", font, 26f, muted);
        RectTransform tagRt = tag.rectTransform;
        tagRt.anchorMin = new Vector2(0.5f, 0.55f);
        tagRt.anchorMax = new Vector2(0.5f, 0.55f);
        tagRt.anchoredPosition = new Vector2(0f, 10f);
        tagRt.sizeDelta = new Vector2(700f, 40f);
        tag.alignment = TextAlignmentOptions.Center;

        Button startBtn = CreateMenuButton(main.transform, "StartButton", "START", font, textColor, btnBg, 0f);
        Button settingsBtn = CreateMenuButton(main.transform, "SettingsButton", "SETTINGS", font, textColor, btnBg, -78f);
        Button creditsBtn = CreateMenuButton(main.transform, "CreditsButton", "CREDITS", font, textColor, btnBg, -156f);

        // SETTINGS
        GameObject settings = CreatePanel(canvasGo.transform, "SettingsPanel", panelBg);
        settings.SetActive(false);
        CreateText(settings.transform, "SettingsTitle", "SETTINGS", font, 40f, textColor).rectTransform.anchoredPosition = new Vector2(0f, 150f);

        Slider volume = CreateLabeledSlider(settings.transform, "Volume", "Master Volume", font, textColor, muted, 40f, out TMP_Text volumeValue);
        Slider sensitivity = CreateLabeledSlider(settings.transform, "Sensitivity", "Mouse Sensitivity", font, textColor, muted, -40f, out TMP_Text sensValue);
        volume.minValue = 0f;
        volume.maxValue = 1f;
        volume.value = 1f;
        sensitivity.minValue = 0.2f;
        sensitivity.maxValue = 8f;
        sensitivity.value = 2f;

        Button settingsBack = CreateMenuButton(settings.transform, "SettingsBack", "BACK", font, textColor, btnBg, -180f);

        // CREDITS
        GameObject credits = CreatePanel(canvasGo.transform, "CreditsPanel", panelBg);
        credits.SetActive(false);
        CreateText(credits.transform, "CreditsTitle", "CREDITS", font, 40f, textColor).rectTransform.anchoredPosition = new Vector2(0f, 180f);

        TextMeshProUGUI creditsBody = CreateText(credits.transform, "CreditsBody",
            "DUALITY\n\nDesign / Narrative\nProgramming\nArt / Sound\n\nThank you for playing.",
            font, 24f, muted);
        RectTransform creditsBodyRt = creditsBody.rectTransform;
        creditsBodyRt.anchoredPosition = new Vector2(0f, 10f);
        creditsBodyRt.sizeDelta = new Vector2(700f, 280f);
        creditsBody.alignment = TextAlignmentOptions.Center;
        creditsBody.lineSpacing = 8f;

        Button creditsBack = CreateMenuButton(credits.transform, "CreditsBack", "BACK", font, textColor, btnBg, -200f);

        StartMenuController menu = canvasGo.AddComponent<StartMenuController>();
        SerializedObject so = new SerializedObject(menu);
        so.FindProperty("introSceneName").stringValue = "IntroScene";
        so.FindProperty("mainPanel").objectReferenceValue = main;
        so.FindProperty("settingsPanel").objectReferenceValue = settings;
        so.FindProperty("creditsPanel").objectReferenceValue = credits;
        so.FindProperty("rootGroup").objectReferenceValue = rootGroup;
        so.FindProperty("startButton").objectReferenceValue = startBtn;
        so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
        so.FindProperty("creditsButton").objectReferenceValue = creditsBtn;
        so.FindProperty("volumeSlider").objectReferenceValue = volume;
        so.FindProperty("sensitivitySlider").objectReferenceValue = sensitivity;
        so.FindProperty("volumeValueText").objectReferenceValue = volumeValue;
        so.FindProperty("sensitivityValueText").objectReferenceValue = sensValue;
        so.FindProperty("settingsBackButton").objectReferenceValue = settingsBack;
        so.FindProperty("creditsBackButton").objectReferenceValue = creditsBack;
        so.ApplyModifiedProperties();

        // Save scene
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);

        // Build settings: StartScene first
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        list.Add(new EditorBuildSettingsScene(ScenePath, true));
        string[] keep = { "IntroScene", "SampleScene", "EndScene", "3dTest" };
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.path == null) continue;
            if (s.path.Replace('\\', '/').EndsWith("StartScene.unity")) continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(s.path);
            bool wanted = false;
            for (int i = 0; i < keep.Length; i++)
                if (keep[i] == name) { wanted = true; break; }
            if (wanted)
                list.Add(new EditorBuildSettingsScene(s.path, s.enabled));
        }
        EditorBuildSettings.scenes = list.ToArray();

        // EndScene → StartScene
        Scene end = EditorSceneManager.OpenScene("Assets/Scenes/EndScene.unity", OpenSceneMode.Additive);
        EndingNarrationController ending = Object.FindObjectOfType<EndingNarrationController>(true);
        if (ending != null)
        {
            SerializedObject eso = new SerializedObject(ending);
            eso.FindProperty("nextSceneName").stringValue = "StartScene";
            eso.ApplyModifiedProperties();
            EditorUtility.SetDirty(ending);
            EditorSceneManager.SaveScene(end);
        }
        EditorSceneManager.CloseScene(end, true);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Debug.Log("StartScene created. Build order starts with StartScene. EndScene returns to StartScene.");
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color bg)
    {
        GameObject go = CreateUiObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(820f, 520f);
        Image img = go.AddComponent<Image>();
        img.color = bg;
        return go;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 60f);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateMenuButton(Transform parent, string name, string label, TMP_FontAsset font, Color textColor, Color bg, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(320f, 58f);
        Image img = go.GetComponent<Image>();
        img.color = bg;
        Button btn = go.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.85f, 0.88f, 0.92f, 1f);
        cb.pressedColor = new Color(0.65f, 0.68f, 0.72f, 1f);
        btn.colors = cb;

        TextMeshProUGUI tmp = CreateText(go.transform, "Label", label, font, 28f, textColor);
        RectTransform lrt = tmp.rectTransform;
        StretchFull(lrt);
        tmp.raycastTarget = false;
        return btn;
    }

    private static Slider CreateLabeledSlider(Transform parent, string name, string label, TMP_FontAsset font, Color textColor, Color muted, float y, out TMP_Text valueText)
    {
        GameObject root = CreateUiObject(name + "Row", parent);
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = new Vector2(0f, y);
        rootRt.sizeDelta = new Vector2(640f, 70f);

        TextMeshProUGUI labelTmp = CreateText(root.transform, "Label", label, font, 22f, textColor);
        RectTransform lrt = labelTmp.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0.5f);
        lrt.anchorMax = new Vector2(0f, 0.5f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.anchoredPosition = new Vector2(20f, 14f);
        lrt.sizeDelta = new Vector2(280f, 30f);
        labelTmp.alignment = TextAlignmentOptions.Left;

        valueText = CreateText(root.transform, "Value", "100%", font, 22f, muted);
        RectTransform vrt = valueText.rectTransform;
        vrt.anchorMin = new Vector2(1f, 0.5f);
        vrt.anchorMax = new Vector2(1f, 0.5f);
        vrt.pivot = new Vector2(1f, 0.5f);
        vrt.anchoredPosition = new Vector2(-20f, 14f);
        vrt.sizeDelta = new Vector2(80f, 30f);
        valueText.alignment = TextAlignmentOptions.Right;

        GameObject sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(root.transform, false);
        RectTransform srt = sliderGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 0.5f);
        srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0f, -14f);
        srt.sizeDelta = new Vector2(560f, 22f);

        Image bg = CreateUiObject("Background", sliderGo.transform).AddComponent<Image>();
        StretchFull(bg.rectTransform);
        bg.color = new Color(1f, 1f, 1f, 0.12f);

        GameObject fillArea = CreateUiObject("Fill Area", sliderGo.transform);
        RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
        StretchFull(fillAreaRt);
        fillAreaRt.offsetMin = new Vector2(0f, 0f);
        fillAreaRt.offsetMax = new Vector2(0f, 0f);
        Image fill = CreateUiObject("Fill", fillArea.transform).AddComponent<Image>();
        StretchFull(fill.rectTransform);
        fill.color = new Color(0.85f, 0.87f, 0.9f, 0.85f);

        GameObject handleArea = CreateUiObject("Handle Slide Area", sliderGo.transform);
        RectTransform handleAreaRt = handleArea.GetComponent<RectTransform>();
        StretchFull(handleAreaRt);
        Image handle = CreateUiObject("Handle", handleArea.transform).AddComponent<Image>();
        RectTransform handleRt = handle.rectTransform;
        handleRt.sizeDelta = new Vector2(18f, 28f);
        handle.color = textColor;

        Slider slider = sliderGo.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }
}
#endif
