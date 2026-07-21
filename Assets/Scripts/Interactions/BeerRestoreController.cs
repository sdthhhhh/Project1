using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

[DisallowMultipleComponent]
public sealed class BeerRestoreController : MonoBehaviour
{
    [Header("Beer Hierarchy")]
    [SerializeField, Tooltip("Complete final Beer root.")] private GameObject completeBeerRoot;
    [SerializeField, Tooltip("Incomplete Beer (1) root shown before restoration.")] private GameObject incompleteBeerRoot;
    [SerializeField, Tooltip("Inspectable Beer/1 piece collected when its Inspect UI closes.")] private GameObject collectiblePiece;

    [Header("Restore Slot")]
    [SerializeField, Tooltip("Renderer on Beer (1)/2. Hidden until final restoration; the GameObject stays active for raycasts.")] private Renderer restoreSlotRenderer;
    [SerializeField, Tooltip("Collider on Beer (1)/2 used by the existing Crosshair raycaster.")] private Collider restoreSlotCollider;
    [SerializeField, Tooltip("Optional existing click component to disable before collection. Usually left empty because InspectableRaycaster checks CanClick directly.")] private MonoBehaviour restoreInteractionComponent;

    public bool HasPiece { get; private set; }
    public bool IsRestored { get; private set; }

    private InspectableObject collectibleInspectable;

    private void Awake()
    {
        ValidateReferences();
        collectibleInspectable=collectiblePiece!=null?collectiblePiece.GetComponent<InspectableObject>():null;
        if(collectibleInspectable!=null)collectibleInspectable.InspectFinished+=CollectPiece;
        else Debug.LogError("[BeerRestoreController] Collectible Piece requires InspectableObject.",this);
        InitializeState();
    }

    private void OnDestroy()
    {
        if(collectibleInspectable!=null)collectibleInspectable.InspectFinished-=CollectPiece;
    }

    public void Configure(GameObject completeRoot,GameObject incompleteRoot,GameObject piece,Renderer slotRenderer,Collider slotCollider)
    {
        completeBeerRoot=completeRoot;
        incompleteBeerRoot=incompleteRoot;
        collectiblePiece=piece;
        restoreSlotRenderer=slotRenderer;
        restoreSlotCollider=slotCollider;
    }

    public void CollectPiece()
    {
        if(HasPiece||IsRestored)return;
        HasPiece=true;
        if(collectiblePiece!=null)collectiblePiece.SetActive(false);
        SetRestoreTargetEnabled(true);
    }

    public bool CanClickRestoreTarget(Collider hitCollider)
    {
        if(!HasPiece||IsRestored||restoreSlotCollider==null||hitCollider==null)return false;
        return hitCollider==restoreSlotCollider||hitCollider.transform.IsChildOf(restoreSlotCollider.transform);
    }

    public void OnRestoreTargetClicked()
    {
        if(!HasPiece||IsRestored)return;
        Restore();
    }

    private void InitializeState()
    {
        HasPiece=false;
        IsRestored=false;
        if(completeBeerRoot!=null)
        {
            completeBeerRoot.SetActive(true);
            foreach(Transform child in completeBeerRoot.transform)
                child.gameObject.SetActive(collectiblePiece!=null&&(child.gameObject==collectiblePiece||collectiblePiece.transform.IsChildOf(child)));
        }
        if(incompleteBeerRoot!=null)
        {
            incompleteBeerRoot.SetActive(true);
            foreach(Transform child in incompleteBeerRoot.transform)child.gameObject.SetActive(true);
        }
        SetRestoreVisual(false);
        SetRestoreTargetEnabled(false);
    }

    private void Restore()
    {
        if(!HasPiece||IsRestored)return;
        IsRestored=true;
        HasPiece=false;
        SetRestoreTargetEnabled(false);
        if(incompleteBeerRoot!=null)incompleteBeerRoot.SetActive(false);
        if(completeBeerRoot!=null)
        {
            completeBeerRoot.SetActive(true);
            Transform[] all=completeBeerRoot.GetComponentsInChildren<Transform>(true);
            foreach(Transform child in all)child.gameObject.SetActive(true);
        }
    }

    private void SetRestoreVisual(bool visible)
    {
        if(restoreSlotRenderer==null)return;
        foreach(Renderer renderer in restoreSlotRenderer.GetComponentsInChildren<Renderer>(true))renderer.enabled=visible;
        restoreSlotRenderer.enabled=visible;
    }

