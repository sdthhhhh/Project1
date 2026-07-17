using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class MedicalReportItem : MonoBehaviour, IInteractable
{
    public static bool HasMedicalReport = false;

    [Header("Inspect UI")]
    [SerializeField] private GameObject inspectPanel;
    [SerializeField] private Image inspectImage;
    [SerializeField] private TMP_Text inspectText;

    [Header("Report Content")]
    [SerializeField] private Sprite reportSprite;

    [TextArea]
    [SerializeField] private string description = "林芳的就诊报告。";

    private bool isInspecting = false;
    private bool canCloseInspect = false;

    public string GetInteractText()
    {
        return "Press E to view report";
    }

    public void Interact()
    {
        if (HasMedicalReport)
            return;

        inspectPanel.SetActive(true);

        if (inspectImage != null)
        {
            inspectImage.gameObject.SetActive(true);
            inspectImage.sprite = reportSprite;
        }

        if (inspectText != null)
            inspectText.text = description;

        isInspecting = true;
        canCloseInspect = false;

        StartCoroutine(EnableCloseNextFrame());
    }

    private IEnumerator EnableCloseNextFrame()
    {
        yield return null;
        canCloseInspect = true;
    }

    private void Update()
    {
        if (!isInspecting || !canCloseInspect)
            return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
        {
            HasMedicalReport = true;
            isInspecting = false;
            canCloseInspect = false;

            inspectPanel.SetActive(false);

            InteractionUI.Instance.ShowStatus("获得林芳的就诊报告");

            gameObject.SetActive(false);
        }
    }
}
