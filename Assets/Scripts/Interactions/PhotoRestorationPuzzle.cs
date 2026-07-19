using TMPro;
using UnityEngine;

public interface IInspectableCollectible { void CollectFromInspection(); }

[DisallowMultipleComponent]
public sealed class PhotoRestorationPuzzle : MonoBehaviour
{
    [Header("Scene Objects")]
    [SerializeField,Tooltip("Visible photo the player collects.")]private GameObject collectiblePhoto;
    [SerializeField,Tooltip("Correct restored photo at the final location.")]private GameObject restoredPhoto;
    [SerializeField,Tooltip("HUD label showing the held photo.")]private TMP_Text inventoryText;
    [Header("Messages")]
    [SerializeField]private string collectedStatus="Interacted Photo collected";
    [SerializeField]private string restoredStatus="Photo restored";
    public bool IsCollected{get;private set;}public bool IsRestored{get;private set;}

    public void Configure(GameObject collectible,GameObject restored,TMP_Text inventory)
    {collectiblePhoto=collectible;restoredPhoto=restored;inventoryText=inventory;}

    private void Start()
    {
        IsCollected=false;IsRestored=false;
        foreach(Transform child in transform)SetRenderers(child.gameObject,child.gameObject==collectiblePhoto);
        if(collectiblePhoto!=null)collectiblePhoto.SetActive(true);
        if(inventoryText!=null)inventoryText.gameObject.SetActive(false);
    }

    public void Collect()
    {
        if(IsCollected||IsRestored)return;IsCollected=true;
        if(collectiblePhoto!=null)collectiblePhoto.SetActive(false);
        if(inventoryText!=null){inventoryText.text="Interacted Photo";inventoryText.color=Color.white;inventoryText.gameObject.SetActive(true);}
        InteractionUI.Instance?.ShowStatus(collectedStatus);
    }

    public void Restore()
    {
        if(!IsCollected||IsRestored)return;IsRestored=true;IsCollected=false;
        foreach(Transform child in transform)if(child.gameObject!=collectiblePhoto)SetRenderers(child.gameObject,true);
        if(restoredPhoto!=null)SetRenderers(restoredPhoto,true);
        if(inventoryText!=null){inventoryText.color=Color.yellow;inventoryText.text="Interacted Photo - Restored";}
        InteractionUI.Instance?.ShowStatus(restoredStatus);
    }

    private static void SetRenderers(GameObject root,bool visible)
    {foreach(Renderer renderer in root.GetComponentsInChildren<Renderer>(true))renderer.enabled=visible;}
}

[DisallowMultipleComponent]
public sealed class PhotoCollectible : MonoBehaviour,IInspectableCollectible
{
    [SerializeField]private PhotoRestorationPuzzle puzzle;
    public void Configure(PhotoRestorationPuzzle owner){puzzle=owner;}
    public void CollectFromInspection(){if(puzzle!=null)puzzle.Collect();else Debug.LogError("PhotoCollectible: Puzzle reference is missing.");}
}

[DisallowMultipleComponent]
public sealed class PhotoRestorePoint : MonoBehaviour,IInteractable
{
    [SerializeField]private PhotoRestorationPuzzle puzzle;
    public void Configure(PhotoRestorationPuzzle owner){puzzle=owner;}
    public string GetInteractText()=>puzzle!=null&&puzzle.IsCollected?"Press E to restore photo":"A photo seems to be missing here";
    public void Interact(){if(puzzle!=null&&puzzle.IsCollected)puzzle.Restore();else InteractionUI.Instance?.ShowStatus("A photo seems to be missing here");}
}