    private void SetRestoreTargetEnabled(bool enabled)
    {
        if(restoreInteractionComponent!=null)restoreInteractionComponent.enabled=enabled;
        // Collider remains enabled so the existing raycast can hit it. CanClickRestoreTarget
        // controls whether that hit changes the Crosshair or accepts a mouse click.
        if(restoreSlotCollider!=null)restoreSlotCollider.enabled=true;
    }

    private void ValidateReferences()
    {
        if(completeBeerRoot==null)Debug.LogError("[BeerRestoreController] Complete Beer Root is missing.",this);
        if(incompleteBeerRoot==null)Debug.LogError("[BeerRestoreController] Incomplete Beer Root is missing.",this);
        if(collectiblePiece==null)Debug.LogError("[BeerRestoreController] Collectible Piece is missing.",this);
        if(restoreSlotRenderer==null)Debug.LogError("[BeerRestoreController] Restore Slot Renderer is missing.",this);
        if(restoreSlotCollider==null)Debug.LogError("[BeerRestoreController] Restore Slot Collider is missing.",this);
    }
}

#if UNITY_EDITOR
[InitializeOnLoad]
public static class BeerRestoreSceneInstaller
{
    private const string SessionKey="BeerRestore.InstallAttempted.v1";
    static BeerRestoreSceneInstaller(){EditorApplication.delayCall+=InstallOnce;}
    private static void InstallOnce(){if(SessionState.GetBool(SessionKey,false))return;SessionState.SetBool(SessionKey,true);Install();}

    [MenuItem("Tools/Beer Restoration/Install From Current Beer Hierarchy")]
    public static void Install()
    {
        if(Application.isPlaying)return;
        GameObject complete=FindSceneObject("Beer");
        GameObject incomplete=FindSceneObject("Beer (1)");
        if(complete==null||incomplete==null){Debug.LogWarning("Beer restoration installer: Beer and Beer (1) must exist in the open scene.");return;}
        Transform piece=FindDirectChild(complete.transform,"1");
        Transform slot=FindDirectChild(incomplete.transform,"2");
        if(piece==null||slot==null){Debug.LogError("Beer restoration installer: direct children Beer/1 or Beer (1)/2 were not found.");return;}

        RemoveLegacyEInteractions(piece);
        RemoveLegacyEInteractions(slot);
        InspectableObject inspectable=piece.GetComponent<InspectableObject>();if(inspectable==null)inspectable=Undo.AddComponent<InspectableObject>(piece.gameObject);
        Collider pieceCollider=piece.GetComponentInChildren<Collider>(true);if(pieceCollider==null)pieceCollider=Undo.AddComponent<BoxCollider>(piece.gameObject);pieceCollider.isTrigger=false;
        Collider slotCollider=slot.GetComponentInChildren<Collider>(true);if(slotCollider==null)slotCollider=Undo.AddComponent<BoxCollider>(slot.gameObject);slotCollider.isTrigger=false;
        Renderer slotRenderer=slot.GetComponentInChildren<Renderer>(true);
        if(slotRenderer==null){Debug.LogError("Beer restoration installer: Beer (1)/2 needs a Renderer or Visual child.");return;}

        BeerRestoreController[] existing=Object.FindObjectsOfType<BeerRestoreController>(true);
        BeerRestoreController controller=existing.Length>0?existing[0]:Undo.AddComponent<BeerRestoreController>(slot.gameObject);
        for(int i=1;i<existing.Length;i++)Undo.DestroyObjectImmediate(existing[i]);
        controller.Configure(complete,incomplete,piece.gameObject,slotRenderer,slotCollider);
        EditorUtility.SetDirty(controller);EditorUtility.SetDirty(inspectable);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject=slot.gameObject;
        Debug.Log("Beer restoration installed: existing Crosshair opens Beer/1 inspection and clicks Beer (1)/2 only after collection. No Press E or new UI was added.");
    }

    private static void RemoveLegacyEInteractions(Transform root)
    {
        foreach(MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if(behaviour is IInteractable)Undo.DestroyObjectImmediate(behaviour);
    }
    private static Transform FindDirectChild(Transform parent,string name){foreach(Transform child in parent)if(child.name==name)return child;return null;}
    private static GameObject FindSceneObject(string name){foreach(GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())if(go.scene.IsValid()&&go.name==name)return go;return null;}
}
#endif
