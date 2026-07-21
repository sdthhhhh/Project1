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
    [SerializeField, Tooltip("Disable this lock collider after successful entry.")] private bool disableLockCollider=true;
    [SerializeField] private UnityEvent onUnlocked;

    public string DisplayName=>string.IsNullOrWhiteSpace(displayName)?"Locked Door":displayName;
    public bool IsUnlocked{get;private set;}

    public bool TryUnlock(string enteredPassword)
    {
        if(IsUnlocked)return true;
        if(!string.Equals(enteredPassword??string.Empty,correctPassword??string.Empty,System.StringComparison.Ordinal))return false;
        IsUnlocked=true;onUnlocked?.Invoke();
        Transform target=doorToOpen!=null?doorToOpen:transform;
        if(target!=null)StartCoroutine(OpenDoor(target));
        if(disableLockCollider)foreach(Collider item in GetComponentsInChildren<Collider>(true))item.enabled=false;
        return true;
    }

    private IEnumerator OpenDoor(Transform target)
    {
        Quaternion start=target.localRotation;Quaternion end=start*Quaternion.Euler(openEulerOffset);float elapsed=0f;
        while(elapsed<openDuration){elapsed+=Time.deltaTime;target.localRotation=Quaternion.Slerp(start,end,Mathf.Clamp01(elapsed/openDuration));yield return null;}
        target.localRotation=end;
    }
}
