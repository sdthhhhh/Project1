using UnityEngine;
using UnityEngine.Events;
using System;

[DisallowMultipleComponent]
public sealed class InspectableObject : MonoBehaviour
{
    [Header("Inspection Content")]
    [SerializeField, Tooltip("Optional clean model/prefab used by the 3D inspection preview. If empty, this object is cloned.")] private GameObject previewModel;
    [SerializeField, Tooltip("Initial rotation used when the model first appears in the preview.")] private Vector3 previewRotation;
    [Header("Optional Preview Photo")]
    [SerializeField,Tooltip("Photo texture placed on the front of this model only in the inspection UI.")]private Texture2D previewPhoto;
    [SerializeField,Tooltip("Optional Renderer name receiving the photo texture. Leave empty to use the largest Renderer.")]private string photoRendererName="";
    [SerializeField,Min(0),Tooltip("Material slot on the selected Renderer receiving the photo texture.")]private int photoMaterialIndex=0;
    [SerializeField, TextArea(2, 5), Tooltip("One-sentence description shown to the right of the image.")] private string description = "A detail that may be worth remembering.";
    [SerializeField, Tooltip("Whether raycast inspection is currently available for this object.")] private bool canInspect = true;
    [Header("Optional Inspect Completion")]
    [SerializeField, Tooltip("Invoked whenever this item's Inspect UI is fully closed. Local zoom close does not invoke it.")] private UnityEvent onInspectFinished;

    public GameObject PreviewModel => previewModel != null ? previewModel : gameObject;
    public Vector3 PreviewRotation => previewRotation;
    public Texture2D PreviewPhoto=>previewPhoto;public string PhotoRendererName=>photoRendererName;public int PhotoMaterialIndex=>photoMaterialIndex;
    public string Description => description;
    public bool CanInspect => canInspect;
    public event Action InspectFinished;

    public void ConfigurePreview(GameObject model,string text,Vector3 rotation)
    {previewModel=model;description=text;previewRotation=rotation;}
    public void SetPreviewPhoto(Texture2D photo){previewPhoto=photo;}
    public void SetCanInspect(bool value){canInspect=value;}
    public void NotifyInspectFinished(){InspectFinished?.Invoke();onInspectFinished?.Invoke();}

    private void Reset()
    {
        if (GetComponentInChildren<Collider>(true) == null) gameObject.AddComponent<BoxCollider>();
    }
}
