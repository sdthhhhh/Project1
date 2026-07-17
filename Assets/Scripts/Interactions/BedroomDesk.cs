using UnityEngine;

public sealed class BedroomDesk : MonoBehaviour, IInteractable
{
    [Header("Diary Puzzle")]
    [SerializeField, Tooltip("Manager that owns the reconstruction UI.")] private DiaryPuzzleManager puzzleManager;
    [SerializeField, Tooltip("Hide this whitebox entity until all six fragments are collected.")] private bool revealAfterCollection;
    private Renderer[] entityRenderers;
    private Collider[] entityColliders;
    private bool revealed;

    public void Configure(DiaryPuzzleManager manager, bool hideUntilCollected)
    {
        puzzleManager = manager;
        revealAfterCollection = hideUntilCollected;
        entityRenderers = GetComponentsInChildren<Renderer>(true);
        entityColliders = GetComponentsInChildren<Collider>(true);
        if (revealAfterCollection) SetEntityVisible(false);
    }

    private void Update()
    {
        if (!revealAfterCollection || revealed || DiaryManager.Instance == null || !DiaryManager.Instance.HasCollectedAllFragments) return;
        revealed = true;
        SetEntityVisible(true);
        InteractionUI.Instance?.ShowStatus("The diary fragments can now be reconstructed at the bedroom desk.");
    }

    private void SetEntityVisible(bool visible)
    {
        if (entityRenderers == null) entityRenderers = GetComponentsInChildren<Renderer>(true);
        if (entityColliders == null) entityColliders = GetComponentsInChildren<Collider>(true);
        foreach (Renderer r in entityRenderers) r.enabled = visible;
        foreach (Collider c in entityColliders) c.enabled = visible;
    }
    public string GetInteractText() => "Press E to reconstruct diary";
    public void Interact()
    {
        if (DiaryManager.Instance == null) { Debug.LogError("BedroomDesk: DiaryManager is missing."); return; }
        if (!DiaryManager.Instance.HasCollectedAllFragments) { InteractionUI.Instance?.ShowStatus("You are still missing diary fragments."); return; }
        if (DiaryManager.Instance.PuzzleCompleted) { InteractionUI.Instance?.ShowStatus("The diary is already reconstructed."); return; }
        if (puzzleManager == null) puzzleManager = FindObjectOfType<DiaryPuzzleManager>();
        if (puzzleManager == null) { Debug.LogError("BedroomDesk: DiaryPuzzleManager is missing."); return; }
        puzzleManager.OpenPuzzle();
    }
}
