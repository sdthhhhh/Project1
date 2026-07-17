using UnityEngine;

public static class DiaryRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Object.FindObjectOfType<DiaryManager>() == null)
            new GameObject("DiaryManager").AddComponent<DiaryManager>();

        GameObject bedroom = GameObject.Find("Bedroom");
        if (bedroom == null) { Debug.LogError("Diary bootstrap: GameObject named Bedroom was not found."); return; }

        GameObject area = GameObject.Find("BedroomFragmentSpawnArea");
        if (area == null)
        {
            area = new GameObject("BedroomFragmentSpawnArea");
            area.transform.SetParent(bedroom.transform, false);
            Bounds bounds = CalculateBounds(bedroom);
            area.transform.position = bounds.center;
            BoxCollider box = area.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(Mathf.Max(bounds.size.x * .9f, 3f), Mathf.Max(bounds.size.y, 2.5f), Mathf.Max(bounds.size.z * .9f, 3f));
        }
        if (area.GetComponent<BoxCollider>() == null) { BoxCollider box = area.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(5, 3, 5); }
        if (area.GetComponent<DiaryFragmentSpawner>() == null) area.AddComponent<DiaryFragmentSpawner>();

        DiaryPuzzleManager puzzle = Object.FindObjectOfType<DiaryPuzzleManager>();
        if (puzzle == null) puzzle = new GameObject("DiaryPuzzleSystem").AddComponent<DiaryPuzzleManager>();

        GameObject desk = FindBedroomDesk(bedroom);
        if (desk == null) { Debug.LogError("Diary bootstrap: Could not find a Desk inside Bedroom."); return; }
        CreatePuzzleEntity(desk, puzzle);
    }

    private static GameObject FindBedroomDesk(GameObject bedroom)
    {
        foreach (Transform t in bedroom.GetComponentsInChildren<Transform>(true))
            if (t.name.ToLowerInvariant().Contains("desk")) return t.gameObject;
        GameObject globalDesk = GameObject.Find("Desk");
        return globalDesk;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, new Vector3(5, 3, 5));
        Bounds result = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
        return result;
    }

    private static void FitColliderToRenderers(GameObject root, BoxCollider collider)
    {
        Bounds world = CalculateBounds(root);
        collider.center = root.transform.InverseTransformPoint(world.center);
        Vector3 scale = root.transform.lossyScale;
        collider.size = new Vector3(world.size.x / Mathf.Max(Mathf.Abs(scale.x), .001f), world.size.y / Mathf.Max(Mathf.Abs(scale.y), .001f), world.size.z / Mathf.Max(Mathf.Abs(scale.z), .001f));
    }

    private static void CreatePuzzleEntity(GameObject desk, DiaryPuzzleManager puzzle)
    {
        GameObject existing = GameObject.Find("DiaryReconstructionBoard");
        if (existing != null)
        {
            BedroomDesk existingInteraction = existing.GetComponent<BedroomDesk>();
            if (existingInteraction == null) existingInteraction = existing.AddComponent<BedroomDesk>();
            existingInteraction.Configure(puzzle, true);
            return;
        }
        Debug.LogError("Diary bootstrap: DiaryReconstructionBoard is missing. Run Tools/Diary Whitebox/Install Board In Bedroom Shelf1.");
    }
}
