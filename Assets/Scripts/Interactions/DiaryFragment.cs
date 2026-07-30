using UnityEngine;

/// <summary>
/// World collectible diary fragment. Place piece models in the scene and set Fragment Id (1–4).
/// </summary>
[DisallowMultipleComponent]
public sealed class DiaryFragment : MonoBehaviour, IInteractable
{
    [Header("Fragment Identity")]
    [SerializeField, Tooltip("Unique ID matching a DiarySlot / PuzzleFragment (1–4).")]
    private int fragmentId = 1;

    private bool collected;

    public int FragmentId => fragmentId;

    public void Configure(int id) => fragmentId = id;

    public string GetInteractText() => "Press E to collect diary fragment";

    public void Interact()
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
