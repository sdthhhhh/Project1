#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PhotoRestorationSceneInstaller
{
    private const string FamilyPhotoPath="Assets/UI/FamilyPhoto.png";
    [InitializeOnLoadMethod]private static void InstallAfterCompile(){EditorApplication.delayCall+=()=>{if(!Application.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode&&GameObject.Find("Photoes")!=null)Install();};}
    [MenuItem("Tools/Object Inspection/Install Photo Restoration")]
    public static void Install()
    {
        if(Application.isPlaying)return;
        GameObject root=GameObject.Find("Photoes");if(root==null){Debug.LogError("Photo restoration: Photoes was not found.");return;}
        Transform collectible=root.transform.Find("InteractedPhoto (1)");Transform restored=root.transform.Find("InteractedPhoto");
        if(collectible==null||restored==null){Debug.LogError("Photo restoration: InteractedPhoto (1) or InteractedPhoto was not found under Photoes.");return;}
        PhotoRestorationPuzzle puzzle=root.GetComponent<PhotoRestorationPuzzle>();if(puzzle==null)puzzle=Undo.AddComponent<PhotoRestorationPuzzle>(root);
        TMP_Text inventory=CreateOrFindInventory();
        InspectableObject inspectable=collectible.GetComponent<InspectableObject>();bool newInspectable=inspectable==null;if(newInspectable)inspectable=Undo.AddComponent<InspectableObject>(collectible.gameObject);
        if(newInspectable)inspectable.ConfigurePreview(collectible.gameObject,"A displaced photograph. It may belong somewhere nearby.",Vector3.zero);
        Texture2D familyPhoto=AssetDatabase.LoadAssetAtPath<Texture2D>(FamilyPhotoPath);if(familyPhoto!=null)inspectable.SetPreviewPhoto(familyPhoto);
        PhotoCollectible pickup=collectible.GetComponent<PhotoCollectible>();if(pickup==null)pickup=Undo.AddComponent<PhotoCollectible>(collectible.gameObject);pickup.Configure(puzzle);
        PhotoRestorePoint place=restored.GetComponent<PhotoRestorePoint>();if(place==null)place=Undo.AddComponent<PhotoRestorePoint>(restored.gameObject);place.Configure(puzzle);
        EnsureCollider(collectible.gameObject);EnsureCollider(restored.gameObject);
        RemoveOldInteraction<PhotoFrameItem>(collectible.gameObject);RemoveOldInteraction<FramePlacePoint>(restored.gameObject);
        puzzle.Configure(collectible.gameObject,restored.gameObject,inventory);
        EditorUtility.SetDirty(puzzle);EditorUtility.SetDirty(inspectable);EditorUtility.SetDirty(pickup);EditorUtility.SetDirty(place);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
        Debug.Log("Photo restoration installed: Q collects, E restores, and Photoes reveal after placement.");
    }

    private static TMP_Text CreateOrFindInventory()
    {
        TMP_Text existing=FindSceneText("PhotoPickupInventory");if(existing!=null)return existing;
        GameObject hud=GameObject.Find("HUDCanvas");if(hud==null)hud=GameObject.Find("Canvas");if(hud==null){Debug.LogError("Photo restoration: HUDCanvas/Canvas was not found.");return null;}
        GameObject go=new GameObject("PhotoPickupInventory",typeof(RectTransform),typeof(TextMeshProUGUI));Undo.RegisterCreatedObjectUndo(go,"Create Photo Inventory UI");go.transform.SetParent(hud.transform,false);
        RectTransform rect=go.GetComponent<RectTransform>();rect.anchorMin=new Vector2(.72f,.72f);rect.anchorMax=new Vector2(.96f,.82f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        TMP_Text text=go.GetComponent<TMP_Text>();text.text="Interacted Photo";text.fontSize=26;text.alignment=TextAlignmentOptions.MidlineRight;text.color=Color.white;go.SetActive(false);return text;
    }

    private static void EnsureCollider(GameObject root)
    {
        if(root.GetComponentInChildren<Collider>(true)!=null)return;
        BoxCollider box=Undo.AddComponent<BoxCollider>(root);Renderer[] renderers=root.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return;
        Bounds bounds=renderers[0].bounds;for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
        box.center=root.transform.InverseTransformPoint(bounds.center);Vector3 scale=root.transform.lossyScale;box.size=new Vector3(bounds.size.x/Mathf.Max(Mathf.Abs(scale.x),.0001f),bounds.size.y/Mathf.Max(Mathf.Abs(scale.y),.0001f),bounds.size.z/Mathf.Max(Mathf.Abs(scale.z),.0001f));
    }
    private static void RemoveOldInteraction<T>(GameObject go)where T:MonoBehaviour{T old=go.GetComponent<T>();if(old!=null)Undo.DestroyObjectImmediate(old);}
    private static TMP_Text FindSceneText(string name){foreach(TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())if(text.gameObject.scene.IsValid()&&text.name==name)return text;return null;}
}
#endif
