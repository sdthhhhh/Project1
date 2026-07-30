using UnityEngine;

/// <summary>
/// E-interactable that hides itself (SetActive false). Can stay locked until another system unlocks it.
/// </summary>
[DisallowMultipleComponent]
public sealed class DeactivateOnInteract : MonoBehaviour, IInteractable
{
    [SerializeField, Tooltip("When off, E does nothing and this object is ignored by interaction.")]
    private bool interactionEnabled;
    [SerializeField] private string interactText = "Press E";
    [SerializeField, Tooltip("Optional colliders gated with interaction. Empty = all colliders on this object.")]
    private Collider[] gatedColliders;

    private void Awake()
    {
        if (gatedColliders == null || gatedColliders.Length == 0)
            gatedColliders = GetComponentsInChildren<Collider>(true);
        ApplyColliderGate();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        ApplyColliderGate();
    }

    public string GetInteractText()
    {
        return interactionEnabled ? interactText : string.Empty;
    }

    public void Interact()
    {
        if (!interactionEnabled)
            return;
        InteractionUI.Instance?.HideInteract();
        gameObject.SetActive(false);
    }

    private void ApplyColliderGate()
    {
        if (gatedColliders == null)
            return;
        for (int i = 0; i < gatedColliders.Length; i++)
        {
            if (gatedColliders[i] != null)
                gatedColliders[i].enabled = interactionEnabled;
        }
    }
}
