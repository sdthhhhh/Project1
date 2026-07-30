using UnityEngine;

/// <summary>
/// Ensures a DiaryManager exists. World fragments are placed manually in the scene
/// (DiaryFragment on piece1–piece4); random spawn is no longer created.
/// </summary>
public static class DiaryRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureDiaryManager()
    {
        if (Object.FindObjectOfType<DiaryManager>() == null)
            new GameObject("DiaryManager").AddComponent<DiaryManager>();
    }
}
