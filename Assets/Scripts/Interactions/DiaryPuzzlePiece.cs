using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DiaryPuzzlePiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int FragmentId { get; private set; }
    private RectTransform rect; private Canvas canvas; private Transform startParent; private Vector2 startPosition; private CanvasGroup group; private bool locked;
    public void Configure(int id, Canvas owner) { FragmentId = id; canvas = owner; rect = GetComponent<RectTransform>(); group = gameObject.AddComponent<CanvasGroup>(); }
    public void OnBeginDrag(PointerEventData e) { if (locked) return; startParent = transform.parent; startPosition = rect.anchoredPosition; group.blocksRaycasts = false; transform.SetAsLastSibling(); }
    public void OnDrag(PointerEventData e) { if (!locked) rect.anchoredPosition += e.delta / canvas.scaleFactor; }
    public void OnEndDrag(PointerEventData e) { if (!locked) { group.blocksRaycasts = true; ReturnToStart(); } }
    public void ReturnToStart() { if (locked || startParent == null) return; transform.SetParent(startParent, false); rect.anchoredPosition = startPosition; group.blocksRaycasts = true; }
    public void LockTo(Transform slot) { locked = true; group.blocksRaycasts = true; transform.SetParent(slot, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
}
