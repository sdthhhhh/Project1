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
    private static void InstallIfNeeded(){if(!Application.isPlaying&&GameObject.Find("DoorPasswordCanvas")==null)Install();}

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

    private static DoorPasswordUIController CreateCanvas()
    {
        GameObject canvasGo=new GameObject("DoorPasswordCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(DoorPasswordUIController));Undo.RegisterCreatedObjectUndo(canvasGo,"Create Door Password Canvas");
        Canvas canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=320;
        CanvasScaler scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        GameObject overlay=UI("DoorPasswordOverlay",canvasGo.transform,new Color(0,0,0,.68f));Stretch(overlay.GetComponent<RectTransform>(),0,0,1,1);
        GameObject card=UI("PasswordCard",overlay.transform,new Color(.08f,.065f,.05f,.98f));RectTransform cardRect=card.GetComponent<RectTransform>();cardRect.anchorMin=new Vector2(.34f,.27f);cardRect.anchorMax=new Vector2(.66f,.73f);cardRect.offsetMin=cardRect.offsetMax=Vector2.zero;
        TMP_Text title=Text("DoorTitle",card.transform,"Locked Door",42,TextAlignmentOptions.Center);Stretch(title.rectTransform,.08f,.75f,.92f,.94f);title.fontStyle=FontStyles.Bold;
        TMP_InputField input=CreateInput(card.transform);
        TMP_Text feedback=Text("PasswordFeedback",card.transform,"Enter password",24,TextAlignmentOptions.Center);Stretch(feedback.rectTransform,.12f,.35f,.88f,.48f);
        Button submit=CreateButton("SubmitButton",card.transform,"UNLOCK",new Vector4(.14f,.12f,.86f,.29f));
        Button close=CreateButton("CloseButton",card.transform,"CLOSE / ESC",new Vector4(.14f,.04f,.86f,.14f));
        DoorPasswordUIController controller=canvasGo.GetComponent<DoorPasswordUIController>();controller.Configure(overlay,title,input,feedback,submit,close);return controller;
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
    private static Transform GetOrCreateUIRoot(){GameObject root=GameObject.Find("UI_ROOT");if(root==null){root=new GameObject("UI_ROOT");Undo.RegisterCreatedObjectUndo(root,"Create UI Root");}return root.transform;}
    private static T FindSceneObject<T>(string name)where T:Component{foreach(T item in Resources.FindObjectsOfTypeAll<T>())if(item.gameObject.scene.IsValid()&&item.gameObject.name==name)return item;return null;}
}
#endif
