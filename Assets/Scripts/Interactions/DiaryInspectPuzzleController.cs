using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Second inspect layer: diary centered as background, collected pieces on the left.
/// Drop a piece onto the diary center → snap to the book's pivot (authored alignment).
/// </summary>
[DisallowMultipleComponent]
public sealed class DiaryInspectPuzzleController : MonoBehaviour
{
    public static DiaryInspectPuzzleController Instance { get; private set; }

    [Header("Sources (scene objects)")]
    [SerializeField] private GameObject bookSource;
    [SerializeField] private GameObject[] pieceSources = new GameObject[4];
    [SerializeField] private DiaryAssemblySocket[] sockets;
    [SerializeField] private InspectableUIController inspectUI;

    [Header("Camera / diary background")]
    [SerializeField, Min(5f)] private float zoomedFieldOfView = 32f;
    [SerializeField, Min(0.5f), Tooltip("Camera distance as a multiple of the diary's largest bounds axis.")]
    private float frameDistanceFactor = 2.2f;
    [SerializeField, Tooltip("Cover-facing rotation while the puzzle layer is open (depression toward camera).")]
    private Vector3 puzzleBookEuler = new Vector3(8f, 0f, 180f);
    [SerializeField, Range(0.05f, 1f), Tooltip("Extra shrink for pieces snapped onto the diary cover.")]
    private float snapScaleMultiplier = 0.35f;

    [Header("Left tray")]
    [SerializeField, Range(0.02f, 0.45f)] private float trayViewportX = 0.08f;
    [SerializeField, Range(0.2f, 0.9f)] private float trayTopViewportY = 0.78f;
    [SerializeField, Min(0.05f)] private float trayViewportYStep = 0.16f;
    [SerializeField, Min(0.2f), Tooltip("Smaller = closer to camera than the diary, so pieces stay clickable.")]
    private float trayDepth = 0.85f;
    [SerializeField, Range(0.02f, 0.25f), Tooltip("Screen-space click radius fallback when outline rim is clicked.")]
    private float pieceClickViewportRadius = 0.1f;

    [Header("Snap")]
    [SerializeField, Range(0.05f, 0.6f), Tooltip("Viewport radius around screen center = diary snap zone.")]
    private float snapViewportRadius = 0.28f;
    [SerializeField, Min(0.01f), Tooltip("Also snap within this world distance of the diary center.")]
    private float snapWorldDistance = 0.55f;
    [SerializeField, Range(0.05f, 1f), Tooltip("Tray piece size vs diary-matched size. Smaller = tinier tray icons.")]
    private float pieceVisualScaleMultiplier = 0.12f;

    [Header("Prompts")]
    [SerializeField, TextArea(2, 4)] private string puzzleDescription =
        "Drag each fragment onto the diary.\nEsc — Back";

    [Header("Reveal On Complete")]
    [SerializeField, Tooltip("Activated when the cover puzzle is finished (e.g. DiaryFragment04). Does not open the diary book UI.")]
    private GameObject[] revealOnComplete;

    private Camera previewCamera;
    private Transform previewPivot;
    private GameObject bookPreview;
    private RawImage previewViewport;
    private Canvas previewCanvas;
    private GameObject trayRoot;
    private readonly List<PuzzlePiece> pieces = new List<PuzzlePiece>();
    private readonly HashSet<int> filledIds = new HashSet<int>();
    private PuzzlePiece dragging;
    private Plane dragPlane;
    private float dragDepth;
    private float pieceScaleOnBook = 1f;

    private Vector3 savedCamLocalPos;
    private Quaternion savedCamLocalRot;
    private float savedCamFov;
    private bool savedCamLocal;
    private Vector3 savedCamWorldPos;
    private Quaternion savedCamWorldRot;
    private Quaternion savedPivotRotation;
    private bool cameraFramed;

    public bool IsOpen { get; private set; }

    private sealed class PuzzlePiece
    {
        public int FragmentId;
        public Transform Transform;
        public Collider Collider;
        public bool Snapped;
        public Vector3 TrayWorldPos;
        public Quaternion TrayWorldRot;
    }

    private void Awake()
    {
        if (IsInspectPreviewClone())
            return;
        Instance = this;
        AutoWireSources();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (!IsInspectPreviewClone())
            Close();
    }

