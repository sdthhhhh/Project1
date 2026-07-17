using UnityEngine;

public class Drawer : MonoBehaviour, IInteractable
{
    [SerializeField] private Vector3 openDirection = Vector3.forward;
    [SerializeField] private float openDistance = 0.35f;
    [SerializeField] private float openSpeed = 3f;
    [SerializeField] private bool isLocked = false;

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private bool isOpen = false;
    private bool isMoving = false;

    private void Start()
    {
        closedPosition = transform.localPosition;
        openPosition = closedPosition + openDirection.normalized * openDistance;
    }

    private void Update()
    {
        if (!isMoving) return;

        Vector3 target = isOpen ? openPosition : closedPosition;

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            target,
            openSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.localPosition, target) < 0.001f)
        {
            transform.localPosition = target;
            isMoving = false;
        }
    }

    public void Interact()
    {
        if (isLocked)
        {
            InteractionUI.Instance.ShowStatus("Drawer is locked");
            Debug.Log("Drawer is locked");
            return;
        }

        isOpen = !isOpen;
        isMoving = true;
    }

    public void UnlockAndOpen()
    {
        isLocked = false;
        isOpen = true;
        isMoving = true;

        InteractionUI.Instance.ShowStatus("Drawer is open");
        Debug.Log("Drawer is open");
    }
    public string GetInteractText()
    {
        return "Press E to interact";
    }
}