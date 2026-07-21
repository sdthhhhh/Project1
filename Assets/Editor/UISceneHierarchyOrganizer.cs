#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UISceneHierarchyOrganizer
{
    [InitializeOnLoadMethod]private static void OrganizeAfterCompile(){EditorApplication.delayCall+=()=>{if(!Application.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode&&(FindSceneObject("UI_ROOT")==null||FindSceneObject("InteractionPromptText")!=null||FindSceneObject("InteractText")!=null))Organize();};}
    [MenuItem("Tools/UI/Organize Complete UI Hierarchy")]
    public static void Organize()
    {
        if(Application.isPlaying)return;
        GameObject root=FindSceneObject("UI_ROOT");if(root==null){root=new GameObject("UI_ROOT");Undo.RegisterCreatedObjectUndo(root,"Create UI Root");}

        GameObject hud=FindSceneObject("GameplayHUDCanvas")??FindSceneObject("HUDCanvas");
        if(hud!=null){hud.name="GameplayHUDCanvas";Undo.SetTransformParent(hud.transform,root.transform,"Group Gameplay HUD");Rename("CrosshairImage","AimCrosshair");GameObject prompt=FindSceneObject("InteractionPromptText")??FindSceneObject("InteractText");if(prompt!=null)Undo.DestroyObjectImmediate(prompt);Rename("StatusText","TemporaryStatusMessage");GameObject oldPanel=FindSceneObject("ItemInspectPanel");if(oldPanel!=null)Undo.DestroyObjectImmediate(oldPanel);}

        GameObject inspect=FindSceneObject("ObjectInspectionCanvas")??FindSceneObject("InspectCanvas");
        if(inspect!=null){inspect.name="ObjectInspectionCanvas";Undo.SetTransformParent(inspect.transform,root.transform,"Group Inspection UI");Rename("InspectPanel","ObjectInspectionOverlay");Rename("Object3DPreview","InspectedObject3DViewport");Rename("DescriptionText","InspectedObjectDescription");Rename("PutBackPrompt","CollectOrCloseControlHint");Rename("QCircle","QKeyIconBackground");Rename("QLabel","QKeyLabel");Rename("PutBackText","CollectOrCloseActionText");Rename("RotatePrompt","RotateControlHint");Rename("ECircle","EKeyIconBackground");Rename("ELabel","EKeyLabel");Rename("RotateText","RotateActionText");}

        GameObject studio=FindSceneObject("InspectPreviewStudio")??FindSceneObject("ObjectInspection3DStudio");if(studio!=null){studio.name="ObjectInspection3DStudio";Undo.SetTransformParent(studio.transform,root.transform,"Group Inspection Studio");Rename("ModelPivot","InspectedModelRotationPivot");Rename("PreviewCamera","ObjectInspectionRenderCamera");Rename("PreviewLight","ObjectInspectionKeyLight");}
        GameObject eventSystem=FindSceneObject("EventSystem");if(eventSystem!=null){eventSystem.name="UIEventSystem";Undo.SetTransformParent(eventSystem.transform,root.transform,"Group UI Event System");}

        MigrateMedicalReports();
        GameObject legacyCanvas=FindSceneObject("Canvas");if(legacyCanvas!=null&&legacyCanvas.transform.Find("BagBackground")!=null)Undo.DestroyObjectImmediate(legacyCanvas);
        GameObject inventory=FindSceneObject("RestorationInventory");if(inventory!=null)Undo.DestroyObjectImmediate(inventory);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();Selection.activeGameObject=root;Debug.Log("UI hierarchy organized under UI_ROOT; obsolete inventory and inspection UI removed.");
    }

    private static void MigrateMedicalReports()
    {
        foreach(MedicalReportItem old in Resources.FindObjectsOfTypeAll<MedicalReportItem>())
        {
            if(!old.gameObject.scene.IsValid())continue;GameObject go=old.gameObject;
            InspectableObject inspectable=go.GetComponent<InspectableObject>();if(inspectable==null){inspectable=Undo.AddComponent<InspectableObject>(go);inspectable.ConfigurePreview(go,"Lin Fang's medical report documents repeated injuries.",Vector3.zero);}
            EvidenceInspectableCollectible collectible=go.GetComponent<EvidenceInspectableCollectible>();if(collectible==null)collectible=Undo.AddComponent<EvidenceInspectableCollectible>(go);collectible.Configure("Lin Fang's medical report collected");
            if(go.GetComponentInChildren<Collider>(true)==null)Undo.AddComponent<BoxCollider>(go);Undo.DestroyObjectImmediate(old);
        }
    }
    private static void Rename(string oldName,string newName){GameObject go=FindSceneObject(oldName);if(go!=null)go.name=newName;}
    private static GameObject FindSceneObject(string name){foreach(GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())if(go.scene.IsValid()&&go.name==name)return go;return null;}
}
#endif
