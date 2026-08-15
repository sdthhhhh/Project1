using UnityEngine;

/// <summary>
/// World diary puzzle piece: LMB opens the shared inspection UI; Q collects it.
/// </summary>
[DisallowMultipleComponent]
public sealed class DiaryPuzzlePieceWorld : MonoBehaviour, IInspectableCollectible
{
    [SerializeField, Tooltip("Fragment id 1–4 matching the cover puzzle tray slot.")]
    private int fragmentId = 1;
    private bool collected;

    public int FragmentId => fragmentId;

    public void Configure(int id) => fragmentId = id;

    private void Awake()
    {
        InspectableObject inspectable = GetComponent<InspectableObject>();
        if (inspectable == null)
            inspectable = gameObject.AddComponent<InspectableObject>();

        inspectable.ConfigurePreview(
            gameObject,
            $"Diary puzzle fragment {fragmentId}.",
            Vector3.zero);
        inspectable.SetContentKind(InspectContentKind.Model3D);
        inspectable.SetCanInspect(true);

        if (GetComponentInChildren<Collider>(true) == null)
            gameObject.AddComponent<BoxCollider>();
    }

    public void CollectFromInspection()
    {
        if (collected)
            return;
        if (DiaryManager.Instance == null)
        {
            Debug.LogError("DiaryPuzzlePieceWorld: DiaryManager is missing.", this);
            return;
        }

        collected = true;
        DiaryManager.Instance.CollectFragment(fragmentId);
        gameObject.SetActive(false);
    }
}
