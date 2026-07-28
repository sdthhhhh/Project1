using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(InspectableObject))]
public sealed class SuYuDiaryFragment : MonoBehaviour
{
    [SerializeField, Range(1, 4), Tooltip("Unique fragment number: 1, 2, 3 or 4.")]
    private int fragmentId = 1;

    private InspectableObject inspectable;
    private bool collected;

    private void Awake() => inspectable = GetComponent<InspectableObject>();

    private void OnEnable()
    {
        if (inspectable == null) inspectable = GetComponent<InspectableObject>();
        if (inspectable != null) inspectable.InspectFinished += CollectAfterInspection;
    }

    private void OnDisable()
    {
        if (inspectable != null) inspectable.InspectFinished -= CollectAfterInspection;
    }

    private void CollectAfterInspection()
    {
        if (collected) return;
        SuYuDiaryTaskManager manager = SuYuDiaryTaskManager.Instance;
        if (manager == null)
        {
            manager = FindObjectOfType<SuYuDiaryTaskManager>();
        }

        if (manager == null)
        {
            Debug.LogError("SuYuDiaryFragment: no DiaryTaskUI exists in this scene.", this);
            return;
        }

        collected = true;
        manager.CollectFragment(fragmentId);
    }
}
