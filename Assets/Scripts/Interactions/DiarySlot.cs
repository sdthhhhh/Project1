using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DiarySlot : MonoBehaviour, IDropHandler
{
    [Header("Slot Identity")]
    [SerializeField, Tooltip("Only this fragment ID is accepted.")] private int correctFragmentId;
    [SerializeField, Tooltip("Manager notified when this slot is filled.")] private DiaryPuzzleManager puzzleManager;
    public bool IsFilled { get; private set; }
    public void Configure(int id, DiaryPuzzleManager manager) { correctFragmentId = id; puzzleManager = manager; }
    public void OnDrop(PointerEventData eventData)
    {
        DiaryPuzzlePiece piece = eventData.pointerDrag ? eventData.pointerDrag.GetComponent<DiaryPuzzlePiece>() : null;
        if (piece == null) return;
        if (IsFilled || piece.FragmentId != correctFragmentId) { piece.ReturnToStart(); return; }
        IsFilled = true; piece.LockTo(transform);
        if (puzzleManager != null) puzzleManager.CheckCompletion();
        else Debug.LogError("DiarySlot: DiaryPuzzleManager reference is missing.");
    }
}
