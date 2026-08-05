#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Restores Photoes / paintingmodel to comic look:
/// OutlineBody fill + Red ThinSheet outline (not grey Lit).
/// Menu: BlindGame/Restore Photo Outlines
/// </summary>
public static class RestorePhotoOutlines
{
    [MenuItem("BlindGame/Restore Photo Outlines")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Stop Play Mode first.");
            return;
        }

        Color body = new Color(0f, 0f, 0f, 1f);
        int total = 0, shells = 0;
        MeshOutlineStyle[] styles = Object.FindObjectsOfType<MeshOutlineStyle>(true);
        for (int i = 0; i < styles.Length; i++)
        {
            MeshOutlineStyle mos = styles[i];
            if (mos == null) continue;
            string n = mos.gameObject.name;
            if (n == "OutlineShell" || n == "OutlineCreases") continue;
            if (!IsPhoto(mos.transform)) continue;

            Undo.RegisterCompleteObjectUndo(mos.gameObject, "Restore photo comic outline");
            SerializedObject so = new SerializedObject(mos);
            so.FindProperty("preserveOriginalMaterials").boolValue = false;
            so.FindProperty("tone").enumValueIndex = 2; // Red
            so.FindProperty("silhouetteMode").enumValueIndex = 2; // ThinSheet
            so.FindProperty("drawSilhouette").boolValue = true;
            so.FindProperty("drawHardEdges").boolValue = true;
            so.FindProperty("scaleWidthToBounds").boolValue = true;
            so.FindProperty("outlineWidthFactor").floatValue = 0.02f;
            so.FindProperty("creaseWidthFactor").floatValue = 0.012f;
            so.FindProperty("bodyColor").colorValue = body;
            so.FindProperty("buildOnAwake").boolValue = true;
            so.ApplyModifiedProperties();
            mos.Rebuild();
            EditorUtility.SetDirty(mos);
            total++;
            if (mos.transform.Find("OutlineShell") != null) shells++;
        }

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Restore Photo Outlines (comic Red ThinSheet): fixed={total} withShell={shells}");
    }

    private static bool IsPhoto(Transform tr)
    {
        if (tr.gameObject.name.IndexOf("paintingmodel", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        Transform t = tr;
        while (t != null)
        {
            if (t.name.IndexOf("Photoes", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }
        return false;
    }
}
#endif
