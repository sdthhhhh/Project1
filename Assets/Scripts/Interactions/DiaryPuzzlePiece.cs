using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DiaryPuzzlePiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Permanent Puzzle Piece")]
    [SerializeField, Tooltip("Unique fragment ID saved in the scene.")] private int fragmentId;
    [SerializeField, Tooltip("Canvas used to calculate drag distance.")] private Canvas canvas;
    public int FragmentId => fragmentId;
    private RectTransform rect; private Transform startParent; private Vector2 startPosition; private CanvasGroup group; private bool locked;
    private void Awake() { rect = GetComponent<RectTransform>(); group = GetComponent<CanvasGroup>(); if (group == null) group = gameObject.AddComponent<CanvasGroup>(); if (canvas == null) canvas = GetComponentInParent<Canvas>(); }
    public void Configure(int id, Canvas owner) { fragmentId = id; canvas = owner; rect = GetComponent<RectTransform>(); group = GetComponent<CanvasGroup>(); if (group == null) group = gameObject.AddComponent<CanvasGroup>(); }
    public void OnBeginDrag(PointerEventData e) { if (locked) return; startParent = transform.parent; startPosition = rect.anchoredPosition; group.blocksRaycasts = false; transform.SetAsLastSibling(); }
    public void OnDrag(PointerEventData e) { if (!locked) rect.anchoredPosition += e.delta / canvas.scaleFactor; }
    public void OnEndDrag(PointerEventData e) { if (!locked) { group.blocksRaycasts = true; ReturnToStart(); } }
    public void ReturnToStart() { if (locked || startParent == null) return; transform.SetParent(startParent, false); rect.anchoredPosition = startPosition; group.blocksRaycasts = true; }
    public void LockTo(Transform slot) { locked = true; group.blocksRaycasts = true; transform.SetParent(slot, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
}