    private bool IsInspectPreviewClone()
    {
        Transform t = transform;
        while (t != null)
        {
            string n = t.name;
            if (n.StartsWith("PreviewModel")
                || n.Contains("InspectedModelPivot")
                || n.Contains("CollectibleModelPivot")
                || n.Contains("ObjectInspection3DStudio")
                || n.Contains("CollectibleInspection3DStudio")
                || n == "DiaryInspectPuzzleRoot"
                || n == "DiaryPuzzleTray")
                return true;
            t = t.parent;
        }
        return false;
    }

    public void Configure(GameObject book, GameObject[] piecesIn, DiaryAssemblySocket[] socketsIn, InspectableUIController ui)
    {
        bookSource = book;
        pieceSources = piecesIn;
        sockets = socketsIn;
        inspectUI = ui;
    }

    public void Open(Camera camera, Transform pivot, GameObject currentInspectPreview, InspectableHotspot hotspot, RawImage viewport = null)
    {
        if (IsOpen)
            return;

        AutoWireSources();
        if (currentInspectPreview == null || camera == null)
        {
            Debug.LogError("DiaryInspectPuzzleController: inspect preview / camera missing.", this);
            return;
        }

        previewCamera = camera;
        previewPivot = pivot;
        bookPreview = currentInspectPreview;
        previewViewport = viewport;
        previewCanvas = viewport != null ? viewport.GetComponentInParent<Canvas>() : null;
        if (previewViewport == null)
        {
            // Fallback: active inspect RawImage.
            RawImage[] raws = Object.FindObjectsOfType<RawImage>(true);
            for (int i = 0; i < raws.Length; i++)
            {
                if (raws[i] != null && raws[i].gameObject.activeInHierarchy
                    && raws[i].name.IndexOf("Viewport", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    previewViewport = raws[i];
                    previewCanvas = raws[i].GetComponentInParent<Canvas>();
                    break;
                }
            }
        }

        if (inspectUI == null)
            inspectUI = FindObjectOfType<InspectableUIController>();

        if (inspectUI != null)
            inspectUI.SetInspectPrompts(puzzleDescription, "Back", showRotatePrompt: false);

        ComputePieceScaleRelativeToBook();
        FrameDiaryAsBackground();
        SetBookCollidersEnabled(false);
        BuildCollectedTray();

        IsOpen = true;
        filledIds.Clear();
        InteractionUI.Instance?.ShowStatus(puzzleDescription);
    }

    public void Close()
    {
        if (!IsOpen && trayRoot == null && !cameraFramed)
            return;

        IsOpen = false;
        dragging = null;
        pieces.Clear();
        filledIds.Clear();

        if (trayRoot != null)
        {
            Destroy(trayRoot);
            trayRoot = null;
        }

        SetBookCollidersEnabled(true);
        RestoreCamera();
        RestorePrompts();

        previewCamera = null;
        previewPivot = null;
        bookPreview = null;
        previewViewport = null;
        previewCanvas = null;
    }

    private void Update()
    {
        if (!IsOpen || previewCamera == null)
            return;

        // Keep the framed diary pose locked (no rotate).
        if (previewPivot != null)
            previewPivot.localRotation = Quaternion.Euler(puzzleBookEuler);

        if (dragging != null)
        {
            if (Input.GetMouseButton(0))
                MoveDragging();
            if (Input.GetMouseButtonUp(0))
                EndDrag();
            return;
        }

        if (Input.GetMouseButtonDown(0))
            TryBeginDrag();
    }

    private void RestorePrompts()
    {
        if (inspectUI == null)
            return;

        string desc = "A diary left on the desk. Rotate it — something on the cover looks incomplete.";
        if (bookSource != null)
        {
            InspectableObject insp = bookSource.GetComponent<InspectableObject>();
            if (insp != null && !string.IsNullOrWhiteSpace(insp.Description))
                desc = insp.Description;
        }

        inspectUI.SetInspectPrompts(desc, "Put Back", showRotatePrompt: true);
    }

    private void ComputePieceScaleRelativeToBook()
    {
        float bookScale = AverageAbsScale(bookSource != null ? bookSource.transform.lossyScale : Vector3.one);
        float pieceScale = 0.05f;
        if (pieceSources != null)
        {
            for (int i = 0; i < pieceSources.Length; i++)
            {
                if (pieceSources[i] == null)
                    continue;
                pieceScale = AverageAbsScale(pieceSources[i].transform.lossyScale);
                break;
            }
        }

        if (bookScale < 1e-6f)
            bookScale = 1f;
        pieceScaleOnBook = pieceScale / bookScale;
    }

    private static float AverageAbsScale(Vector3 s)
    {
        return (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
    }

    private Collider[] disabledBookColliders;

    private void SetBookCollidersEnabled(bool enabled)
    {
        if (!enabled)
        {
            if (bookPreview == null)
                return;
            Collider[] all = bookPreview.GetComponentsInChildren<Collider>(true);
            var list = new List<Collider>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].enabled)
                    continue;
                // Keep hotspot usable if needed; sockets/book body block tray picks.
                if (all[i].GetComponent<InspectableHotspot>() != null)
                    continue;
                all[i].enabled = false;
                list.Add(all[i]);
            }
            disabledBookColliders = list.ToArray();
            return;
        }

