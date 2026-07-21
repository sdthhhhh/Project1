using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InspectableRaycaster : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField, Min(.5f), Tooltip("Maximum inspection distance.")] private float inspectDistance = 5f;
    [SerializeField, Tooltip("Layers containing inspectable objects.")] private LayerMask inspectableLayers = ~0;
    [Header("Crosshair")]
    [SerializeField, Tooltip("Existing scene Crosshair Image.")] private Image crosshairImage;
    [FormerlySerializedAs("handSprite")]
    [SerializeField, Tooltip("Magnifying-glass sprite for ordinary inspectable objects.")] private Sprite magnifierSprite;
    [SerializeField, Tooltip("Small hand sprite for collectible inspectable objects.")] private Sprite handSprite;
    [FormerlySerializedAs("handColor")]
    [SerializeField] private Color magnifierColor = Color.yellow;
    [SerializeField] private Color handColor = Color.white;
    [FormerlySerializedAs("handSize")]
    [SerializeField] private Vector2 magnifierSize = new Vector2(48f,48f);
    [SerializeField] private Vector2 handSize = new Vector2(48f,48f);
    [SerializeField, Tooltip("Door-lock sprite shown only over DoorPasswordLock colliders.")] private Sprite doorLockSprite;
    [SerializeField] private Color doorLockColor = Color.white;
    [SerializeField] private Vector2 doorLockSize = new Vector2(48f,48f);
    [Header("UI")]
    [SerializeField, Tooltip("Original magnifier inspection Canvas.")] private InspectableUIController inspectUI;
    [SerializeField, Tooltip("Separate hand/collect inspection Canvas.")] private InspectableUIController collectibleInspectUI;
    [SerializeField, Tooltip("Separate password entry Canvas for doors.")] private DoorPasswordUIController doorPasswordUI;

    private Sprite normalSprite; private Color normalColor; private Vector2 normalSize; private InspectableObject hovered; private BeerRestoreController hoveredRestoreTarget;private DoorPasswordLock hoveredDoorLock;
    private FirstPersonMovement movement; private FirstPersonLook look; private PlayerInteraction playerInteraction;
    private Transform bodyTransform, lookTransform; private Quaternion lockedBodyRotation, lockedLookRotation; private bool controlsLocked;

    public void Configure(Image crosshair, Sprite magnifier, Sprite hand, InspectableUIController ui, InspectableUIController collectibleUI)
    { crosshairImage=crosshair;magnifierSprite=magnifier;handSprite=hand;inspectUI=ui;collectibleInspectUI=collectibleUI;CacheReferences();CacheNormalCrosshair(); }
    public void ConfigureDoorPassword(Sprite icon,DoorPasswordUIController ui){doorLockSprite=icon;doorPasswordUI=ui;}

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
        if(doorPasswordUI!=null&&doorPasswordUI.IsOpen)
        {
            if(Input.GetKeyDown(KeyCode.Escape))doorPasswordUI.Hide();
            return;
        }
        InspectableUIController openUI=collectibleInspectUI!=null&&collectibleInspectUI.IsOpen?collectibleInspectUI:inspectUI!=null&&inspectUI.IsOpen?inspectUI:null;
        if (openUI != null)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!openUI.TryCloseZoom()) CloseInspection(openUI);
            }
            else if (Input.GetKeyDown(KeyCode.Q))
            {
                if (openUI.TryCloseZoom()) return;
                openUI.TryCollectCurrent();
                CloseInspection(openUI);
            }
            return;
        }
        hovered=null;hoveredRestoreTarget=null;hoveredDoorLock=null;
        if (Physics.Raycast(transform.position,transform.forward,out RaycastHit hit,inspectDistance,inspectableLayers,QueryTriggerInteraction.Collide))
        {
            hoveredDoorLock=hit.collider.GetComponentInParent<DoorPasswordLock>();
            if(hoveredDoorLock==null)
            {
                BeerRestoreController restore=hit.collider.GetComponentInParent<BeerRestoreController>();
                if(restore!=null)
                {
                    if(restore.CanClickRestoreTarget(hit.collider))hoveredRestoreTarget=restore;
                }
                else
                {
                    InspectableObject candidate=hit.collider.GetComponentInParent<InspectableObject>();
                    if(candidate!=null&&candidate.CanInspect)hovered=candidate;
                }
            }
        }
        bool collectible=hovered!=null&&hovered.GetComponent<IInspectableCollectible>()!=null;
        if(hoveredDoorLock!=null)SetDoorCrosshair(true);else SetCrosshair(hovered!=null||hoveredRestoreTarget!=null,collectible||hoveredRestoreTarget!=null);
        if(Input.GetMouseButtonDown(0))
        {
            if(hoveredDoorLock!=null)OpenDoorPassword(hoveredDoorLock);
            else if(hovered!=null)OpenInspection(hovered,collectible);
            else if(hoveredRestoreTarget!=null)hoveredRestoreTarget.OnRestoreTargetClicked();
        }
    }

    private void OpenInspection(InspectableObject target,bool collectible)
    {
        InspectableUIController targetUI=collectible?collectibleInspectUI:inspectUI;
        if (targetUI==null) { Debug.LogError("InspectableRaycaster: matching inspection Canvas controller is missing."); return; }
        InteractionUI.Instance?.HideInteract(); targetUI.Show(target); LockControls(); SetCrosshair(false,false);
    }
    private void CloseInspection(InspectableUIController ui) { ui.Hide(); UnlockControls(); }
    private void OpenDoorPassword(DoorPasswordLock target)
    {
        if(doorPasswordUI==null){Debug.LogError("InspectableRaycaster: DoorPasswordCanvas controller is missing.");return;}
        InteractionUI.Instance?.HideInteract();LockControls();SetCrosshair(false,false);doorPasswordUI.Show(target,UnlockControls);
    }
    private void SetCrosshair(bool active,bool useHand)
    {
        if (crosshairImage==null) return;
        crosshairImage.sprite=active?(useHand&&handSprite!=null?handSprite:magnifierSprite!=null?magnifierSprite:normalSprite):normalSprite;
        crosshairImage.color=active?(useHand?handColor:magnifierColor):normalColor;
        crosshairImage.rectTransform.sizeDelta=active?(useHand?handSize:magnifierSize):normalSize;
        crosshairImage.preserveAspect=true;
    }

    private void SetDoorCrosshair(bool active)
    {
        if(crosshairImage==null)return;
        crosshairImage.sprite=active&&doorLockSprite!=null?doorLockSprite:normalSprite;
        crosshairImage.color=active?doorLockColor:normalColor;
        crosshairImage.rectTransform.sizeDelta=active?doorLockSize:normalSize;
        crosshairImage.preserveAspect=true;
    }

    private void OnDisable(){SetCrosshair(false,false);}
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
