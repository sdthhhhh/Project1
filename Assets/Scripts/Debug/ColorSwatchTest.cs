using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IntroScene test: black / white / red mesh outlines with silhouette + sharp structural edges.
/// </summary>
public sealed class ColorSwatchTest : MonoBehaviour
{
    [SerializeField] private float distance = 2.8f;
    [SerializeField] private float spacing = 1.45f;
    [SerializeField] private float size = 0.9f;
    [SerializeField, Range(0.005f, 0.08f)] private float outlineWidthFactor = 0.015f;
    [SerializeField, Range(0.005f, 0.05f)] private float creaseWidthFactor = 0.01f;
    [SerializeField] private Color bodyColor = new Color(0.07f, 0.075f, 0.08f, 1f);
    [SerializeField] private bool useStructuredMesh = true;

    private readonly GameObject[] spawned = new GameObject[3];

    private void Start()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.035f, 0.038f, 0.042f, 1f);
        }

        Vector3 origin = cam != null
            ? cam.transform.position + cam.transform.forward * distance + cam.transform.up * -0.1f
            : transform.position + Vector3.forward * distance;
        Vector3 right = cam != null ? cam.transform.right : Vector3.right;

        Spawn(0, "OutlineSwatch_Black", MeshOutlineStyle.OutlineTone.Black, origin - right * spacing);
        Spawn(1, "OutlineSwatch_White", MeshOutlineStyle.OutlineTone.White, origin);
        Spawn(2, "OutlineSwatch_Red", MeshOutlineStyle.OutlineTone.Red, origin + right * spacing);

        Debug.Log("ColorSwatchTest: silhouette + hard-edge structure outlines.");
    }

    private void Spawn(int index, string name, MeshOutlineStyle.OutlineTone tone, Vector3 position)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, true);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * size;
        go.transform.rotation = Quaternion.Euler(16f, 32f - index * 12f, 8f);

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = useStructuredMesh ? BuildBlockMesh() : GetBuiltinCube();

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        go.AddComponent<BoxCollider>();

        MeshOutlineStyle style = go.AddComponent<MeshOutlineStyle>();
        style.Configure(tone, outlineWidthFactor, bodyColor, true, creaseWidthFactor, 60f);
        style.Rebuild();

        spawned[index] = go;
    }

    private static Mesh GetBuiltinCube()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return mesh;
    }

    /// <summary>
    /// Stepped block — more internal hard edges so structure reads clearly.
    /// </summary>
    private static Mesh BuildBlockMesh()
    {
        // Two stacked boxes forming an L / step shape in local space.
        var verts = new List<Vector3>(48);
        var tris = new List<int>(72);

        AddBox(verts, tris, new Vector3(0f, -0.15f, 0f), new Vector3(1f, 0.45f, 0.7f));
        AddBox(verts, tris, new Vector3(-0.15f, 0.25f, 0.05f), new Vector3(0.55f, 0.35f, 0.55f));

        var mesh = new Mesh { name = "StructuredBlock" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static void AddBox(List<Vector3> verts, List<int> tris, Vector3 center, Vector3 size)
    {
        Vector3 h = size * 0.5f;
        Vector3[] corners =
        {
            center + new Vector3(-h.x, -h.y, -h.z),
            center + new Vector3( h.x, -h.y, -h.z),
            center + new Vector3( h.x, -h.y,  h.z),
            center + new Vector3(-h.x, -h.y,  h.z),
            center + new Vector3(-h.x,  h.y, -h.z),
            center + new Vector3( h.x,  h.y, -h.z),
            center + new Vector3( h.x,  h.y,  h.z),
            center + new Vector3(-h.x,  h.y,  h.z)
        };

        // 6 faces, each with unique verts so hard edges are detected.
        AddFace(verts, tris, corners[0], corners[1], corners[2], corners[3]); // bottom
        AddFace(verts, tris, corners[4], corners[7], corners[6], corners[5]); // top
        AddFace(verts, tris, corners[0], corners[4], corners[5], corners[1]); // back
        AddFace(verts, tris, corners[3], corners[2], corners[6], corners[7]); // front
        AddFace(verts, tris, corners[0], corners[3], corners[7], corners[4]); // left
        AddFace(verts, tris, corners[1], corners[5], corners[6], corners[2]); // right
    }

    private static void AddFace(
        List<Vector3> verts,
        List<int> tris,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        Vector3 v3)
    {
        int i = verts.Count;
        verts.Add(v0);
        verts.Add(v1);
        verts.Add(v2);
        verts.Add(v3);
        tris.Add(i);
        tris.Add(i + 1);
        tris.Add(i + 2);
        tris.Add(i);
        tris.Add(i + 2);
        tris.Add(i + 3);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < spawned.Length; i++)
        {
            if (spawned[i] != null)
                Destroy(spawned[i]);
        }
    }
}
