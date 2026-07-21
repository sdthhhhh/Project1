using UnityEngine;

[DisallowMultipleComponent]
public sealed class EvidenceInspectableCollectible:MonoBehaviour,IInspectableCollectible
{
    [SerializeField,Tooltip("Status message shown after collecting this evidence.")]private string collectedMessage="Evidence collected";
    public void Configure(string message){collectedMessage=message;}
    public void CollectFromInspection(){InteractionUI.Instance?.ShowStatus(collectedMessage);gameObject.SetActive(false);}
}
