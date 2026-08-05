using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Black screen fade: opaque→clear on enter, clear→opaque then load next scene on countdown end.
/// </summary>
public sealed class SceneTransitionManager : MonoBehaviour
{
    [Header("Countdown Source")]
    [SerializeField, Tooltip("Timer whose completion starts the fade-out.")]
    private CountdownTimer countdownTimer;

    [Header("Destination")]
    [Tooltip("Exact name of a scene included in Build Settings.")]
    public string nextSceneName = "EndScene";

    [Header("Fade")]
    [SerializeField, Tooltip("Full-screen black Image used as the fade mask.")]
    private Image fadeMask;
    [SerializeField, Min(0.01f)] private float fadeInDuration = 3f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 3f;
    [SerializeField, Min(0f)] private float waitAfterFadeOut = 0.15f;

    [Header("Player Scripts To Disable")]
    public MonoBehaviour movementScript;
    public MonoBehaviour lookScript;
    public MonoBehaviour jumpScript;
    public MonoBehaviour interactionScript;
    [SerializeField] private MonoBehaviour[] additionalScriptsToDisable;

    private bool transitionStarted;

    private void Awake()
    {
        if (countdownTimer == null)
            countdownTimer = FindObjectOfType<CountdownTimer>();
        if (fadeMask == null)
        {
            var named = transform.Find("FadeMask");
            if (named != null)
                fadeMask = named.GetComponent<Image>();
        }
        if (fadeMask == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject.name == "FadeMask")
                {
                    fadeMask = images[i];
                    break;
                }
            }
        }

        if (fadeMask != null)
            SetMaskAlpha(1f);
    }

    private void OnEnable()
    {
        if (countdownTimer != null)
            countdownTimer.Completed += BeginTransition;
    }

    private void OnDisable()
    {
        if (countdownTimer != null)
            countdownTimer.Completed -= BeginTransition;
    }

    private void Start()
    {
        if (fadeMask == null)
        {
            Debug.LogError("SceneTransitionManager: Fade Mask Image is not assigned.", this);
            return;
        }

        fadeMask.gameObject.SetActive(true);
        fadeMask.raycastTarget = true;
        SetMaskAlpha(1f);
        StartCoroutine(FadeInRoutine());
    }

    [ContextMenu("Preview Fade Out Now")]
    public void BeginTransition()
    {
        if (transitionStarted || !isActiveAndEnabled)
            return;
        if (fadeMask == null)
        {
            Debug.LogError("SceneTransitionManager: Fade Mask Image is not assigned.", this);
            return;
        }

        transitionStarted = true;
        DisablePlayerControl();
        StartCoroutine(FadeOutAndLoadScene());
    }

    private IEnumerator FadeInRoutine()
    {
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInDuration);
            SetMaskAlpha(1f - t);
            yield return null;
        }

        SetMaskAlpha(0f);
        fadeMask.raycastTarget = false;
    }

    private IEnumerator FadeOutAndLoadScene()
    {
        fadeMask.raycastTarget = true;
        float elapsed = 0f;
        float startAlpha = fadeMask.color.a;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            SetMaskAlpha(Mathf.Lerp(startAlpha, 1f, t));
            yield return null;
        }

        SetMaskAlpha(1f);
        if (waitAfterFadeOut > 0f)
            yield return new WaitForSecondsRealtime(waitAfterFadeOut);

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("SceneTransitionManager: Next Scene Name is empty.", this);
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError(
                "SceneTransitionManager: Scene '" + nextSceneName + "' is not available. Add it to Build Settings.",
                this);
            yield break;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void SetMaskAlpha(float alpha)
    {
        Color c = fadeMask.color;
        c.r = 0f;
        c.g = 0f;
        c.b = 0f;
        c.a = Mathf.Clamp01(alpha);
        fadeMask.color = c;
    }

    private void DisablePlayerControl()
    {
        Disable(movementScript);
        Disable(lookScript);
        Disable(jumpScript);
        Disable(interactionScript);
        if (additionalScriptsToDisable != null)
        {
            for (int i = 0; i < additionalScriptsToDisable.Length; i++)
                Disable(additionalScriptsToDisable[i]);
        }

        if (movementScript != null && movementScript.TryGetComponent(out Rigidbody body))
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private static void Disable(MonoBehaviour script)
    {
        if (script != null)
            script.enabled = false;
    }
}
