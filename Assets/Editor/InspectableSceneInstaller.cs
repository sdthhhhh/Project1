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
    private const string MagnifierIconPath="Assets/UI/GeneratedMagnifier.png";

    [InitializeOnLoadMethod]
    private static void InstallAfterLeavingPlayMode()
    {
        EditorApplication.playModeStateChanged-=OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged+=OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Install;
    }

    [InitializeOnLoadMethod]
    private static void UpgradeOldPromptAfterCompile()
    {
        EditorApplication.delayCall+=()=>
        {
            if(Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode)return;
            CreateMagnifierSprite();
            GameObject canvas=GameObject.Find("ObjectInspectionCanvas")??GameObject.Find("InspectCanvas");
            GameObject collectibleCanvas=GameObject.Find("CollectibleInspectionCanvas");
            Transform panel=canvas!=null?(canvas.transform.Find("ObjectInspectionOverlay")??canvas.transform.Find("InspectPanel")):null;
            Transform prompt=panel!=null?panel.Find("PutBackPrompt"):null;
            Transform oldFrame=panel!=null?panel.Find("ObjectImageFrame"):null;
            Transform legacyImage=panel!=null?panel.Find("ObjectSpriteUI"):null;
            Transform descriptionFrame=panel!=null?panel.Find("DescriptionFrame"):null;
            InspectableRaycaster raycaster=Object.FindObjectOfType<InspectableRaycaster>();
            bool handNeedsUpgrade=false;
            if(raycaster!=null)
            {
                SerializedObject serializedRaycaster=new SerializedObject(raycaster);
                SerializedProperty magnifierProperty=serializedRaycaster.FindProperty("magnifierSprite");
                SerializedProperty collectibleUIProperty=serializedRaycaster.FindProperty("collectibleInspectUI");
                handNeedsUpgrade=magnifierProperty==null||magnifierProperty.objectReferenceValue==null||collectibleUIProperty==null||collectibleUIProperty.objectReferenceValue==null;
            }
            if(canvas==null||collectibleCanvas==null||panel==null||raycaster==null||handNeedsUpgrade||(prompt!=null&&prompt.Find("QCircle")==null)||oldFrame!=null||legacyImage!=null||descriptionFrame!=null||(GameObject.Find("ObjectInspection3DStudio")??GameObject.Find("InspectPreviewStudio"))==null||canvas.GetComponent<InspectZoomController>()==null||panel.Find("HotspotMagnifierButton")==null||panel.Find("HotspotZoomOverlay")==null)Install();
        };
    }

    [MenuItem("Tools/Object Inspection/Install Scene UI")]
    public static void Install()
    {
        if(Application.isPlaying){Debug.LogWarning("Stop Play Mode before installing the inspection UI.");return;}
        GameObject old=GameObject.Find("ObjectInspectionCanvas")??GameObject.Find("InspectCanvas");
        InspectableUIController controller;
        if(old==null) controller=CreateCanvas(); else controller=UpgradeExistingCanvas(old);
        if(controller==null){Debug.LogError("Inspect installer: InspectCanvas controller could not be created.");return;}
        GameObject collectibleCanvasGo=GameObject.Find("CollectibleInspectionCanvas");
        InspectableUIController collectibleController=collectibleCanvasGo!=null?collectibleCanvasGo.GetComponent<InspectableUIController>():CreateCollectibleCanvas();
        if(collectibleController==null){Debug.LogError("Inspect installer: CollectibleInspectionCanvas could not be created.");return;}

        PlayerInteraction interaction=Object.FindObjectOfType<PlayerInteraction>();
        if(interaction==null){Debug.LogError("Inspect installer: PlayerInteraction was not found.");return;}
        Image crosshair=FindSceneObject<Image>("AimCrosshair")??FindSceneObject<Image>("CrosshairImage");
        if(crosshair==null){Debug.LogError("Inspect installer: AimCrosshair/CrosshairImage was not found.");return;}
        InspectableRaycaster raycaster=interaction.GetComponent<InspectableRaycaster>();
        if(raycaster==null)raycaster=Undo.AddComponent<InspectableRaycaster>(interaction.gameObject);
        Sprite magnifier=CreateMagnifierSprite();Sprite hand=null;
        SerializedObject serializedRaycaster=new SerializedObject(raycaster);
        SerializedProperty existingHand=serializedRaycaster.FindProperty("handSprite");
        if(existingHand!=null&&existingHand.objectReferenceValue is Sprite customHand)hand=customHand;
        raycaster.Configure(crosshair,magnifier,hand,controller,collectibleController);
        Transform uiRoot=GetOrCreateUIRoot();
        ParentUnder(controller.transform,uiRoot);ParentUnder(collectibleController.transform,uiRoot);
        GameObject objectStudio=GameObject.Find("ObjectInspection3DStudio")??GameObject.Find("InspectPreviewStudio");if(objectStudio!=null)ParentUnder(objectStudio.transform,uiRoot);
        GameObject collectibleStudio=GameObject.Find("CollectibleInspection3DStudio");if(collectibleStudio!=null)ParentUnder(collectibleStudio.transform,uiRoot);
        EditorUtility.SetDirty(raycaster);EditorUtility.SetDirty(controller);EditorUtility.SetDirty(collectibleController);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject=controller.gameObject;
        Debug.Log("Separate ObjectInspectionCanvas and CollectibleInspectionCanvas installed under UI_ROOT.");
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

    [MenuItem("Tools/Object Inspection/Mark Selected Objects Collectible")]
    public static void MarkSelectedCollectible()
    {
        foreach(GameObject go in Selection.gameObjects)
        {
            if(go.GetComponent<InspectableObject>()==null)Undo.AddComponent<InspectableObject>(go);
            if(go.GetComponent<InspectableCollectible>()==null)Undo.AddComponent<InspectableCollectible>(go);
            if(go.GetComponentInChildren<Collider>(true)==null)Undo.AddComponent<BoxCollider>(go);
            EditorUtility.SetDirty(go);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Object Inspection/Add 4721 Hotspot To Selected")]
    public static void AddExampleHotspot()
    {
        if(Selection.activeTransform==null){Debug.LogError("Select the inspected model or one of its children first.");return;}
        GameObject hotspot=new GameObject("BackNumberHotspot");
        Undo.RegisterCreatedObjectUndo(hotspot,"Add 4721 Inspect Hotspot");
        hotspot.transform.SetParent(Selection.activeTransform,false);
        BoxCollider collider=hotspot.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=new Vector3(.12f,.08f,.025f);
        hotspot.AddComponent<InspectableHotspot>();
        Selection.activeGameObject=hotspot;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("BackNumberHotspot created with Zoomed Text 4721. Move it over the rear detail and point local +Z away from the item surface.");
    }

    private static InspectableUIController CreateCanvas()
    {
        GameObject canvasGo=new GameObject("ObjectInspectionCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(InspectableUIController));
        Undo.RegisterCreatedObjectUndo(canvasGo,"Create InspectCanvas");
        Canvas canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=300;
        CanvasScaler scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        GameObject panel=UI("ObjectInspectionOverlay",canvasGo.transform,new Color(.025f,.022f,.02f,.55f));Stretch(panel.GetComponent<RectTransform>(),0,0,1,1);
        GameObject imageGo=RawUI("Object3DPreview",panel.transform);Stretch(imageGo.GetComponent<RectTransform>(),.08f,.13f,.64f,.88f);
        RawImage image=imageGo.GetComponent<RawImage>();
        TMP_Text description=Text("DescriptionText",panel.transform,"A detail that may be worth remembering.",30,TextAlignmentOptions.MidlineLeft);Stretch(description.rectTransform,.70f,.32f,.92f,.74f);
        TMP_Text prompt=CreatePutBackPrompt(panel.transform);
        TMP_Text rotate=CreateRotatePrompt(panel.transform);
        CreatePreviewStudio(out Camera previewCamera,out Transform previewPivot);
        InspectableUIController controller=canvasGo.GetComponent<InspectableUIController>();controller.Configure(panel,image,description,prompt,rotate,previewCamera,previewPivot);
        InstallHotspotExtension(canvasGo,panel.transform,image,controller,previewCamera);
        return controller;
    }

    private static InspectableUIController UpgradeExistingCanvas(GameObject canvasGo)
    {
        InspectableUIController controller=canvasGo.GetComponent<InspectableUIController>();
        Transform panel=canvasGo.transform.Find("ObjectInspectionOverlay")??canvasGo.transform.Find("InspectPanel");
        if(controller==null||panel==null)return controller;
        Transform oldPrompt=panel.Find("PutBackPrompt");
        if(oldPrompt!=null)Undo.DestroyObjectImmediate(oldPrompt.gameObject);
        TMP_Text prompt=CreatePutBackPrompt(panel);
        Transform directImage=panel.Find("InspectedObject3DViewport")??panel.Find("Object3DPreview");
        if(directImage==null)directImage=panel.Find("ObjectSpriteUI");
        Transform oldFrame=panel.Find("ObjectImageFrame");
        if(directImage==null&&oldFrame!=null)
        {
            directImage=oldFrame.Find("ObjectLargeImage");
            if(directImage!=null)
            {
                Undo.SetTransformParent(directImage,panel,"Remove Object Image Frame");
                directImage.name="Object3DPreview";
                Stretch(directImage.GetComponent<RectTransform>(),.08f,.13f,.64f,.88f);
            }
            Undo.DestroyObjectImmediate(oldFrame.gameObject);
        }
        if(directImage==null)
        {
            GameObject imageGo=RawUI("Object3DPreview",panel);
            Stretch(imageGo.GetComponent<RectTransform>(),.08f,.13f,.64f,.88f);
            directImage=imageGo.transform;
        }
        Image oldImage=directImage.GetComponent<Image>();if(oldImage!=null)Undo.DestroyObjectImmediate(oldImage);
        RawImage image=directImage.GetComponent<RawImage>();if(image==null)image=Undo.AddComponent<RawImage>(directImage.gameObject);directImage.name="InspectedObject3DViewport";image.color=Color.white;
        Transform descriptionTransform=panel.Find("ObjectDescriptionText")??panel.Find("DescriptionText");
        Transform descriptionFrame=panel.Find("DescriptionFrame");
        if(descriptionTransform==null&&descriptionFrame!=null)
        {
            descriptionTransform=descriptionFrame.Find("DescriptionText");
            if(descriptionTransform!=null){Undo.SetTransformParent(descriptionTransform,panel,"Remove Description Background");Stretch(descriptionTransform.GetComponent<RectTransform>(),.70f,.32f,.92f,.74f);}
        }
        if(descriptionFrame!=null)Undo.DestroyObjectImmediate(descriptionFrame.gameObject);
        TMP_Text description=descriptionTransform!=null?descriptionTransform.GetComponent<TMP_Text>():null;
        if(description==null){description=Text("DescriptionText",panel,"A detail that may be worth remembering.",30,TextAlignmentOptions.MidlineLeft);Stretch(description.rectTransform,.70f,.32f,.92f,.74f);}
        Transform oldRotate=panel.Find("RotatePrompt");if(oldRotate!=null)Undo.DestroyObjectImmediate(oldRotate.gameObject);
        TMP_Text rotate=CreateRotatePrompt(panel);
        CreatePreviewStudio(out Camera previewCamera,out Transform previewPivot);
        controller.Configure(panel.gameObject,image,description,prompt,rotate,previewCamera,previewPivot);
        InstallHotspotExtension(canvasGo,panel,image,controller,previewCamera);
        return controller;
    }

    private static InspectableUIController CreateCollectibleCanvas()
    {
        GameObject canvasGo=new GameObject("CollectibleInspectionCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(InspectableUIController));
        Undo.RegisterCreatedObjectUndo(canvasGo,"Create Collectible Inspection Canvas");
        Canvas canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=301;
        CanvasScaler scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        GameObject panel=UI("CollectibleInspectionOverlay",canvasGo.transform,new Color(.025f,.022f,.02f,.55f));Stretch(panel.GetComponent<RectTransform>(),0,0,1,1);
        GameObject imageGo=RawUI("CollectibleObject3DViewport",panel.transform);Stretch(imageGo.GetComponent<RectTransform>(),.08f,.13f,.64f,.88f);
        TMP_Text description=Text("CollectibleDescriptionText",panel.transform,"A collectible object.",30,TextAlignmentOptions.MidlineLeft);Stretch(description.rectTransform,.70f,.32f,.92f,.74f);
        TMP_Text collect=CreatePutBackPrompt(panel.transform);collect.transform.parent.name="CollectPrompt";collect.name="CollectActionText";collect.text="Collect";
        TMP_Text rotate=CreateRotatePrompt(panel.transform);
        CreateCollectiblePreviewStudio(out Camera previewCamera,out Transform previewPivot);
        InspectableUIController controller=canvasGo.GetComponent<InspectableUIController>();controller.Configure(panel,imageGo.GetComponent<RawImage>(),description,collect,rotate,previewCamera,previewPivot);
        return controller;
    }

    private static void InstallHotspotExtension(GameObject canvasGo,Transform panel,RawImage viewport,InspectableUIController inspectController,Camera previewCamera)
    {
        Transform buttonTransform=panel.Find("HotspotMagnifierButton");
        Button magnifierButton;
        Image magnifierIcon;
        if(buttonTransform==null)
        {
            GameObject buttonGo=UI("HotspotMagnifierButton",panel,Color.clear);buttonTransform=buttonGo.transform;
            RectTransform rect=buttonGo.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(68,68);rect.anchoredPosition=Vector2.zero;
            magnifierButton=buttonGo.AddComponent<Button>();
            GameObject iconGo=UI("MagnifierIcon",buttonGo.transform,Color.yellow);Stretch(iconGo.GetComponent<RectTransform>(),.12f,.12f,.88f,.88f);
            magnifierIcon=iconGo.GetComponent<Image>();magnifierIcon.sprite=CreateMagnifierSprite();magnifierIcon.preserveAspect=true;magnifierIcon.raycastTarget=false;
        }
        else
        {
            magnifierButton=buttonTransform.GetComponent<Button>();if(magnifierButton==null)magnifierButton=Undo.AddComponent<Button>(buttonTransform.gameObject);
            Transform iconTransform=buttonTransform.Find("MagnifierIcon");
            if(iconTransform==null){GameObject iconGo=UI("MagnifierIcon",buttonTransform,Color.yellow);Stretch(iconGo.GetComponent<RectTransform>(),.12f,.12f,.88f,.88f);iconTransform=iconGo.transform;}
            magnifierIcon=iconTransform.GetComponent<Image>();magnifierIcon.sprite=CreateMagnifierSprite();magnifierIcon.preserveAspect=true;magnifierIcon.raycastTarget=false;
        }

        Transform overlayTransform=panel.Find("HotspotZoomOverlay");
        if(overlayTransform==null)
        {
            GameObject overlayGo=UI("HotspotZoomOverlay",panel,new Color(0,0,0,0));overlayTransform=overlayGo.transform;Stretch(overlayGo.GetComponent<RectTransform>(),0,0,1,1);
            overlayGo.GetComponent<Image>().raycastTarget=true;
        }

        Transform imageTransform=overlayTransform.Find("HotspotZoomImage");
        if(imageTransform==null){GameObject imageGo=UI("HotspotZoomImage",overlayTransform,Color.white);imageTransform=imageGo.transform;Stretch(imageGo.GetComponent<RectTransform>(),.09f,.13f,.67f,.88f);}
        Image zoomImage=imageTransform.GetComponent<Image>();zoomImage.preserveAspect=true;zoomImage.raycastTarget=false;

        Transform textTransform=overlayTransform.Find("HotspotZoomText");
        TMP_Text zoomText;
        if(textTransform==null){zoomText=Text("HotspotZoomText",overlayTransform,"4721",54,TextAlignmentOptions.Center);Stretch(zoomText.rectTransform,.70f,.28f,.93f,.72f);zoomText.fontStyle=FontStyles.Bold;}
        else zoomText=textTransform.GetComponent<TMP_Text>();

        Transform backTransform=overlayTransform.Find("HotspotZoomBackButton");
        Button backButton;
        if(backTransform==null)
        {
            GameObject backGo=UI("HotspotZoomBackButton",overlayTransform,new Color(.18f,.14f,.10f,.9f));backTransform=backGo.transform;Stretch(backGo.GetComponent<RectTransform>(),.76f,.07f,.93f,.16f);
            backButton=backGo.AddComponent<Button>();TMP_Text label=Text("BackButtonText",backGo.transform,"Back / Esc",23,TextAlignmentOptions.Center);Stretch(label.rectTransform,0,0,1,1);
        }
        else {backButton=backTransform.GetComponent<Button>();if(backButton==null)backButton=Undo.AddComponent<Button>(backTransform.gameObject);}

        InspectZoomController zoomController=canvasGo.GetComponent<InspectZoomController>();
        if(zoomController==null)zoomController=Undo.AddComponent<InspectZoomController>(canvasGo);
        zoomController.Configure(canvasGo.GetComponent<Canvas>(),panel.GetComponent<RectTransform>(),viewport,previewCamera,magnifierButton,magnifierIcon,overlayTransform.gameObject,zoomImage,zoomText,backButton);
        inspectController.ConfigureZoomController(zoomController);
        buttonTransform.gameObject.SetActive(false);overlayTransform.gameObject.SetActive(false);
        EditorUtility.SetDirty(zoomController);EditorUtility.SetDirty(inspectController);
    }

    private static TMP_Text CreatePutBackPrompt(Transform panel)
    {
        GameObject root=new GameObject("PutBackPrompt",typeof(RectTransform));root.transform.SetParent(panel,false);
        Stretch(root.GetComponent<RectTransform>(),.76f,.07f,.94f,.16f);
        GameObject circle=UI("QCircle",root.transform,new Color(.92f,.88f,.78f,1f));
        RectTransform circleRect=circle.GetComponent<RectTransform>();circleRect.anchorMin=new Vector2(0,.5f);circleRect.anchorMax=new Vector2(0,.5f);circleRect.sizeDelta=new Vector2(54,54);circleRect.anchoredPosition=new Vector2(27,0);
        circle.GetComponent<Image>().sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        TMP_Text q=Text("QLabel",circle.transform,"Q",27,TextAlignmentOptions.Center);Stretch(q.rectTransform,0,0,1,1);q.color=new Color(.08f,.065f,.05f,1f);q.fontStyle=FontStyles.Bold;
        TMP_Text label=Text("PutBackText",root.transform,"Put Back",24,TextAlignmentOptions.MidlineLeft);label.rectTransform.anchorMin=new Vector2(0,0);label.rectTransform.anchorMax=new Vector2(1,1);label.rectTransform.offsetMin=new Vector2(72,0);label.rectTransform.offsetMax=Vector2.zero;
        return label;
    }

    private static TMP_Text CreateRotatePrompt(Transform panel)
    {
        GameObject root=new GameObject("RotatePrompt",typeof(RectTransform));root.transform.SetParent(panel,false);Stretch(root.GetComponent<RectTransform>(),.56f,.07f,.74f,.16f);
        GameObject circle=UI("ECircle",root.transform,new Color(.92f,.88f,.78f,1f));RectTransform cr=circle.GetComponent<RectTransform>();cr.anchorMin=cr.anchorMax=new Vector2(0,.5f);cr.sizeDelta=new Vector2(54,54);cr.anchoredPosition=new Vector2(27,0);circle.GetComponent<Image>().sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        TMP_Text e=Text("ELabel",circle.transform,"E",27,TextAlignmentOptions.Center);Stretch(e.rectTransform,0,0,1,1);e.color=new Color(.08f,.065f,.05f,1f);e.fontStyle=FontStyles.Bold;
        TMP_Text label=Text("RotateText",root.transform,"Rotate",24,TextAlignmentOptions.MidlineLeft);label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(72,0);label.rectTransform.offsetMax=Vector2.zero;return label;
    }

    private static void CreatePreviewStudio(out Camera camera,out Transform pivot)
    {
        GameObject studio=GameObject.Find("ObjectInspection3DStudio")??GameObject.Find("InspectPreviewStudio");
        if(studio==null){studio=new GameObject("InspectPreviewStudio");Undo.RegisterCreatedObjectUndo(studio,"Create 3D Preview Studio");studio.transform.position=new Vector3(1000,1000,1000);}
        Transform pivotTransform=studio.transform.Find("InspectedModelPivot")??studio.transform.Find("ModelPivot");if(pivotTransform==null){GameObject p=new GameObject("InspectedModelPivot");p.transform.SetParent(studio.transform,false);pivotTransform=p.transform;}
        Transform cameraTransform=studio.transform.Find("ObjectPreviewCamera")??studio.transform.Find("PreviewCamera");
        if(cameraTransform==null){GameObject c=new GameObject("PreviewCamera",typeof(Camera));c.transform.SetParent(studio.transform,false);cameraTransform=c.transform;cameraTransform.localPosition=new Vector3(0,0,-4);cameraTransform.localRotation=Quaternion.identity;}
        camera=cameraTransform.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;camera.fieldOfView=35;camera.allowHDR=false;camera.allowMSAA=false;camera.enabled=false;
        Transform lightTransform=studio.transform.Find("ObjectPreviewLight")??studio.transform.Find("PreviewLight");if(lightTransform==null){GameObject l=new GameObject("ObjectPreviewLight",typeof(Light));l.transform.SetParent(studio.transform,false);lightTransform=l.transform;lightTransform.localRotation=Quaternion.Euler(35,-30,0);Light light=l.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;}
        pivot=pivotTransform;
    }

    private static void CreateCollectiblePreviewStudio(out Camera camera,out Transform pivot)
    {
        GameObject studio=GameObject.Find("CollectibleInspection3DStudio");
        if(studio==null){studio=new GameObject("CollectibleInspection3DStudio");Undo.RegisterCreatedObjectUndo(studio,"Create Collectible Inspection 3D Studio");studio.transform.position=new Vector3(1010,1000,1000);}
        Transform pivotTransform=studio.transform.Find("CollectibleModelPivot");if(pivotTransform==null){GameObject p=new GameObject("CollectibleModelPivot");p.transform.SetParent(studio.transform,false);pivotTransform=p.transform;}
        Transform cameraTransform=studio.transform.Find("CollectiblePreviewCamera");if(cameraTransform==null){GameObject c=new GameObject("CollectiblePreviewCamera",typeof(Camera));c.transform.SetParent(studio.transform,false);cameraTransform=c.transform;cameraTransform.localPosition=new Vector3(0,0,-4);cameraTransform.localRotation=Quaternion.identity;}
        camera=cameraTransform.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;camera.fieldOfView=35;camera.allowHDR=false;camera.allowMSAA=false;camera.enabled=false;
        Transform lightTransform=studio.transform.Find("CollectiblePreviewLight");if(lightTransform==null){GameObject l=new GameObject("CollectiblePreviewLight",typeof(Light));l.transform.SetParent(studio.transform,false);lightTransform=l.transform;lightTransform.localRotation=Quaternion.Euler(35,-30,0);Light light=l.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;}
        pivot=pivotTransform;
    }

    private static Transform GetOrCreateUIRoot(){GameObject root=GameObject.Find("UI_ROOT");if(root==null){root=new GameObject("UI_ROOT");Undo.RegisterCreatedObjectUndo(root,"Create UI Root");}return root.transform;}
    private static void ParentUnder(Transform child,Transform parent){if(child!=null&&child.parent!=parent)Undo.SetTransformParent(child,parent,"Group inspection UI under UI_ROOT");}

    private static Sprite CreateMagnifierSprite()
    {
        Sprite existing=AssetDatabase.LoadAssetAtPath<Sprite>(MagnifierIconPath);
        if(existing!=null)return existing;
        if(!Directory.Exists("Assets/UI"))Directory.CreateDirectory("Assets/UI");
        const int size=64;Texture2D tex=new Texture2D(size,size,TextureFormat.RGBA32,false);Color clear=new Color(0,0,0,0),white=Color.white;
        Color[] pixels=new Color[size*size];for(int i=0;i<pixels.Length;i++)pixels[i]=clear;
        Vector2 center=new Vector2(26,37);float radius=15;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){float d=Vector2.Distance(new Vector2(x,y),center);if(d>radius-4f&&d<radius+4f)pixels[y*size+x]=white;}
        for(int i=0;i<23;i++)for(int w=-4;w<=4;w++){int x=37+i,y=26-i+w;if(x>=0&&x<size&&y>=0&&y<size)pixels[y*size+x]=white;}
        tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(MagnifierIconPath,tex.EncodeToPNG());Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(MagnifierIconPath,ImportAssetOptions.ForceUpdate);
        TextureImporter importer=(TextureImporter)AssetImporter.GetAtPath(MagnifierIconPath);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=64;importer.alphaIsTransparency=true;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(MagnifierIconPath);
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
