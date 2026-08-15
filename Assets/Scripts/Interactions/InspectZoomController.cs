using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InspectZoomController : MonoBehaviour
{
    [Header("Existing Inspect UI")]
    [SerializeField] private Canvas inspectCanvas;
    [SerializeField] private RectTransform inspectPanel;
    [SerializeField] private RawImage inspectedObjectViewport;
    [SerializeField] private Camera previewCamera;

    [Header("Shared Hotspot UI")]
    [SerializeField] private Button magnifierButton;
    [SerializeField] private Image magnifierIcon;
    [SerializeField] private GameObject zoomOverlay;
    [SerializeField] private Image zoomImage;
    [SerializeField] private TMP_Text zoomText;
    [SerializeField] private Button zoomBackButton;

    [Header("Visibility")]
    [SerializeField, Tooltip("Layers checked between the preview camera and hotspot. Preview clones currently use layer 31.")] private LayerMask occlusionMask = ~0;
    [SerializeField, Range(0f, .25f), Tooltip("Keeps the current hotspot selected unless another candidate is clearly better, preventing icon flicker.")] private float selectionHysteresis = .035f;

    private InspectableHotspot[] hotspots = Array.Empty<InspectableHotspot>();
    private InspectableHotspot currentHotspot;
    private Transform previewRoot;
    private bool inspecting;
    private DiaryInspectPuzzleController activeDiaryPuzzle;

    public bool IsZoomOpen { get; private set; }

    private void Awake()
    {
        BindButtons();
        if (magnifierButton == null) Debug.LogError("[InspectZoomController] Magnifier Button reference is missing.", this);
        if (zoomOverlay == null) Debug.LogError("[InspectZoomController] Zoom Overlay reference is missing.", this);
        if (zoomImage == null) Debug.LogError("[InspectZoomController] Zoom Image reference is missing.", this);
        if (zoomText == null) Debug.LogError("[InspectZoomController] Zoom Text reference is missing.", this);
        HideAllUI();
    }

    private void Update()
    {
        if (!inspecting || IsZoomOpen) return;
        RefreshVisibleHotspot();
    }

    public void Configure(Canvas canvas, RectTransform panel, RawImage viewport, Camera camera,
        Button hotspotButton, Image hotspotIcon, GameObject overlay, Image closeupImage,
        TMP_Text closeupText, Button backButton)
    {
        inspectCanvas = canvas;
        inspectPanel = panel;
        inspectedObjectViewport = viewport;
        previewCamera = camera;
        magnifierButton = hotspotButton;
        magnifierIcon = hotspotIcon;
        zoomOverlay = overlay;
        zoomImage = closeupImage;
        zoomText = closeupText;
        zoomBackButton = backButton;
        BindButtons();
        HideAllUI();
    }

    public void BeginInspection(GameObject clonedPreviewRoot)
    {
        StopInspection();
        if (clonedPreviewRoot == null || previewCamera == null || inspectedObjectViewport == null) return;
        previewRoot = clonedPreviewRoot.transform;
        hotspots = clonedPreviewRoot.GetComponentsInChildren<InspectableHotspot>(true);
        foreach (InspectableHotspot hotspot in hotspots) hotspot.SetInspectMode(true);
        inspecting = true;
    }

    public void StopInspection()
    {
        if (activeDiaryPuzzle != null)
        {
            activeDiaryPuzzle.Close();
            activeDiaryPuzzle = null;
        }
        foreach (InspectableHotspot hotspot in hotspots)
            if (hotspot != null) hotspot.SetInspectMode(false);
        hotspots = Array.Empty<InspectableHotspot>();
        previewRoot = null;
        currentHotspot = null;
        inspecting = false;
        IsZoomOpen = false;
        HideAllUI();
    }

    public void OpenZoom(InspectableHotspot hotspot)
    {
        if (!inspecting || IsZoomOpen || hotspot == null) return;
        if (hotspot.OpenDiaryPuzzle && DiaryManager.Instance != null && DiaryManager.Instance.PuzzleCompleted)
        {
            hotspot.SetInspectMode(false);
            hotspot.enabled = false;
            InteractionUI.Instance?.ShowStatus("The diary cover is already complete.");
            return;
        }
        currentHotspot = hotspot;
        IsZoomOpen = true;
        if (magnifierButton != null) magnifierButton.gameObject.SetActive(false);

        if (hotspot.OpenDiaryPuzzle)
        {
            DiaryInspectPuzzleController puzzle = DiaryInspectPuzzleController.Instance
                ?? FindObjectOfType<DiaryInspectPuzzleController>();
            if (puzzle != null && previewRoot != null && previewCamera != null)
            {
                activeDiaryPuzzle = puzzle;
                puzzle.Open(
                    previewCamera,
                    previewRoot.parent != null ? previewRoot.parent : previewRoot,
                    previewRoot.gameObject,
                    hotspot,
                    inspectedObjectViewport);
                return;
            }
            Debug.LogWarning("InspectZoomController: diary puzzle hotspot clicked but DiaryInspectPuzzleController is missing.", this);
        }

        if (zoomOverlay != null) { zoomOverlay.SetActive(true); zoomOverlay.transform.SetAsLastSibling(); }
        if (zoomImage != null)
        {
            zoomImage.sprite = hotspot.ZoomedImage;
            zoomImage.preserveAspect = true;
            zoomImage.gameObject.SetActive(hotspot.ZoomedImage != null);
        }
        if (zoomText != null)
        {
            zoomText.text = hotspot.ZoomedText;
            zoomText.gameObject.SetActive(!string.IsNullOrWhiteSpace(hotspot.ZoomedText));
        }
    }

    public void CloseZoom()
    {
        if (!IsZoomOpen) return;
        InspectableHotspot closing = currentHotspot;
        if (activeDiaryPuzzle != null)
        {
            activeDiaryPuzzle.Close();
            activeDiaryPuzzle = null;
        }
        IsZoomOpen = false;
        if (zoomOverlay != null) zoomOverlay.SetActive(false);
        if (zoomImage != null) { zoomImage.sprite = null; zoomImage.gameObject.SetActive(false); }
        if (zoomText != null) { zoomText.text = string.Empty; zoomText.gameObject.SetActive(false); }
        currentHotspot = null;
        if (closing != null)
            closing.ApplyRevealOnZoomClose();
    }

    private void RefreshVisibleHotspot()
    {
        InspectableHotspot best = null;
        Vector3 bestViewport = default;
        float bestScore = float.MaxValue;
        bool currentStillVisible = false;
        float currentScore = float.MaxValue;
        Vector3 currentViewport = default;

        foreach (InspectableHotspot hotspot in hotspots)
        {
            if (hotspot == null || !TryGetVisibleViewport(hotspot, out Vector3 viewport)) continue;
            float score = ((Vector2)viewport - new Vector2(.5f, .5f)).sqrMagnitude;
            if (hotspot == currentHotspot) { currentStillVisible = true; currentScore = score; currentViewport = viewport; }
            if (score < bestScore) { best = hotspot; bestScore = score; bestViewport = viewport; }
        }

        if (currentStillVisible && currentScore <= bestScore + selectionHysteresis)
        { best = currentHotspot; bestViewport = currentViewport; }

        currentHotspot = best;
        if (magnifierButton == null) return;
        magnifierButton.gameObject.SetActive(best != null);
        if (best != null) PositionMagnifier(bestViewport);
    }

    private bool TryGetVisibleViewport(InspectableHotspot hotspot, out Vector3 viewport)
    {
        viewport = previewCamera.WorldToViewportPoint(hotspot.transform.position);
        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) return false;
        Vector3 directionToCamera = (previewCamera.transform.position - hotspot.transform.position).normalized;
        if (Vector3.Dot(hotspot.transform.forward, directionToCamera) < hotspot.VisibleDotThreshold) return false;
        return !IsOccluded(hotspot);
    }

    private bool IsOccluded(InspectableHotspot hotspot)
    {
        Vector3 origin = previewCamera.transform.position;
        Vector3 toHotspot = hotspot.transform.position - origin;
        float distance = toHotspot.magnitude;
        if (distance <= .001f) return false;
        RaycastHit[] hits = Physics.RaycastAll(origin, toHotspot / distance, distance + hotspot.OcclusionTolerance,
            occlusionMask, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hotspot.OwnsCollider(hit.collider)) return false;
            // Other parts of the same inspect preview (pot, sibling meshes) must not
            // block nested hotspots — otherwise buried props become nearly impossible to zoom.
            if (previewRoot != null && hit.transform.IsChildOf(previewRoot)) continue;
            if (hit.distance < distance - hotspot.OcclusionTolerance) return true;
        }
        return false;
    }

    private void PositionMagnifier(Vector3 viewport)
    {
        RectTransform viewportRect = inspectedObjectViewport.rectTransform;
        Vector2 local = new Vector2((viewport.x - .5f) * viewportRect.rect.width,
            (viewport.y - .5f) * viewportRect.rect.height);
        magnifierButton.transform.position = viewportRect.TransformPoint(local);
    }

    private void OpenCurrentHotspot()
    {
        if (currentHotspot != null) OpenZoom(currentHotspot);
    }

    private void BindButtons()
    {
        if (magnifierButton != null)
        {
            magnifierButton.onClick.RemoveListener(OpenCurrentHotspot);
            magnifierButton.onClick.AddListener(OpenCurrentHotspot);
        }
        if (zoomBackButton != null)
        {
            zoomBackButton.onClick.RemoveListener(CloseZoom);
            zoomBackButton.onClick.AddListener(CloseZoom);
        }
    }

    private void HideAllUI()
    {
        if (magnifierButton != null) magnifierButton.gameObject.SetActive(false);
        if (zoomOverlay != null) zoomOverlay.SetActive(false);
    }
}
