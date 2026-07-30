using UnityEngine;

/// <summary>
/// World diary fragment: LMB inspect shows a text page; Q collects into DiaryManager.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InspectableObject))]
public sealed class DiaryFragment : MonoBehaviour, IInspectableCollectible
{
    [Header("Fragment Identity")]
    [SerializeField, Tooltip("Unique ID matching a DiarySlot / PuzzleFragment (1–4).")]
    private int fragmentId = 1;

    [Header("Diary Page")]
    [SerializeField, TextArea(12, 40), Tooltip("Full diary entry shown in the inspect text area.")]
    private string diaryText =
        "(Diary entry text — replace with the final writing.)";

    private bool collected;
    private InspectableObject inspectable;

    public int FragmentId => fragmentId;
    public string DiaryText => diaryText;
    public bool IsCollected => collected;

    public void Configure(int id) => fragmentId = id;

    public void SetDiaryText(string text) => diaryText = text ?? string.Empty;

    private void Awake()
    {
        inspectable = GetComponent<InspectableObject>();
        if (inspectable != null)
        {
            inspectable.SetCanInspect(true);
            if (string.IsNullOrWhiteSpace(inspectable.Description) ||
                inspectable.Description == "A detail that may be worth remembering.")
            {
                inspectable.ConfigurePreview(
                    gameObject,
                    "Diary Fragment " + fragmentId,
                    Vector3.zero);
            }
        }
    }

    public void CollectFromInspection()
    {
        if (collected)
            return;
        if (DiaryManager.Instance == null)
        {
            Debug.LogError("DiaryFragment: DiaryManager is missing.", this);
            return;
        }

        collected = true;
        DiaryManager.Instance.CollectFragment(fragmentId);
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (fragmentId < 1)
            fragmentId = 1;
    }

    private void Reset()
    {
        EnsureInteractCollider();
        if (GetComponent<InspectableObject>() == null)
            gameObject.AddComponent<InspectableObject>();
    }

    [ContextMenu("Ensure Interact Collider")]
    private void EnsureInteractCollider()
    {
        if (GetComponentInChildren<Collider>() != null)
            return;

        var box = gameObject.AddComponent<BoxCollider>();
        var filter = GetComponentInChildren<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
        {
            Bounds b = filter.sharedMesh.bounds;
            box.center = b.center;
            box.size = Vector3.Max(b.size, new Vector3(0.05f, 0.02f, 0.05f));
        }
    }
#endif
}
