#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class UISceneHierarchyOrganizer
{
    private const string SessionKey = "UIHierarchy.Organized.v8";

    static UISceneHierarchyOrganizer() { EditorApplication.delayCall += OrganizeOnce; }

    private static void OrganizeOnce()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        Organize();
    }

    [MenuItem("Tools/UI/Organize And Install Permanent Diary UI")]
    public static void Organize()
    {
        if (Application.isPlaying) { Debug.LogWarning("Stop Play Mode before organizing scene UI."); return; }
        if (!SceneManager.GetActiveScene().IsValid()) return;

        GameObject root = FindSceneObject("UI_ROOT");
        if (root == null)
        {
            root = new GameObject("UI_ROOT");
            Undo.RegisterCreatedObjectUndo(root, "Create UI Root");
        }

        GameObject hud = FindSceneObject("GameplayHUDCanvas") ?? FindSceneObject("HUDCanvas");
        RenameAndParent(hud, "GameplayHUDCanvas", root.transform);
        Rename("CrosshairImage", "AimCrosshair");
        Rename("StatusText", "GameplayStatusMessage");

        // The project no longer displays a separate Press-E prompt. InteractionUI is null-safe.
        DeleteSceneObject("InteractText");
        DeleteSceneObject("InteractionPromptText");

        GameObject inspection = FindSceneObject("ObjectInspectionCanvas") ?? FindSceneObject("InspectCanvas");
        RenameAndParent(inspection, "ObjectInspectionCanvas", root.transform);
        GameObject duplicateInspection = FindSceneObject("InspectCanvas");
        if (duplicateInspection != null && duplicateInspection != inspection) Undo.DestroyObjectImmediate(duplicateInspection);
        Rename("InspectPanel", "ObjectInspectionOverlay");
        Rename("DescriptionText", "ObjectDescriptionText");
        Rename("PutBackText", "PutBackActionText");
        Rename("RotateText", "RotateActionText");

        GameObject collectibleInspection = FindSceneObject("CollectibleInspectionCanvas");
        RenameAndParent(collectibleInspection, "CollectibleInspectionCanvas", root.transform);

        GameObject doorPassword = FindSceneObject("DoorPasswordCanvas");
        RenameAndParent(doorPassword, "DoorPasswordCanvas", root.transform);

        GameObject studio = FindSceneObject("ObjectInspection3DStudio") ?? FindSceneObject("InspectPreviewStudio");
        RenameAndParent(studio, "ObjectInspection3DStudio", root.transform);
        GameObject duplicateStudio = FindSceneObject("InspectPreviewStudio");
        if (duplicateStudio != null && duplicateStudio != studio) Undo.DestroyObjectImmediate(duplicateStudio);
        Rename("PreviewCamera", "ObjectPreviewCamera");
        Rename("ModelPivot", "InspectedModelPivot");
        Rename("PreviewLight", "ObjectPreviewLight");

        GameObject collectibleStudio = FindSceneObject("CollectibleInspection3DStudio");
        RenameAndParent(collectibleStudio, "CollectibleInspection3DStudio", root.transform);

        GameObject transition = FindSceneObject("TimedSceneTransitionCanvas");
        RenameAndParent(transition, "TimedSceneTransitionCanvas", root.transform);
        Rename("CountdownTimerBackground", "SceneCountdownTimerBackground");
        Rename("CountdownTimerText", "SceneCountdownTimerText");
        Rename("HandLeft", "EyeCoveringLeftHand");
        Rename("HandRight", "EyeCoveringRightHand");

        GameObject eventSystem = FindSceneObject("UIEventSystem") ?? FindSceneObject("EventSystem");
        RenameAndParent(eventSystem, "UIEventSystem", root.transform);

        // This inactive canvas only contains the obsolete BagBackground. The active restoration
        // system creates its own RestorationInventory, so keeping both produces duplicate UI.
        GameObject obsoleteBagCanvas = FindSceneObject("Canvas");
        if (obsoleteBagCanvas != null && obsoleteBagCanvas.GetComponent<Canvas>() != null && !obsoleteBagCanvas.activeSelf)
            Undo.DestroyObjectImmediate(obsoleteBagCanvas);

        // Still used by MedicalReportItem and PhotoFrameItem, so retain but make its legacy role clear.
        Rename("ItemInspectPanel", "LegacyEvidenceInspectionPanel");
        Rename("InspectImage", "LegacyEvidenceImage");
        Rename("InspectText", "LegacyEvidenceDescriptionText");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        DiaryWhiteboxSceneInstaller.InstallPermanentPuzzleUI();
        DiaryWhiteboxSceneInstaller.ShowPuzzleForEditing();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = FindSceneObject("DiaryReconstructionPuzzlePanel") ?? root;
        Debug.Log("UI hierarchy organized under UI_ROOT. Obsolete bag and interaction prompt UI removed; permanent Reconstruction Diary UI installed.");
    }

    private static void Rename(string oldName, string newName)
    {
        GameObject go = FindSceneObject(newName) ?? FindSceneObject(oldName);
        if (go != null) go.name = newName;
    }

    private static void RenameAndParent(GameObject go, string newName, Transform parent)
    {
        if (go == null) return;
        go.name = newName;
        if (go.transform.parent != parent) Undo.SetTransformParent(go.transform, parent, "Group " + newName);
    }

    private static void DeleteSceneObject(string name)
    {
        GameObject go = FindSceneObject(name);
        if (go != null) Undo.DestroyObjectImmediate(go);
    }

    private static GameObject FindSceneObject(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.scene.IsValid() && go.name == name) return go;
        return null;
    }
}
#endif
