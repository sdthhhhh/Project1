using UnityEngine;

/// <summary>
/// Controls the Photoes reveal puzzle while reusing the existing hand Crosshair,
/// CollectibleInspectionCanvas, E rotation and Q collection flow.
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
    }

    private void CachePhotoComponents()
    {
        photoRenderers = photoesRoot != null ? photoesRoot.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        photoColliders = photoesRoot != null ? photoesRoot.GetComponentsInChildren<Collider>(true) : new Collider[0];
        originalColliderStates = new bool[photoColliders.Length];
        for (int i = 0; i < photoColliders.Length; i++)
            originalColliderStates[i] = photoColliders[i] != null && photoColliders[i].enabled;
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
