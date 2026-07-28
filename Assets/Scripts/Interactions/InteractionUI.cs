using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class InteractionUI : MonoBehaviour
{
    public static InteractionUI Instance;

    [Header("Crosshair")]
    [SerializeField] private Image crosshairImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color interactColor = Color.yellow;

    [Header("Interact Text")]
    [SerializeField] private GameObject interactTextObject;
    [SerializeField] private TMP_Text interactText;
    [SerializeField] private string message = "Press E to interact";

    [Header("Status Text")]
    [SerializeField] private GameObject statusTextObject;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private float statusDuration = 2f;

    private Coroutine statusCoroutine;

    private void Awake()
    {
        Instance = this;

        // Not serialized in scenes today; keep creating it at runtime until it is placed explicitly.
        if (FindObjectOfType<ItemRestorationSystem>() == null)
            gameObject.AddComponent<ItemRestorationSystem>();

        if (crosshairImage != null) crosshairImage.color = normalColor;

        if (interactTextObject != null) interactTextObject.SetActive(false);
        if (interactText != null) interactText.text = message;

        if (statusTextObject != null) statusTextObject.SetActive(false);
    }

    public void ShowInteract(string text)
    {
        if (statusTextObject != null && statusTextObject.activeSelf)
            return;

        if (crosshairImage != null) crosshairImage.color = interactColor;
        if (interactTextObject != null) interactTextObject.SetActive(true);
        if (interactText != null) interactText.text = text;
    }

    public void HideInteract()
    {
        if (crosshairImage != null) crosshairImage.color = normalColor;
        if (interactTextObject != null) interactTextObject.SetActive(false);
    }

    public void ShowStatus(string text)
    {
        HideInteract();

        if (statusCoroutine != null)
            StopCoroutine(statusCoroutine);

        statusCoroutine = StartCoroutine(StatusRoutine(text));
    }

    private IEnumerator StatusRoutine(string text)
    {
        if (statusText == null || statusTextObject == null) yield break;
        statusText.text = text;
        statusTextObject.SetActive(true);

        yield return new WaitForSeconds(statusDuration);

        statusTextObject.SetActive(false);
    }
}
