#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DiaryWhiteboxSceneInstaller
{
    private const string SessionKey = "DiaryWhitebox.PermanentUIInstallAttempted.v2";
    private const string PuzzleName = "DiaryReconstructionPuzzlePanel";
    private const string BookName = "ReconstructedDiaryBookPanel";

    static DiaryWhiteboxSceneInstaller() { EditorApplication.delayCall += InstallOnce; }

    private static void InstallOnce()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        InstallBoard();
        InstallPermanentPuzzleUI();
    }

    [MenuItem("Tools/Diary Whitebox/Install Board In Bedroom Shelf1")]
    public static void InstallBoard()
    {
        if (Application.isPlaying) { Debug.LogWarning("Stop Play Mode before installing DiaryReconstructionBoard."); return; }
        GameObject existing = GameObject.Find("DiaryReconstructionBoard");
        if (existing != null) return;
        GameObject bedroom = GameObject.Find("Bedroom");
        if (bedroom == null) return;
        Transform shelf = FindShelf1(bedroom.transform);
        if (shelf == null) return;

        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(board, "Create Diary Reconstruction Board");
        board.name = "DiaryReconstructionBoard";
        board.transform.SetParent(shelf, false);
        board.transform.localPosition = Vector3.zero;
        board.transform.localRotation = Quaternion.identity;
        board.transform.localScale = new Vector3(.62f, .035f, .82f);
        board.AddComponent<BedroomDesk>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        board.GetComponent<Renderer>().sharedMaterial = new Material(shader) { name = "DiaryBoard_Whitebox_Material", color = new Color(.28f, .08f, .055f) };
        SaveScene();
    }

    [MenuItem("Tools/Diary Whitebox/Install Permanent Puzzle UI")]
    public static void InstallPermanentPuzzleUI()
    {
        if (Application.isPlaying) { Debug.LogWarning("Stop Play Mode before installing the permanent diary UI."); return; }
        Canvas canvas = FindSceneCanvas();
        if (canvas == null) { Debug.LogError("Diary UI installer: GameplayHUDCanvas/HUDCanvas was not found."); return; }

        DiaryPuzzleManager manager = FindSceneComponent<DiaryPuzzleManager>();
        if (manager == null)
        {
            GameObject controller = new GameObject("DiaryPuzzleUIController");
            Undo.RegisterCreatedObjectUndo(controller, "Create Diary Puzzle UI Controller");
            controller.transform.SetParent(canvas.transform, false);
            manager = controller.AddComponent<DiaryPuzzleManager>();
        }

        GameObject puzzle = FindDirectChild(canvas.transform, PuzzleName);
        if (puzzle == null) puzzle = BuildPuzzlePanel(canvas, manager);
        GameObject book = FindDirectChild(canvas.transform, BookName);
        if (book == null) book = BuildBookPanel(canvas);

        TMP_Text left = FindByName(book.transform, "LeftPageText")?.GetComponent<TMP_Text>();
        TMP_Text right = FindByName(book.transform, "RightPageText")?.GetComponent<TMP_Text>();
        TMP_Text number = FindByName(book.transform, "PageNumberText")?.GetComponent<TMP_Text>();
        Button previous = FindByName(book.transform, "PreviousPagesButton")?.GetComponent<Button>();
        Button next = FindByName(book.transform, "NextPagesButton")?.GetComponent<Button>();
        Button close = FindByName(book.transform, "CloseDiaryButton")?.GetComponent<Button>();
        manager.ConfigurePermanentUI(canvas, puzzle, book, left, right, number, previous, next, close);
        EditorUtility.SetDirty(manager);
        puzzle.SetActive(false);
        book.SetActive(false);
        SaveScene();
        Selection.activeGameObject = puzzle;
        Debug.Log("Permanent diary puzzle UI is now saved under GameplayHUDCanvas. Use the Diary Whitebox menu to show it for editing.");
    }

    [MenuItem("Tools/Diary Whitebox/Show Puzzle UI For Editing")]
    public static void ShowPuzzleForEditing() { SetEditingView(true, false); }

    [MenuItem("Tools/Diary Whitebox/Show Diary Book UI For Editing")]
    public static void ShowBookForEditing() { SetEditingView(false, true); }

    [MenuItem("Tools/Diary Whitebox/Hide Diary UI")]
    public static void HideDiaryUI() { SetEditingView(false, false); }

    private static GameObject BuildPuzzlePanel(Canvas canvas, DiaryPuzzleManager manager)
    {
        GameObject panel = UI(PuzzleName, canvas.transform, new Color(.035f, .03f, .025f, .92f));
        SetRect(panel.GetComponent<RectTransform>(), .12f, .08f, .88f, .92f);
        TMP_Text title = Label("PuzzleTitleText", "Reconstruct the diary", panel.transform, 30);
        SetRect(title.rectTransform, .05f, .88f, .95f, .98f);

        GameObject pieces = UI("AvailableDiaryFragments", panel.transform, Color.clear);
        SetRect(pieces.GetComponent<RectTransform>(), .04f, .08f, .30f, .84f);
        GameObject slots = UI("DiaryFragmentSlots", panel.transform, new Color(.12f, .10f, .08f, .8f));
        SetRect(slots.GetComponent<RectTransform>(), .36f, .12f, .96f, .84f);
        Color[] colors = { Color.red, new Color(1,.5f,0), Color.yellow, Color.green, Color.cyan, new Color(.65f,.35f,1) };

        for (int i = 1; i <= 6; i++)
        {
            int row = (i - 1) / 2, col = (i - 1) % 2;
            GameObject slot = UI($"DiarySlot{i:00}", slots.transform, new Color(.25f, .22f, .18f, 1));
            RectTransform sr = slot.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(.08f + col * .48f, .68f - row * .3f);
            sr.anchorMax = new Vector2(.48f + col * .48f, .92f - row * .3f);
            sr.offsetMin = sr.offsetMax = Vector2.zero;
            slot.AddComponent<DiarySlot>().Configure(i, manager);
            Label("SlotNumberText", i.ToString(), slot.transform, 26);

            GameObject piece = UI($"PuzzleFragment{i:00}", pieces.transform, colors[i - 1]);
            RectTransform pr = piece.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = new Vector2(.5f, .88f - (i - 1) * .15f);
            pr.sizeDelta = new Vector2(145, 62);
            pr.anchoredPosition = Vector2.zero;
            Label("FragmentNumberText", i.ToString(), piece.transform, 28);
            piece.AddComponent<DiaryPuzzlePiece>().Configure(i, canvas);
        }
        return panel;
    }

    private static GameObject BuildBookPanel(Canvas canvas)
    {
        GameObject book = UI(BookName, canvas.transform, new Color(.025f, .02f, .015f, .92f));
        SetRect(book.GetComponent<RectTransform>(), .08f, .06f, .92f, .94f);
        GameObject cover = UI("OpenDiaryBook", book.transform, new Color(.25f, .105f, .065f, 1));
        SetRect(cover.GetComponent<RectTransform>(), .08f, .1f, .92f, .92f);
        GameObject left = UI("LeftDiaryPage", cover.transform, new Color(.88f, .83f, .67f, 1));
        SetRect(left.GetComponent<RectTransform>(), .035f, .05f, .495f, .95f);
        GameObject right = UI("RightDiaryPage", cover.transform, new Color(.9f, .85f, .7f, 1));
        SetRect(right.GetComponent<RectTransform>(), .505f, .05f, .965f, .95f);
        GameObject spine = UI("DiaryBookSpine", cover.transform, new Color(.18f, .075f, .045f, 1));
        SetRect(spine.GetComponent<RectTransform>(), .492f, .04f, .508f, .96f);

        TMP_Text leftText = Label("LeftPageText", "Diary left page preview", left.transform, 22);
        leftText.alignment = TextAlignmentOptions.TopLeft; leftText.color = new Color(.12f, .09f, .06f); leftText.margin = new Vector4(30, 34, 30, 40);
        TMP_Text rightText = Label("RightPageText", "Diary right page preview", right.transform, 22);
        rightText.alignment = TextAlignmentOptions.TopLeft; rightText.color = new Color(.12f, .09f, .06f); rightText.margin = new Vector4(30, 34, 30, 40);
        CreateButton("PreviousPagesButton", "Previous", book.transform, new Vector2(.12f, .025f), new Vector2(.3f, .085f));
        CreateButton("NextPagesButton", "Next", book.transform, new Vector2(.7f, .025f), new Vector2(.88f, .085f));
        CreateButton("CloseDiaryButton", "Close Diary", book.transform, new Vector2(.4f, .025f), new Vector2(.6f, .085f));
        TMP_Text pageNumber = Label("PageNumberText", "Pages 1-2 / 6", book.transform, 17);
        SetRect(pageNumber.rectTransform, .3f, .925f, .7f, .98f);
        return book;
    }

    private static void SetEditingView(bool showPuzzle, bool showBook)
    {
        Canvas canvas = FindSceneCanvas();
        if (canvas == null) return;
        GameObject puzzle = FindDirectChild(canvas.transform, PuzzleName);
        GameObject book = FindDirectChild(canvas.transform, BookName);
        if (puzzle == null || book == null) { InstallPermanentPuzzleUI(); puzzle = FindDirectChild(canvas.transform, PuzzleName); book = FindDirectChild(canvas.transform, BookName); }
        if (puzzle != null) puzzle.SetActive(showPuzzle);
        if (book != null) book.SetActive(showBook);
        Selection.activeGameObject = showBook ? book : puzzle;
        SceneView.RepaintAll();
    }

    private static Canvas FindSceneCanvas()
    {
        foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            if (canvas.gameObject.scene.IsValid() && (canvas.name == "GameplayHUDCanvas" || canvas.name == "HUDCanvas")) return canvas;
        return null;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        foreach (T item in Resources.FindObjectsOfTypeAll<T>()) if (item.gameObject.scene.IsValid()) return item;
        return null;
    }

    private static GameObject UI(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static TMP_Text Label(string name, string text, Transform parent, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        TMP_Text label = go.GetComponent<TMP_Text>();
        label.text = text; label.fontSize = size; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        SetRect(label.rectTransform, 0, 0, 1, 1);
        return label;
    }

    private static Button CreateButton(string name, string text, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = UI(name, parent, new Color(.24f, .17f, .1f, 1));
        RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        Button button = go.AddComponent<Button>();
        Label("ButtonText", text, go.transform, 19);
        return button;
    }

    private static void SetRect(RectTransform rect, float x1, float y1, float x2, float y2)
    { rect.anchorMin = new Vector2(x1, y1); rect.anchorMax = new Vector2(x2, y2); rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static GameObject FindDirectChild(Transform parent, string name) { Transform child = parent.Find(name); return child != null ? child.gameObject : null; }
    private static Transform FindByName(Transform root, string name) { foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t; return null; }
    private static Transform FindShelf1(Transform bedroom) { foreach (Transform t in bedroom.GetComponentsInChildren<Transform>(true)) { string n = t.name.Replace(" ", "").Replace("_", "").ToLowerInvariant(); if (n == "shelf1") return t; } return null; }
    private static void SaveScene() { EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes(); }
}
#endif
