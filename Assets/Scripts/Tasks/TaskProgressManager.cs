using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cross-scene task progress. Survives Intro → Sample via DontDestroyOnLoad.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class TaskProgressManager : MonoBehaviour
{
    public static TaskProgressManager Instance { get; private set; }

    public event Action OnProgressChanged;

    [SerializeField] private List<MainTaskData> mainTasks = new List<MainTaskData>();
    [SerializeField, Min(0.1f)] private float advanceDelaySeconds = 0.8f;
    [SerializeField] private string sampleSceneName = "SampleScene";

    private int currentMainIndex = -1;
    private bool showSubtasks;
    private bool advancing;
    private bool fadedOut;

    public bool ShowSubtasks => showSubtasks;
    public bool IsFadedOut => fadedOut;
    public int CurrentMainIndex => currentMainIndex;

    public MainTaskData CurrentMain =>
        currentMainIndex >= 0 && currentMainIndex < mainTasks.Count
            ? mainTasks[currentMainIndex]
            : null;

    public IReadOnlyList<MainTaskData> MainTasks => mainTasks;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (mainTasks == null || mainTasks.Count == 0)
            SeedDefaultTasks();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == sampleSceneName && currentMainIndex >= 0)
            SetShowSubtasks(true);
    }

    public void EnsureDefaults()
    {
        if (mainTasks == null || mainTasks.Count == 0)
            SeedDefaultTasks();
    }

    /// <summary>Called from Intro when dialogue ends — reveal first main task (no subtasks yet).</summary>
    public void StartFirstMainTask()
    {
        EnsureDefaults();

        TaskBarBootstrap bootstrap = GetComponent<TaskBarBootstrap>();
        if (bootstrap != null)
            bootstrap.EnsureBuilt();

        fadedOut = false;
        showSubtasks = false;
        currentMainIndex = 0;
        ResetRuntimeFlagsFrom(0);
        NotifyChanged();
    }

    public void SetShowSubtasks(bool value)
    {
        if (showSubtasks == value) return;
        showSubtasks = value;
        NotifyChanged();
    }

    public void CompleteSubTask(string subTaskId)
    {
        if (string.IsNullOrWhiteSpace(subTaskId) || advancing || fadedOut)
            return;

        MainTaskData main = CurrentMain;
        if (main == null || main.completed) return;

        SubTaskData target = null;
        for (int i = 0; i < main.subTasks.Count; i++)
        {
            if (main.subTasks[i].id == subTaskId)
            {
                target = main.subTasks[i];
                break;
            }
        }

        if (target == null || target.completed) return;

        target.completed = true;
        NotifyChanged();

        if (AreAllSubsComplete(main))
            StartCoroutine(CompleteMainAndAdvance());
    }

    private IEnumerator CompleteMainAndAdvance()
    {
        if (advancing) yield break;
        advancing = true;

        MainTaskData main = CurrentMain;
        if (main != null)
            main.completed = true;

        NotifyChanged();
        yield return new WaitForSecondsRealtime(advanceDelaySeconds);

        int next = currentMainIndex + 1;
        if (next >= mainTasks.Count)
        {
            fadedOut = true;
            NotifyChanged();
            advancing = false;
            yield break;
        }

        currentMainIndex = next;
        ResetRuntimeFlagsFrom(currentMainIndex);
        // Stay in Sample — keep subtasks visible for the new main.
        showSubtasks = true;
        advancing = false;
        NotifyChanged();
    }

    private static bool AreAllSubsComplete(MainTaskData main)
    {
        if (main.subTasks == null || main.subTasks.Count == 0)
            return true;
        for (int i = 0; i < main.subTasks.Count; i++)
        {
            if (!main.subTasks[i].completed)
                return false;
        }
        return true;
    }

    private void ResetRuntimeFlagsFrom(int startIndex)
    {
        for (int m = startIndex; m < mainTasks.Count; m++)
        {
            MainTaskData main = mainTasks[m];
            main.completed = false;
            if (main.subTasks == null) continue;
            for (int s = 0; s < main.subTasks.Count; s++)
                main.subTasks[s].completed = false;
        }
    }

    private void NotifyChanged()
    {
        OnProgressChanged?.Invoke();
    }

    private void SeedDefaultTasks()
    {
        mainTasks = new List<MainTaskData>
        {
            new MainTaskData
            {
                id = "main_1",
                displayText = "Task1",
                subTasks = new List<SubTaskData>
                {
                    new SubTaskData { id = "m1_s1", displayText = "Interact with the drawer" },
                    new SubTaskData { id = "m1_s2", displayText = "Interact with the desk" },
                    new SubTaskData { id = "m1_s3", displayText = "Interact with the medical report" },
                }
            },
            new MainTaskData
            {
                id = "main_2",
                displayText = "Task2",
                subTasks = new List<SubTaskData>
                {
                    new SubTaskData { id = "m2_s1", displayText = "Interact with ____" },
                    new SubTaskData { id = "m2_s2", displayText = "Interact with ____" },
                }
            }
        };
    }

#if UNITY_EDITOR
    [ContextMenu("Reset To Default Tasks")]
    private void EditorResetDefaults()
    {
        SeedDefaultTasks();
        currentMainIndex = -1;
        showSubtasks = false;
        fadedOut = false;
        NotifyChanged();
    }
#endif
}
