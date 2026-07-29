using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-left task bar (scene instance). Position via TaskPanel RectTransform.
/// Fades in/out instead of popping.
/// </summary>
[DisallowMultipleComponent]
public sealed class TaskBarUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private RectTransform panel;
    [SerializeField] private Image mainBoxImage;
    [SerializeField] private TMP_Text mainMarkText;
    [SerializeField] private TMP_Text mainLabelText;
    [SerializeField] private RectTransform subTaskContainer;
    [SerializeField] private CanvasGroup subTaskGroup;
    [SerializeField] private GameObject subTaskRowPrefab;

    [Header("Colors")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color completedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("Motion")]
    [SerializeField, Min(0.05f)] private float fadeInDuration = 0.55f;
    [SerializeField, Min(0.05f)] private float fadeOutDuration = 0.4f;
    [SerializeField] private float slidePixels = 28f;

    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private TaskProgressManager subscribed;
    private Coroutine motionRoutine;
    private bool contentVisible;
    private int lastMainIndex = -999;
    private bool lastShowSubs;
    private Vector2 panelRestPos;
    private bool restPosCached;

    public void Bind(
        CanvasGroup group,
        RectTransform panelRt,
        Image mainBox,
        TMP_Text mainMark,
        TMP_Text mainLabel,
        RectTransform subContainer,
        CanvasGroup subGroup,
        GameObject subPrefab)
    {
        rootGroup = group;
        panel = panelRt;
        mainBoxImage = mainBox;
        mainMarkText = mainMark;
        mainLabelText = mainLabel;
        subTaskContainer = subContainer;
        subTaskGroup = subGroup;
        subTaskRowPrefab = subPrefab;
    }

    private void Awake()
    {
        CacheRestPos();
        if (Application.isPlaying)
        {
            if (rootGroup != null)
                rootGroup.alpha = 0f;
            if (subTaskGroup != null)
                subTaskGroup.alpha = 0f;
            contentVisible = false;
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
        Refresh(true);
    }

    private void Start()
    {
        TrySubscribe();
        Refresh(true);
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (motionRoutine != null)
        {
            StopCoroutine(motionRoutine);
            motionRoutine = null;
        }
    }

    private void CacheRestPos()
    {
        if (panel == null || restPosCached) return;
        panelRestPos = panel.anchoredPosition;
        restPosCached = true;
    }

    private void TrySubscribe()
    {
        TaskProgressManager mgr = TaskProgressManager.Instance;
        if (mgr == null || mgr == subscribed) return;
        Unsubscribe();
        subscribed = mgr;
        subscribed.OnProgressChanged += OnProgressChanged;
    }

    private void Unsubscribe()
    {
        if (subscribed != null)
        {
            subscribed.OnProgressChanged -= OnProgressChanged;
            subscribed = null;
        }
    }

    private void OnProgressChanged()
    {
        Refresh(false);
    }

    public void Refresh()
    {
        Refresh(false);
    }

    private void Refresh(bool instant)
    {
        TrySubscribe();
        CacheRestPos();

        TaskProgressManager mgr = TaskProgressManager.Instance;
        bool wantVisible = mgr != null && mgr.CurrentMain != null && !mgr.IsFadedOut;

        if (!wantVisible)
        {
            if (contentVisible || (rootGroup != null && rootGroup.alpha > 0.01f))
                StartMotion(FadeOutRoutine(instant));
            else
                ApplyHiddenImmediate();
            return;
        }

        bool mainChanged = mgr.CurrentMainIndex != lastMainIndex;
        bool subsJustOn = mgr.ShowSubtasks && !lastShowSubs;

        if (!contentVisible)
        {
            ApplyContent(mgr);
            lastMainIndex = mgr.CurrentMainIndex;
            lastShowSubs = mgr.ShowSubtasks;
            StartMotion(FadeInRoutine(instant));
            return;
        }

        if (mainChanged)
        {
            lastMainIndex = mgr.CurrentMainIndex;
            lastShowSubs = mgr.ShowSubtasks;
            StartMotion(CrossFadeMainRoutine(mgr, instant));
            return;
        }

        // Same main — update checks / maybe reveal subtasks.
        ApplyContent(mgr);
        if (subsJustOn)
        {
            lastShowSubs = true;
            StartMotion(FadeInSubsRoutine(instant));
        }
        else
        {
            lastShowSubs = mgr.ShowSubtasks;
            if (subTaskGroup != null)
                subTaskGroup.alpha = mgr.ShowSubtasks ? 1f : 0f;
        }
    }

    private void ApplyContent(TaskProgressManager mgr)
    {
        MainTaskData main = mgr.CurrentMain;
        if (main == null) return;

        bool mainDone = main.completed;
        Color mainColor = mainDone ? completedColor : activeColor;

        if (mainLabelText != null)
        {
            mainLabelText.text = main.displayText;
            mainLabelText.color = mainColor;
        }

        ApplyCheck(mainBoxImage != null ? mainBoxImage.transform : null, mainMarkText, mainDone, mainColor);

        ClearSubRows();

        if (!mgr.ShowSubtasks || subTaskContainer == null || subTaskRowPrefab == null)
        {
            if (subTaskGroup != null && !contentVisible)
                subTaskGroup.alpha = 0f;
            return;
        }

        int mainNum = mgr.CurrentMainIndex + 1;
        for (int i = 0; i < main.subTasks.Count; i++)
        {
            SubTaskData sub = main.subTasks[i];
            GameObject row = Instantiate(subTaskRowPrefab, subTaskContainer);
            row.name = "SubTaskRow_" + i;
            row.SetActive(true);
            spawnedRows.Add(row);

            Transform check = row.transform.Find("Check");
            Transform labelT = row.transform.Find("Label");
            TMP_Text label = labelT != null ? labelT.GetComponent<TMP_Text>() : null;
            TMP_Text mark = check != null && check.Find("Mark") != null
                ? check.Find("Mark").GetComponent<TMP_Text>()
                : null;

            bool done = sub.completed;
            Color c = done ? completedColor : activeColor;

            if (label != null)
            {
                label.text = "–" + mainNum + "." + (i + 1);
                label.color = c;
            }

            ApplyCheck(check, mark, done, c);
        }
    }

    private void StartMotion(IEnumerator routine)
    {
        if (motionRoutine != null)
            StopCoroutine(motionRoutine);
        motionRoutine = StartCoroutine(routine);
    }

    private IEnumerator FadeInRoutine(bool instant)
    {
        contentVisible = true;
        if (panel != null)
            panel.gameObject.SetActive(true);

        if (rootGroup != null)
            rootGroup.alpha = 0f;
        if (panel != null)
            panel.anchoredPosition = panelRestPos + new Vector2(-slidePixels, 0f);

        bool showSubs = TaskProgressManager.Instance != null && TaskProgressManager.Instance.ShowSubtasks;
        if (subTaskGroup != null)
            subTaskGroup.alpha = showSubs ? (instant ? 1f : 0f) : 0f;

        if (instant)
        {
            if (rootGroup != null) rootGroup.alpha = 1f;
            if (panel != null) panel.anchoredPosition = panelRestPos;
            if (subTaskGroup != null) subTaskGroup.alpha = showSubs ? 1f : 0f;
            motionRoutine = null;
            yield break;
        }

        float dur = fadeInDuration;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            if (rootGroup != null) rootGroup.alpha = u;
            if (panel != null)
                panel.anchoredPosition = Vector2.Lerp(panelRestPos + new Vector2(-slidePixels, 0f), panelRestPos, u);
            yield return null;
        }

        if (rootGroup != null) rootGroup.alpha = 1f;
        if (panel != null) panel.anchoredPosition = panelRestPos;

        if (showSubs && subTaskGroup != null)
            yield return FadeCanvasGroup(subTaskGroup, 0f, 1f, fadeInDuration * 0.75f);

        motionRoutine = null;
    }

    private IEnumerator FadeOutRoutine(bool instant)
    {
        if (instant)
        {
            ApplyHiddenImmediate();
            motionRoutine = null;
            yield break;
        }

        float start = rootGroup != null ? rootGroup.alpha : 1f;
        Vector2 startPos = panel != null ? panel.anchoredPosition : panelRestPos;
        float dur = fadeOutDuration;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            if (rootGroup != null) rootGroup.alpha = Mathf.Lerp(start, 0f, u);
            if (panel != null)
                panel.anchoredPosition = Vector2.Lerp(startPos, panelRestPos + new Vector2(-slidePixels * 0.5f, 0f), u);
            yield return null;
        }

        ApplyHiddenImmediate();
        motionRoutine = null;
    }

    private IEnumerator CrossFadeMainRoutine(TaskProgressManager mgr, bool instant)
    {
        if (!instant)
        {
            float start = rootGroup != null ? rootGroup.alpha : 1f;
            float dur = fadeOutDuration * 0.7f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                if (rootGroup != null) rootGroup.alpha = Mathf.Lerp(start, 0f, u);
                yield return null;
            }
        }

        ApplyContent(mgr);
        if (subTaskGroup != null)
            subTaskGroup.alpha = mgr.ShowSubtasks ? 0f : 0f;

        yield return FadeInRoutine(instant);
    }

    private IEnumerator FadeInSubsRoutine(bool instant)
    {
        if (subTaskGroup == null)
        {
            motionRoutine = null;
            yield break;
        }

        if (instant)
        {
            subTaskGroup.alpha = 1f;
            motionRoutine = null;
            yield break;
        }

        yield return FadeCanvasGroup(subTaskGroup, subTaskGroup.alpha, 1f, fadeInDuration * 0.8f);
        motionRoutine = null;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;
        float t = 0f;
        group.alpha = from;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            group.alpha = Mathf.Lerp(from, to, u);
            yield return null;
        }
        group.alpha = to;
    }

    private void ApplyHiddenImmediate()
    {
        contentVisible = false;
        lastMainIndex = -999;
        lastShowSubs = false;
        if (rootGroup != null) rootGroup.alpha = 0f;
        if (subTaskGroup != null) subTaskGroup.alpha = 0f;
        if (panel != null)
        {
            panel.anchoredPosition = panelRestPos;
            // Keep active so you can still tweak layout in the hierarchy.
            panel.gameObject.SetActive(true);
        }
        ClearSubRows();
    }

    private static void ApplyCheck(Transform checkRoot, TMP_Text mark, bool done, Color color)
    {
        if (mark != null)
        {
            mark.text = done ? "✓" : "";
            mark.color = color;
        }

        if (checkRoot == null) return;
        for (int i = 0; i < checkRoot.childCount; i++)
        {
            Transform child = checkRoot.GetChild(i);
            if (child.name == "Mark") continue;
            Image edge = child.GetComponent<Image>();
            if (edge != null)
                edge.color = color;
        }
    }

    private void ClearSubRows()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            if (spawnedRows[i] != null)
                Destroy(spawnedRows[i]);
        }
        spawnedRows.Clear();

        if (subTaskContainer == null) return;
        for (int i = subTaskContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = subTaskContainer.GetChild(i);
            if (child.name == "SubTaskRow") continue; // keep template prefab if nested
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }
}
