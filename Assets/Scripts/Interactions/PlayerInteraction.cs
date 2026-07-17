using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactDistance = 5f;

    private void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                InteractionUI.Instance.ShowInteract(interactable.GetInteractText());

                if (Input.GetKeyDown(KeyCode.E))
                {
                    InteractionUI.Instance.HideInteract();
                    interactable.Interact();
                }

                return;
            }
        }

        InteractionUI.Instance.HideInteract();
    }
}