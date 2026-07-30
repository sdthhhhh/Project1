using UnityEngine;

/// <summary>
/// World diary puzzle piece: E collects into DiaryManager; no inspect UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class DiaryPuzzlePieceWorld : MonoBehaviour, IInteractable
{
    [SerializeField, Tooltip("Fragment id 1–4 matching the cover puzzle tray slot.")]
    private int fragmentId = 1;
    [SerializeField] private string interactText = "Press E to collect";

    private bool collected;

    public int FragmentId => fragmentId;

    public void Configure(int id) => fragmentId = id;

    public string GetInteractText()
    {
        return collected ? string.Empty : interactText;
    }

    public void Interact()
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
        InteractionUI.Instance?.HideInteract();
        gameObject.SetActive(false);
    }
}
