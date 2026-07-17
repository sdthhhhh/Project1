using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PhotoFrameItem : MonoBehaviour, IInteractable
{
    public static bool HasPhotoFrame = false;

    [Header("Inspect UI")]
    [SerializeField] private GameObject inspectPanel;
    [SerializeField] private Image inspectImage;
    [SerializeField] private TMP_Text inspectText;

    [Header("Photo Frame")]
    [SerializeField] private Sprite frameSprite;

    [TextArea]
    [SerializeField] private string description =
        "一张旧相框。\n照片中的林芳站在这张桌子旁。";

    private bool isInspecting = false;

    public void Interact()
    {
        Debug.Log("PhotoFrame Interact called");

        if (HasPhotoFrame)
            return;

        inspectPanel.SetActive(true);

        if (frameSprite != null)
        {
            inspectImage.sprite = frameSprite;
        }

        inspectText.text = description;

        isInspecting = true;
    }

    private void Update()
    {
        if (!isInspecting)
            return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
        {
            HasPhotoFrame = true;
            isInspecting = false;

            inspectPanel.SetActive(false);
            gameObject.SetActive(false);

            Debug.Log("获得相框");
        }
    }
    public string GetInteractText()
    {
        return "Press E to inspect frame";
    }
}
