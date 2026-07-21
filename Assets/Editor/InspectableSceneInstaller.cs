#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class InspectableSceneInstaller
{
    private const string IconPath="Assets/UI/GeneratedMagnifier.png";

    [InitializeOnLoadMethod]
    private static void UpgradeOldPromptAfterCompile()
    {
        EditorApplication.delayCall+=()=>
        {
            if(Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode)return;
            CreateMagnifierSprite();
            GameObject canvas=GameObject.Find("ObjectInspectionCanvas");if(canvas==null)canvas=GameObject.Find("InspectCanvas");
            Transform prompt=canvas!=null?canvas.transform.Find("ObjectInspectionOverlay/CollectOrCloseControlHint"):null;if(prompt==null&&canvas!=null)prompt=canvas.transform.Find("InspectPanel/PutBackPrompt");
            Transform oldFrame=canvas!=null?canvas.transform.Find("ObjectInspectionOverlay/ObjectImageFrame"):null;if(oldFrame==null&&canvas!=null)oldFrame=canvas.transform.Find("InspectPanel/ObjectImageFrame");
            Transform legacyImage=canvas!=null?canvas.transform.Find("ObjectInspectionOverlay/ObjectSpriteUI"):null;if(legacyImage==null&&canvas!=null)legacyImage=canvas.transform.Find("InspectPanel/ObjectSpriteUI");
            Transform descriptionFrame=canvas!=null?canvas.transform.Find("ObjectInspectionOverlay/DescriptionFrame"):null;if(descriptionFrame==null&&canvas!=null)descriptionFrame=canvas.transform.Find("InspectPanel/DescriptionFrame");
            if((prompt!=null&&prompt.Find("QKeyIconBackground")==null&&prompt.Find("QCircle")==null)||oldFrame!=null||legacyImage!=null||descriptionFrame!=null||(GameObject.Find("ObjectInspection3DStudio")==null&&GameObject.Find("InspectPreviewStudio")==null))Install();
        };
    }

    [MenuItem("Tools/Object Inspection/Install Scene UI")]
    public static void Install()
    {
        if(Application.isPlaying){Debug.LogWarning("Stop Play Mode before installing the inspection UI.");return;}
        GameObject old=GameObject.Find("ObjectInspectionCanvas");if(old==null)old=GameObject.Find("InspectCanvas");
        InspectableUIController controller;
        if(old==null) controller=CreateCanvas(); else controller=UpgradeExistingCanvas(old);
        if(controller==null){Debug.LogError("Inspect installer: InspectCanvas controller could not be created.");return;}

        PlayerInteraction interaction=Object.FindObjectOfType<PlayerInteraction>();
        if(interaction==null){Debug.LogError("Inspect installer: PlayerInteraction was not found.");return;}
        Image crosshair=FindSceneObject<Image>("CrosshairImage");
        if(crosshair==null){Debug.LogError("Inspect installer: CrosshairImage was not found.");return;}
        Sprite icon=CreateMagnifierSprite();
        InspectableRaycaster raycaster=interaction.GetComponent<InspectableRaycaster>();
        if(raycaster==null)raycaster=Undo.AddComponent<InspectableRaycaster>(interaction.gameObject);
        raycaster.Configure(crosshair,icon,controller);
        EditorUtility.SetDirty(raycaster);EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject=controller.gameObject;
        Debug.Log("Object Inspection installed. InspectCanvas is permanent and editable in the Hierarchy.");
    }

    [MenuItem("Tools/Object Inspection/Mark Selected Objects Inspectable")]
    public static void MarkSelected()
    {
        foreach(GameObject go in Selection.gameObjects)
        {
            if(go.GetComponent<InspectableObject>()==null)Undo.AddComponent<InspectableObject>(go);
            if(go.GetComponentInChildren<Collider>(true)==null)Undo.AddComponent<BoxCollider>(go);
            EditorUtility.SetDirty(go);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static InspectableUIController CreateCanvas()
    {
        GameObject canvasGo=new GameObject("ObjectInspectionCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(InspectableUIController));
        Undo.RegisterCreatedObjectUndo(canvasGo,"Create InspectCanvas");
        Canvas canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=300;
        CanvasScaler scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        GameObject panel=UI("ObjectInspectionOverlay",canvasGo.transform,new Color(.025f,.022f,.02f,.55f));Stretch(panel.GetComponent<RectTransform>(),0,0,1,1);
        GameObject imageGo=RawUI("InspectedObject3DViewport",panel.transform);Stretch(imageGo.GetComponent<RectTransform>(),.08f,.13f,.64f,.88f);
        RawImage image=imageGo.GetComponent<RawImage>();
        TMP_Text description=Text("InspectedObjectDescription",panel.transform,"A detail that may be worth remembering.",30,TextAlignmentOptions.MidlineLeft);Stretch(description.rectTransform,.70f,.32f,.92f,.74f);
        TMP_Text prompt=CreatePutBackPrompt(panel.transform);
        TMP_Text rotate=CreateRotatePrompt(panel.transform);
        CreatePreviewStudio(out Camera previewCamera,out Transform previewPivot);
        InspectableUIController controller=canvasGo.GetComponent<InspectableUIController>();controller.Configure(panel,image,description,prompt,rotate,previewCamera,previewPivot);
        return controller;
    }

    private static InspectableUIController UpgradeExistingCanvas(GameObject canvasGo)
    {
        InspectableUIController controller=canvasGo.GetComponent<InspectableUIController>();
        Transform panel=canvasGo.transform.Find("ObjectInspectionOverlay");if(panel==null)panel=canvasGo.transform.Find("InspectPanel");
        if(controller==null||panel==null)return controller;
        Transform oldPrompt=panel.Find("CollectOrCloseControlHint");if(oldPrompt==null)oldPrompt=panel.Find("PutBackPrompt");
        if(oldPrompt!=null)Undo.DestroyObjectImmediate(oldPrompt.gameObject);
        TMP_Text prompt=CreatePutBackPrompt(panel);
        Transform directImage=panel.Find("InspectedObject3DViewport");if(directImage==null)directImage=panel.Find("Object3DPreview");
        if(directImage==null)directImage=panel.Find("ObjectSpriteUI");
        Transform oldFrame=panel.Find("ObjectImageFrame");
        if(directImage==null&&oldFrame!=null)
        {
            directImage=oldFrame.Find("ObjectLargeImage");
            if(directImage!=null)
            {
                Undo.SetTransformParent(directImage,panel,"Remove Object Image Frame");
                directImage.name="InspectedObject3DViewport";
                Stretch(directImage.GetComponent<RectTransform>(),.08f,.13f,.64f,.88f);
            }
            Undo.DestroyObjectImmediate(oldFrame.gameObject);
        }
        if(directImage==null)
        {
            GameObject imageGo=RawUI("InspectedObject3DViewport",panel);
            Stretch(imageGo.GetComponent<RectTransform>(),.08f,.13f,.64f,.88f);
            directImage=imageGo.transform;
        }
        Image oldImage=directImage.GetComponent<Image>();if(oldImage!=null)Undo.DestroyObjectImmediate(oldImage);
        RawImage image=directImage.GetComponent<RawImage>();if(image==null)image=Undo.AddComponent<RawImage>(directImage.gameObject);directImage.name="InspectedObject3DViewport";image.color=Color.white;
        Transform descriptionTransform=panel.Find("InspectedObjectDescription");if(descriptionTransform==null)descriptionTransform=panel.Find("DescriptionText");
        Transform descriptionFrame=panel.Find("DescriptionFrame");
        if(descriptionTransform==null&&descriptionFrame!=null)
        {
            descriptionTransform=descriptionFrame.Find("DescriptionText");
            if(descriptionTransform!=null){Undo.SetTransformParent(descriptionTransform,panel,"Remove Description Background");Stretch(descriptionTransform.GetComponent<RectTransform>(),.70f,.32f,.92f,.74f);}
        }
        if(descriptionFrame!=null)Undo.DestroyObjectImmediate(descriptionFrame.gameObject);
        TMP_Text description=descriptionTransform!=null?descriptionTransform.GetComponent<TMP_Text>():null;
        if(description==null){description=Text("InspectedObjectDescription",panel,"A detail that may be worth remembering.",30,TextAlignmentOptions.MidlineLeft);Stretch(description.rectTransform,.70f,.32f,.92f,.74f);}
        Transform oldRotate=panel.Find("RotateControlHint");if(oldRotate==null)oldRotate=panel.Find("RotatePrompt");if(oldRotate!=null)Undo.DestroyObjectImmediate(oldRotate.gameObject);
        TMP_Text rotate=CreateRotatePrompt(panel);
        CreatePreviewStudio(out Camera previewCamera,out Transform previewPivot);
        controller.Configure(panel.gameObject,image,description,prompt,rotate,previewCamera,previewPivot);
        return controller;
    }

    private static TMP_Text CreatePutBackPrompt(Transform panel)
    {
        GameObject root=new GameObject("CollectOrCloseControlHint",typeof(RectTransform));root.transform.SetParent(panel,false);
        Stretch(root.GetComponent<RectTransform>(),.76f,.07f,.94f,.16f);
        GameObject circle=UI("QKeyIconBackground",root.transform,new Color(.92f,.88f,.78f,1f));
        RectTransform circleRect=circle.GetComponent<RectTransform>();circleRect.anchorMin=new Vector2(0,.5f);circleRect.anchorMax=new Vector2(0,.5f);circleRect.sizeDelta=new Vector2(54,54);circleRect.anchoredPosition=new Vector2(27,0);
        circle.GetComponent<Image>().sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        TMP_Text q=Text("QKeyLabel",circle.transform,"Q",27,TextAlignmentOptions.Center);Stretch(q.rectTransform,0,0,1,1);q.color=new Color(.08f,.065f,.05f,1f);q.fontStyle=FontStyles.Bold;
        TMP_Text label=Text("CollectOrCloseActionText",root.transform,"Put Back",24,TextAlignmentOptions.MidlineLeft);label.rectTransform.anchorMin=new Vector2(0,0);label.rectTransform.anchorMax=new Vector2(1,1);label.rectTransform.offsetMin=new Vector2(72,0);label.rectTransform.offsetMax=Vector2.zero;
        return label;
    }

    private static TMP_Text CreateRotatePrompt(Transform panel)
    {
        GameObject root=new GameObject("RotateControlHint",typeof(RectTransform));root.transform.SetParent(panel,false);Stretch(root.GetComponent<RectTransform>(),.56f,.07f,.74f,.16f);
        GameObject circle=UI("EKeyIconBackground",root.transform,new Color(.92f,.88f,.78f,1f));RectTransform cr=circle.GetComponent<RectTransform>();cr.anchorMin=cr.anchorMax=new Vector2(0,.5f);cr.sizeDelta=new Vector2(54,54);cr.anchoredPosition=new Vector2(27,0);circle.GetComponent<Image>().sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        TMP_Text e=Text("EKeyLabel",circle.transform,"E",27,TextAlignmentOptions.Center);Stretch(e.rectTransform,0,0,1,1);e.color=new Color(.08f,.065f,.05f,1f);e.fontStyle=FontStyles.Bold;
        TMP_Text label=Text("RotateActionText",root.transform,"Rotate",24,TextAlignmentOptions.MidlineLeft);label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(72,0);label.rectTransform.offsetMax=Vector2.zero;return label;
    }

    private static void CreatePreviewStudio(out Camera camera,out Transform pivot)
    {
        GameObject studio=GameObject.Find("ObjectInspection3DStudio");if(studio==null)studio=GameObject.Find("InspectPreviewStudio");
        if(studio==null){studio=new GameObject("ObjectInspection3DStudio");Undo.RegisterCreatedObjectUndo(studio,"Create 3D Preview Studio");studio.transform.position=new Vector3(1000,1000,1000);}
        Transform pivotTransform=studio.transform.Find("InspectedModelRotationPivot");if(pivotTransform==null)pivotTransform=studio.transform.Find("ModelPivot");if(pivotTransform==null){GameObject p=new GameObject("InspectedModelRotationPivot");p.transform.SetParent(studio.transform,false);pivotTransform=p.transform;}
        Transform cameraTransform=studio.transform.Find("ObjectInspectionRenderCamera");if(cameraTransform==null)cameraTransform=studio.transform.Find("PreviewCamera");
        if(cameraTransform==null){GameObject c=new GameObject("ObjectInspectionRenderCamera",typeof(Camera));c.transform.SetParent(studio.transform,false);cameraTransform=c.transform;cameraTransform.localPosition=new Vector3(0,0,-4);cameraTransform.localRotation=Quaternion.identity;}
        camera=cameraTransform.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.022f,.02f,0);camera.cullingMask=1<<31;camera.fieldOfView=35;camera.allowHDR=false;camera.allowMSAA=false;camera.enabled=false;
        Transform lightTransform=studio.transform.Find("ObjectInspectionKeyLight");if(lightTransform==null)lightTransform=studio.transform.Find("PreviewLight");if(lightTransform==null){GameObject l=new GameObject("ObjectInspectionKeyLight",typeof(Light));l.transform.SetParent(studio.transform,false);lightTransform=l.transform;lightTransform.localRotation=Quaternion.Euler(35,-30,0);Light light=l.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;}
        pivot=pivotTransform;
    }

    private static Sprite CreateMagnifierSprite()
    {
        if(!Directory.Exists("Assets/UI"))Directory.CreateDirectory("Assets/UI");
        const int size=64;Texture2D tex=new Texture2D(size,size,TextureFormat.RGBA32,false);Color clear=new Color(0,0,0,0),white=Color.white;
        Color[] pixels=new Color[size*size];for(int i=0;i<pixels.Length;i++)pixels[i]=clear;
        Vector2 center=new Vector2(26,37);float radius=15;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {float d=Vector2.Distance(new Vector2(x,y),center);if(d>radius-4f&&d<radius+4f)pixels[y*size+x]=white;}
        for(int i=0;i<23;i++)for(int w=-4;w<=4;w++){int x=37+i,y=26-i+w;if(x>=0&&x<size&&y>=0&&y<size)pixels[y*size+x]=white;}
        tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(IconPath,tex.EncodeToPNG());Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(IconPath,ImportAssetOptions.ForceUpdate);
        TextureImporter importer=(TextureImporter)AssetImporter.GetAtPath(IconPath);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=64;importer.alphaIsTransparency=true;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
    }

    private static GameObject UI(string name,Transform parent,Color color)
    {GameObject go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=color;return go;}
    private static GameObject RawUI(string name,Transform parent)
    {GameObject go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));go.transform.SetParent(parent,false);go.GetComponent<RawImage>().color=Color.white;return go;}
    private static TMP_Text Text(string name,Transform parent,string value,float size,TextAlignmentOptions alignment)
    {GameObject go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);TMP_Text t=go.GetComponent<TMP_Text>();t.text=value;t.fontSize=size;t.alignment=alignment;t.color=new Color(.92f,.88f,.78f);return t;}
    private static void Stretch(RectTransform r,float x1,float y1,float x2,float y2){r.anchorMin=new Vector2(x1,y1);r.anchorMax=new Vector2(x2,y2);r.offsetMin=r.offsetMax=Vector2.zero;}
    private static T FindSceneObject<T>(string name)where T:Component
    {foreach(T item in Resources.FindObjectsOfTypeAll<T>())if(item.gameObject.scene.IsValid()&&item.gameObject.name==name)return item;return null;}
}
#endif
