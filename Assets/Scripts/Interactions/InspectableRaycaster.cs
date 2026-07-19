using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InspectableRaycaster : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField, Min(.5f), Tooltip("Maximum inspection distance.")] private float inspectDistance = 5f;
    [SerializeField, Tooltip("Layers containing inspectable objects.")] private LayerMask inspectableLayers = ~0;
    [Header("Crosshair")]
    [SerializeField, Tooltip("Existing scene Crosshair Image.")] private Image crosshairImage;
    [SerializeField, Tooltip("Magnifying-glass sprite shown over inspectable objects.")] private Sprite magnifierSprite;
    [SerializeField, Tooltip("Magnifier tint while hovering.")] private Color magnifierColor = Color.yellow;
    [SerializeField, Tooltip("Independent width and height used only while the magnifier is visible.")] private Vector2 magnifierSize = new Vector2(48f,48f);
    [Header("UI")]
    [SerializeField, Tooltip("Permanent InspectCanvas controller.")] private InspectableUIController inspectUI;

    private Sprite normalSprite; private Color normalColor; private Vector2 normalSize; private InspectableObject hovered;
    private FirstPersonMovement movement; private FirstPersonLook look; private PlayerInteraction playerInteraction;
    private Transform bodyTransform, lookTransform; private Quaternion lockedBodyRotation, lockedLookRotation; private bool controlsLocked;

    public void Configure(Image crosshair, Sprite magnifier, InspectableUIController ui)
    { crosshairImage=crosshair;magnifierSprite=magnifier;inspectUI=ui;CacheReferences();CacheNormalCrosshair(); }

    private void Awake() { CacheReferences(); CacheNormalCrosshair(); }
    private void CacheReferences()
    {
        movement=GetComponentInParent<FirstPersonMovement>(); look=GetComponent<FirstPersonLook>();
        if (look==null) look=GetComponentInChildren<FirstPersonLook>(true);
        playerInteraction=GetComponent<PlayerInteraction>(); if (playerInteraction==null) playerInteraction=GetComponentInChildren<PlayerInteraction>(true);
    }
    private void CacheNormalCrosshair() { if (crosshairImage!=null) { normalSprite=crosshairImage.sprite;normalColor=crosshairImage.color;normalSize=crosshairImage.rectTransform.sizeDelta; } }

    private void Update()
    {
        if (inspectUI != null && inspectUI.IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Q)){inspectUI.TryCollectCurrent();CloseInspection();}
            return;
        }
        hovered=null;
        if (Physics.Raycast(transform.position,transform.forward,out RaycastHit hit,inspectDistance,inspectableLayers,QueryTriggerInteraction.Ignore))
            hovered=hit.collider.GetComponentInParent<InspectableObject>();
        SetMagnifier(hovered!=null);
        if (hovered!=null && Input.GetMouseButtonDown(0)) OpenInspection(hovered);
    }

    private void OpenInspection(InspectableObject target)
    {
        if (inspectUI==null) { Debug.LogError("InspectableRaycaster: InspectCanvas controller is missing."); return; }
        InteractionUI.Instance?.HideInteract(); inspectUI.Show(target); LockControls(); SetMagnifier(false);
    }
    private void CloseInspection() { inspectUI.Hide(); UnlockControls(); }
    private void SetMagnifier(bool active)
    {
        if (crosshairImage==null) return;
        crosshairImage.sprite=active&&magnifierSprite!=null?magnifierSprite:normalSprite;
        crosshairImage.color=active?magnifierColor:normalColor;
        crosshairImage.rectTransform.sizeDelta=active?magnifierSize:normalSize;
        crosshairImage.preserveAspect=true;
    }

    private void OnDisable(){SetMagnifier(false);}
    private void LockControls()
    {
        controlsLocked=true; bodyTransform=movement!=null?movement.transform:null;lookTransform=look!=null?look.transform:null;
        if(bodyTransform!=null)lockedBodyRotation=bodyTransform.rotation;if(lookTransform!=null)lockedLookRotation=lookTransform.localRotation;
        if(movement!=null){movement.enabled=false;if(movement.TryGetComponent(out Rigidbody rb)){rb.velocity=Vector3.zero;rb.angularVelocity=Vector3.zero;}}
        if(look!=null)look.enabled=false;if(playerInteraction!=null)playerInteraction.enabled=false;
        Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
    }
    private void UnlockControls()
    {
        controlsLocked=false;if(movement!=null)movement.enabled=true;if(look!=null)look.enabled=true;if(playerInteraction!=null)playerInteraction.enabled=true;
        Cursor.visible=false;Cursor.lockState=CursorLockMode.Locked;
    }
    private void LateUpdate(){if(!controlsLocked)return;if(bodyTransform!=null)bodyTransform.rotation=lockedBodyRotation;if(lookTransform!=null)lookTransform.localRotation=lockedLookRotation;}
}
