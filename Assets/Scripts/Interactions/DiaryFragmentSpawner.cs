using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class DiaryFragmentSpawner : MonoBehaviour
{
    [Header("Spawn Area")]
    [SerializeField, Tooltip("Trigger collider defining the random area.")] private BoxCollider spawnArea;
    [SerializeField, Tooltip("Layers accepted as valid surfaces.")] private LayerMask validSurfaceMask = ~0;
    [SerializeField, Tooltip("Layers fragments may not overlap.")] private LayerMask obstacleMask;
    [Header("Placement Rules")]
    [SerializeField, Min(.1f), Tooltip("Minimum distance between fragments.")] private float minimumFragmentDistance = .5f;
    [SerializeField, Min(1), Tooltip("Maximum attempts per fragment.")] private int maxSpawnAttempts = 50;
    [SerializeField, Min(.2f), Tooltip("Extra ray height above the box.")] private float raycastHeight = 3f;
    private readonly List<Vector3> usedPositions = new List<Vector3>();

    private void Reset() { spawnArea = GetComponent<BoxCollider>(); spawnArea.isTrigger = true; }
    private void Start()
    {
        if (spawnArea == null) spawnArea = GetComponent<BoxCollider>();
        if (spawnArea == null) { Debug.LogError("DiaryFragmentSpawner: BoxCollider is missing."); return; }
        spawnArea.isTrigger = true;
        if (DiaryManager.Instance == null) new GameObject("DiaryManager").AddComponent<DiaryManager>();
        for (int id = 1; id <= 6; id++) Spawn(id);
    }
    private void Spawn(int id)
    {
        Bounds b = spawnArea.bounds;
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            float originY = Mathf.Min(b.max.y - .1f, b.center.y + Mathf.Min(b.extents.y * .35f, 1.5f));
            Vector3 origin = new Vector3(Random.Range(b.min.x, b.max.x), originY, Random.Range(b.min.z, b.max.z));
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, b.size.y + raycastHeight, validSurfaceMask, QueryTriggerInteraction.Ignore)) continue;
            if (hit.point.y < b.min.y || hit.point.y > b.max.y) continue;
            Vector3 point = hit.point + hit.normal * .012f;
            if (usedPositions.Exists(p => Vector3.Distance(p, point) < minimumFragmentDistance)) continue;
            if (Physics.CheckBox(point + Vector3.up * .02f, new Vector3(.1f, .025f, .135f), Quaternion.identity, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
            CreateWhitebox(id, point, hit.normal); usedPositions.Add(point); return;
        }
        Debug.LogError($"DiaryFragmentSpawner: Could not place DiaryFragment{id:00}. Check layer masks and area size.");
    }
    private void CreateWhitebox(int id, Vector3 point, Vector3 normal)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = $"DiaryFragment{id:00}"; go.transform.SetParent(transform);
        go.transform.position = point; go.transform.localScale = new Vector3(.18f, .015f, .25f);
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader); mat.color = Color.HSVToRGB((id - 1) / 6f, .35f, .9f); go.GetComponent<Renderer>().material = mat;
        go.AddComponent<DiaryFragment>().Configure(id);
    }
}
