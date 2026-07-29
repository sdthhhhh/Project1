using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mesh outline tools. "Memory" = which objects already have MeshOutlineStyle.
/// Generate / Clear / Preview only touch those components — never auto-add to bare meshes.
/// </summary>
public sealed class MeshOutlineTools : EditorWindow
{
    private static readonly Color DefaultBody = new Color(0.09f, 0.09f, 0.1f, 1f);

    private float outlineWidthFactor = 0.015f;
    private MeshOutlineStyle.OutlineTone tone = MeshOutlineStyle.OutlineTone.White;
    private Vector2 scroll;
    private Vector2 listScroll;
    private bool showTrackedList = true;
    private bool showAddComponent;

    [MenuItem("Tools/Mesh Outline/Open Tools…")]
    public static void Open()
    {
        var win = GetWindow<MeshOutlineTools>("Mesh Outline");
        win.minSize = new Vector2(340f, 420f);
        win.Show();
    }

    [MenuItem("Tools/Mesh Outline/Generate (Only With Component)")]
    public static void MenuGenerate()
    {
        GenerateExistingInActiveScene();
    }

    [MenuItem("Tools/Mesh Outline/Clear Generated (Keep Components)")]
    public static void MenuClearGenerated()
    {
        ClearGeneratedInActiveScene();
    }

    [MenuItem("Tools/Mesh Outline/Editor: Original Colors")]
    public static void MenuOriginalColors()
    {
        SetEditorPreviewMode(keepOriginal: true);
    }

