using UnityEngine;
using UnityEngine.Events;
using System;

/// <summary>
/// Add this beside InspectableObject to make any scene item collectible from the
/// shared inspection UI. Q invokes the event and optionally hides the scene item.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InspectableObject))]
public sealed class InspectableCollectible : MonoBehaviour, IInspectableCollectible
{
    [SerializeField, Tooltip("Hide this object after Q Collect. Disable this when another system owns its visibility.")]
    private bool hideAfterCollect = true;
    [SerializeField, Tooltip("Invoked once when the player collects this item.")]
    private UnityEvent onCollected;

    public bool IsCollected { get; private set; }
    public event Action Collected;

    public void Configure(bool hideSceneObjectAfterCollect)
    {
        hideAfterCollect = hideSceneObjectAfterCollect;
    }

    public void CollectFromInspection()
    {
        if (IsCollected) return;
        IsCollected = true;
        onCollected?.Invoke();
        Collected?.Invoke();
        if (hideAfterCollect) gameObject.SetActive(false);
    }
}
