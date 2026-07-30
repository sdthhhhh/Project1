using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class InspectableHotspot : MonoBehaviour
{
    [Header("Hotspot Content")]
    [SerializeField, Tooltip("Stable identifier used for notes or future save data.")] private string hotspotId = "BackNumber4721";
    [SerializeField, Tooltip("Prepared high-resolution close-up sprite. Optional when Zoomed Text is sufficient.")] private Sprite zoomedImage;
    [SerializeField, TextArea(2, 8), Tooltip("Text displayed beside the close-up. Leave empty to hide ZoomText.")] private string zoomedText = "4721";
    [SerializeField, Tooltip("If on, magnifier opens the diary cover puzzle instead of a photo close-up.")] private bool openDiaryPuzzle;
    [SerializeField, Tooltip("Activated when the player closes this hotspot's zoom (after reading the clue).")]
    private GameObject[] revealOnZoomClose;
    [SerializeField, Tooltip("If set, closing this zoom loads the scene (Build Settings name). E.g. EndScene after the flower-bed knife.")]
    private string loadSceneOnZoomClose;

    [Header("Hotspot Geometry")]
    [SerializeField, Tooltip("Small collider positioned over the detail being investigated.")] private Collider hotspotCollider;
    [SerializeField, Tooltip("Optional world-space icon. The current implementation uses one shared screen-space button, so this stays hidden.")] private GameObject magnifierIcon;
    [SerializeField, Range(-1f, 1f), Tooltip("Minimum dot product between hotspot forward and direction to preview camera. Local +Z should point away from the item surface.")] private float visibleDotThreshold = .35f;
    [SerializeField, Min(0f), Tooltip("Tolerance in metres when deciding whether the item's own surface blocks this hotspot.")] private float occlusionTolerance = .035f;

    public string HotspotId => hotspotId;
    public Sprite ZoomedImage => zoomedImage;
    public string ZoomedText => zoomedText;
    public bool OpenDiaryPuzzle => openDiaryPuzzle;
    public Collider HotspotCollider => hotspotCollider;
    public float VisibleDotThreshold => visibleDotThreshold;
    public float OcclusionTolerance => occlusionTolerance;

    public void ConfigureDiaryPuzzle(string id, string text)
    {
        hotspotId = id;
        zoomedText = text ?? string.Empty;
        openDiaryPuzzle = true;
        zoomedImage = null;
    }

    public void ApplyRevealOnZoomClose()
    {
        if (revealOnZoomClose != null)
        {
            for (int i = 0; i < revealOnZoomClose.Length; i++)
            {
                GameObject go = revealOnZoomClose[i];
                if (go == null)
                    continue;
                go.SetActive(true);
                // Force outline/crease build for styles that were inactive at Play start.
                MeshOutlineStyle[] styles = go.GetComponentsInChildren<MeshOutlineStyle>(true);
                for (int s = 0; s < styles.Length; s++)
                {
                    if (styles[s] == null)
                        continue;
                    if (styles[s].transform.Find("OutlineShell") == null)
                        MeshOutlinePlayBuilder.Enqueue(styles[s]);
                }
            }
        }

        TryLoadSceneOnZoomClose();
    }

    private void TryLoadSceneOnZoomClose()
    {
        if (string.IsNullOrWhiteSpace(loadSceneOnZoomClose))
            return;

        string sceneName = loadSceneOnZoomClose.Trim();
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"InspectableHotspot '{hotspotId}': scene '{sceneName}' is not in Build Settings.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void Awake()
    {
        ResolveCollider();
        SetInspectMode(false);
    }

    public void SetInspectMode(bool active)
    {
        ResolveCollider();
        if (hotspotCollider != null) hotspotCollider.enabled = active;
        if (magnifierIcon != null) magnifierIcon.SetActive(false);
    }

    public bool OwnsCollider(Collider candidate)
    {
        return candidate != null && hotspotCollider != null &&
               (candidate == hotspotCollider || candidate.transform.IsChildOf(transform));
    }

    private void ResolveCollider()
    {
        if (hotspotCollider == null) hotspotCollider = GetComponent<Collider>();
    }

    private void Reset()
    {
        hotspotCollider = GetComponent<Collider>();
        if (hotspotCollider == null) hotspotCollider = gameObject.AddComponent<BoxCollider>();
        hotspotCollider.isTrigger = true;
    }
}
