using UnityEngine;

public sealed class DiaryFragment : MonoBehaviour, IInteractable
{
    [Header("Fragment Identity")]
    [SerializeField, Tooltip("Unique ID from 1 to 6.")] private int fragmentId;
    private bool collected;
    public void Configure(int id) => fragmentId = id;
    public string GetInteractText() => "Press E to collect diary fragment";
    public void Interact()
    {
        if (collected) return;
        if (DiaryManager.Instance == null) { Debug.LogError("DiaryFragment: DiaryManager is missing."); return; }
        collected = true; DiaryManager.Instance.CollectFragment(fragmentId); gameObject.SetActive(false);
    }
}
