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

        if (FindObjectOfType<ItemRestorationSystem>() == null)
            gameObject.AddComponent<ItemRestorationSystem>();

        crosshairImage.color = normalColor;

        interactTextObject.SetActive(false);
        interactText.text = message;

        statusTextObject.SetActive(false);
    }

    public void ShowInteract(string text)
    {
        if (statusTextObject.activeSelf)
            return;

        crosshairImage.color = interactColor;
        interactTextObject.SetActive(true);
        interactText.text = text;
    }

    public void HideInteract()
    {
        crosshairImage.color = normalColor;
        interactTextObject.SetActive(false);
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
        statusText.text = text;
        statusTextObject.SetActive(true);

        yield return new WaitForSeconds(statusDuration);

        statusTextObject.SetActive(false);
    }
}
