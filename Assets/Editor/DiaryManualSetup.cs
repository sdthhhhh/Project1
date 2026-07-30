using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places Assets/3D/Diary/piece1–4 into the active scene for manual positioning.
/// </summary>
public static class DiaryManualSetup
{
    private const string PiecesRootName = "DiaryFragments";
    private static readonly string[] PiecePaths =
    {
        "Assets/3D/Diary/piece1.fbx",
        "Assets/3D/Diary/piece2.fbx",
        "Assets/3D/Diary/piece3.fbx",
        "Assets/3D/Diary/piece4.fbx",
    };

    [MenuItem("Tools/Diary/Setup Manual Fragments In Scene")]
    public static void SetupManualFragments()
    {
        Transform parent = FindOrCreatePiecesRoot();
        Vector3 anchor = ResolveAnchor();

        int created = 0;
        for (int i = 0; i < PiecePaths.Length; i++)
        {
            int id = i + 1;
            string pieceName = "DiaryFragment0" + id;
            Transform existing = parent.Find(pieceName);
            if (existing != null)
            {
                EnsureFragmentComponents(existing.gameObject, id);
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PiecePaths[i]);
            if (prefab == null)
            {
                Debug.LogError("Diary setup: missing asset " + PiecePaths[i]);
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = pieceName;
            // Spread around the diary board so you can drag each piece to its final spot.
            float angle = i * 90f * Mathf.Deg2Rad;
            instance.transform.position = anchor + new Vector3(Mathf.Cos(angle) * 0.45f, 0.02f, Mathf.Sin(angle) * 0.45f);
            instance.transform.rotation = Quaternion.identity;
            EnsureFragmentComponents(instance, id);
            created++;
        }

        // Remove legacy random spawn area if present.
        GameObject legacy = GameObject.Find("BedroomFragmentSpawnArea");
        if (legacy != null)
        {
            Object.DestroyImmediate(legacy);
            Debug.Log("Diary setup: removed BedroomFragmentSpawnArea (random spawn).");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = parent.gameObject;
        Debug.Log($"Diary setup: manual fragments ready under '{PiecesRootName}' (new={created}). Drag pieces in Scene view to place them.");
    }

    private static Transform FindOrCreatePiecesRoot()
    {
        GameObject root = GameObject.Find(PiecesRootName);
        if (root == null)
        {
            GameObject interactables = GameObject.Find("INTERACTABLES");
            root = new GameObject(PiecesRootName);
            if (interactables != null)
                root.transform.SetParent(interactables.transform, false);
            Undo.RegisterCreatedObjectUndo(root, "Create DiaryFragments");
        }

        return root.transform;
    }

    private static Vector3 ResolveAnchor()
    {
        GameObject board = GameObject.Find("DiaryReconstructionBoard");
        if (board != null)
            return board.transform.position;
        GameObject bedroom = GameObject.Find("Bedroom");
        if (bedroom != null)
            return bedroom.transform.position;
        return Vector3.zero;
    }

    private static void EnsureFragmentComponents(GameObject go, int fragmentId)
    {
        DiaryFragment frag = go.GetComponent<DiaryFragment>();
        if (frag == null)
            frag = Undo.AddComponent<DiaryFragment>(go);

        SerializedObject so = new SerializedObject(frag);
        so.FindProperty("fragmentId").intValue = fragmentId;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (go.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider box = Undo.AddComponent<BoxCollider>(go);
            MeshFilter filter = go.GetComponentInChildren<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Bounds b = filter.sharedMesh.bounds;
                box.center = b.center;
                Vector3 size = b.size;
                if (size.x < 0.05f) size.x = 0.05f;
                if (size.y < 0.02f) size.y = 0.02f;
                if (size.z < 0.05f) size.z = 0.05f;
                box.size = size;
            }
        }

        EditorUtility.SetDirty(go);
    }
}
