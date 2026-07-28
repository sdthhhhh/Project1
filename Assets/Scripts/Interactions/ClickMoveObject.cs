using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class ClickMoveObject : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Tooltip("Object to move. Leave empty to move this object.")]
    private Transform target;
    [SerializeField, Tooltip("Movement relative to the initial local position.")]
    private Vector3 moveOffset = new Vector3(.5f, 0f, 0f);
    [SerializeField, Min(.01f)]
    private float moveDuration = .8f;

    [Header("Interaction")]
    [SerializeField, Tooltip("Collider used by the shared Crosshair raycast.")]
    private Collider interactionCollider;
    [SerializeField, Tooltip("Disable the collider after moving so objects underneath can be targeted.")]
    private bool disableColliderAfterMove = true;
    [SerializeField, Tooltip("Allow this object to move only once.")]
    private bool oneShot = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onMoveFinished;

    private Vector3 openPosition;
    private bool isMoving;
    private bool hasMoved;

    public bool CanInteract => isActiveAndEnabled && !isMoving && (!oneShot || !hasMoved);

    private void Awake()
    {
        ResolveReferences();
        openPosition = target.localPosition + moveOffset;
    }

    private void Reset()
    {
        target = transform;
        interactionCollider = GetComponent<Collider>();
    }

    public void Move()
    {
        if (!CanInteract || target == null) return;
        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        isMoving = true;
        Vector3 start = target.localPosition;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveDuration));
            target.localPosition = Vector3.LerpUnclamped(start, openPosition, t);
            yield return null;
        }

        target.localPosition = openPosition;
        isMoving = false;
        hasMoved = true;
        if (disableColliderAfterMove && interactionCollider != null)
            interactionCollider.enabled = false;
        onMoveFinished?.Invoke();
    }

    private void ResolveReferences()
    {
        if (target == null) target = transform;
        if (interactionCollider == null) interactionCollider = GetComponent<Collider>();
    }
}
