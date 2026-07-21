using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneTransitionManager : MonoBehaviour
{
    [Header("Countdown Source")]
    [SerializeField, Tooltip("Timer whose completion starts the transition.")]
    private CountdownTimer countdownTimer;

    [Header("Destination")]
    [Tooltip("Exact name of a scene included in Build Settings.")]
    public string nextSceneName;
    [Min(.01f)] public float handAnimationDuration = 1.5f;
    [Min(0f)] public float waitAfterCovered = .3f;

    [Header("Hand Overlay")]
    [SerializeField, Tooltip("Full-screen overlay canvas containing both hand images.")]
    private Canvas transitionCanvas;
    [SerializeField, Tooltip("Left hand RectTransform. Its editor position is the fully-covered position.")]
    private RectTransform handLeft;
    [SerializeField, Tooltip("Right hand RectTransform. Its editor position is the fully-covered position.")]
    private RectTransform handRight;
    [SerializeField, Tooltip("How far beyond the canvas edge each hand begins, in pixels.")]
    private float extraOffscreenDistance = 80f;

    [Header("Player Scripts To Disable")]
    public MonoBehaviour movementScript;
    public MonoBehaviour lookScript;
    public MonoBehaviour jumpScript;
    public MonoBehaviour interactionScript;
    [SerializeField, Tooltip("Any additional gameplay scripts that must stop during transition.")]
    private MonoBehaviour[] additionalScriptsToDisable;

    private Vector2 leftCoveredPosition;
    private Vector2 rightCoveredPosition;
    private Vector2 leftOffscreenPosition;
    private Vector2 rightOffscreenPosition;
    private bool transitionStarted;
    private bool handPositionsPrepared;

    private void Awake()
    {
        if (countdownTimer == null) countdownTimer = FindObjectOfType<CountdownTimer>();
        if (transitionCanvas == null) transitionCanvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        if (countdownTimer != null) countdownTimer.Completed += BeginTransition;
    }

    private void Start()
    {
        if (!ValidateReferences()) return;
        PrepareHandPositions();
    }

    private void PrepareHandPositions()
    {
        if (handPositionsPrepared) return;
        Canvas.ForceUpdateCanvases();
        leftCoveredPosition = handLeft.anchoredPosition;
        rightCoveredPosition = handRight.anchoredPosition;
        float canvasWidth = ((RectTransform)transitionCanvas.transform).rect.width;
        float travelDistance = canvasWidth + extraOffscreenDistance;
        leftOffscreenPosition = leftCoveredPosition + Vector2.left * travelDistance;
        rightOffscreenPosition = rightCoveredPosition + Vector2.right * travelDistance;
        handLeft.anchoredPosition = leftOffscreenPosition;
        handRight.anchoredPosition = rightOffscreenPosition;
        handPositionsPrepared = true;
    }

    private void OnDisable()
    {
        if (countdownTimer != null) countdownTimer.Completed -= BeginTransition;
    }

    public void Configure(CountdownTimer timer, Canvas owner, RectTransform left, RectTransform right,
        MonoBehaviour movement, MonoBehaviour look, MonoBehaviour jump, MonoBehaviour interaction)
    {
        countdownTimer = timer;
        transitionCanvas = owner;
        handLeft = left;
        handRight = right;
        movementScript = movement;
        lookScript = look;
        jumpScript = jump;
        interactionScript = interaction;
    }

    [ContextMenu("Preview Transition Now")]
    public void BeginTransition()
    {
        if (transitionStarted || !isActiveAndEnabled) return;
        if (!ValidateReferences()) return;
        PrepareHandPositions();
        transitionStarted = true;
        DisablePlayerControl();
        StartCoroutine(CoverEyesAndLoadScene());
    }

    private IEnumerator CoverEyesAndLoadScene()
    {
        float elapsed = 0f;
        while (elapsed < handAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / handAnimationDuration);
            t = t * t * (3f - 2f * t);
            handLeft.anchoredPosition = Vector2.LerpUnclamped(leftOffscreenPosition, leftCoveredPosition, t);
            handRight.anchoredPosition = Vector2.LerpUnclamped(rightOffscreenPosition, rightCoveredPosition, t);
            yield return null;
        }

        handLeft.anchoredPosition = leftCoveredPosition;
        handRight.anchoredPosition = rightCoveredPosition;
        if (waitAfterCovered > 0f) yield return new WaitForSecondsRealtime(waitAfterCovered);

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("SceneTransitionManager: Next Scene Name is empty. Player remains covered so the setup error is visible.", this);
            yield break;
        }
        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"SceneTransitionManager: Scene '{nextSceneName}' is not available. Add it to Build Settings.", this);
            yield break;
        }
        SceneManager.LoadScene(nextSceneName);
    }

    private void DisablePlayerControl()
    {
        Disable(movementScript);
        Disable(lookScript);
        Disable(jumpScript);
        Disable(interactionScript);
        if (additionalScriptsToDisable != null)
            foreach (MonoBehaviour script in additionalScriptsToDisable) Disable(script);
        if (movementScript != null && movementScript.TryGetComponent(out Rigidbody body))
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private bool ValidateReferences()
    {
        bool valid = true;
        if (countdownTimer == null) { Debug.LogError("SceneTransitionManager: Countdown Timer is not assigned.", this); valid = false; }
        if (transitionCanvas == null) { Debug.LogError("SceneTransitionManager: Transition Canvas is not assigned.", this); valid = false; }
        if (handLeft == null || handRight == null) { Debug.LogError("SceneTransitionManager: Hand Left and Hand Right must both be assigned.", this); valid = false; }
        return valid;
    }

    private static void Disable(MonoBehaviour script)
    {
        if (script != null) script.enabled = false;
    }
}
