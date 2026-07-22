using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class DoorPasswordLock : MonoBehaviour
{
    [Header("Password")]
    [SerializeField, Tooltip("Correct password for this door. Each door can use a different value.")] private string correctPassword="1234";
    [SerializeField, Tooltip("Title shown on the password UI.")] private string displayName="Locked Door";
    [Header("Door Opening")]
    [SerializeField, Tooltip("Transform rotated after a correct password. Leave empty to use this transform.")] private Transform doorToOpen;
    [SerializeField] private Vector3 openEulerOffset=new Vector3(0f,90f,0f);
    [SerializeField,Min(.01f)] private float openDuration=.8f;
    [SerializeField] private UnityEvent onUnlocked;
    [SerializeField] private UnityEvent onOpened;

    public string DisplayName=>string.IsNullOrWhiteSpace(displayName)?"Locked Door":displayName;
    public bool IsUnlocked{get;private set;}
    public bool IsOpen{get;private set;}
    private bool isOpening;

    public void ConfigureDoor(Transform target)
    {
        if(doorToOpen==null)doorToOpen=target;
    }

    public bool TryUnlock(string enteredPassword)
    {
        if(IsUnlocked)return true;
        if(!string.Equals(enteredPassword??string.Empty,correctPassword??string.Empty,System.StringComparison.Ordinal))return false;
        IsUnlocked=true;onUnlocked?.Invoke();
        return true;
    }

    public void OpenDoor()
    {
        if(!IsUnlocked||IsOpen||isOpening)return;
        Transform target=doorToOpen!=null?doorToOpen:transform;
        if(target!=null)StartCoroutine(OpenDoorRoutine(target));
    }

    private IEnumerator OpenDoorRoutine(Transform target)
    {
        isOpening=true;
        Quaternion start=target.localRotation;Quaternion end=start*Quaternion.Euler(openEulerOffset);float elapsed=0f;
        while(elapsed<openDuration){elapsed+=Time.deltaTime;target.localRotation=Quaternion.Slerp(start,end,Mathf.Clamp01(elapsed/openDuration));yield return null;}
        target.localRotation=end;isOpening=false;IsOpen=true;onOpened?.Invoke();
        // Once the door is open the lock must stop participating in the shared
        // Crosshair raycast, otherwise the hand icon remains over an opened door.
        foreach(Collider item in GetComponentsInChildren<Collider>(true))item.enabled=false;
    }
}
