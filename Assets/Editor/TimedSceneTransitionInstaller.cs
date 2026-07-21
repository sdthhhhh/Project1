#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class TimedSceneTransitionInstaller
{
    private const string CanvasName = "TimedSceneTransitionCanvas";
    private const string SessionKey = "TimedSceneTransition.InstallAttempted.v1";

    static TimedSceneTransitionInstaller() { EditorApplication.delayCall += InstallOnce; }

    private static void InstallOnce()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        InstallInCurrentScene();
    }

    [MenuItem("Tools/Timed Scene Transition/Install In Current Scene")]
    public static void InstallInCurrentScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Stop Play Mode before installing the timed scene transition UI.");
            return;
        }
        if (!SceneManager.GetActiveScene().IsValid() || string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) return;

        GameObject existing = FindSceneObject(CanvasName);
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            Debug.Log("TimedSceneTransitionCanvas already exists. Existing scene layout was preserved.");
            return;
        }

        Transform parent = FindSceneObject("UI_ROOT")?.transform;
        GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Install Timed Scene Transition");
        if (parent != null) canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        GameObject timerBackground = UI("CountdownTimerBackground", canvasObject.transform, new Color(.035f, .03f, .025f, .72f));
        RectTransform timerBackgroundRect = timerBackground.GetComponent<RectTransform>();
        timerBackgroundRect.anchorMin = timerBackgroundRect.anchorMax = new Vector2(1f, 1f);
        timerBackgroundRect.pivot = new Vector2(1f, 1f);
        timerBackgroundRect.sizeDelta = new Vector2(190f, 74f);
        timerBackgroundRect.anchoredPosition = new Vector2(-28f, -24f);

        TMP_Text timerText = Label("CountdownTimerText", "05:00", timerBackground.transform, 38f);
        timerText.fontStyle = FontStyles.Bold;
        timerText.color = new Color(.92f, .88f, .78f, 1f);
        CountdownTimer timer = timerText.gameObject.AddComponent<CountdownTimer>();
        timer.Configure(timerText, 300f);

        GameObject left = UI("HandLeft", canvasObject.transform, new Color(.46f, .25f, .18f, 1f));
        RectTransform leftRect = left.GetComponent<RectTransform>();
        SetStretch(leftRect, 0f, 0f, .53f, 1f);
        left.GetComponent<Image>().raycastTarget = true;

        GameObject right = UI("HandRight", canvasObject.transform, new Color(.49f, .28f, .20f, 1f));
        RectTransform rightRect = right.GetComponent<RectTransform>();
        SetStretch(rightRect, .47f, 0f, 1f, 1f);
        right.GetComponent<Image>().raycastTarget = true;

        SceneTransitionManager manager = canvasObject.AddComponent<SceneTransitionManager>();
        FirstPersonMovement movement = FindSceneComponent<FirstPersonMovement>();
        FirstPersonLook look = FindSceneComponent<FirstPersonLook>();
        Jump jump = FindSceneComponent<Jump>();
        PlayerInteraction interaction = FindSceneComponent<PlayerInteraction>();
        manager.Configure(timer, canvas, leftRect, rightRect, movement, look, jump, interaction);
        manager.nextSceneName = FindAndRegisterNextScene();
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(timer);

        SaveScene();
        Selection.activeGameObject = canvasObject;
        Debug.Log($"Timed scene transition installed and saved. Next Scene Name: '{manager.nextSceneName}'. Replace HandLeft/HandRight Source Image with transparent hand sprites.");
    }

    [MenuItem("Tools/Timed Scene Transition/Select Transition Canvas")]
    public static void SelectCanvas()
    {
        Selection.activeGameObject = FindSceneObject(CanvasName);
    }

    private static string FindAndRegisterNextScene()
    {
        string currentPath = SceneManager.GetActiveScene().path;
        string nextPath = null;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && scene.path != currentPath) { nextPath = scene.path; break; }
        }
        if (string.IsNullOrEmpty(nextPath))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path != currentPath) { nextPath = path; break; }
            }
        }
        if (string.IsNullOrEmpty(nextPath)) return string.Empty;

        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        AddBuildSceneIfMissing(scenes, currentPath);
        AddBuildSceneIfMissing(scenes, nextPath);
        EditorBuildSettings.scenes = scenes.ToArray();
        return Path.GetFileNameWithoutExtension(nextPath);
    }

    private static void AddBuildSceneIfMissing(List<EditorBuildSettingsScene> scenes, string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        foreach (EditorBuildSettingsScene scene in scenes)
            if (scene.path == path) { scene.enabled = true; return; }
        scenes.Add(new EditorBuildSettingsScene(path, true));
    }

    private static GameObject UI(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static TMP_Text Label(string name, string text, Transform parent, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text label = go.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        SetStretch(label.rectTransform, 0f, 0f, 1f, 1f);
        return label;
    }

    private static void SetStretch(RectTransform rect, float x1, float y1, float x2, float y2)
    {
        rect.anchorMin = new Vector2(x1, y1);
        rect.anchorMax = new Vector2(x2, y2);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static GameObject FindSceneObject(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.scene.IsValid() && go.name == name) return go;
        return null;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
            if (component.gameObject.scene.IsValid()) return component;
        return null;
    }

    private static void SaveScene()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }
}
#endif
