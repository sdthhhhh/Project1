using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DiaryPuzzleManager : MonoBehaviour
{
    [Header("Permanent Scene UI")]
    [SerializeField, Tooltip("Canvas containing the permanent diary UI hierarchy.")] private Canvas uiCanvas;
    [SerializeField, Tooltip("Permanent reconstruction puzzle panel saved in the scene.")] private GameObject panel;
    [SerializeField, Tooltip("Permanent completed diary book panel saved in the scene.")] private GameObject bookPanel;
    [SerializeField] private TMP_Text leftPageText;
    [SerializeField] private TMP_Text rightPageText;
    [SerializeField] private TMP_Text pageNumberText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeBookButton;

    [Header("Diary Book Pages")]
    [SerializeField, TextArea(5, 12), Tooltip("Page contents. Two pages are shown per spread.")] private string[] diaryPages = {
        "March 3\n\nMom said things would become better after the wedding. I want to believe her, but the house feels quieter whenever he comes home.",
        "April 16\n\nHe was drinking again. I heard them arguing in the kitchen. Mom told me to lock my door and pretend I was asleep.",
        "May 9\n\nHe tried my door last night. I pushed the chair against it. In the morning he laughed and said I was imagining things.",
        "May 21\n\nI told Mom I was afraid. She cried and said we had nowhere else to go. I know she is afraid too.",
        "June 2\n\nI hid the fruit knife beside my bed. I do not want to hurt anyone. I only want him to stay away.",
        "June 7\n\nMom is working tonight. I locked the door twice. If he comes in again, I have to protect myself."
    };

    [Header("Player Controls")]
    [SerializeField, Tooltip("Player movement component; auto-detected if empty.")] private FirstPersonMovement playerMovement;
    [SerializeField, Tooltip("Player look component; auto-detected if empty.")] private FirstPersonLook playerLook;
    [SerializeField, Tooltip("Player raycast interaction; auto-detected if empty.")] private PlayerInteraction playerInteraction;

    private int spreadIndex;
    private bool playerLocked;
    private Quaternion lockedBodyRotation, lockedLookRotation;
    private Transform playerBody, lookTransform;

    private const int DiaryBookCanvasSortOrder = 400;
    private int savedCanvasSortOrder = int.MinValue;
    private bool comicStyleApplied;

    private void Awake()
    {
        if (uiCanvas == null)
        {
            GameObject hud = GameObject.Find("GameplayHUDCanvas") ?? GameObject.Find("HUDCanvas");
            if (hud != null) uiCanvas = hud.GetComponent<Canvas>();
        }
        if (playerMovement == null) playerMovement = FindObjectOfType<FirstPersonMovement>();
        if (playerLook == null) playerLook = FindObjectOfType<FirstPersonLook>();
        if (playerInteraction == null) playerInteraction = FindObjectOfType<PlayerInteraction>();

        if (panel == null || bookPanel == null)
        {
            Debug.LogError("DiaryPuzzleManager: permanent diary UI is missing from the scene.", this);
            return;
        }

        BindButtons();
        ApplyComicDiaryStyle();
        panel.SetActive(false);
        bookPanel.SetActive(false);
    }

    public void ConfigurePermanentUI(Canvas owner, GameObject puzzle, GameObject book, TMP_Text left, TMP_Text right,
        TMP_Text pageNumber, Button previous, Button next, Button close)
    {
        uiCanvas = owner;
        panel = puzzle;
        bookPanel = book;
        leftPageText = left;
        rightPageText = right;
        pageNumberText = pageNumber;
        previousButton = previous;
        nextButton = next;
        closeBookButton = close;
    }

    public void OpenPuzzle()
    {
        // Prefer 3D socket assembly on the diary FBX; fall back to legacy 2D panel only if missing.
        DiaryAssemblyController assembly = FindObjectOfType<DiaryAssemblyController>();
        if (assembly != null)
        {
            InspectableUIController ui = FindObjectOfType<InspectableUIController>();
            assembly.Configure(ui, OpenBookAfterAssembly);
            assembly.BeginAssembly();
            return;
        }

        if (panel == null) { Debug.LogError("DiaryPuzzleManager: permanent puzzle panel is missing.", this); return; }
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        SetPlayerControl(false);
    }

    private void OpenBookAfterAssembly()
    {
        // Cover reconstruction no longer opens the diary book reading UI.
    }

    /// <summary>Legacy entry — diary book UI is not opened from reconstruction anymore.</summary>
    public void OpenCompletedBook()
    {
        Debug.Log("DiaryPuzzleManager.OpenCompletedBook skipped (reconstruction reveals fragment instead).", this);
    }

    public void CheckCompletion()
    {
        if (panel == null) return;
        foreach (DiarySlot slot in panel.GetComponentsInChildren<DiarySlot>(true))
            if (slot.gameObject.activeInHierarchy && !slot.IsFilled) return;
        DiaryManager.Instance?.MarkPuzzleCompleted();
        panel.SetActive(false);
        // Do not open diary book UI after reconstruction.
    }

    private IEnumerator ShowDiaryNextFrame()
    {
        // Make sure the inspect overlay is gone before the book UI appears.
        ForceCloseAnyInspectUI();
        yield return null;
        ForceCloseAnyInspectUI();

        SetPlayerControl(false);
        spreadIndex = 0;
        BringBookCanvasToFront();
        ApplyComicDiaryStyle();
        bookPanel.SetActive(true);
        bookPanel.transform.SetAsLastSibling();
        RefreshBook();
    }

    private static void ForceCloseAnyInspectUI()
    {
        InspectableRaycaster raycaster = Object.FindObjectOfType<InspectableRaycaster>();
        if (raycaster != null)
            raycaster.ForceCloseInspection();

        InspectableUIController[] uis = Object.FindObjectsOfType<InspectableUIController>(true);
        for (int i = 0; i < uis.Length; i++)
        {
            if (uis[i] != null && uis[i].IsOpen)
                uis[i].Hide();
        }

        if (DiaryInspectPuzzleController.Instance != null && DiaryInspectPuzzleController.Instance.IsOpen)
            DiaryInspectPuzzleController.Instance.Close();
    }

    private void BringBookCanvasToFront()
    {
        Canvas canvas = uiCanvas;
        if (canvas == null && bookPanel != null)
            canvas = bookPanel.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        if (savedCanvasSortOrder == int.MinValue)
            savedCanvasSortOrder = canvas.sortingOrder;
        canvas.sortingOrder = Mathf.Max(savedCanvasSortOrder, DiaryBookCanvasSortOrder);
        canvas.overrideSorting = true;
    }

    private void RestoreBookCanvasSort()
    {
        Canvas canvas = uiCanvas;
        if (canvas == null && bookPanel != null)
            canvas = bookPanel.GetComponentInParent<Canvas>();
        if (canvas == null || savedCanvasSortOrder == int.MinValue)
            return;
        canvas.sortingOrder = savedCanvasSortOrder;
    }

    /// <summary>Black pages, white frame, white text — matches the comic outline look.</summary>
    private void ApplyComicDiaryStyle()
    {
        if (bookPanel == null || comicStyleApplied)
            return;

        Color black = new Color(0.04f, 0.04f, 0.045f, 1f);
        Color white = new Color(0.95f, 0.95f, 0.96f, 1f);
        Color dimWhite = new Color(0.82f, 0.82f, 0.84f, 1f);

        SetGraphicColor(bookPanel.GetComponent<Image>(), new Color(0f, 0f, 0f, 0.82f));

        Transform openBook = bookPanel.transform.Find("OpenDiaryBook");
        if (openBook != null)
        {
            SetGraphicColor(openBook.GetComponent<Image>(), black);
            EnsureWhiteFrame(openBook.gameObject, 4f);
        }

        SetNamedImage(bookPanel.transform, "LeftDiaryPage", black);
        SetNamedImage(bookPanel.transform, "RightDiaryPage", black);
        SetNamedImage(bookPanel.transform, "DiaryBookSpine", white);

        if (leftPageText != null) leftPageText.color = white;
        if (rightPageText != null) rightPageText.color = white;
        if (pageNumberText != null) pageNumberText.color = dimWhite;

        StyleButton(previousButton, black, white);
        StyleButton(nextButton, black, white);
        StyleButton(closeBookButton, black, white);

        comicStyleApplied = true;
    }

    private static void SetNamedImage(Transform root, string name, Color color)
    {
        Transform t = root.Find(name);
        if (t == null)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    t = child;
                    break;
                }
            }
        }
        if (t != null)
            SetGraphicColor(t.GetComponent<Image>(), color);
    }

    private static void SetGraphicColor(Graphic g, Color color)
    {
        if (g != null)
            g.color = color;
    }

    private static void EnsureWhiteFrame(GameObject target, float distance)
    {
        if (target == null)
            return;
        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
            outline = target.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }

    private static void StyleButton(Button button, Color bg, Color text)
    {
        if (button == null)
            return;
        Image img = button.GetComponent<Image>();
        SetGraphicColor(img, bg);
        EnsureWhiteFrame(button.gameObject, 2f);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = text;
    }

    private void BindButtons()
    {
        if (previousButton != null) { previousButton.onClick.RemoveListener(PreviousSpread); previousButton.onClick.AddListener(PreviousSpread); }
        if (nextButton != null) { nextButton.onClick.RemoveListener(NextSpread); nextButton.onClick.AddListener(NextSpread); }
        if (closeBookButton != null) { closeBookButton.onClick.RemoveListener(CloseBook); closeBookButton.onClick.AddListener(CloseBook); }
    }

    private void SetPlayerControl(bool enabled)
    {
        if (!enabled && !playerLocked)
        {
            playerLocked = true;
            playerBody = playerMovement != null ? playerMovement.transform : null;
            lookTransform = playerLook != null ? playerLook.transform : null;
            if (playerBody != null) lockedBodyRotation = playerBody.rotation;
            if (lookTransform != null) lockedLookRotation = lookTransform.localRotation;
            if (playerMovement != null && playerMovement.TryGetComponent(out Rigidbody body))
            { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }
        if (enabled) playerLocked = false;
        if (playerMovement != null) playerMovement.enabled = enabled;
        if (playerLook != null) playerLook.enabled = enabled;
        if (playerInteraction != null) playerInteraction.enabled = enabled;
        Cursor.visible = !enabled;
        Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
    }

    private void LateUpdate()
    {
        if (!playerLocked) return;
        if (playerBody != null) playerBody.rotation = lockedBodyRotation;
        if (lookTransform != null) lookTransform.localRotation = lockedLookRotation;
    }

    private void PreviousSpread() { if (spreadIndex > 0) { spreadIndex--; RefreshBook(); } }
    private void NextSpread() { if ((spreadIndex + 1) * 2 < diaryPages.Length) { spreadIndex++; RefreshBook(); } }
    private void CloseBook()
    {
        bookPanel.SetActive(false);
        RestoreBookCanvasSort();
        SetPlayerControl(true);
    }

    private void Update()
    {
        // Book reading also closes with Esc (same habit as item inspect).
        if (bookPanel != null && bookPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseBook();
    }

    private void RefreshBook()
    {
        if (leftPageText == null || rightPageText == null || pageNumberText == null) return;
        int leftIndex = spreadIndex * 2, rightIndex = leftIndex + 1;
        leftPageText.text = leftIndex < diaryPages.Length ? diaryPages[leftIndex] : "";
        rightPageText.text = rightIndex < diaryPages.Length ? diaryPages[rightIndex] : "";
        int totalSpreads = Mathf.Max(1, Mathf.CeilToInt(diaryPages.Length / 2f));
        pageNumberText.text = $"Pages {leftIndex + 1}-{Mathf.Min(rightIndex + 1, diaryPages.Length)} / {diaryPages.Length}";
        if (previousButton != null) previousButton.interactable = spreadIndex > 0;
        if (nextButton != null) nextButton.interactable = spreadIndex < totalSpreads - 1;
    }
}
