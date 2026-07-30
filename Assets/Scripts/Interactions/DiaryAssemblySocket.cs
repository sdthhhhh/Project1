using UnityEngine;

/// <summary>
/// User-placed success region on the diary. Resize/move the BoxCollider in the Scene view.
/// When the matching collected fragment is clicked onto this socket, the piece FBX snaps here.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class DiaryAssemblySocket : MonoBehaviour
{
    [SerializeField, Tooltip("Fragment id this groove accepts (1–4).")]
    private int correctFragmentId = 1;

    [SerializeField, Tooltip("Optional: prefab/model to show when filled. If empty, loads Assets/3D/Diary/pieceN.fbx at runtime.")]
    private GameObject placedVisualPrefab;

    public int CorrectFragmentId => correctFragmentId;
    public bool IsFilled { get; private set; }

    private GameObject placedInstance;
    private BoxCollider box;

    public void Configure(int fragmentId, GameObject visualPrefab = null)
    {
        correctFragmentId = fragmentId;
        placedVisualPrefab = visualPrefab;
    }

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        if (box != null)
            box.isTrigger = true;
    }

    public bool TryPlace(int fragmentId)
    {
        if (IsFilled || fragmentId != correctFragmentId)
            return false;

        IsFilled = true;
        SpawnVisual();
        return true;
    }

    public void ClearPlacement()
    {
        IsFilled = false;
        if (placedInstance != null)
        {
            Destroy(placedInstance);
            placedInstance = null;
        }
    }

    private void SpawnVisual()
    {
        if (placedInstance != null)
            return;

        GameObject source = placedVisualPrefab;
#if UNITY_EDITOR
        if (source == null && !Application.isPlaying)
        {
            string path = "Assets/3D/Diary/piece" + correctFragmentId + ".fbx";
            source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
#endif
        if (source == null)
        {
            // Runtime Resources fallback — pieces are addressable via Resources if moved; else create empty marker.
            source = Resources.Load<GameObject>("Diary/piece" + correctFragmentId);
        }

        if (source != null)
        {
            placedInstance = Instantiate(source, transform);
            placedInstance.name = "PlacedPiece0" + correctFragmentId;
            placedInstance.transform.localPosition = Vector3.zero;
            placedInstance.transform.localRotation = Quaternion.identity;
            placedInstance.transform.localScale = Vector3.one;
            foreach (var col in placedInstance.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
            foreach (var frag in placedInstance.GetComponentsInChildren<DiaryFragment>(true))
                Destroy(frag);
        }
        else
        {
            // Still mark filled; visual can be assigned in Inspector later.
            Debug.LogWarning("DiaryAssemblySocket: no visual for piece " + correctFragmentId + ". Assign Placed Visual Prefab.", this);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (box == null)
            box = GetComponent<BoxCollider>();
        if (box == null)
            return;

        Gizmos.color = IsFilled ? new Color(0.3f, 0.9f, 0.4f, 0.35f) : new Color(0.95f, 0.75f, 0.2f, 0.35f);
        Matrix4x4 m = transform.localToWorldMatrix * Matrix4x4.TRS(box.center, Quaternion.identity, box.size);
        Gizmos.matrix = m;
        Gizmos.DrawCube(Vector3.zero, Vector3.one);
        Gizmos.color = IsFilled ? new Color(0.2f, 0.8f, 0.35f, 0.9f) : new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
#endif
}
