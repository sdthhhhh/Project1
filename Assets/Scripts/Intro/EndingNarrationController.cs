using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// EndScene flow: pre-choice dialogue → accuse LinFang / SuYu → post-choice dialogue
/// (optional branch lines) → optional next scene.
/// Click / Space / Enter advances lines (disabled while the choice panel is open).
/// </summary>
[DisallowMultipleComponent]
public sealed class EndingNarrationController : MonoBehaviour
{
    [Header("Dialogue Source")]
    [SerializeField, Tooltip("Lines before the killer choice.")]
    private TextAsset preChoiceCsv;
    [SerializeField, Tooltip("Shared lines after either choice.")]
    private TextAsset postChoiceCsv;
    [SerializeField, Tooltip("Optional extra lines only if player picks LinFang.")]
    private TextAsset postChoiceLinFangCsv;
    [SerializeField, Tooltip("Optional extra lines only if player picks SuYu.")]
    private TextAsset postChoiceSuYuCsv;

    [Header("UI")]
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private GameObject speakerRow;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private Button choiceLinFangButton;
    [SerializeField] private Button choiceSuYuButton;
    [SerializeField] private TMP_Text choicePromptText;

    [Header("Choice Labels")]
    [SerializeField] private string choicePrompt = "Who is the real killer?";
    [SerializeField] private string choiceLinFangLabel = "A. LinFang";
    [SerializeField] private string choiceSuYuLabel = "B. SuYu";

