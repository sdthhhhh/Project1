using UnityEngine;

/// <summary>
/// Controls the Photoes reveal puzzle while reusing the shared inspection Canvas,
/// hand Crosshair, E rotation and Q collection flow.
/// </summary>
[DisallowMultipleComponent]
public sealed class PhotoRestoreController : MonoBehaviour
{
    [Header("Photo Hierarchy")]
    [SerializeField, Tooltip("Root containing every photograph model that is revealed after restoration.")]
    private GameObject photoesRoot;
    [SerializeField, Tooltip("InteractedPhoto (1), inspected and collected through the shared collectible UI.")]
    private GameObject collectiblePhoto;

    [Header("Restore Position")]
    [SerializeField, Tooltip("Renderer belonging to InteractedPhoto. It stays hidden until restoration.")]
    private Renderer restoreTargetRenderer;
    [SerializeField, Tooltip("Collider belonging to InteractedPhoto. The existing Crosshair raycaster uses it for placement.")]
    private Collider restoreTargetCollider;

    [Header("Reveal On Restore")]
    [SerializeField, Tooltip("Activated when the photo is put back (e.g. DiaryFragment02). Same list pattern as BeerRestoreController.")]
    private GameObject[] revealOnRestore;

    public bool HasPhoto { get; private set; }
    public bool IsRestored { get; private set; }

    private InspectableCollectible collectible;
    private Renderer[] photoRenderers;
    private Collider[] photoColliders;
    private bool[] originalColliderStates;

    private void Awake()
    {
        ValidateReferences();
        CachePhotoComponents();

        collectible = collectiblePhoto != null
            ? collectiblePhoto.GetComponent<InspectableCollectible>()
            : null;

        if (collectible != null) collectible.Collected += CollectPhoto;
        else Debug.LogError("[PhotoRestoreController] InteractedPhoto (1) requires InspectableCollectible.", this);

        InitializeState();
    }

    private void OnDestroy()
    {
        if (collectible != null) collectible.Collected -= CollectPhoto;
    }

    public void Configure(GameObject root, GameObject sourcePhoto, Renderer targetRenderer, Collider targetCollider)
    {
        photoesRoot = root;
        collectiblePhoto = sourcePhoto;
        restoreTargetRenderer = targetRenderer;
        restoreTargetCollider = targetCollider;
    }

    public void CollectPhoto()
    {
        if (HasPhoto || IsRestored) return;
        HasPhoto = true;

        // InspectableCollectible hides the scene copy after Q. This explicit call
        // also makes the state safe when CollectPhoto is invoked from Inspector.
        if (collectiblePhoto != null) collectiblePhoto.SetActive(false);
    }

    public bool CanClickRestoreTarget(Collider hitCollider)
    {
        if (!HasPhoto || IsRestored || restoreTargetCollider == null || hitCollider == null) return false;
        return hitCollider == restoreTargetCollider || hitCollider.transform.IsChildOf(restoreTargetCollider.transform);
    }

    public void OnRestoreTargetClicked()
    {
        if (!HasPhoto || IsRestored) return;
        RestorePhotoes();
    }

    private void InitializeState()
    {
        HasPhoto = false;
        IsRestored = false;
        if (photoesRoot != null) photoesRoot.SetActive(true);
        if (collectiblePhoto != null) collectiblePhoto.SetActive(true);

        foreach (Renderer renderer in photoRenderers)
            if (renderer != null) renderer.enabled = IsPartOf(renderer.transform, collectiblePhoto);

        // Hidden photographs must not become invisible raycast blockers.
        for (int i = 0; i < photoColliders.Length; i++)
        {
            Collider collider = photoColliders[i];
            if (collider == null) continue;
            collider.enabled = IsPartOf(collider.transform, collectiblePhoto) || collider == restoreTargetCollider;
        }

        if (restoreTargetRenderer != null) restoreTargetRenderer.enabled = false;
        if (restoreTargetCollider != null) restoreTargetCollider.enabled = true;

        SetRevealOnRestoreActive(false);
        SyncPhotoOutlineVisibility();
    }

    private void RestorePhotoes()
    {
        IsRestored = true;
        HasPhoto = false;

        if (collectiblePhoto != null) collectiblePhoto.SetActive(false);
        if (photoesRoot != null) photoesRoot.SetActive(true);

        foreach (Renderer renderer in photoRenderers)
            if (renderer != null) renderer.enabled = !IsPartOf(renderer.transform, collectiblePhoto);

        for (int i = 0; i < photoColliders.Length; i++)
            if (photoColliders[i] != null) photoColliders[i].enabled = originalColliderStates[i];

        if (restoreTargetRenderer != null) restoreTargetRenderer.enabled = true;

        SetRevealOnRestoreActive(true);
        SyncPhotoOutlineVisibility();
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

    private void CachePhotoComponents()
    {
        if (photoesRoot == null)
        {
            photoRenderers = new Renderer[0];
            photoColliders = new Collider[0];
            originalColliderStates = new bool[0];
            return;
        }

        // Body renderers only — OutlineShell/Creases sync via MeshOutlineStyle (may be created later).
        var renderers = photoesRoot.GetComponentsInChildren<Renderer>(true);
        var bodyRenderers = new System.Collections.Generic.List<Renderer>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            string n = r.gameObject.name;
            if (n == "OutlineShell" || n == "OutlineCreases")
                continue;
            bodyRenderers.Add(r);
        }

        photoRenderers = bodyRenderers.ToArray();
        photoColliders = photoesRoot.GetComponentsInChildren<Collider>(true);
        originalColliderStates = new bool[photoColliders.Length];
        for (int i = 0; i < photoColliders.Length; i++)
            originalColliderStates[i] = photoColliders[i] != null && photoColliders[i].enabled;
    }

    private void SyncPhotoOutlineVisibility()
    {
        if (photoesRoot == null)
            return;

        MeshOutlineStyle[] styles = photoesRoot.GetComponentsInChildren<MeshOutlineStyle>(true);
        for (int i = 0; i < styles.Length; i++)
        {
            if (styles[i] != null)
                styles[i].SyncGeneratedVisibility();
        }
    }

    private static bool IsPartOf(Transform candidate, GameObject root)
    {
        return candidate != null && root != null &&
               (candidate.gameObject == root || candidate.IsChildOf(root.transform));
    }

    private void ValidateReferences()
    {
        if (photoesRoot == null) Debug.LogError("[PhotoRestoreController] Photoes Root is missing.", this);
        if (collectiblePhoto == null) Debug.LogError("[PhotoRestoreController] Collectible Photo is missing.", this);
        if (restoreTargetRenderer == null) Debug.LogError("[PhotoRestoreController] Restore Target Renderer is missing.", this);
        if (restoreTargetCollider == null) Debug.LogError("[PhotoRestoreController] Restore Target Collider is missing.", this);
    }
}
