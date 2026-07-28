using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SuYuDiaryTaskManager : MonoBehaviour
{
    public static SuYuDiaryTaskManager Instance { get; private set; }

    [Header("Editable Scene UI")]
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Image[] fragmentIcons = new Image[4];
    [SerializeField] private GameObject completedObject;
    [SerializeField] private Color missingColor = new Color(1f, 1f, 1f, .3f);
    [SerializeField] private Color collectedColor = Color.white;

    private readonly HashSet<int> collectedFragments = new HashSet<int>();

    public int CollectedCount => collectedFragments.Count;
    public int TotalFragments => 4;
    public bool IsCompleted => CollectedCount >= TotalFragments;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one SuYuDiaryTaskManager may exist in a scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void CollectFragment(int fragmentId)
    {
        if (fragmentId < 1 || fragmentId > TotalFragments)
        {
            Debug.LogError($"Fragment ID must be between 1 and {TotalFragments}.", this);
            return;
        }

        if (!collectedFragments.Add(fragmentId)) return;
        RefreshUI();
        InteractionUI.Instance?.ShowStatus($"Su Yu Diary Fragment ({CollectedCount}/{TotalFragments})");
    }

    private void RefreshUI()
    {
        if (progressText != null)
            progressText.text = $"{CollectedCount} / {TotalFragments}";

        for (int i = 0; i < fragmentIcons.Length; i++)
        {
            if (fragmentIcons[i] != null)
                fragmentIcons[i].color = collectedFragments.Contains(i + 1) ? collectedColor : missingColor;
        }

        if (completedObject != null)
            completedObject.SetActive(IsCompleted);
    }
}
