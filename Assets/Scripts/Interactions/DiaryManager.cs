using System.Collections.Generic;
using UnityEngine;

public sealed class DiaryManager : MonoBehaviour
{
    public static DiaryManager Instance { get; private set; }

    [SerializeField, Min(1), Tooltip("How many world fragments must be collected before the desk puzzle unlocks.")]
    private int totalFragments = 4;

    public int CollectedCount => collected.Count;
    public int TotalFragments => totalFragments;
    public bool HasCollectedAllFragments => CollectedCount == TotalFragments;
    public bool PuzzleCompleted { get; private set; }
    private readonly HashSet<int> collected = new HashSet<int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool HasFragment(int fragmentId) => collected.Contains(fragmentId);

    public void CollectFragment(int fragmentId)
    {
        if (fragmentId < 1 || fragmentId > TotalFragments || !collected.Add(fragmentId))
            return;
        InteractionUI.Instance?.ShowStatus($"Diary Fragment Collected ({CollectedCount}/{TotalFragments})");
    }

    public void MarkPuzzleCompleted() => PuzzleCompleted = true;
}
