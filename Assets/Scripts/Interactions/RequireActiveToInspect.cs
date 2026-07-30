using UnityEngine;

/// <summary>
/// Keeps InspectableObject.CanInspect in sync with another object's active state
/// (e.g. only after mud footprints are revealed).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InspectableObject))]
public sealed class RequireActiveToInspect : MonoBehaviour
{
    [SerializeField, Tooltip("World object that must be active (e.g. footprint trail) before this can be inspected.")]
    private GameObject requiredActive;
    [SerializeField, Tooltip("Optional colliders gated with CanInspect. Empty = colliders on this object only.")]
    private Collider[] gatedColliders;

    private InspectableObject inspectable;
    private bool lastOk = true;

    public void Configure(GameObject required)
    {
        requiredActive = required;
        Apply();
    }

    private void Awake()
    {
        inspectable = GetComponent<InspectableObject>();
        if (gatedColliders == null || gatedColliders.Length == 0)
            gatedColliders = GetComponents<Collider>();
        Apply();
    }

    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        if (inspectable == null)
            inspectable = GetComponent<InspectableObject>();
        if (inspectable == null)
            return;

        bool ok = requiredActive != null && requiredActive.activeInHierarchy;
        if (ok == lastOk && Application.isPlaying)
        {
            // Still refresh CanInspect in case something else changed it.
        }
        lastOk = ok;
        inspectable.SetCanInspect(ok);

        if (gatedColliders == null)
            return;
        for (int i = 0; i < gatedColliders.Length; i++)
        {
            if (gatedColliders[i] != null)
                gatedColliders[i].enabled = ok;
        }
    }
}
