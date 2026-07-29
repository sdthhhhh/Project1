using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the sketch-style transparent task bar at runtime.
/// Layout: Label then hollow square checkbox on the right; indented subtasks.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TaskProgressManager))]
[DefaultExecutionOrder(-40)]
public sealed class TaskBarBootstrap : MonoBehaviour
{
    [SerializeField] private TaskBarUI taskBarUi;

    public TaskBarUI TaskBar => taskBarUi;

    private static Sprite s_whiteSprite;

    private void Awake()
    {
        EnsureBuilt();
    }

    public void EnsureBuilt()
    {
        if (taskBarUi == null)
            taskBarUi = GetComponentInChildren<TaskBarUI>(true);

        if (taskBarUi == null)
            Build();
        else
            taskBarUi.Refresh();
    }

    [ContextMenu("Rebuild Task Bar UI")]
    public void Build()
    {
        Transform existing = transform.Find("TaskBarCanvas");
        if (existing != null)
            DestroyImmediateSafe(existing.gameObject);

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        Sprite white = GetWhiteSprite();

        GameObject canvasGo = new GameObject("TaskBarCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.AddComponent<RectTransform>();
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();
        CanvasGroup rootGroup = canvasGo.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        GameObject panel = new GameObject("TaskPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = new Vector2(40f, -40f);
        panelRt.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.spacing = 8f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = false;
        ContentSizeFitter panelFit = panel.AddComponent<ContentSizeFitter>();
        panelFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        panelFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject mainRow = CreateLabelCheckRow(panel.transform, "MainTaskRow", 36f, 14f, 26f, true, font, white);
        TextMeshProUGUI mainLabel = mainRow.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        Image mainBox = mainRow.transform.Find("Check").GetComponent<Image>();
        TMP_Text mainMark = mainRow.transform.Find("Check/Mark").GetComponent<TMP_Text>();

        GameObject subContainer = new GameObject("SubTaskContainer");
        subContainer.transform.SetParent(panel.transform, false);
        subContainer.AddComponent<RectTransform>();
        VerticalLayoutGroup subV = subContainer.AddComponent<VerticalLayoutGroup>();
        subV.spacing = 6f;
        subV.padding = new RectOffset(28, 0, 0, 0);
        subV.childAlignment = TextAnchor.UpperLeft;
        subV.childControlHeight = true;
        subV.childControlWidth = true;
        subV.childForceExpandHeight = false;
        subV.childForceExpandWidth = false;
        ContentSizeFitter subFit = subContainer.AddComponent<ContentSizeFitter>();
        subFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        subFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        CanvasGroup subGroup = subContainer.AddComponent<CanvasGroup>();
        subGroup.alpha = 1f;
        subGroup.blocksRaycasts = false;
        subGroup.interactable = false;

        GameObject subPrefab = CreateLabelCheckRow(null, "SubTaskRow", 26f, 12f, 20f, false, font, white);
        subPrefab.SetActive(false);
        subPrefab.transform.SetParent(canvasGo.transform, false);

        // Preview labels in editor so you can place the panel.
        mainLabel.text = "Task1";

        taskBarUi = canvasGo.AddComponent<TaskBarUI>();
        taskBarUi.Bind(
            rootGroup,
            panelRt,
            mainBox,
            mainMark,
            mainLabel,
            subContainer.GetComponent<RectTransform>(),
            subGroup,
            subPrefab);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            rootGroup.alpha = 1f;
            UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(taskBarUi);
            so.FindProperty("rootGroup").objectReferenceValue = rootGroup;
            so.FindProperty("panel").objectReferenceValue = panelRt;
            so.FindProperty("mainBoxImage").objectReferenceValue = mainBox;
            so.FindProperty("mainMarkText").objectReferenceValue = mainMark;
            so.FindProperty("mainLabelText").objectReferenceValue = mainLabel;
            so.FindProperty("subTaskContainer").objectReferenceValue = subContainer.GetComponent<RectTransform>();
            so.FindProperty("subTaskGroup").objectReferenceValue = subGroup;
            so.FindProperty("subTaskRowPrefab").objectReferenceValue = subPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.EditorUtility.SetDirty(taskBarUi);
            UnityEditor.EditorUtility.SetDirty(canvasGo);
        }
#endif
    }

    private static GameObject CreateLabelCheckRow(
        Transform parent,
        string name,
        float height,
        float spacing,
        float boxSize,
        bool boldLabel,
        TMP_FontAsset font,
        Sprite white)
    {
        GameObject row = new GameObject(name);
        if (parent != null)
            row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        LayoutElement rowLe = row.AddComponent<LayoutElement>();
        rowLe.minHeight = height;
        rowLe.preferredHeight = height;
        ContentSizeFitter rowFit = row.AddComponent<ContentSizeFitter>();
        rowFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rowFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI label = CreateTmp(
            row.transform,
            "Label",
            boldLabel ? 30f : 20f,
            boldLabel ? FontStyles.Bold : FontStyles.Normal,
            font);
        ContentSizeFitter labelFit = label.gameObject.AddComponent<ContentSizeFitter>();
        labelFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        labelFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject check = new GameObject("Check", typeof(RectTransform));
        check.transform.SetParent(row.transform, false);
        LayoutElement checkLe = check.AddComponent<LayoutElement>();
        checkLe.preferredWidth = boxSize;
        checkLe.preferredHeight = boxSize;
        checkLe.minWidth = boxSize;
        checkLe.minHeight = boxSize;

        CreateEdge(check.transform, "Top", white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(boxSize, 2f));
        CreateEdge(check.transform, "Bottom", white, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(boxSize, 2f));
        CreateEdge(check.transform, "Left", white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(1f, 0f), new Vector2(2f, boxSize));
        CreateEdge(check.transform, "Right", white, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-1f, 0f), new Vector2(2f, boxSize));

        Image boxImage = check.AddComponent<Image>();
        boxImage.color = new Color(1f, 1f, 1f, 0f);
        boxImage.raycastTarget = false;

        GameObject markGo = new GameObject("Mark");
        markGo.transform.SetParent(check.transform, false);
        RectTransform markRt = markGo.AddComponent<RectTransform>();
        markRt.anchorMin = Vector2.zero;
        markRt.anchorMax = Vector2.one;
        markRt.offsetMin = Vector2.zero;
        markRt.offsetMax = Vector2.zero;
        TextMeshProUGUI mark = markGo.AddComponent<TextMeshProUGUI>();
        mark.text = "";
        mark.fontSize = boxSize * 0.75f;
        mark.color = Color.white;
        mark.alignment = TextAlignmentOptions.Center;
        mark.raycastTarget = false;
        if (font != null) mark.font = font;

        return row;
    }

    private static void CreateEdge(
        Transform parent,
        string name,
        Sprite white,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPos,
        Vector2 size)
    {
        GameObject edge = new GameObject(name);
        edge.transform.SetParent(parent, false);
        RectTransform rt = edge.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        Image img = edge.AddComponent<Image>();
        img.sprite = white;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite != null) return s_whiteSprite;
        Texture2D tex = Texture2D.whiteTexture;
        s_whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return s_whiteSprite;
    }

    private static TextMeshProUGUI CreateTmp(
        Transform parent,
        string name,
        float size,
        FontStyles style,
        TMP_FontAsset font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        if (font != null)
            tmp.font = font;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = size + 4f;
        le.preferredHeight = size + 6f;
        return tmp;
    }

    private static void DestroyImmediateSafe(Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(obj);
            return;
        }
#endif
        Object.Destroy(obj);
    }
}
