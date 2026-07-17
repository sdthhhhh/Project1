using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public sealed class DiaryPuzzleManager : MonoBehaviour
{
    [Header("UI Parent")]
    [SerializeField, Tooltip("Canvas used for the generated puzzle. HUDCanvas is used when empty.")] private Canvas uiCanvas;
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
    private GameObject panel, bookPanel;
    private TMP_Text leftPageText, rightPageText, pageNumberText;
    private Button previousButton, nextButton, closeBookButton;
    private int spreadIndex;
    private bool playerLocked;
    private Quaternion lockedBodyRotation, lockedLookRotation;
    private Transform playerBody, lookTransform;

    private void Awake()
    {
        if (uiCanvas == null) { GameObject hud = GameObject.Find("HUDCanvas"); if (hud != null) uiCanvas = hud.GetComponent<Canvas>(); }
        if (playerMovement == null) playerMovement = FindObjectOfType<FirstPersonMovement>();
        if (playerLook == null) playerLook = FindObjectOfType<FirstPersonLook>();
        if (playerInteraction == null) playerInteraction = FindObjectOfType<PlayerInteraction>();
        if (uiCanvas == null) { Debug.LogError("DiaryPuzzleManager: HUDCanvas/Canvas is missing."); return; }
        BuildWhiteboxUI();
        BuildBookUI();
    }

    public void OpenPuzzle()
    {
        if (panel == null) { Debug.LogError("DiaryPuzzleManager: DiaryPuzzlePanel was not built."); return; }
        panel.SetActive(true); SetPlayerControl(false);
    }

    public void CheckCompletion()
    {
        foreach (DiarySlot slot in panel.GetComponentsInChildren<DiarySlot>()) if (!slot.IsFilled) return;
        DiaryManager.Instance?.MarkPuzzleCompleted(); panel.SetActive(false); StartCoroutine(ShowDiaryNextFrame());
    }

    private IEnumerator ShowDiaryNextFrame()
    {
        yield return null; SetPlayerControl(false); spreadIndex = 0; bookPanel.SetActive(true); bookPanel.transform.SetAsLastSibling(); RefreshBook();
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
        Cursor.visible = !enabled; Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
    }

    private void LateUpdate()
    {
        if (!playerLocked) return;
        if (playerBody != null) playerBody.rotation = lockedBodyRotation;
        if (lookTransform != null) lookTransform.localRotation = lockedLookRotation;
    }

    private void BuildWhiteboxUI()
    {
        panel = UI("DiaryPuzzlePanel", uiCanvas.transform, new Color(.035f, .03f, .025f, .97f));
        RectTransform root = panel.GetComponent<RectTransform>(); root.anchorMin = new Vector2(.12f, .08f); root.anchorMax = new Vector2(.88f, .92f); root.offsetMin = root.offsetMax = Vector2.zero;
        TMP_Text title = Label("Reconstruct the diary", panel.transform, 30); SetRect(title.rectTransform, .05f, .88f, .95f, .98f);
        GameObject pieces = UI("Pieces", panel.transform, Color.clear); SetRect(pieces.GetComponent<RectTransform>(), .04f, .08f, .30f, .84f);
        GameObject slots = UI("Slots", panel.transform, new Color(.12f, .1f, .08f, .8f)); SetRect(slots.GetComponent<RectTransform>(), .36f, .12f, .96f, .84f);
        Color[] colors = { Color.red, new Color(1,.5f,0), Color.yellow, Color.green, Color.cyan, new Color(.65f,.35f,1) };
        for (int i = 1; i <= 6; i++)
        {
            int row = (i - 1) / 2, col = (i - 1) % 2;
            GameObject slot = UI($"DiarySlot{i:00}", slots.transform, new Color(.25f,.22f,.18f,1));
            RectTransform sr = slot.GetComponent<RectTransform>(); sr.anchorMin = new Vector2(.08f + col*.48f, .68f-row*.3f); sr.anchorMax = new Vector2(.48f+col*.48f, .92f-row*.3f); sr.offsetMin=sr.offsetMax=Vector2.zero;
            slot.AddComponent<DiarySlot>().Configure(i, this); Label(i.ToString(), slot.transform, 26);
            GameObject piece = UI($"PuzzleFragment{i:00}", pieces.transform, colors[i-1]);
            RectTransform pr = piece.GetComponent<RectTransform>(); pr.anchorMin=pr.anchorMax=new Vector2(.5f,.88f-(i-1)*.15f); pr.sizeDelta=new Vector2(145,62); pr.anchoredPosition=Vector2.zero;
            Label(i.ToString(), piece.transform, 28); piece.AddComponent<DiaryPuzzlePiece>().Configure(i, uiCanvas);
        }
        panel.SetActive(false);
    }

    private void BuildBookUI()
    {
        bookPanel = UI("DiaryBookPanel", uiCanvas.transform, new Color(.025f,.02f,.015f,.98f));
        RectTransform root=bookPanel.GetComponent<RectTransform>(); root.anchorMin=new Vector2(.08f,.06f);root.anchorMax=new Vector2(.92f,.94f);root.offsetMin=root.offsetMax=Vector2.zero;
        GameObject cover=UI("OpenBook",bookPanel.transform,new Color(.25f,.105f,.065f,1));SetRect(cover.GetComponent<RectTransform>(),.08f,.1f,.92f,.92f);
        GameObject left=UI("LeftPage",cover.transform,new Color(.88f,.83f,.67f,1));SetRect(left.GetComponent<RectTransform>(),.035f,.05f,.495f,.95f);
        GameObject right=UI("RightPage",cover.transform,new Color(.9f,.85f,.7f,1));SetRect(right.GetComponent<RectTransform>(),.505f,.05f,.965f,.95f);
        GameObject spine=UI("BookSpine",cover.transform,new Color(.18f,.075f,.045f,1));SetRect(spine.GetComponent<RectTransform>(),.492f,.04f,.508f,.96f);
        leftPageText=Label("",left.transform,22); leftPageText.alignment=TextAlignmentOptions.TopLeft; leftPageText.color=new Color(.12f,.09f,.06f); leftPageText.margin=new Vector4(30,34,30,40);
        rightPageText=Label("",right.transform,22); rightPageText.alignment=TextAlignmentOptions.TopLeft; rightPageText.color=new Color(.12f,.09f,.06f); rightPageText.margin=new Vector4(30,34,30,40);
        previousButton=CreateButton("Previous",bookPanel.transform,new Vector2(.12f,.025f),new Vector2(.3f,.085f),PreviousSpread);
        nextButton=CreateButton("Next",bookPanel.transform,new Vector2(.7f,.025f),new Vector2(.88f,.085f),NextSpread);
        closeBookButton=CreateButton("Close Diary",bookPanel.transform,new Vector2(.4f,.025f),new Vector2(.6f,.085f),CloseBook);
        pageNumberText=Label("",bookPanel.transform,17);SetRect(pageNumberText.rectTransform,.3f,.925f,.7f,.98f);
        bookPanel.SetActive(false);
    }

    private void PreviousSpread() { if (spreadIndex>0) { spreadIndex--; RefreshBook(); } }
    private void NextSpread() { if ((spreadIndex+1)*2<diaryPages.Length) { spreadIndex++; RefreshBook(); } }
    private void CloseBook() { bookPanel.SetActive(false); SetPlayerControl(true); }
    private void RefreshBook()
    {
        int leftIndex=spreadIndex*2,rightIndex=leftIndex+1;
        leftPageText.text=leftIndex<diaryPages.Length?diaryPages[leftIndex]:"";
        rightPageText.text=rightIndex<diaryPages.Length?diaryPages[rightIndex]:"";
        int totalSpreads=Mathf.Max(1,Mathf.CeilToInt(diaryPages.Length/2f)); pageNumberText.text=$"Pages {leftIndex+1}-{Mathf.Min(rightIndex+1,diaryPages.Length)} / {diaryPages.Length}";
        previousButton.interactable=spreadIndex>0; nextButton.interactable=spreadIndex<totalSpreads-1;
    }

    private static GameObject UI(string name, Transform parent, Color color)
    { GameObject go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)); go.transform.SetParent(parent,false); go.GetComponent<Image>().color=color; return go; }
    private static TMP_Text Label(string text, Transform parent, float size)
    { GameObject go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false); TMP_Text t=go.GetComponent<TMP_Text>(); t.text=text;t.fontSize=size;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false; SetRect(t.rectTransform,0,0,1,1); return t; }
    private static void SetRect(RectTransform r,float x1,float y1,float x2,float y2)
    { r.anchorMin=new Vector2(x1,y1);r.anchorMax=new Vector2(x2,y2);r.offsetMin=r.offsetMax=Vector2.zero; }
    private static Button CreateButton(string text,Transform parent,Vector2 min,Vector2 max,UnityAction action)
    { GameObject go=UI(text,parent,new Color(.24f,.17f,.1f,1));RectTransform r=go.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;Button b=go.AddComponent<Button>();b.onClick.AddListener(action);Label(text,go.transform,19);return b; }
}
