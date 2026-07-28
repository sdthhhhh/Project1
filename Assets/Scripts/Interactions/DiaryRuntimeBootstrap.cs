using UnityEngine;

/// <summary>
/// Minimal runtime fallback for diary pieces that are not serialized in the scene.
/// Only ensures DiaryManager + BedroomFragmentSpawnArea/Spawner exist; does not rewrite UI.
/// </summary>
public static class DiaryRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureFragmentSpawnSupport()
    {
        if (Object.FindObjectOfType<DiaryManager>() == null)
            new GameObject("DiaryManager").AddComponent<DiaryManager>();

        GameObject bedroom = GameObject.Find("Bedroom");
        if (bedroom == null) return;

        GameObject area = GameObject.Find("BedroomFragmentSpawnArea");
        if (area == null)
        {
            area = new GameObject("BedroomFragmentSpawnArea");
            area.transform.SetParent(bedroom.transform, false);
            Bounds bounds = CalculateBounds(bedroom);
            area.transform.position = bounds.center;
            BoxCollider box = area.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(
                Mathf.Max(bounds.size.x * .9f, 3f),
                Mathf.Max(bounds.size.y, 2.5f),
                Mathf.Max(bounds.size.z * .9f, 3f));
        }

        if (area.GetComponent<BoxCollider>() == null)
        {
            BoxCollider box = area.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(5f, 3f, 5f);
        }

        if (area.GetComponent<DiaryFragmentSpawner>() == null)
            area.AddComponent<DiaryFragmentSpawner>();
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, new Vector3(5f, 3f, 5f));
        Bounds result = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
        return result;
    }
}
