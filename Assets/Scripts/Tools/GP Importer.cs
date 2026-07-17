using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

public class GPencilImporterWindow : EditorWindow
{
    private const string DefaultMaterialPath = "Assets/3D/Material/GPLineMaterial.mat";
    private const string PrefKeyMaterial = "GPImporter_LineMaterial";

    private string jsonFolderPath = "Assets/StreamingAssets";
    private string prefabOutputPath = "Assets/GPPrefab";
    private Material lineMaterial;
    private float lineWidth = 0.02f;
    private int endCapVertices = 5;


    [MenuItem("Tools/GPencil Importer")]
    public static void ShowWindow()
    {
        GetWindow<GPencilImporterWindow>("GPencil Importer");
    }

    private void OnEnable()
    {
        string savedGuid = EditorPrefs.GetString(PrefKeyMaterial, string.Empty);
        if (!string.IsNullOrEmpty(savedGuid))
        {
            string savedPath = AssetDatabase.GUIDToAssetPath(savedGuid);
            if (!string.IsNullOrEmpty(savedPath))
                lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(savedPath);
        }

        if (lineMaterial == null)
            lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
    }

    private void OnGUI()
    {
        GUILayout.Label("Grease Pencil JSON Importer", EditorStyles.boldLabel);

        jsonFolderPath = EditorGUILayout.TextField("JSON Folder", jsonFolderPath);
        prefabOutputPath = EditorGUILayout.TextField("Prefab Output Path", prefabOutputPath);

        EditorGUI.BeginChangeCheck();
        lineMaterial = (Material)EditorGUILayout.ObjectField("Line Material", lineMaterial, typeof(Material), false);
        if (EditorGUI.EndChangeCheck())
        {
            string guid = lineMaterial != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(lineMaterial)) : string.Empty;
            EditorPrefs.SetString(PrefKeyMaterial, guid);
        }

        lineWidth = EditorGUILayout.FloatField("Line Width", lineWidth);
        endCapVertices = EditorGUILayout.IntSlider("End Cap Vertices", endCapVertices, 0, 32);

        if (lineMaterial == null)
            EditorGUILayout.HelpBox("未指定 Line Material 时，线条会使用 Unity 默认材质（通常为洋红色）。", MessageType.Warning);
        else if (lineMaterial.shader != null && !lineMaterial.shader.name.Contains("GPLine"))
            EditorGUILayout.HelpBox("Grease Pencil 线条请使用 GPLineMaterial（Custom/URP/GPLine）。OutLineShader 是给 3D 模型 Mesh 描边用的，不能用于 LineRenderer。", MessageType.Info);

        GUILayout.Space(10);

        if (GUILayout.Button("Import All JSON Files"))
        {
            ImportAll();
        }

        EditorGUI.BeginDisabledGroup(lineMaterial == null);
        if (GUILayout.Button("Apply Material to Existing Prefabs"))
        {
            ApplyMaterialToExistingPrefabs();
        }
        EditorGUI.EndDisabledGroup();
    }

    private void ImportAll()
    {
        if (!Directory.Exists(jsonFolderPath))
        {
            Debug.LogError("❌ JSON folder does not exist: " + jsonFolderPath);
            return;
        }

        if (!Directory.Exists(prefabOutputPath))
            Directory.CreateDirectory(prefabOutputPath);

        string[] jsonFiles = Directory.GetFiles(jsonFolderPath, "*.json");
        if (jsonFiles.Length == 0)
        {
            Debug.LogWarning("⚠ No JSON files found.");
            return;
        }

        foreach (var file in jsonFiles)
        {
            ImportSingle(file);
        }

        AssetDatabase.Refresh();
        Debug.Log("✅ All JSON files imported successfully!");
    }

    private void ImportSingle(string path)
    {
        string jsonText = File.ReadAllText(path);

        JObject json;
        try
        {
            json = JObject.Parse(jsonText);
        }
        catch
        {
            Debug.LogError("❌ Failed to parse JSON: " + path);
            return;
        }

        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(path));

        JArray layers = (JArray)json["layers"];
        if (layers == null)
        {
            Debug.LogWarning("⚠ No layers found in: " + path);
            DestroyImmediate(root);
            return;
        }

        foreach (var layer in layers)
        {
            string layerName = layer["name"]?.ToString() ?? "Layer";
            GameObject layerObj = new GameObject(layerName);
            layerObj.transform.parent = root.transform;

            JArray frames = (JArray)layer["frames"];
            if (frames == null) continue;

            foreach (var frame in frames)
            {
                JArray strokes = (JArray)frame["strokes"];
                if (strokes == null) continue;

                foreach (var stroke in strokes)
                {
                    JArray points = (JArray)stroke["points"];
                    if (points == null || points.Count < 2) continue;

                    GameObject strokeObj = new GameObject("Stroke");
                    strokeObj.transform.parent = layerObj.transform;

                    LineRenderer lr = strokeObj.AddComponent<LineRenderer>();
                    if (lineMaterial != null)
                        lr.sharedMaterial = lineMaterial;
                    lr.startWidth = lineWidth;
                    lr.endWidth = lineWidth;
                    lr.positionCount = points.Count;
                    lr.useWorldSpace = false;
                    lr.numCapVertices = endCapVertices;

                    for (int i = 0; i < points.Count; i++)
                    {
                        lr.SetPosition(i, BlenderPointToUnity((JArray)points[i]));
                    }

                    if (stroke["color"] != null)
                    {
                        JArray c = (JArray)stroke["color"];
                        Color col = new Color(
                            c[0].ToObject<float>(),
                            c[1].ToObject<float>(),
                            c[2].ToObject<float>(),
                            c[3].ToObject<float>()
                        );
                        lr.startColor = col;
                        lr.endColor = col;
                    }
                }
            }
        }

        string fileName = Path.GetFileNameWithoutExtension(path);
        string prefabPath = $"{prefabOutputPath}/{fileName}.prefab";

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root);

        Debug.Log("✅ Imported: " + fileName);
    }

    private void ApplyMaterialToExistingPrefabs()
    {
        if (lineMaterial == null)
        {
            Debug.LogError("❌ 请先指定 Line Material。");
            return;
        }

        if (!Directory.Exists(prefabOutputPath))
        {
            Debug.LogError("❌ Prefab folder does not exist: " + prefabOutputPath);
            return;
        }

        string[] prefabPaths = Directory.GetFiles(prefabOutputPath, "*.prefab", SearchOption.AllDirectories);
        if (prefabPaths.Length == 0)
        {
            Debug.LogWarning("⚠ No prefabs found in: " + prefabOutputPath);
            return;
        }

        int updated = 0;
        foreach (string prefabPath in prefabPaths)
        {
            string assetPath = prefabPath.Replace('\\', '/');
            if (!assetPath.StartsWith("Assets/"))
            {
                int assetsIndex = assetPath.IndexOf("Assets/");
                if (assetsIndex >= 0)
                    assetPath = assetPath.Substring(assetsIndex);
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
            bool changed = false;

            foreach (LineRenderer lr in prefabRoot.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lr.sharedMaterial != lineMaterial)
                {
                    lr.sharedMaterial = lineMaterial;
                    changed = true;
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                updated++;
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"✅ Applied material to {updated} prefab(s).");
    }

    private static Vector3 BlenderPointToUnity(JArray p)
    {
        float bx = p[0].ToObject<float>();
        float by = p[1].ToObject<float>();
        float bz = p[2].ToObject<float>();
        // Blender (X,Y,Z) -> Unity, including 180° Y flip to match FBX mesh orientation
        return new Vector3(-bx, bz, -by);
    }
}
