#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DoorPasswordSceneInstaller
{
    private const string IconPath="Assets/UI/GeneratedDoorLock.png";

    [InitializeOnLoadMethod]
    private static void ScheduleInstall()
    {
        EditorApplication.delayCall+=InstallIfNeeded;
        EditorApplication.playModeStateChanged-=OnPlayModeStateChanged;EditorApplication.playModeStateChanged+=OnPlayModeStateChanged;
    }
    private static void OnPlayModeStateChanged(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=InstallIfNeeded;}
    private static void InstallIfNeeded()
    {
        if(Application.isPlaying)return;
        if(GameObject.Find("DoorPasswordCanvas")==null)Install();
        else {SimplifyExistingCanvas();InstallDoorFolderLocks();}
    }

    [MenuItem("Tools/Door Password/Install Password UI")]
    public static void Install()
    {
        if(Application.isPlaying){Debug.LogWarning("Stop Play Mode before installing Door Password UI.");return;}
        PlayerInteraction interaction=Object.FindObjectOfType<PlayerInteraction>();if(interaction==null){Debug.LogError("Door Password installer: PlayerInteraction was not found.");return;}
        InspectableRaycaster raycaster=interaction.GetComponent<InspectableRaycaster>();if(raycaster==null){Debug.LogError("Install Object Inspection UI before Door Password UI.");return;}
        Image crosshair=FindSceneObject<Image>("AimCrosshair")??FindSceneObject<Image>("CrosshairImage");if(crosshair==null){Debug.LogError("Door Password installer: crosshair Image was not found.");return;}
        GameObject canvasGo=GameObject.Find("DoorPasswordCanvas");DoorPasswordUIController controller=canvasGo!=null?canvasGo.GetComponent<DoorPasswordUIController>():CreateCanvas();
        if(controller==null)return;
        Sprite icon=CreateDoorLockSprite();SerializedObject serialized=new SerializedObject(raycaster);SerializedProperty iconProperty=serialized.FindProperty("doorLockSprite");
        if(iconProperty!=null&&iconProperty.objectReferenceValue is Sprite customIcon)icon=customIcon;
        raycaster.ConfigureDoorPassword(icon,controller);
        Transform root=GetOrCreateUIRoot();if(controller.transform.parent!=root)Undo.SetTransformParent(controller.transform,root,"Group Door Password UI");
        EditorUtility.SetDirty(raycaster);EditorUtility.SetDirty(controller);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
        InstallDoorFolderLocks();
        Debug.Log("DoorPasswordCanvas installed under UI_ROOT. Select lock colliders and use Tools/Door Password/Mark Selected As Password Locks.");
    }

    [MenuItem("Tools/Door Password/Mark Selected As Password Locks")]
    public static void MarkSelected()
    {
        foreach(GameObject go in Selection.gameObjects)
        {
            if(go.GetComponent<DoorPasswordLock>()==null)Undo.AddComponent<DoorPasswordLock>(go);
            if(go.GetComponent<Collider>()==null){BoxCollider collider=Undo.AddComponent<BoxCollider>(go);collider.size=new Vector3(.25f,.25f,.12f);collider.isTrigger=true;}
            EditorUtility.SetDirty(go);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Door Password/Install Locks Under Door Folder")]
    public static void InstallDoorFolderLocks()
    {
        if(Application.isPlaying)return;
        GameObject doorFolder=FindSceneGameObject("Door");
        if(doorFolder==null)return;

        int configured=0;
        foreach(Transform candidate in doorFolder.GetComponentsInChildren<Transform>(true))
        {
            if(candidate==doorFolder.transform||candidate.name.IndexOf("lock",System.StringComparison.OrdinalIgnoreCase)<0)continue;
            DoorPasswordLock passwordLock=candidate.GetComponent<DoorPasswordLock>();
            if(passwordLock==null)passwordLock=Undo.AddComponent<DoorPasswordLock>(candidate.gameObject);
            Collider collider=candidate.GetComponent<Collider>();
            if(collider==null){BoxCollider box=Undo.AddComponent<BoxCollider>(candidate.gameObject);box.size=new Vector3(.25f,.25f,.12f);box.isTrigger=true;collider=box;}
            passwordLock.ConfigureDoor(ResolveDoorTransform(candidate,doorFolder.transform));
            EditorUtility.SetDirty(passwordLock);EditorUtility.SetDirty(collider);configured++;
        }

        if(configured==0){Debug.LogWarning("Door Password: a Door folder was found, but no child name containing 'lock' was found.");return;}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
        Debug.Log($"Door Password: configured {configured} lock object(s) below Door. Locked Crosshair uses the lock icon; unlocked Crosshair uses the shared hand icon.");
    }

    private static Transform ResolveDoorTransform(Transform lockTransform,Transform doorFolder)
    {
        if(lockTransform.parent!=null&&lockTransform.parent!=doorFolder&&lockTransform.parent.name.IndexOf("lock",System.StringComparison.OrdinalIgnoreCase)<0)
            return lockTransform.parent;
        foreach(Transform child in doorFolder)
            if(child!=lockTransform&&child.name.IndexOf("door",System.StringComparison.OrdinalIgnoreCase)>=0)return child;
        return doorFolder;
    }

    private static DoorPasswordUIController CreateCanvas()
    {
        GameObject canvasGo=new GameObject("DoorPasswordCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(DoorPasswordUIController));Undo.RegisterCreatedObjectUndo(canvasGo,"Create Door Password Canvas");
        Canvas canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=320;
        CanvasScaler scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        GameObject overlay=UI("DoorPasswordOverlay",canvasGo.transform,new Color(0,0,0,.58f));Stretch(overlay.GetComponent<RectTransform>(),0,0,1,1);
        TMP_InputField input=CreateInput(overlay.transform);SetAnchors(input.GetComponent<RectTransform>(),.35f,.56f,.65f,.65f);
        Button submit=CreateButton("SubmitButton",overlay.transform,"UNLOCK",new Vector4(.35f,.45f,.65f,.53f));
        Button close=CreateButton("CloseButton",overlay.transform,"CLOSE",new Vector4(.35f,.35f,.65f,.43f));
        DoorPasswordUIController controller=canvasGo.GetComponent<DoorPasswordUIController>();controller.Configure(overlay,null,input,null,submit,close);return controller;
    }

    private static void SimplifyExistingCanvas()
    {
        GameObject canvasGo=GameObject.Find("DoorPasswordCanvas");if(canvasGo==null)return;
        DoorPasswordUIController controller=canvasGo.GetComponent<DoorPasswordUIController>();
        Transform overlay=FindNamed(canvasGo.transform,"DoorPasswordOverlay");
        TMP_InputField input=FindNamed(canvasGo.transform,"PasswordInput")?.GetComponent<TMP_InputField>();
        Button submit=FindNamed(canvasGo.transform,"SubmitButton")?.GetComponent<Button>();
        Button close=FindNamed(canvasGo.transform,"CloseButton")?.GetComponent<Button>();
        if(controller==null||overlay==null||input==null||submit==null||close==null){Debug.LogError("Door Password: existing Canvas is missing PasswordInput, SubmitButton or CloseButton.");return;}

        Undo.SetTransformParent(input.transform,overlay,"Simplify Password UI");
        Undo.SetTransformParent(submit.transform,overlay,"Simplify Password UI");
        Undo.SetTransformParent(close.transform,overlay,"Simplify Password UI");
        SetAnchors(input.GetComponent<RectTransform>(),.35f,.56f,.65f,.65f);
        SetAnchors(submit.GetComponent<RectTransform>(),.35f,.45f,.65f,.53f);
        SetAnchors(close.GetComponent<RectTransform>(),.35f,.35f,.65f,.43f);

        for(int i=overlay.childCount-1;i>=0;i--)
        {
            Transform child=overlay.GetChild(i);
            if(child!=input.transform&&child!=submit.transform&&child!=close.transform)Undo.DestroyObjectImmediate(child.gameObject);
        }

        controller.Configure(overlay.gameObject,null,input,null,submit,close);EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
        Debug.Log("DoorPasswordCanvas simplified: PasswordInput, SubmitButton and CloseButton only. Mouse button listeners are bound at runtime.");
    }

    private static TMP_InputField CreateInput(Transform parent)
    {
        GameObject root=UI("PasswordInput",parent,new Color(.92f,.88f,.78f,1f));Stretch(root.GetComponent<RectTransform>(),.14f,.52f,.86f,.69f);TMP_InputField input=root.AddComponent<TMP_InputField>();input.contentType=TMP_InputField.ContentType.Password;input.characterLimit=32;
        TMP_Text placeholder=Text("Placeholder",root.transform,"Password",30,TextAlignmentOptions.Center);Stretch(placeholder.rectTransform,.04f,.05f,.96f,.95f);placeholder.color=new Color(.25f,.22f,.18f,.65f);
        TMP_Text value=Text("Text",root.transform,string.Empty,30,TextAlignmentOptions.Center);Stretch(value.rectTransform,.04f,.05f,.96f,.95f);value.color=new Color(.06f,.05f,.04f);input.textViewport=root.GetComponent<RectTransform>();input.textComponent=value;input.placeholder=placeholder;return input;
    }

    private static Button CreateButton(string name,Transform parent,string label,Vector4 anchors)
    {GameObject go=UI(name,parent,new Color(.32f,.22f,.12f,1f));Stretch(go.GetComponent<RectTransform>(),anchors.x,anchors.y,anchors.z,anchors.w);Button button=go.AddComponent<Button>();TMP_Text text=Text(name+"Text",go.transform,label,25,TextAlignmentOptions.Center);Stretch(text.rectTransform,0,0,1,1);return button;}

    private static Sprite CreateDoorLockSprite()
    {
        Sprite existing=AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);if(existing!=null)return existing;if(!Directory.Exists("Assets/UI"))Directory.CreateDirectory("Assets/UI");
        const int size=64;Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false);Color[] pixels=new Color[size*size];Color clear=Color.clear,white=Color.white;for(int i=0;i<pixels.Length;i++)pixels[i]=clear;
        for(int y=10;y<38;y++)for(int x=13;x<51;x++)if(x>=15&&x<=49&&y>=10&&y<=35)pixels[y*size+x]=white;
        Vector2 center=new Vector2(32,42);for(int y=30;y<59;y++)for(int x=15;x<50;x++){float distance=Vector2.Distance(new Vector2(x,y),center);if(distance>12&&distance<17&&y>=42)pixels[y*size+x]=white;}
        for(int y=23;y<34;y++)for(int x=29;x<35;x++)pixels[y*size+x]=new Color(.08f,.065f,.05f,1f);
        texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(IconPath,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(IconPath,ImportAssetOptions.ForceUpdate);TextureImporter importer=(TextureImporter)AssetImporter.GetAtPath(IconPath);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.spritePixelsPerUnit=64;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
    }

    private static GameObject UI(string name,Transform parent,Color color){GameObject go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=color;return go;}
    private static TMP_Text Text(string name,Transform parent,string value,float size,TextAlignmentOptions alignment){GameObject go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);TMP_Text text=go.GetComponent<TMP_Text>();text.text=value;text.fontSize=size;text.alignment=alignment;text.color=new Color(.92f,.88f,.78f);return text;}
    private static void Stretch(RectTransform rect,float x1,float y1,float x2,float y2){rect.anchorMin=new Vector2(x1,y1);rect.anchorMax=new Vector2(x2,y2);rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static void SetAnchors(RectTransform rect,float x1,float y1,float x2,float y2){if(rect==null)return;rect.anchorMin=new Vector2(x1,y1);rect.anchorMax=new Vector2(x2,y2);rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static Transform FindNamed(Transform root,string objectName){foreach(Transform item in root.GetComponentsInChildren<Transform>(true))if(item.name==objectName)return item;return null;}
    private static Transform GetOrCreateUIRoot(){GameObject root=GameObject.Find("UI_ROOT");if(root==null){root=new GameObject("UI_ROOT");Undo.RegisterCreatedObjectUndo(root,"Create UI Root");}return root.transform;}
    private static T FindSceneObject<T>(string name)where T:Component{foreach(T item in Resources.FindObjectsOfTypeAll<T>())if(item.gameObject.scene.IsValid()&&item.gameObject.name==name)return item;return null;}
    private static GameObject FindSceneGameObject(string name){foreach(GameObject item in Resources.FindObjectsOfTypeAll<GameObject>())if(item.scene.IsValid()&&item.name==name)return item;return null;}
}
#endif
