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
    [SerializeField, Tooltip("Hotspot and close-up extension hosted by this same Inspect Canvas.")] private InspectZoomController inspectZoomController;
    [SerializeField, Min(128), Tooltip("Square RenderTexture resolution.")] private int textureResolution=1024;
    [SerializeField, Min(.05f), Tooltip("Mouse drag rotation sensitivity.")] private float rotationSensitivity=.35f;
    [SerializeField, Min(.1f), Tooltip("Target maximum model size inside the studio.")] private float fittedModelSize=2f;
    [SerializeField, Range(0f,1f), Tooltip("Transparency of the full inspection Canvas background. Lower values reveal more of the game scene.")] private float canvasBackgroundAlpha=.55f;
    [Header("Preview Background")]
    [SerializeField, Tooltip("Remove transparent, magenta-key, or URP pure-black pixels from the 3D viewport so the overlay behind it remains visible.")] private bool transparentPreviewBackground=true;
    [SerializeField, Tooltip("Solid viewport background used when Transparent Preview Background is disabled.")] private Color previewFallbackColor=new Color(.08f,.065f,.05f,1f);
    private RenderTexture renderTexture;private Material transparentPreviewMaterial;private GameObject previewInstance;private InspectableObject currentTarget;private bool rotationMode;private Vector3 lastMouse;
    public bool IsOpen => inspectPanel != null && inspectPanel.activeSelf;
    public bool IsZoomOpen => inspectZoomController != null && inspectZoomController.IsZoomOpen;
    public bool IsUtilityOverlay { get; private set; }

    public void Configure(GameObject panel, RawImage preview, TMP_Text description, TMP_Text putBack, TMP_Text rotate, Camera camera, Transform pivot)
    {inspectPanel=panel;objectPreview=preview;descriptionText=description;putBackPrompt=putBack;rotatePrompt=rotate;previewCamera=camera;previewPivot=pivot;if(inspectPanel!=null)inspectPanel.SetActive(false);}

    public void ConfigureZoomController(InspectZoomController controller){inspectZoomController=controller;}

    private float savedBackgroundAlpha = -1f;
    private bool savedPreviewRaycast;
    private bool savedPanelRaycast;

    /// <summary>
    /// Opens the shared item-inspect canvas as a dim overlay (no 3D preview). Esc/Q close via InspectableRaycaster.
    /// </summary>
    public void ShowUtilityOverlay(string description, string closePrompt = "Put Back")
    {
        if (inspectPanel == null)
        {
            Debug.LogError("InspectableUIController: inspect panel is missing.");
            return;
        }

        currentTarget = null;
        IsUtilityOverlay = true;
        DestroyPreview();
        if (previewCamera != null)
            previewCamera.enabled = false;
        if (objectPreview != null)
        {
            savedPreviewRaycast = objectPreview.raycastTarget;
            objectPreview.enabled = false;
            objectPreview.raycastTarget = false;
        }

        Image panelBackground = inspectPanel.GetComponent<Image>();
        if (panelBackground != null)
        {
            savedPanelRaycast = panelBackground.raycastTarget;
            panelBackground.raycastTarget = false;
        }

        savedBackgroundAlpha = canvasBackgroundAlpha;
        canvasBackgroundAlpha = Mathf.Min(canvasBackgroundAlpha, 0.28f);

        inspectPanel.SetActive(true);
        inspectPanel.transform.SetAsLastSibling();
        ApplyPanelBackgroundAlpha();
        DisableLegacyDescriptionBackground();
        if (descriptionText != null)
            descriptionText.text = description ?? string.Empty;
        if (putBackPrompt != null)
            putBackPrompt.text = closePrompt ?? "Put Back";
        if (rotatePrompt != null)
            rotatePrompt.gameObject.SetActive(false);
        rotationMode = false;
    }

    public void Show(InspectableObject target)
    {
        if (target == null || inspectPanel == null) { Debug.LogError("InspectableUIController: Target or panel is missing."); return; }
        currentTarget=target;
        IsUtilityOverlay = false;
        if (objectPreview != null) objectPreview.enabled = true;
        if (rotatePrompt != null) rotatePrompt.gameObject.SetActive(true);
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
        if(IsUtilityOverlay||IsZoomOpen)return;
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

    private bool previewUsingComicOutline;

    private void CreatePreview(InspectableObject target)
    {
        if(previewCamera==null||previewPivot==null||objectPreview==null){Debug.LogError("InspectableUIController: 3D preview references are missing.",this);return;}
        DestroyPreview();
        if(renderTexture==null)
        {
            renderTexture=new RenderTexture(textureResolution,textureResolution,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default)
            {name="InspectableObjectRenderTexture",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,useMipMap=false,autoGenerateMips=false,antiAliasing=4};
            renderTexture.Create();
        }

        // Comic MeshOutlineStyle (black body + white shell): opaque black RT so body isn't keyed out.
        previewUsingComicOutline = UsesComicOutline(target);
        ApplyPreviewBackgroundSettings();
        previewCamera.allowHDR=false;previewCamera.allowMSAA=true;
        previewCamera.targetTexture=renderTexture;previewCamera.enabled=true;objectPreview.texture=renderTexture;objectPreview.color=Color.white;
        if (!previewUsingComicOutline)
            EnsurePreviewLightEnabled();
        previewPivot.localRotation=Quaternion.identity;
        previewInstance=Instantiate(target.PreviewModel,previewPivot);previewInstance.name="PreviewModel_Instance";previewInstance.transform.localPosition=Vector3.zero;previewInstance.transform.localRotation=Quaternion.identity;
        PrepareClone(previewInstance.transform);
        Renderer[] renderers=previewInstance.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0){Debug.LogError("InspectableUIController: Preview model has no Renderer.");return;}
        Bounds bounds=default;bool hasBounds=false;
        for(int i=0;i<renderers.Length;i++)
        {
            // Outline helpers are larger than the body; including them makes a huge white frame.
            string n=renderers[i].name;
            if(IsOutlineHelperName(n))continue;
            if(!hasBounds){bounds=renderers[i].bounds;hasBounds=true;}
            else bounds.Encapsulate(renderers[i].bounds);
        }
        if(!hasBounds){bounds=renderers[0].bounds;hasBounds=true;}
        float largest=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
        if(largest>.0001f)previewInstance.transform.localScale*=fittedModelSize/largest;
        hasBounds=false;
        for(int i=0;i<renderers.Length;i++)
        {
            string n=renderers[i].name;
            if(IsOutlineHelperName(n))continue;
            if(!hasBounds){bounds=renderers[i].bounds;hasBounds=true;}
            else bounds.Encapsulate(renderers[i].bounds);
        }
        if(hasBounds)previewInstance.transform.position+=previewPivot.position-bounds.center;
        if(target.PreviewPhoto!=null)ApplyPhotoToPreviewMaterial(target);
        previewPivot.localRotation=Quaternion.Euler(target.PreviewRotation);
        if(inspectZoomController!=null)inspectZoomController.BeginInspection(previewInstance);
    }

    private static bool UsesComicOutline(InspectableObject target)
    {
        if (target == null)
            return false;
        if (target.GetComponentInChildren<MeshOutlineStyle>(true) != null)
            return true;
        GameObject model = target.PreviewModel;
        if (model == null)
            return false;
        foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] mats = renderer.sharedMaterials;
            if (mats == null)
                continue;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].shader != null && mats[i].shader.name.Contains("OutlineBody"))
                    return true;
            }
        }
        return false;
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

    private static bool IsOutlineHelperName(string n)
    {
        return n == "OutlineShell" || n == "OutlineCreases"
            || n == "OutlineShell_Detached" || n == "OutlineCreases_Detached";
    }

    private static void PrepareClone(Transform root)
    {
        // Keep black OutlineBody + white OutlineShell. Instantiated clones copy shell children
        // but not MeshOutlineStyle's private refs — Detach resolves by name, then Destroy is
        // safe (Cleanup won't purge renamed helpers). Rebuild if PlayBuilder hasn't run yet.
        foreach (MeshOutlineStyle style in root.GetComponentsInChildren<MeshOutlineStyle>(true))
        {
            MeshOutlinePlayBuilder.Cancel(style);
            if (style.transform.Find("OutlineShell") == null
                && style.transform.Find("OutlineShell_Detached") == null)
                style.Rebuild();
            style.DetachGeneratedHelpersKeepVisible();
            Object.Destroy(style);
        }

        foreach (DiaryAssemblyController c in root.GetComponentsInChildren<DiaryAssemblyController>(true))
            Object.Destroy(c);
        foreach (DiaryInspectPuzzleController c in root.GetComponentsInChildren<DiaryInspectPuzzleController>(true))
            Object.Destroy(c);
        foreach (BedroomDesk c in root.GetComponentsInChildren<BedroomDesk>(true))
            Object.Destroy(c);
        foreach (InspectableObject c in root.GetComponentsInChildren<InspectableObject>(true))
            Object.Destroy(c);
        foreach (DiaryFragment c in root.GetComponentsInChildren<DiaryFragment>(true))
            Object.Destroy(c);

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
        foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour is InspectableHotspot)
                continue;
            behaviour.enabled = false;
        }
        foreach (InspectableHotspot hotspot in root.GetComponentsInChildren<InspectableHotspot>(true))
            hotspot.enabled = true;
    }

    public void SetInspectPrompts(string description, string putBackPromptText = null, bool showRotatePrompt = true)
    {
        if (descriptionText != null)
            descriptionText.text = description ?? string.Empty;
        if (putBackPromptText != null && putBackPrompt != null)
            putBackPrompt.text = putBackPromptText;
        if (rotatePrompt != null)
            rotatePrompt.gameObject.SetActive(showRotatePrompt);
        if (!showRotatePrompt)
            rotationMode = false;
    }

    private void EnsurePreviewLightEnabled()
    {
        if (previewCamera == null)
            return;
        Light[] lights = previewCamera.GetComponentsInChildren<Light>(true);
        if (lights == null || lights.Length == 0)
        {
            Transform studio = previewCamera.transform.parent;
            if (studio != null)
                lights = studio.GetComponentsInChildren<Light>(true);
        }
        if (lights == null)
            return;
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].enabled = true;
        }
    }

    private void DestroyPreview()
    {
        if(previewInstance!=null)Destroy(previewInstance);
        previewInstance=null;
        previewUsingComicOutline=false;
    }

    private void ApplyPreviewBackgroundSettings()
    {
        if(previewCamera==null||objectPreview==null)return;
        previewCamera.clearFlags=CameraClearFlags.SolidColor;

        // Black body + white outline: solid black clear (no magenta/black chroma key).
        if(previewUsingComicOutline || !transparentPreviewBackground)
        {
            Color fallback = previewUsingComicOutline ? new Color(0f, 0f, 0f, 1f) : previewFallbackColor;
            fallback.a=1f;
            previewCamera.backgroundColor=fallback;
            objectPreview.material=null;
            return;
        }

        // Magenta is intentional: when URP preserves target alpha it is removed by alpha; when
        // a renderer forces alpha to one the shader removes this key colour. The shader also
        // removes pure black for URP configurations that replace the clear colour with black.
        previewCamera.backgroundColor=new Color(1f,0f,1f,0f);
        if(transparentPreviewMaterial==null)
        {
            Shader shader=Shader.Find("UI/InspectablePreviewTransparent");
            if(shader!=null)transparentPreviewMaterial=new Material(shader){name="InspectablePreviewTransparent_Runtime"};
            else Debug.LogError("InspectableUIController: UI/InspectablePreviewTransparent shader was not found.",this);
        }
        objectPreview.material=transparentPreviewMaterial;
    }
    public bool TryCollectCurrent()
    {
        if(currentTarget==null)return false;
        IInspectableCollectible collectible=currentTarget.GetComponent<IInspectableCollectible>();
        if(collectible==null)return false;
        collectible.CollectFromInspection();return true;
    }
    public bool TryCloseZoom(){if(inspectZoomController==null||!inspectZoomController.IsZoomOpen)return false;inspectZoomController.CloseZoom();return true;}
    public void Hide()
    {
        InspectableObject finishedTarget=currentTarget;
        if(inspectZoomController!=null)inspectZoomController.StopInspection();
        DestroyPreview();
        currentTarget=null;
        rotationMode=false;
        if(IsUtilityOverlay)
        {
            if(savedBackgroundAlpha>=0f)canvasBackgroundAlpha=savedBackgroundAlpha;
            savedBackgroundAlpha=-1f;
            Image panelBackground=inspectPanel!=null?inspectPanel.GetComponent<Image>():null;
            if(panelBackground!=null)panelBackground.raycastTarget=savedPanelRaycast;
            if(objectPreview!=null)objectPreview.raycastTarget=savedPreviewRaycast;
        }
        IsUtilityOverlay=false;
        if(previewCamera!=null)previewCamera.enabled=false;
        if(objectPreview!=null)objectPreview.enabled=true;
        if(rotatePrompt!=null)rotatePrompt.gameObject.SetActive(true);
        if(inspectPanel!=null)inspectPanel.SetActive(false);
        if(finishedTarget!=null)finishedTarget.NotifyInspectFinished();
    }
    private void OnDestroy(){DestroyPreview();if(renderTexture!=null){renderTexture.Release();Destroy(renderTexture);}if(transparentPreviewMaterial!=null)Destroy(transparentPreviewMaterial);}
}