    [Header("Typewriter")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 42f;
    [SerializeField] private bool allowClickToSkipTyping = true;

    [Header("Flow")]
    [SerializeField, Tooltip("Optional scene after the final post-choice line. Leave empty to stay here.")]
    private string nextSceneName = "";
    [SerializeField, Min(0f)] private float delayBeforeSceneLoad = 0.35f;

    private enum Phase
    {
        PreChoice,
        AwaitingChoice,
        PostChoice,
        Finished
    }

    private readonly List<IntroDialogueLine> preLines = new List<IntroDialogueLine>();
    private readonly List<IntroDialogueLine> postLines = new List<IntroDialogueLine>();
    private List<IntroDialogueLine> activeLines;
    private int index = -1;
    private Phase phase = Phase.PreChoice;
    private bool typing;
    private Coroutine typingRoutine;
    private string currentFullText = "";

    private void Awake()
    {
        EndingChoice.Reset();
        LoadCsv(preChoiceCsv, preLines);
        if (bodyText != null) bodyText.text = string.Empty;
        if (speakerText != null) speakerText.text = string.Empty;
        if (speakerRow != null) speakerRow.SetActive(false);
        if (panelGroup != null)
        {
            panelGroup.alpha = 1f;
            panelGroup.blocksRaycasts = true;
        }
        HideChoicePanel();
        WireChoiceButtons();
    }

    private void Start()
    {
        if (preLines.Count == 0)
        {
            Debug.LogError("EndingNarrationController: preChoiceCsv has no lines.", this);
            ShowChoicePanel();
            return;
        }

        activeLines = preLines;
        phase = Phase.PreChoice;
        ShowNextLine();
    }

    private void OnDestroy()
    {
        UnwireChoiceButtons();
    }

    private void Update()
    {
        if (phase == Phase.Finished) return;

        if (phase == Phase.AwaitingChoice)
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Alpha1))
                SelectKiller(EndingChoice.Killer.LinFang);
            else if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Alpha2))
                SelectKiller(EndingChoice.Killer.SuYu);
            return;
        }

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            Advance();
    }

    public void Advance()
    {
        if (phase == Phase.AwaitingChoice || phase == Phase.Finished) return;

        if (typing)
        {
            if (!allowClickToSkipTyping) return;
            FinishTypingImmediate();
            return;
        }

        ShowNextLine();
    }

    private void ShowNextLine()
    {
        index++;
        if (activeLines == null || index >= activeLines.Count)
        {
            OnLinesExhausted();
            return;
        }

        IntroDialogueLine line = activeLines[index];
        bool hasSpeaker = !string.IsNullOrWhiteSpace(line.speaker);
        if (speakerRow != null) speakerRow.SetActive(hasSpeaker);
        if (speakerText != null) speakerText.text = hasSpeaker ? line.speaker : string.Empty;

        currentFullText = line.text ?? string.Empty;
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypeLine(currentFullText));
    }

    private void OnLinesExhausted()
    {
        if (phase == Phase.PreChoice)
        {
            ShowChoicePanel();
            return;
        }

        if (phase == Phase.PostChoice)
        {
            StartCoroutine(EndAndLoadScene());
            return;
        }
    }

    private void ShowChoicePanel()
    {
        phase = Phase.AwaitingChoice;
        typing = false;
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (choicePromptText != null)
            choicePromptText.text = choicePrompt;

        SetButtonLabel(choiceLinFangButton, choiceLinFangLabel);
        SetButtonLabel(choiceSuYuButton, choiceSuYuLabel);

        if (bodyText != null)
            bodyText.text = choicePrompt;
        if (speakerRow != null) speakerRow.SetActive(false);

        if (choicePanel != null)
            choicePanel.SetActive(true);
    }

    private void HideChoicePanel()
    {
        if (choicePanel != null)
            choicePanel.SetActive(false);
    }

    private void SelectKiller(EndingChoice.Killer killer)
    {
        if (phase != Phase.AwaitingChoice) return;

        EndingChoice.Set(killer);
        HideChoicePanel();
        BuildPostLines(killer);
        phase = Phase.PostChoice;
        index = -1;
        activeLines = postLines;

        if (postLines.Count == 0)
        {
            StartCoroutine(EndAndLoadScene());
            return;
        }

        ShowNextLine();
    }

    private void BuildPostLines(EndingChoice.Killer killer)
    {
        postLines.Clear();
        LoadCsv(postChoiceCsv, postLines);
        if (killer == EndingChoice.Killer.LinFang)
            LoadCsv(postChoiceLinFangCsv, postLines);
        else if (killer == EndingChoice.Killer.SuYu)
            LoadCsv(postChoiceSuYuCsv, postLines);
    }

    private static void LoadCsv(TextAsset asset, List<IntroDialogueLine> into)
    {
        if (asset == null) return;
        try
        {
            into.AddRange(IntroDialogueCsv.LoadFromTextAsset(asset));
        }
        catch (System.Exception ex)
        {
            Debug.LogError("EndingNarrationController: failed to load CSV.\n" + ex);
        }
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
        phase = Phase.Finished;
        if (delayBeforeSceneLoad > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeSceneLoad);

        if (string.IsNullOrWhiteSpace(nextSceneName))
            yield break;

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"EndingNarrationController: scene '{nextSceneName}' is not in Build Settings.", this);
            yield break;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void WireChoiceButtons()
    {
        if (choiceLinFangButton != null)
        {
            choiceLinFangButton.onClick.RemoveListener(OnClickLinFang);
            choiceLinFangButton.onClick.AddListener(OnClickLinFang);
        }
        if (choiceSuYuButton != null)
        {
            choiceSuYuButton.onClick.RemoveListener(OnClickSuYu);
            choiceSuYuButton.onClick.AddListener(OnClickSuYu);
        }
    }

    private void UnwireChoiceButtons()
    {
        if (choiceLinFangButton != null)
            choiceLinFangButton.onClick.RemoveListener(OnClickLinFang);
        if (choiceSuYuButton != null)
            choiceSuYuButton.onClick.RemoveListener(OnClickSuYu);
    }

    private void OnClickLinFang() => SelectKiller(EndingChoice.Killer.LinFang);
    private void OnClickSuYu() => SelectKiller(EndingChoice.Killer.SuYu);

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = label;
    }
}
