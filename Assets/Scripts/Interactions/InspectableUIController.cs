using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InspectableUIController : MonoBehaviour
{
    [Header("Permanent Scene UI")]
    [SerializeField, Tooltip("Root panel enabled while inspecting.")] private GameObject inspectPanel;
    [SerializeField, Tooltip("RawImage displaying the preview camera RenderTexture.")] private RawImage objectPreview;
    [SerializeField, Tooltip("One-sentence description on the right.")] private TMP_Text descriptionText;
    [SerializeField, Tooltip("Bottom-right Q prompt.")] private TMP_Text putBackPrompt;
    [SerializeField, Tooltip("Bottom-right E rotate prompt.")] private TMP_Text rotatePrompt;
    [Header("3D Preview Studio")]
    [SerializeField, Tooltip("Camera rendering only the preview model.")] private Camera previewCamera;
    [SerializeField, Tooltip("Pivot under which the temporary preview model is created.")] private Transform previewPivot;
    [SerializeField, Min(128), Tooltip("Square RenderTexture resolution.")] private int textureResolution=1024;
    [SerializeField, Min(.05f), Tooltip("Mouse drag rotation sensitivity.")] private float rotationSensitivity=.35f;
    [SerializeField, Min(.1f), Tooltip("Target maximum model size inside the studio.")] private float fittedModelSize=2f;
    [SerializeField, Range(0f,1f), Tooltip("Transparency of the full inspection Canvas background. Lower values reveal more of the game scene.")] private float canvasBackgroundAlpha=.55f;
    private RenderTexture renderTexture;private Material transparentPreviewMaterial;private GameObject previewInstance;private InspectableObject currentTarget;private bool rotationMode;private Vector3 lastMouse;
    public bool IsOpen => inspectPanel != null && inspectPanel.activeSelf;

    public void Configure(GameObject panel, RawImage preview, TMP_Text description, TMP_Text putBack, TMP_Text rotate, Camera camera, Transform pivot)
    {inspectPanel=panel;objectPreview=preview;descriptionText=description;putBackPrompt=putBack;rotatePrompt=rotate;previewCamera=camera;previewPivot=pivot;if(inspectPanel!=null)inspectPanel.SetActive(false);}

    public void Show(InspectableObject target)
    {
        if (target == null || inspectPanel == null) { Debug.LogError("InspectableUIController: Target or panel is missing."); return; }
        currentTarget=target;
        inspectPanel.SetActive(true); inspectPanel.transform.SetAsLastSibling();
        ApplyPanelBackgroundAlpha();DisableLegacyDescriptionBackground();
        CreatePreview(target);
        if (descriptionText != null) descriptionText.text = target.Description;
        if (putBackPrompt != null) putBackPrompt.text = target.GetComponent<IInspectableCollectible>()!=null?"Collect":"Put Back";
        rotationMode=false;UpdateRotatePrompt();
    }

    private void Update()
    {
        if(!IsOpen)return;
        ApplyPanelBackgroundAlpha();
        if(Input.GetKeyDown(KeyCode.E)){rotationMode=!rotationMode;UpdateRotatePrompt();}
        if(!rotationMode)return;
        if(Input.GetMouseButtonDown(0))lastMouse=Input.mousePosition;
        if(Input.GetMouseButton(0)&&previewPivot!=null)
        {Vector3 delta=Input.mousePosition-lastMouse;previewPivot.Rotate(Vector3.up,-delta.x*rotationSensitivity,Space.World);previewPivot.Rotate(Vector3.right,delta.y*rotationSensitivity,Space.World);lastMouse=Input.mousePosition;}
    }

    private void ApplyPanelBackgroundAlpha()
    {
        if(inspectPanel==null)return;
        Image panelBackground=inspectPanel.GetComponent<Image>();
        if(panelBackground==null)return;
        Color panelColor=panelBackground.color;panelColor.a=canvasBackgroundAlpha;panelBackground.color=panelColor;
    }

    private void DisableLegacyDescriptionBackground()
    {
        if(descriptionText==null||inspectPanel==null)return;
        Transform parent=descriptionText.transform.parent;
        if(parent==null||parent.gameObject==inspectPanel)return;
        Image legacyBackground=parent.GetComponent<Image>();
        if(legacyBackground!=null)legacyBackground.enabled=false;
    }

    private void OnValidate(){ApplyPanelBackgroundAlpha();DisableLegacyDescriptionBackground();}

    private void UpdateRotatePrompt(){if(rotatePrompt!=null){rotatePrompt.text=rotationMode?"Drag to Rotate":"Rotate";rotatePrompt.color=rotationMode?Color.yellow:new Color(.92f,.88f,.78f);}}

    private void CreatePreview(InspectableObject target)
    {
        if(previewCamera==null||previewPivot==null||objectPreview==null){Debug.LogError("InspectableUIController: 3D preview references are missing. Run Tools/Object Inspection/Install Scene UI.");return;}
        DestroyPreview();
        if(renderTexture==null){renderTexture=new RenderTexture(textureResolution,textureResolution,24,RenderTextureFormat.ARGB32){name="InspectableObjectRenderTexture"};renderTexture.Create();}
        previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=new Color(1f,0f,1f,1f);
        previewCamera.allowHDR=false;previewCamera.allowMSAA=false;
        previewCamera.targetTexture=renderTexture;previewCamera.enabled=true;objectPreview.texture=renderTexture;objectPreview.color=Color.white;
        if(transparentPreviewMaterial==null)
        {
            Shader shader=Shader.Find("UI/InspectablePreviewTransparent");
            if(shader!=null)transparentPreviewMaterial=new Material(shader){name="InspectablePreviewTransparent_Runtime"};
            else Debug.LogError("InspectableUIController: UI/InspectablePreviewTransparent shader was not found.");
        }
        objectPreview.material=transparentPreviewMaterial;
        previewPivot.localRotation=Quaternion.identity;
        previewInstance=Instantiate(target.PreviewModel,previewPivot);previewInstance.name="PreviewModel_Instance";previewInstance.transform.localPosition=Vector3.zero;previewInstance.transform.localRotation=Quaternion.identity;
        PrepareClone(previewInstance.transform);
        Renderer[] renderers=previewInstance.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0){Debug.LogError("InspectableUIController: Preview model has no Renderer.");return;}
        Bounds bounds=renderers[0].bounds;for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
        float largest=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
        if(largest>.0001f)previewInstance.transform.localScale*=fittedModelSize/largest;
        bounds=renderers[0].bounds;for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
        previewInstance.transform.position+=previewPivot.position-bounds.center;
        if(target.PreviewPhoto!=null)ApplyPhotoToPreviewMaterial(target);
        previewPivot.localRotation=Quaternion.Euler(target.PreviewRotation);
    }

    private void ApplyPhotoToPreviewMaterial(InspectableObject target)
    {
        Renderer[] renderers=previewInstance.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return;
        if(string.IsNullOrWhiteSpace(target.PhotoRendererName))
        {
            foreach(Renderer renderer in renderers)
            {
                Material[] allMaterials=renderer.materials;
                for(int i=0;i<allMaterials.Length;i++)ApplyPhotoTexture(allMaterials[i],target.PreviewPhoto);
                renderer.materials=allMaterials;
            }
            return;
        }
        Renderer selected=null;
        if(!string.IsNullOrWhiteSpace(target.PhotoRendererName))foreach(Renderer renderer in renderers)if(renderer.name==target.PhotoRendererName){selected=renderer;break;}
        if(selected==null){selected=renderers[0];float largest=selected.bounds.size.sqrMagnitude;for(int i=1;i<renderers.Length;i++){float size=renderers[i].bounds.size.sqrMagnitude;if(size>largest){largest=size;selected=renderers[i];}}}
        Material[] materials=selected.materials;if(materials.Length==0){Debug.LogError("InspectableUIController: Selected photo Renderer has no material slots.");return;}int index=Mathf.Clamp(target.PhotoMaterialIndex,0,materials.Length-1);Material material=materials[index];
        ApplyPhotoTexture(material,target.PreviewPhoto);selected.materials=materials;
    }

    private static void ApplyPhotoTexture(Material material,Texture texture)
    {
        if(material==null||texture==null)return;
        if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
        if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",texture);
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",Color.white);
        if(material.HasProperty("_Color"))material.SetColor("_Color",Color.white);
        material.mainTexture=texture;
    }

    private static void PrepareClone(Transform root)
    {
        foreach(Transform t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
        foreach(Collider c in root.GetComponentsInChildren<Collider>(true))c.enabled=false;
        foreach(Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true)){rb.isKinematic=true;rb.useGravity=false;}
        foreach(MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
    }

    private void DestroyPreview(){if(previewInstance!=null)Destroy(previewInstance);previewInstance=null;}
    public bool TryCollectCurrent()
    {
        if(currentTarget==null)return false;
        IInspectableCollectible collectible=currentTarget.GetComponent<IInspectableCollectible>();
        if(collectible==null)return false;
        collectible.CollectFromInspection();return true;
    }
    public void Hide(){DestroyPreview();currentTarget=null;rotationMode=false;if(previewCamera!=null)previewCamera.enabled=false;if(inspectPanel!=null)inspectPanel.SetActive(false);}
    private void OnDestroy(){DestroyPreview();if(renderTexture!=null){renderTexture.Release();Destroy(renderTexture);}if(transparentPreviewMaterial!=null)Destroy(transparentPreviewMaterial);}
}
