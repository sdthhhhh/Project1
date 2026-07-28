using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Bottom-center narration for IntroScene. Click / Space / Enter:
/// - while typing: finish the line
/// - when complete: next line
/// - after last line: load nextSceneName
/// </summary>
[DisallowMultipleComponent]
public sealed class IntroNarrationController : MonoBehaviour
{
    [Header("Dialogue Source")]
    [SerializeField, Tooltip("CSV TextAsset (Excel can edit the .csv on disk).")]
    private TextAsset dialogueCsv;
    [SerializeField, Tooltip("Optional StreamingAssets relative path, e.g. Intro/IntroDialogue.csv")]
    private string streamingAssetsRelativePath = "";

    [Header("UI")]
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private GameObject speakerRow;
    [SerializeField] private CanvasGroup panelGroup;

    [Header("Typewriter")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 42f;
    [SerializeField] private bool allowClickToSkipTyping = true;

    [Header("Flow")]
    [SerializeField] private string nextSceneName = "SampleScene";
    [SerializeField, Min(0f)] private float delayBeforeSceneLoad = 0.35f;

    private readonly List<IntroDialogueLine> lines = new List<IntroDialogueLine>();
    private int index = -1;
    private bool typing;
    private bool finished;
    private Coroutine typingRoutine;
    private string currentFullText = "";

    private void Awake()
    {
        LoadLines();
        if (bodyText != null) bodyText.text = string.Empty;
        if (speakerText != null) speakerText.text = string.Empty;
        if (speakerRow != null) speakerRow.SetActive(false);
        if (panelGroup != null)
        {
            panelGroup.alpha = 1f;
            panelGroup.blocksRaycasts = true;
        }
    }

    private void Start()
    {
        if (lines.Count == 0)
        {
            Debug.LogError("IntroNarrationController: no dialogue lines loaded. Assign CSV or StreamingAssets path.", this);
            return;
        }
        ShowNextLine();
    }

    private void Update()
    {
        if (finished) return;
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            Advance();
    }

    public void Advance()
    {
        if (finished) return;

        if (typing)
        {
            if (!allowClickToSkipTyping) return;
            FinishTypingImmediate();
            return;
        }

        ShowNextLine();
    }

    private void LoadLines()
    {
        lines.Clear();
        try
        {
            // Prefer TextAsset (Assets/Data) for editing in Excel + Unity;
            // StreamingAssets is an optional runtime override path.
            if (dialogueCsv != null)
                lines.AddRange(IntroDialogueCsv.LoadFromTextAsset(dialogueCsv));
            else if (!string.IsNullOrWhiteSpace(streamingAssetsRelativePath))
                lines.AddRange(IntroDialogueCsv.LoadFromStreamingAssets(streamingAssetsRelativePath));
        }
        catch (System.Exception ex)
        {
            Debug.LogError("IntroNarrationController: failed to load CSV.\n" + ex, this);
        }
    }

    private void ShowNextLine()
    {
        index++;
        if (index >= lines.Count)
        {
            StartCoroutine(EndAndLoadScene());
            return;
        }

        IntroDialogueLine line = lines[index];
        bool hasSpeaker = !string.IsNullOrWhiteSpace(line.speaker);
        if (speakerRow != null) speakerRow.SetActive(hasSpeaker);
        if (speakerText != null) speakerText.text = hasSpeaker ? line.speaker : string.Empty;

        currentFullText = line.text ?? string.Empty;
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypeLine(currentFullText));
    }

    private IEnumerator TypeLine(string full)
    {
        typing = true;
        if (bodyText != null) bodyText.text = string.Empty;
        if (string.IsNullOrEmpty(full) || charactersPerSecond <= 0f)
        {
            if (bodyText != null) bodyText.text = full;
            typing = false;
            typingRoutine = null;
            yield break;
        }

        float delay = 1f / charactersPerSecond;
        for (int i = 1; i <= full.Length; i++)
        {
            if (bodyText != null) bodyText.text = full.Substring(0, i);
            yield return new WaitForSecondsRealtime(delay);
        }

        typing = false;
        typingRoutine = null;
    }

    private void FinishTypingImmediate()
    {
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = null;
        typing = false;
        if (bodyText != null) bodyText.text = currentFullText;
    }

    private IEnumerator EndAndLoadScene()
    {
        finished = true;
        if (delayBeforeSceneLoad > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeSceneLoad);

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning("IntroNarrationController: nextSceneName is empty.", this);
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"IntroNarrationController: scene '{nextSceneName}' is not in Build Settings.", this);
            yield break;
        }

        SceneManager.LoadScene(nextSceneName);
    }
}
