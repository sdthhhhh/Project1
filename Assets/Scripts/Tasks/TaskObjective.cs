using UnityEngine;

/// <summary>
/// Hang on an interactable object. PlayerInteraction notifies this after Interact().
/// </summary>
[DisallowMultipleComponent]
public sealed class TaskObjective : MonoBehaviour
{
    [SerializeField] private string subTaskId;

    public string SubTaskId => subTaskId;

    public void NotifyInteracted()
    {
        if (string.IsNullOrWhiteSpace(subTaskId)) return;
        TaskProgressManager.Instance?.CompleteSubTask(subTaskId);
    }
}
