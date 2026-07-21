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

        if(crosshairImage!=null)crosshairImage.color = normalColor;

        if(statusTextObject!=null)statusTextObject.SetActive(false);
    }

    public void ShowInteract(string text)
    {
        if (statusTextObject!=null&&statusTextObject.activeSelf)
            return;

        if(crosshairImage!=null)crosshairImage.color = interactColor;
    }

    public void HideInteract()
    {
        if(crosshairImage!=null)crosshairImage.color = normalColor;
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
        if(statusText!=null)statusText.text = text;
        if(statusTextObject!=null)statusTextObject.SetActive(true);

        yield return new WaitForSeconds(statusDuration);

        if(statusTextObject!=null)statusTextObject.SetActive(false);
    }
}
