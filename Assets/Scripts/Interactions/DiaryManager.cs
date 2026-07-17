using System.Collections.Generic;
using UnityEngine;

public sealed class DiaryManager : MonoBehaviour
{
    public static DiaryManager Instance { get; private set; }
    public int CollectedCount => collected.Count;
    public int TotalFragments => 6;
    public bool HasCollectedAllFragments => CollectedCount == TotalFragments;
    public bool PuzzleCompleted { get; private set; }
    private readonly HashSet<int> collected = new HashSet<int>();

    private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
    public void CollectFragment(int fragmentId)
    {
        if (fragmentId < 1 || fragmentId > TotalFragments || !collected.Add(fragmentId)) return;
        InteractionUI.Instance?.ShowStatus($"Diary Fragment Collected ({CollectedCount}/{TotalFragments})");
    }
    public void MarkPuzzleCompleted() => PuzzleCompleted = true;
}
