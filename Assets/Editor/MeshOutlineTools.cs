using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Component = who has outlines. Tool only Generates / Clears runtime shells (DontSave).
/// Never auto-adds MeshOutlineStyle to bare meshes unless you use Add To Selection.
/// </summary>
public sealed class MeshOutlineTools : EditorWindow
{
    private static readonly Color DefaultBody = new Color(0f, 0f, 0f, 1f);

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
        win.minSize = new Vector2(340f, 400f);
        win.Show();
    }

    [InitializeOnLoadMethod]
    private static void AutoEnableReadableBeforePlay()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChangedForReadable;
        EditorApplication.playModeStateChanged += OnPlayModeStateChangedForReadable;
    }

    private static void OnPlayModeStateChangedForReadable(PlayModeStateChange state)
    {
        // Tool Generate already enables Read/Write; direct Play often skipped that step,
        // so crease/sealed builds silently produced nothing.
        if (state == PlayModeStateChange.ExitingEditMode)
            EnableReadWriteForOutlineMeshes(showDialog: false);
    }

    [MenuItem("Tools/Mesh Outline/Photos → Thin-Sheet Outline + Generate")]
    public static void MenuPhotosThinSheet()
    {
        ApplyThinSheetToPhotosInActiveScene();
    }

    /// <summary>
    /// Find MeshOutlineStyle under Photo paths / photo-like names, force thin-sheet inflate, Rebuild.
    /// </summary>
    public static void ApplyThinSheetToPhotosInActiveScene()
    {
        var styles = GetStylesInActiveScene();
        var photos = new List<MeshOutlineStyle>();
        for (int i = 0; i < styles.Count; i++)
        {
            if (IsPhotoOutlineTarget(styles[i]))
                photos.Add(styles[i]);
        }

        if (photos.Count == 0)
        {
            Debug.LogWarning("Mesh Outline: no photo-like MeshOutlineStyle found.");
            return;
        }

        try
        {
            for (int i = 0; i < photos.Count; i++)
            {
                if (i % 10 == 0)
                    EditorUtility.DisplayProgressBar("Mesh Outline", "Photos thin-sheet…", (float)i / photos.Count);

                MeshOutlineStyle style = photos[i];
                var so = new SerializedObject(style);
                so.FindProperty("forceThinSheetOutline").boolValue = true;
                so.FindProperty("drawSilhouette").boolValue = true;
                SerializedProperty minWorld = so.FindProperty("minWorldOutlineWidth");
                if (minWorld != null && minWorld.floatValue < 0.005f)
                    minWorld.floatValue = 0.008f;
                SerializedProperty factor = so.FindProperty("outlineWidthFactor");
                if (factor != null && factor.floatValue < 0.03f)
                    factor.floatValue = 0.04f;
                so.ApplyModifiedPropertiesWithoutUndo();

                style.Rebuild();
                EditorUtility.SetDirty(style);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        MarkActiveSceneDirty();
        Debug.Log($"Mesh Outline: applied thin-sheet inflate to {photos.Count} photos.");
    }

    private static bool IsPhotoOutlineTarget(MeshOutlineStyle style)
    {
        if (style == null)
            return false;

        Transform t = style.transform;
        while (t != null)
        {
            string n = t.name;
            if (n.IndexOf("photo", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("paintingmodel", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("日记碎片", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            t = t.parent;
        }

        return false;
    }

    [MenuItem("Tools/Mesh Outline/Enable Read/Write For Outline Meshes")]
    public static void MenuEnableReadable()
    {
        EnableReadWriteForOutlineMeshes(showDialog: true);
    }

    [MenuItem("Tools/Mesh Outline/Generate (Component Only)")]
    public static void MenuGenerate()
    {
        GenerateExistingInActiveScene();
    }

    [MenuItem("Tools/Mesh Outline/Clear Generated (Keep Components)")]
    public static void MenuClearGenerated()
    {
        ClearGeneratedInActiveScene();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        var tracked = GetStylesInActiveScene();

        EditorGUILayout.HelpBox(
            "名单 = MeshOutlineStyle 组件。\n" +
            "Generate（Edit）：密封描边（DontSave）。\n" +
            "Clear：删生成物。\n" +
            "Play：同样密封 Rebuild（需 Mesh Read/Write）。\n" +
            "首次请先点 Enable Read/Write。",
            MessageType.Info);

        EditorGUILayout.LabelField("Tracked in active scene", tracked.Count.ToString(), EditorStyles.boldLabel);

        showTrackedList = EditorGUILayout.Foldout(showTrackedList, "Show tracked objects", true);
        if (showTrackedList)
        {
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(140f));
            if (tracked.Count == 0)
                EditorGUILayout.LabelField("(none)");
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

        EditorGUILayout.Space(10f);
        using (new EditorGUI.DisabledScope(tracked.Count == 0))
        {
            if (GUILayout.Button("Enable Read/Write For Outline Meshes", GUILayout.Height(28f)))
                EnableReadWriteForOutlineMeshes(showDialog: true);
            if (GUILayout.Button("Photos → Thin-Sheet Outline + Generate", GUILayout.Height(32f)))
                ApplyThinSheetToPhotosInActiveScene();
            if (GUILayout.Button("Generate Outlines", GUILayout.Height(36f)))
                GenerateExistingInActiveScene();
            if (GUILayout.Button("Clear Generated", GUILayout.Height(32f)))
                ClearGeneratedInActiveScene();
        }

        EditorGUILayout.Space(10f);
        showAddComponent = EditorGUILayout.Foldout(showAddComponent, "Add MeshOutlineStyle (manual)", true);
        if (showAddComponent)
        {
            tone = (MeshOutlineStyle.OutlineTone)EditorGUILayout.EnumPopup("Tone", tone);
            outlineWidthFactor = EditorGUILayout.Slider("Width Factor", outlineWidthFactor, 0.005f, 0.08f);
            if (GUILayout.Button("Add Component To Selection + Generate"))
                AddComponentToSelection(outlineWidthFactor, tone);
            if (GUILayout.Button("Remove All Components From Scene…"))
            {
                if (EditorUtility.DisplayDialog(
                        "Remove MeshOutlineStyle?",
                        "Clear generated helpers, restore materials when cached, remove components.",
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
        style.Configure(outlineTone, widthFactor, DefaultBody, hardEdges: true);
        var so = new SerializedObject(style);
        SerializedProperty build = so.FindProperty("buildOnAwake");
        SerializedProperty sil = so.FindProperty("drawSilhouette");
        SerializedProperty hard = so.FindProperty("drawHardEdges");
        if (build != null) build.boolValue = true;
        if (sil != null) sil.boolValue = true;
        if (hard != null) hard.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void GenerateExistingInActiveScene()
    {
        var styles = GetStylesInActiveScene();
        if (styles.Count == 0)
        {
            Debug.LogWarning("Mesh Outline: no MeshOutlineStyle in active scene.");
            return;
        }

        // Play Mode sealed extrusion needs CPU mesh data.
        EnableReadWriteForOutlineMeshes(showDialog: false);

        try
        {
            for (int i = 0; i < styles.Count; i++)
            {
                if (i % 15 == 0)
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
        Debug.Log($"Mesh Outline: generated {styles.Count} (DontSave helpers; components kept).");
    }

    /// <summary>
    /// Sealed outline needs mesh.GetVertices/GetTriangles. Non-readable meshes fail in Play Mode.
    /// </summary>
    public static int EnableReadWriteForOutlineMeshes(bool showDialog)
    {
        var styles = GetStylesInActiveScene();
        var paths = new HashSet<string>();
        int already = 0;
        int skippedBuiltin = 0;

        for (int i = 0; i < styles.Count; i++)
        {
            MeshFilter filter = styles[i] != null ? styles[i].GetComponent<MeshFilter>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
                continue;

            if (mesh.isReadable)
            {
                already++;
                continue;
            }

            string path = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", System.StringComparison.Ordinal))
            {
                skippedBuiltin++;
                continue;
            }

            paths.Add(path);
        }

        if (paths.Count == 0)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Mesh Outline",
                    already > 0
                        ? $"All outline meshes already readable ({already}). Builtin/skipped: {skippedBuiltin}."
                        : "No importable outline meshes found.",
                    "OK");
            }
            return 0;
        }

        if (showDialog &&
            !EditorUtility.DisplayDialog(
                "Enable Read/Write?",
                $"Will set Read/Write Enabled on {paths.Count} model/mesh assets used by MeshOutlineStyle.\n" +
                "This is required for Play Mode sealed outlines (same as Tool).\n\nReimport may take a minute.",
                "Enable",
                "Cancel"))
        {
            return 0;
        }

        int changed = 0;
        try
        {
            int n = 0;
            foreach (string path in paths)
            {
                n++;
                EditorUtility.DisplayProgressBar("Mesh Outline", "Read/Write… " + path, (float)n / paths.Count);

                ModelImporter modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;
                if (modelImporter != null)
                {
                    if (!modelImporter.isReadable)
                    {
                        modelImporter.isReadable = true;
                        modelImporter.SaveAndReimport();
                        changed++;
                    }
                    continue;
                }

                // Standalone .asset meshes: toggle via SerializedObject if possible.
                var importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                    continue;
                var so = new SerializedObject(importer);
                SerializedProperty readable = so.FindProperty("m_IsReadable");
                if (readable != null && !readable.boolValue)
                {
                    readable.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    importer.SaveAndReimport();
                    changed++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"Mesh Outline: enabled Read/Write on {changed} assets (already readable refs≈{already}, builtin/skipped={skippedBuiltin}).");
        if (showDialog)
            EditorUtility.DisplayDialog("Mesh Outline", $"Enabled Read/Write on {changed} assets.", "OK");
        return changed;
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
            Debug.LogWarning("Mesh Outline: select objects first.");
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
            if (styles[i] == null) continue;
            styles[i].ClearGenerated();
            Undo.DestroyObjectImmediate(styles[i]);
        }

        MarkActiveSceneDirty();
        Debug.Log($"Mesh Outline: removed {styles.Count} components.");
    }

    private static void MarkActiveSceneDirty()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);
    }
}
