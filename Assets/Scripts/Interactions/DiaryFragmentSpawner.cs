using UnityEngine;

/// <summary>
/// Legacy random spawner — disabled.
/// Place piece1–piece4 in the scene and add <see cref="DiaryFragment"/> instead.
/// </summary>
[System.Obsolete("Diary fragments are placed manually in the scene. Do not use random spawn.")]
public sealed class DiaryFragmentSpawner : MonoBehaviour
{
    private void Awake()
    {
        Debug.LogWarning(
            "DiaryFragmentSpawner is obsolete. Remove this component; use manually placed DiaryFragment pieces.",
            this);
        enabled = false;
    }
}