    [MenuItem("Tools/Mesh Outline/Editor: Comic Preview")]
    public static void MenuComicPreview()
    {
        SetEditorPreviewMode(keepOriginal: false);
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        var tracked = GetStylesInActiveScene();

        EditorGUILayout.HelpBox(
            "记住谁有描边 = 场景里是否挂了 MeshOutlineStyle。\n" +
            "Generate / Clear / 预览切换 都只处理已挂组件的物体；没挂的一律不动。\n" +
            "保存场景后这份名单会一起保存。",
            MessageType.Info);

        EditorGUILayout.LabelField("Tracked in active scene", tracked.Count.ToString(), EditorStyles.boldLabel);

        showTrackedList = EditorGUILayout.Foldout(showTrackedList, "Show tracked objects", true);
        if (showTrackedList)
        {
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(140f));
            if (tracked.Count == 0)
            {
                EditorGUILayout.LabelField("(none — add MeshOutlineStyle, or use Add Component below)");
            }
            else
            {
                for (int i = 0; i < tracked.Count; i++)
                {
                    MeshOutlineStyle style = tracked[i];
                    if (style == null) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(style.gameObject, typeof(GameObject), true);
                        if (GUILayout.Button("Ping", GUILayout.Width(44f)))
                        {
                            EditorGUIUtility.PingObject(style.gameObject);
                            Selection.activeGameObject = style.gameObject;
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Generate / Clear (component only)", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(tracked.Count == 0))
        {
            if (GUILayout.Button("Generate Outlines", GUILayout.Height(32f)))
                GenerateExistingInActiveScene();
            if (GUILayout.Button("Clear Generated Helpers", GUILayout.Height(28f)))
                ClearGeneratedInActiveScene();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Edit Preview (component only)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(tracked.Count == 0))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Original Colors", GUILayout.Height(28f)))
                SetEditorPreviewMode(keepOriginal: true);
            if (GUILayout.Button("Comic Preview", GUILayout.Height(28f)))
                SetEditorPreviewMode(keepOriginal: false);
        }

        EditorGUILayout.Space(8f);
        showAddComponent = EditorGUILayout.Foldout(showAddComponent, "Add MeshOutlineStyle (manual)", true);
        if (showAddComponent)
        {
            EditorGUILayout.HelpBox(
                "只有这里才会给物体新挂组件。全场景一键挂上已移除，避免误伤。",
                MessageType.None);
            tone = (MeshOutlineStyle.OutlineTone)EditorGUILayout.EnumPopup("Tone", tone);
            outlineWidthFactor = EditorGUILayout.Slider("Width Factor", outlineWidthFactor, 0.005f, 0.08f);

            if (GUILayout.Button("Add Component To Selection + Generate"))
                AddComponentToSelection(outlineWidthFactor, tone);

            if (GUILayout.Button("Remove Components From Scene…"))
            {
                if (EditorUtility.DisplayDialog(
                        "Remove MeshOutlineStyle?",
                        "Restores original materials when cached, then removes MeshOutlineStyle from the active scene.",
                        "Remove",
                        "Cancel"))
                {
                    RemoveStylesFromActiveScene();
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static List<MeshOutlineStyle> GetStylesInActiveScene()
    {
        var styles = Object.FindObjectsByType<MeshOutlineStyle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var list = new List<MeshOutlineStyle>();
        string scene = SceneManager.GetActiveScene().name;
        for (int i = 0; i < styles.Length; i++)
        {
            if (styles[i] == null) continue;
            if (!styles[i].gameObject.scene.IsValid() || styles[i].gameObject.scene.name != scene)
                continue;
            if (styles[i].gameObject.name == "OutlineShell" || styles[i].gameObject.name == "OutlineCreases")
                continue;
            list.Add(styles[i]);
        }
        return list;
    }

    private static void ConfigureDefaults(MeshOutlineStyle style, float widthFactor, MeshOutlineStyle.OutlineTone outlineTone)
    {
        style.Configure(outlineTone, widthFactor, DefaultBody, hardEdges: false);
        style.KeepOriginalColorsInEditor = true;
        var so = new SerializedObject(style);
        SerializedProperty build = so.FindProperty("buildOnAwake");
        SerializedProperty sil = so.FindProperty("drawSilhouette");
        SerializedProperty hard = so.FindProperty("drawHardEdges");
        if (build != null) build.boolValue = true;
        if (sil != null) sil.boolValue = true;
        if (hard != null) hard.boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetEditorPreviewMode(bool keepOriginal)
    {
        var styles = GetStylesInActiveScene();
        try
        {
            for (int i = 0; i < styles.Count; i++)
            {
                if (i % 20 == 0)
                {
                    EditorUtility.DisplayProgressBar(
                        "Mesh Outline",
                        keepOriginal ? "Original colors…" : "Comic preview…",
                        (float)i / Mathf.Max(1, styles.Count));
                }

                styles[i].KeepOriginalColorsInEditor = keepOriginal;
                styles[i].Rebuild();
                EditorUtility.SetDirty(styles[i]);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        MarkActiveSceneDirty();
        Debug.Log($"Mesh Outline: editor preview → {(keepOriginal ? "Original Colors" : "Comic")} ({styles.Count} with component).");
    }

    /// <summary>Rebuild outlines only for objects that already have MeshOutlineStyle.</summary>
    public static void GenerateExistingInActiveScene()
    {
        var styles = GetStylesInActiveScene();
        if (styles.Count == 0)
        {
            Debug.LogWarning("Mesh Outline: no MeshOutlineStyle in active scene — nothing to generate.");
            return;
        }

        try
        {
            for (int i = 0; i < styles.Count; i++)
            {
                if (i % 20 == 0)
                    EditorUtility.DisplayProgressBar("Mesh Outline", "Generate…", (float)i / styles.Count);
                styles[i].Rebuild();
                EditorUtility.SetDirty(styles[i]);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        MarkActiveSceneDirty();
        Debug.Log($"Mesh Outline: generated {styles.Count} (component-only).");
    }

    public static void ClearGeneratedInActiveScene()
    {
        var styles = GetStylesInActiveScene();
        for (int i = 0; i < styles.Count; i++)
        {
            styles[i].ClearGenerated();
            EditorUtility.SetDirty(styles[i]);
        }

        var all = Resources.FindObjectsOfTypeAll<Transform>();
        string scene = SceneManager.GetActiveScene().name;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            GameObject go = all[i].gameObject;
            if (!go.scene.IsValid() || go.scene.name != scene) continue;
            if (go.name == "OutlineShell" || go.name == "OutlineCreases")
                Undo.DestroyObjectImmediate(go);
        }

        MarkActiveSceneDirty();
        Debug.Log($"Mesh Outline: cleared generated helpers ({styles.Count} components kept).");
    }

    public static void AddComponentToSelection(float widthFactor, MeshOutlineStyle.OutlineTone outlineTone)
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("Mesh Outline: select one or more objects first.");
            return;
        }

        int applied = 0;
        var seen = new HashSet<int>();
        for (int i = 0; i < selected.Length; i++)
        {
            MeshFilter[] filters = selected[i].GetComponentsInChildren<MeshFilter>(true);
            for (int f = 0; f < filters.Length; f++)
            {
                MeshFilter mf = filters[f];
                if (mf == null || mf.sharedMesh == null) continue;
                if (mf.GetComponent<MeshRenderer>() == null) continue;
                if (mf.gameObject.name == "OutlineShell" || mf.gameObject.name == "OutlineCreases") continue;
                if (mf.GetComponentInParent<Canvas>() != null) continue;
                if (!seen.Add(mf.GetInstanceID())) continue;

                MeshOutlineStyle style = mf.GetComponent<MeshOutlineStyle>();
                if (style == null)
                    style = Undo.AddComponent<MeshOutlineStyle>(mf.gameObject);
                ConfigureDefaults(style, widthFactor, outlineTone);
                style.Rebuild();
                applied++;
            }
        }

        MarkActiveSceneDirty();
        Debug.Log($"Mesh Outline: added/generated on selection ({applied}).");
    }

    public static void RemoveStylesFromActiveScene()
    {
        var styles = GetStylesInActiveScene();
        for (int i = 0; i < styles.Count; i++)
        {
            MeshOutlineStyle style = styles[i];
            if (style == null) continue;
            style.ClearGenerated();
            Undo.DestroyObjectImmediate(style);
        }

        MarkActiveSceneDirty();
        Debug.Log($"Mesh Outline: removed {styles.Count} MeshOutlineStyle components.");
    }

    private static void MarkActiveSceneDirty()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);
    }
}
