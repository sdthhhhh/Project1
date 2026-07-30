using UnityEngine;

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

    [Header("Reveal On Restore")]
    [SerializeField, Tooltip("Activated when beer is put back (e.g. DiaryFragment01). Reuse this list pattern for fragments 2–4 on other restore puzzles.")]
    private GameObject[] revealOnRestore;

    public bool HasPiece { get; private set; }
    public bool IsRestored { get; private set; }

    private InspectableObject collectibleInspectable;

    public AudioClip clip; // sfx bgm

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
        AudioSource audisour = new AudioSource();
        audisour.PlayOneShot(clip);
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
        SetRevealOnRestoreActive(false);
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
        SetRevealOnRestoreActive(true);
    }

    private void SetRevealOnRestoreActive(bool active)
    {
        if (revealOnRestore == null)
            return;
        for (int i = 0; i < revealOnRestore.Length; i++)
        {
            if (revealOnRestore[i] != null)
                revealOnRestore[i].SetActive(active);
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
