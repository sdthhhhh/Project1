using UnityEngine;

public class FramePlacePoint : MonoBehaviour, IInteractable
{
    public static bool IsPhotoFramePlaced { get; private set; }
    [SerializeField] private GameObject placedFrame;
    [SerializeField] private Drawer bottomDrawer;
    [SerializeField] private GameObject medicalReport;

    private bool placed = false;

    private void Start()
    {
        IsPhotoFramePlaced = false;
        placedFrame.SetActive(false);
        medicalReport.SetActive(false);
    }

    public void Interact()
    {
        if (placed) return;

        if (!PhotoFrameItem.HasPhotoFrame)
        {
            Debug.Log("这里似乎可以放什么东西");
            return;
        }

        placed = true;
        IsPhotoFramePlaced = true;
        PhotoFrameItem.HasPhotoFrame = false;

        placedFrame.SetActive(true);
        Debug.Log("相框归位");
    }
    public string GetInteractText()
    {
        return "Press E to place frame";
    }
}
