#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class DiaryWhiteboxSceneInstaller
{
    private const string SessionKey = "DiaryWhitebox.BoardInstallAttempted";

    static DiaryWhiteboxSceneInstaller()
    {
        EditorApplication.delayCall += InstallOnce;
    }

    private static void InstallOnce()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        InstallBoard();
    }

    [MenuItem("Tools/Diary Whitebox/Install Board In Bedroom Shelf1")]
    public static void InstallBoard()
    {
        if (Application.isPlaying) { Debug.LogWarning("Stop Play Mode before installing DiaryReconstructionBoard."); return; }
        GameObject existing = GameObject.Find("DiaryReconstructionBoard");
        if (existing != null) { Selection.activeGameObject = existing; Debug.Log("DiaryReconstructionBoard already exists."); return; }

        GameObject bedroom = GameObject.Find("Bedroom");
        if (bedroom == null) { Debug.LogError("Diary installer: Bedroom was not found in the open scene."); return; }
        Transform shelf = FindShelf1(bedroom.transform);
        if (shelf == null) { Debug.LogError("Diary installer: Shelf1 was not found under Bedroom. Check the hierarchy name, then use the Tools menu again."); return; }

        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(board, "Create Diary Reconstruction Board");
        board.name = "DiaryReconstructionBoard";
        board.transform.SetParent(shelf, false);
        board.transform.localPosition = Vector3.zero;
        board.transform.localRotation = Quaternion.identity;
        board.transform.localScale = new Vector3(.62f, .035f, .82f);
        board.AddComponent<BedroomDesk>();

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = "DiaryBoard_Whitebox_Material", color = new Color(.28f, .08f, .055f) };
        board.GetComponent<Renderer>().sharedMaterial = material;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = board;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("DiaryReconstructionBoard was created under Bedroom/Shelf1 and saved. Adjust its Transform directly in the scene.");
    }

    private static Transform FindShelf1(Transform bedroom)
    {
        foreach (Transform t in bedroom.GetComponentsInChildren<Transform>(true))
        {
            string normalized = t.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            if (normalized == "shelf1") return t;
        }
        return null;
    }
}
#endif