        if (disabledBookColliders == null)
            return;
        for (int i = 0; i < disabledBookColliders.Length; i++)
        {
            if (disabledBookColliders[i] != null)
                disabledBookColliders[i].enabled = true;
        }
        disabledBookColliders = null;
    }

    private void FrameDiaryAsBackground()
    {
        if (previewCamera == null || bookPreview == null)
            return;

        Transform cam = previewCamera.transform;
        if (cam.parent != null)
        {
            savedCamLocal = true;
            savedCamLocalPos = cam.localPosition;
            savedCamLocalRot = cam.localRotation;
        }
        else
        {
            savedCamLocal = false;
            savedCamWorldPos = cam.position;
            savedCamWorldRot = cam.rotation;
        }
        savedCamFov = previewCamera.fieldOfView;
        if (previewPivot != null)
            savedPivotRotation = previewPivot.localRotation;
        cameraFramed = true;

        if (previewPivot != null)
            previewPivot.localRotation = Quaternion.Euler(puzzleBookEuler);

        Bounds bounds = GetBookBounds();
        Vector3 center = bounds.center;
        float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (largest < 0.01f)
            largest = 1f;

        // Cover / depression faces local +Z (CoverPuzzleHotspot). Stand on that side.
        Vector3 coverNormal = GetCoverNormal();
        Vector3 eye = center + coverNormal * (largest * frameDistanceFactor);
        eye += Vector3.up * (largest * 0.05f);
        cam.position = eye;
        // Use the book's own up so cover text/depression isn't rolled upside-down.
        Vector3 coverUp = bookPreview.transform.up;
        if (Vector3.Dot(coverUp, Vector3.up) < 0f)
            coverUp = -coverUp;
        cam.rotation = Quaternion.LookRotation((center - eye).normalized, coverUp);
        previewCamera.fieldOfView = zoomedFieldOfView;
    }

    /// <summary>World-space normal of the diary cover (depression side).</summary>
    private Vector3 GetCoverNormal()
    {
        if (bookPreview == null)
            return Vector3.forward;

        Transform hotspot = FindCoverHotspot(bookPreview.transform);
        if (hotspot != null)
        {
            Vector3 fromCenter = hotspot.position - GetBookBounds().center;
            if (fromCenter.sqrMagnitude > 1e-8f)
                return fromCenter.normalized;
        }

        // Mesh cover is authored on local +Z.
        Vector3 n = bookPreview.transform.forward;
        return n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.forward;
    }

    private static Transform FindCoverHotspot(Transform root)
    {
        if (root == null)
            return null;
        Transform direct = root.Find("CoverPuzzleHotspot");
        if (direct != null)
            return direct;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "CoverPuzzleHotspot")
                return t;
        }
        return null;
    }

    private void RestoreCamera()
    {
        if (!cameraFramed || previewCamera == null)
        {
            cameraFramed = false;
            return;
        }

        Transform cam = previewCamera.transform;
        if (savedCamLocal && cam.parent != null)
        {
            cam.localPosition = savedCamLocalPos;
            cam.localRotation = savedCamLocalRot;
        }
        else
        {
            cam.position = savedCamWorldPos;
            cam.rotation = savedCamWorldRot;
        }
        previewCamera.fieldOfView = savedCamFov;
        if (previewPivot != null)
            previewPivot.localRotation = savedPivotRotation;
        cameraFramed = false;
    }

    private void BuildCollectedTray()
    {
        if (previewCamera == null || bookPreview == null)
            return;

        trayRoot = new GameObject("DiaryPuzzleTray");
        trayRoot.transform.SetParent(previewPivot != null ? previewPivot : bookPreview.transform.parent, true);
        SetLayerRecursive(trayRoot.transform, 31);

        pieces.Clear();
        int trayIndex = 0;
        if (pieceSources == null)
            return;

        for (int i = 0; i < pieceSources.Length; i++)
        {
            if (pieceSources[i] == null)
                continue;
            int id = ResolveFragmentId(pieceSources[i], i + 1);
            if (DiaryManager.Instance != null && !DiaryManager.Instance.HasFragment(id))
                continue;

            GameObject pieceGo = ClonePieceVisual(pieceSources[i], trayRoot.transform, "TrayPiece0" + id);
            ApplyTrayScale(pieceGo.transform);

            float y = trayTopViewportY - trayIndex * trayViewportYStep;
            Vector3 world = previewCamera.ViewportToWorldPoint(new Vector3(trayViewportX, y, trayDepth));
            pieceGo.transform.position = world;
            pieceGo.transform.rotation = Quaternion.LookRotation(previewCamera.transform.forward, previewCamera.transform.up);

            // After final pose/scale: pick collider must cover outline shells (visible white rim).
            Collider col = EnsurePickCollider(pieceGo);
            pieces.Add(new PuzzlePiece
            {
                FragmentId = id,
                Transform = pieceGo.transform,
                Collider = col,
                Snapped = false,
                TrayWorldPos = world,
                TrayWorldRot = pieceGo.transform.rotation
            });
            trayIndex++;
        }

        if (pieces.Count == 0)
            InteractionUI.Instance?.ShowStatus("You have no diary fragments yet. Collect them in the world first.");
    }

    private void ApplyTrayScale(Transform piece)
    {
        float bookNow = bookPreview != null ? AverageAbsScale(bookPreview.transform.lossyScale) : 1f;
        float targetWorld = bookNow * pieceScaleOnBook * pieceVisualScaleMultiplier;
        float parentScale = piece.parent != null ? AverageAbsScale(piece.parent.lossyScale) : 1f;
        if (parentScale < 1e-6f)
            parentScale = 1f;
        piece.localScale = Vector3.one * (targetWorld / parentScale);
    }

    private void ApplyScaleSnappedToBook(Transform piece)
    {
        // Parent = diary; shrink further so fragments fit the cover depression.
        piece.localScale = Vector3.one * (pieceScaleOnBook * snapScaleMultiplier);
    }

    private bool IsOverDiarySnapZone(Vector3 worldPoint)
    {
        if (bookPreview == null || previewCamera == null)
            return false;

        Vector3 bookCenter = GetBookBounds().center;
        if (Vector3.Distance(worldPoint, bookCenter) <= snapWorldDistance)
            return true;

        Vector3 vp = previewCamera.WorldToViewportPoint(worldPoint);
        if (vp.z <= 0f)
            return false;
        Vector2 delta = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        return delta.magnitude <= snapViewportRadius;
    }

    private void TryBeginDrag()
    {
        if (!TryGetPreviewMouseViewport(out Vector2 mouseVp))
            return;

        PuzzlePiece piece = null;
        float hitDistance = trayDepth;

        if (TryGetPreviewRay(mouseVp, out Ray ray))
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, 50f, ~0, QueryTriggerInteraction.Collide);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < hits.Length; i++)
                {
                    PuzzlePiece candidate = FindPiece(hits[i].collider);
                    if (candidate == null || candidate.Snapped)
                        continue;
                    piece = candidate;
                    hitDistance = hits[i].distance;
                    break;
                }
            }
        }

        if (piece == null)
            piece = FindClosestPieceToPreviewMouse(mouseVp, out hitDistance);

        if (piece == null)
            return;

        dragging = piece;
        dragDepth = Mathf.Max(0.05f, hitDistance);
        dragPlane = new Plane(-previewCamera.transform.forward, piece.Transform.position);
        piece.Transform.SetParent(trayRoot != null ? trayRoot.transform : previewPivot, true);
    }

    private void MoveDragging()
    {
        if (dragging == null)
            return;

        if (!TryGetPreviewMouseViewport(out Vector2 mouseVp) || !TryGetPreviewRay(mouseVp, out Ray ray))
            return;

        if (dragPlane.Raycast(ray, out float enter))
            dragging.Transform.position = ray.GetPoint(enter);
        else
            dragging.Transform.position = ray.GetPoint(dragDepth);
    }

    private bool TryGetPreviewMouseViewport(out Vector2 viewport)
    {
        viewport = default;
        if (previewCamera == null)
            return false;

        if (previewViewport == null)
        {
            Vector3 sp = previewCamera.ScreenToViewportPoint(Input.mousePosition);
            viewport = new Vector2(sp.x, sp.y);
            return sp.z >= 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }

        Camera eventCam = null;
        if (previewCanvas != null && previewCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCam = previewCanvas.worldCamera;

        RectTransform rt = previewViewport.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, Input.mousePosition, eventCam, out Vector2 local))
            return false;

        Rect r = rt.rect;
        if (r.width < 1e-4f || r.height < 1e-4f)
            return false;

        float u = (local.x - r.xMin) / r.width;
        float v = (local.y - r.yMin) / r.height;
        viewport = new Vector2(u, v);
        return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
    }

    private bool TryGetPreviewRay(Vector2 viewport, out Ray ray)
    {
        ray = default;
        if (previewCamera == null)
            return false;
        ray = previewCamera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
        return true;
    }

    private PuzzlePiece FindClosestPieceToPreviewMouse(Vector2 mouseVp, out float distance)
    {
        distance = trayDepth;
        PuzzlePiece best = null;
        float bestScore = pieceClickViewportRadius * pieceClickViewportRadius;

        for (int i = 0; i < pieces.Count; i++)
        {
            PuzzlePiece p = pieces[i];
            if (p == null || p.Snapped || p.Transform == null)
                continue;

            Bounds b = GetPieceBounds(p.Transform);
            Vector3 vp3 = previewCamera.WorldToViewportPoint(b.center);
            if (vp3.z <= 0f)
                continue;

            float score = ((Vector2)vp3 - mouseVp).sqrMagnitude;
            if (score > bestScore)
                continue;

            bestScore = score;
            best = p;
            distance = Vector3.Distance(previewCamera.transform.position, b.center);
        }

        return best;
    }

    private static Bounds GetPieceBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return new Bounds(root.position, Vector3.one * 0.05f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private void EndDrag()
    {
        if (dragging == null)
            return;

        PuzzlePiece piece = dragging;
        dragging = null;

        if (!IsOverDiarySnapZone(piece.Transform.position))
        {
            piece.Transform.SetParent(trayRoot != null ? trayRoot.transform : previewPivot, true);
            piece.Transform.position = piece.TrayWorldPos;
            piece.Transform.rotation = piece.TrayWorldRot;
            ApplyTrayScale(piece.Transform);
            return;
        }

        // Snap to diary pivot — piece meshes share the book's local layout.
        piece.Transform.SetParent(bookPreview.transform, false);
        // Sit just in front of the cover (+local Z). Negative Z buries pieces inside the book mesh.
        piece.Transform.localPosition = new Vector3(0f, 0f, GetCoverSnapLocalZ());
        piece.Transform.localRotation = Quaternion.identity;
        ApplyScaleSnappedToBook(piece.Transform);
        EnsureOutlineHelpersVisible(piece.Transform);
        EnsurePieceRenderersVisible(piece.Transform);
        piece.Snapped = true;
        filledIds.Add(piece.FragmentId);
        if (piece.Collider != null)
            piece.Collider.enabled = false;

        InteractionUI.Instance?.ShowStatus($"Fragment {piece.FragmentId} placed ({filledIds.Count}/{RequiredCount()})");

        if (AllRequiredPlaced())
            CompletePuzzle();
    }

    private Bounds GetBookBounds()
    {
        Renderer[] renderers = bookPreview.GetComponentsInChildren<Renderer>(true);
        Bounds? bounds = null;
        for (int i = 0; i < renderers.Length; i++)
        {
            string n = renderers[i].name;
            if (IsOutlineHelperRendererName(n) || n.StartsWith("TrayPiece") || n.StartsWith("PuzzlePiece"))
                continue;
            if (trayRoot != null && renderers[i].transform.IsChildOf(trayRoot.transform))
                continue;
            // Skip already-snapped pieces parented under the book.
            if (n.StartsWith("TrayPiece") || IsSnappedPieceRenderer(renderers[i]))
                continue;
            if (bounds == null)
                bounds = renderers[i].bounds;
            else
            {
                Bounds b = bounds.Value;
                b.Encapsulate(renderers[i].bounds);
                bounds = b;
            }
        }
        return bounds ?? new Bounds(bookPreview.transform.position, Vector3.one * 0.2f);
    }

    private bool IsSnappedPieceRenderer(Renderer r)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].Snapped && r.transform.IsChildOf(pieces[i].Transform))
                return true;
        }
        return false;
    }

    private int RequiredCount()
    {
        if (DiaryManager.Instance != null && DiaryManager.Instance.HasCollectedAllFragments)
            return DiaryManager.Instance.TotalFragments;
        return Mathf.Max(1, pieces.Count);
    }

    private bool AllRequiredPlaced()
    {
        if (DiaryManager.Instance != null && DiaryManager.Instance.HasCollectedAllFragments)
            return filledIds.Count >= DiaryManager.Instance.TotalFragments;

        for (int i = 0; i < pieces.Count; i++)
        {
            if (!pieces[i].Snapped)
                return false;
        }
        return pieces.Count > 0;
    }

    private void CompletePuzzle()
    {
        DiaryManager.Instance?.MarkPuzzleCompleted();
        dragging = null;

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].Collider != null)
                pieces[i].Collider.enabled = false;
        }

        SetRevealOnCompleteActive(true);
        InteractionUI.Instance?.ShowStatus("The diary cover is complete.");

        // Close inspect / puzzle — do not open the diary book reading UI.
        Close();
        if (inspectUI != null)
            inspectUI.Hide();
        InspectableRaycaster raycaster = FindObjectOfType<InspectableRaycaster>();
        if (raycaster != null)
            raycaster.ForceCloseInspection();
    }

    private void SetRevealOnCompleteActive(bool active)
    {
        if (revealOnComplete == null)
            return;
        for (int i = 0; i < revealOnComplete.Length; i++)
        {
            if (revealOnComplete[i] != null)
                revealOnComplete[i].SetActive(active);
        }
    }

    private PuzzlePiece FindPiece(Collider col)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].Collider == col
                || pieces[i].Transform == col.transform
                || col.transform.IsChildOf(pieces[i].Transform))
                return pieces[i];
        }
        return null;
    }

    private static int ResolveFragmentId(GameObject source, int fallback)
    {
        DiaryFragment frag = source.GetComponentInChildren<DiaryFragment>(true);
        if (frag != null)
            return frag.FragmentId;

        string name = source.name;
        if (name.Length > 0 && char.IsDigit(name[name.Length - 1]))
        {
            int parsed = name[name.Length - 1] - '0';
            if (parsed >= 1 && parsed <= 9)
                return parsed;
        }
        return fallback;
    }

    private static GameObject ClonePieceVisual(GameObject source, Transform parent, string name)
    {
        GameObject clone = Object.Instantiate(source, parent);
        clone.name = name;
        clone.SetActive(true);

        foreach (MeshOutlineStyle style in clone.GetComponentsInChildren<MeshOutlineStyle>(true))
        {
            MeshOutlinePlayBuilder.Cancel(style);
            if (style.transform.Find("OutlineShell") == null
                && style.transform.Find("OutlineShell_Detached") == null)
                style.Rebuild();
            style.DetachGeneratedHelpersKeepVisible();
            Object.Destroy(style);
        }

        foreach (Component c in clone.GetComponentsInChildren<Component>(true))
        {
            if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer
                || c is MeshCollider || c is BoxCollider || c is CapsuleCollider || c is SkinnedMeshRenderer)
                continue;
            Object.Destroy(c);
        }

        for (int i = clone.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = clone.transform.GetChild(i);
            string n = child.name;
            if (n.StartsWith("AssemblySocket") || n.Contains("Hotspot"))
                Object.Destroy(child.gameObject);
        }

        EnsureOutlineHelpersVisible(clone.transform);
        SetLayerRecursive(clone.transform, 31);
        return clone;
    }

    private static bool IsOutlineHelperRendererName(string n)
    {
        return n == "OutlineShell" || n == "OutlineCreases"
            || n == "OutlineShell_Detached" || n == "OutlineCreases_Detached";
    }

    /// <summary>Local Z just above the cover face so snapped pieces aren't buried in the diary body.</summary>
    private float GetCoverSnapLocalZ()
    {
        float z = 0.02f;
        if (bookPreview == null)
            return z;

        MeshFilter mf = bookPreview.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            z = mf.sharedMesh.bounds.max.z + 0.012f;

        Transform hotspot = FindCoverHotspot(bookPreview.transform);
        if (hotspot != null)
            z = Mathf.Max(z, hotspot.localPosition.z * 0.5f);

        return z;
    }

    private static void EnsurePieceRenderersVisible(Transform root)
    {
        foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr == null)
                continue;
            mr.enabled = true;
            mr.gameObject.SetActive(true);
        }
    }

    private static void EnsureOutlineHelpersVisible(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            string n = child.name;
            if (n != "OutlineShell" && n != "OutlineCreases"
                && n != "OutlineShell_Detached" && n != "OutlineCreases_Detached")
                continue;
            child.gameObject.SetActive(true);
            MeshRenderer mr = child.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.enabled = true;
        }
    }

    private static Collider EnsurePickCollider(GameObject pieceGo)
    {
        // Replace author colliders with one box that covers body + OutlineShell (what the player sees).
        Collider[] existing = pieceGo.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null)
                Object.Destroy(existing[i]);
        }

        Bounds world = GetPieceBounds(pieceGo.transform);
        BoxCollider box = pieceGo.AddComponent<BoxCollider>();
        box.isTrigger = false;

        Transform t = pieceGo.transform;
        Vector3 localCenter = t.InverseTransformPoint(world.center);
        Vector3 worldSize = world.size;
        Vector3 lossy = t.lossyScale;
        Vector3 localSize = new Vector3(
            worldSize.x / Mathf.Max(1e-6f, Mathf.Abs(lossy.x)),
            worldSize.y / Mathf.Max(1e-6f, Mathf.Abs(lossy.y)),
            worldSize.z / Mathf.Max(1e-6f, Mathf.Abs(lossy.z)));

        // Pad so the white outline rim stays easy to click.
        localSize *= 1.15f;
        // Avoid degenerate thin-axis colliders.
        localSize.x = Mathf.Max(localSize.x, 0.02f);
        localSize.y = Mathf.Max(localSize.y, 0.02f);
        localSize.z = Mathf.Max(localSize.z, 0.02f);

        box.center = localCenter;
        box.size = localSize;
        box.enabled = true;
        return box;
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }

    private void AutoWireSources()
    {
        if (bookSource == null)
        {
            bookSource = GameObject.Find("DiaryReconstructionBoard");
            if (bookSource == null)
            {
                foreach (Transform t in Object.FindObjectsOfType<Transform>(true))
                {
                    if (t.name == "DiaryReconstructionBoard")
                    {
                        bookSource = t.gameObject;
                        break;
                    }
                }
            }
        }

        if (pieceSources == null || pieceSources.Length == 0 || pieceSources[0] == null)
        {
            pieceSources = new GameObject[4];
            GameObject root = GameObject.Find("DiaryFragments");
            if (root == null)
            {
                foreach (Transform t in Object.FindObjectsOfType<Transform>(true))
                {
                    if (t.name == "DiaryFragments")
                    {
                        root = t.gameObject;
                        break;
                    }
                }
            }
            if (root != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    Transform child = root.transform.Find("DiaryFragment0" + (i + 1));
                    if (child != null)
                        pieceSources[i] = child.gameObject;
                }
            }
        }

        if (sockets == null || sockets.Length == 0)
        {
            if (bookSource != null)
                sockets = bookSource.GetComponentsInChildren<DiaryAssemblySocket>(true);
        }

        if (inspectUI == null)
            inspectUI = Object.FindObjectOfType<InspectableUIController>();
    }
}
