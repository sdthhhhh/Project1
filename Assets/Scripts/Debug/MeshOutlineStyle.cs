using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Mesh outline with:
/// 1) silhouette via face-slab extrusion + edge fins + corner patches (seals hard edges)
/// 2) sharp/crease tubes with corner plugs for structural read
/// Does not use ScreenSpaceOutlines.
/// </summary>
[DisallowMultipleComponent]
public sealed class MeshOutlineStyle : MonoBehaviour
{
    public enum OutlineTone
    {
        Black,
        White,
        Red
    }

    [SerializeField] private OutlineTone tone = OutlineTone.White;
    [SerializeField, Tooltip("If on, outline/crease widths scale with each mesh's size.")]
    private bool scaleWidthToBounds = true;
    [SerializeField, Range(0.005f, 0.08f), Tooltip("Outline width as a fraction of mesh size (when scaled).")]
    private float outlineWidthFactor = 0.015f;
    [SerializeField, Range(0.002f, 0.05f), Tooltip("Crease width as a fraction of mesh size (when scaled).")]
    private float creaseWidthFactor = 0.01f;
    [SerializeField, Range(0.005f, 0.12f), Tooltip("Absolute LOCAL width used when Scale Width To Bounds is off.")]
    private float outlineWidth = 0.02f;
    [SerializeField] private bool drawSilhouette = true;
    [SerializeField] private bool drawHardEdges = true;
    [SerializeField, Range(20f, 90f)] private float hardEdgeAngleDegrees = 60f;
    [SerializeField, Range(0.005f, 0.08f)] private float creaseWidth = 0.015f;
    [SerializeField, Range(0.05f, 0.25f), Tooltip("Outline cannot exceed this fraction of the thinnest bounds axis.")]
    private float maxRelativeToMinAxis = 0.12f;
    [SerializeField] private Color bodyColor = new Color(0.09f, 0.09f, 0.1f, 1f);
    [SerializeField] private bool buildOnAwake = false;

    private static readonly Color ToneBlack = new Color(0.05f, 0.05f, 0.055f, 1f);
    private static readonly Color ToneWhite = new Color(0.93f, 0.94f, 0.96f, 1f);
    private static readonly Color ToneRed = new Color(0.9f, 0.2f, 0.16f, 1f);

    private GameObject shell;
    private GameObject creases;
    private Material bodyMat;
    private Material shellMat;
    private Material creaseMat;
    private Mesh creaseMesh;
    private Mesh shellMesh;

    public OutlineTone Tone
    {
        get => tone;
        set
        {
            tone = value;
            ApplyColors();
        }
    }

    public void Configure(
        OutlineTone newTone,
        float newOutlineWidthFactor,
        Color newBodyColor,
        bool hardEdges = true,
        float newCreaseWidthFactor = 0.01f,
        float newHardEdgeAngleDegrees = 60f)
    {
        tone = newTone;
        scaleWidthToBounds = true;
        outlineWidthFactor = Mathf.Clamp(newOutlineWidthFactor, 0.005f, 0.08f);
        bodyColor = newBodyColor;
        drawHardEdges = hardEdges;
        creaseWidthFactor = Mathf.Clamp(newCreaseWidthFactor, 0.002f, 0.05f);
        hardEdgeAngleDegrees = Mathf.Clamp(newHardEdgeAngleDegrees, 20f, 90f);
    }

    private static float CharacteristicSize(Vector3 size)
    {
        // Average axis — less dominated by one long side.
        return (size.x + size.y + size.z) / 3f;
    }

    private static float MinAxis(Vector3 size)
    {
        return Mathf.Max(1e-6f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
    }

    /// <summary>
    /// Local-space outline width for the extrusion shader (scales with the mesh itself).
    /// </summary>
    private float ResolveLocalOutlineWidth(Mesh sourceMesh)
    {
        Vector3 size = sourceMesh.bounds.size;
        float avg = Mathf.Max(1e-6f, CharacteristicSize(size));
        float minAxis = MinAxis(size);

        if (!scaleWidthToBounds)
            return Mathf.Min(outlineWidth, minAxis * maxRelativeToMinAxis);

        float raw = avg * outlineWidthFactor;
        float cap = minAxis * maxRelativeToMinAxis;
        return Mathf.Min(raw, cap);
    }

    private float ResolveLocalCreaseWidth(Mesh sourceMesh)
    {
        Vector3 size = sourceMesh.bounds.size;
        float avg = Mathf.Max(1e-6f, CharacteristicSize(size));
        float minAxis = MinAxis(size);

        if (!scaleWidthToBounds)
            return Mathf.Min(creaseWidth, minAxis * maxRelativeToMinAxis * 0.75f);

        float raw = avg * creaseWidthFactor;
        float cap = minAxis * maxRelativeToMinAxis * 0.75f;
        return Mathf.Min(raw, cap);
    }

    [ContextMenu("Rebuild Outline")]
    public void RebuildFromMenu()
    {
        Rebuild();
    }

    private void Awake()
    {
        if (buildOnAwake)
            Rebuild();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        // Drop tracked refs first.
        SafeDestroy(shell);
        SafeDestroy(creases);
        SafeDestroy(bodyMat);
        SafeDestroy(shellMat);
        SafeDestroy(creaseMat);
        SafeDestroy(creaseMesh);
        SafeDestroy(shellMesh);
        shell = null;
        creases = null;
        bodyMat = null;
        shellMat = null;
        creaseMat = null;
        creaseMesh = null;
        shellMesh = null;

        // Editor Rebuild used to leave orphans (Destroy is delayed). Sweep by name.
        PurgeGeneratedChildren(transform);
    }

    private static void PurgeGeneratedChildren(Transform root)
    {
        if (root == null) return;

        // Copy list because we destroy while iterating.
        var toDelete = new List<GameObject>();
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null) continue;
            string n = child.name;
            if (n == "OutlineShell" || n == "OutlineCreases")
                toDelete.Add(child.gameObject);
        }

        for (int i = 0; i < toDelete.Count; i++)
            SafeDestroy(toDelete[i]);
    }

    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(obj);
            return;
        }
#endif
        Object.Destroy(obj);
    }

    public void Rebuild()
    {
        // Never build on the generated helper objects themselves.
        if (gameObject.name == "OutlineShell" || gameObject.name == "OutlineCreases")
            return;

        MeshFilter sourceFilter = GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer = GetComponent<Renderer>() as MeshRenderer;
        if (sourceFilter == null || sourceFilter.sharedMesh == null || sourceRenderer == null)
        {
            Debug.LogWarning("MeshOutlineStyle needs MeshFilter + MeshRenderer.", this);
            return;
        }

        Shader bodyShader = Shader.Find("Custom/URP/OutlineBody");
        Shader shellShader = Shader.Find("Custom/URP/OutlineShell");
        if (bodyShader == null || shellShader == null)
        {
            Debug.LogError("MeshOutlineStyle: outline shaders missing.", this);
            return;
        }

        Cleanup();

        bodyMat = new Material(bodyShader);
        sourceRenderer.sharedMaterial = bodyMat;

        Mesh sourceMesh = sourceFilter.sharedMesh;
        float localOutline = ResolveLocalOutlineWidth(sourceMesh);
        float localCrease = ResolveLocalCreaseWidth(sourceMesh);

        if (drawSilhouette)
        {
            // Face slabs + edge fins + corner patches — seals dihedral gaps hard normals leave.
            shellMesh = BuildSealedOutlineShell(sourceMesh, localOutline);
            if (shellMesh != null)
            {
                shellMat = new Material(shellShader);
                shellMat.SetFloat("_OutlineWidth", 0f); // already extruded in mesh

                shell = new GameObject("OutlineShell");
                shell.transform.SetParent(transform, false);
                shell.transform.localPosition = Vector3.zero;
                shell.transform.localRotation = Quaternion.identity;
                shell.transform.localScale = Vector3.one;
                shell.layer = gameObject.layer;

                MeshFilter shellFilter = shell.AddComponent<MeshFilter>();
                shellFilter.sharedMesh = shellMesh;

                MeshRenderer shellRenderer = shell.AddComponent<MeshRenderer>();
                shellRenderer.sharedMaterial = shellMat;
                shellRenderer.shadowCastingMode = ShadowCastingMode.Off;
                shellRenderer.receiveShadows = false;
            }
        }

        if (drawHardEdges)
        {
            creaseMat = new Material(shellShader);
            creaseMat.renderQueue = (int)RenderQueue.Geometry + 30;
            // Crease mesh is in local space; disable world extrusion on this material.
            creaseMat.SetFloat("_OutlineWidth", 0f);
            creaseMesh = BuildCreaseMesh(sourceMesh, hardEdgeAngleDegrees, localCrease);
            if (creaseMesh != null)
            {
                creases = new GameObject("OutlineCreases");
                creases.transform.SetParent(transform, false);
                creases.transform.localPosition = Vector3.zero;
                creases.transform.localRotation = Quaternion.identity;
                creases.transform.localScale = Vector3.one;
                creases.layer = gameObject.layer;

                MeshFilter creaseFilter = creases.AddComponent<MeshFilter>();
                creaseFilter.sharedMesh = creaseMesh;

                MeshRenderer creaseRenderer = creases.AddComponent<MeshRenderer>();
                creaseRenderer.sharedMaterial = creaseMat;
                creaseRenderer.shadowCastingMode = ShadowCastingMode.Off;
                creaseRenderer.receiveShadows = false;
            }
        }

        ApplyColors(localOutline);
    }

    public void ApplyColors()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        float localOutline = filter != null && filter.sharedMesh != null
            ? ResolveLocalOutlineWidth(filter.sharedMesh)
            : outlineWidth;
        ApplyColors(localOutline);
    }

    private void ApplyColors(float localOutlineWidth)
    {
        Color outline = ResolveToneColor(tone);
        if (bodyMat != null)
            bodyMat.SetColor("_BaseColor", bodyColor);
        if (shellMat != null)
        {
            shellMat.SetColor("_OutlineColor", outline);
            shellMat.SetFloat("_OutlineWidth", 0f);
        }
        if (creaseMat != null)
        {
            creaseMat.SetColor("_OutlineColor", outline);
            creaseMat.SetFloat("_OutlineWidth", 0f);
        }
    }

    public static Color ResolveToneColor(OutlineTone t)
    {
        switch (t)
        {
            case OutlineTone.Black: return ToneBlack;
            case OutlineTone.Red: return ToneRed;
            default: return ToneWhite;
        }
    }

    /// <summary>
    /// Sealed silhouette shell:
    /// - each triangle becomes an outward face slab (faceNormal * width)
    /// - shared edges get a bridging fin between the two slabs
    /// - vertices with 3+ distinct face normals get a corner patch
    /// This fills the open V where hard-edged faces would otherwise separate.
    /// </summary>
    private static Mesh BuildSealedOutlineShell(Mesh source, float width)
    {
        if (source == null || width <= 1e-6f)
            return null;

        Vector3[] srcVerts = source.vertices;
        int[] tris = source.triangles;
        if (srcVerts == null || tris == null || tris.Length < 3)
            return null;

        int triCount = tris.Length / 3;
        var faceNormals = new Vector3[triCount];
        var edgeFaces = new Dictionary<EdgeKey, EdgeFacePair>(tris.Length);
        var normalsAtPos = new Dictionary<Vector3, List<Vector3>>(srcVerts.Length);

        for (int t = 0; t < tris.Length; t += 3)
        {
            int triIndex = t / 3;
            int i0 = tris[t];
            int i1 = tris[t + 1];
            int i2 = tris[t + 2];
            Vector3 v0 = srcVerts[i0];
            Vector3 v1 = srcVerts[i1];
            Vector3 v2 = srcVerts[i2];
            Vector3 fn = Vector3.Cross(v1 - v0, v2 - v0);
            if (fn.sqrMagnitude < 1e-12f)
            {
                faceNormals[triIndex] = Vector3.zero;
                continue;
            }

            fn.Normalize();
            faceNormals[triIndex] = fn;

            AccumulateFaceNormal(normalsAtPos, v0, fn);
            AccumulateFaceNormal(normalsAtPos, v1, fn);
            AccumulateFaceNormal(normalsAtPos, v2, fn);

            RegisterEdgeFace(edgeFaces, v0, v1, triIndex, fn);
            RegisterEdgeFace(edgeFaces, v1, v2, triIndex, fn);
            RegisterEdgeFace(edgeFaces, v2, v0, triIndex, fn);
        }

        // Estimate: slabs + fins + corners
        var verts = new List<Vector3>(srcVerts.Length * 3);
        var indices = new List<int>(tris.Length * 3);

        // 1) Face slabs — each triangle pushed out along its own normal.
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 fn = faceNormals[t / 3];
            if (fn.sqrMagnitude < 0.5f)
                continue;

            Vector3 o0 = srcVerts[tris[t]] + fn * width;
            Vector3 o1 = srcVerts[tris[t + 1]] + fn * width;
            Vector3 o2 = srcVerts[tris[t + 2]] + fn * width;
            int b = verts.Count;
            verts.Add(o0);
            verts.Add(o1);
            verts.Add(o2);
            indices.Add(b);
            indices.Add(b + 1);
            indices.Add(b + 2);
        }

        // 2) Edge fins — bridge the gap between neighboring face slabs.
        foreach (KeyValuePair<EdgeKey, EdgeFacePair> pair in edgeFaces)
        {
            EdgeFacePair faces = pair.Value;
            if (!faces.hasB)
                continue;

            // Skip nearly coplanar neighbors — fin would be a zero-area strip.
            if (Vector3.Dot(faces.normalA, faces.normalB) > 0.999f)
                continue;

            Vector3 a = pair.Key.a;
            Vector3 b = pair.Key.b;
            Vector3 aA = a + faces.normalA * width;
            Vector3 bA = b + faces.normalA * width;
            Vector3 aB = a + faces.normalB * width;
            Vector3 bB = b + faces.normalB * width;

            // Prefer outward winding for convex meshes (Cull Front shows backs from outside).
            Vector3 outward = (faces.normalA + faces.normalB).normalized;
            Vector3 cross = Vector3.Cross(bA - aA, aB - aA);
            int baseIndex = verts.Count;
            verts.Add(aA);
            verts.Add(bA);
            verts.Add(bB);
            verts.Add(aB);
            if (Vector3.Dot(cross, outward) >= 0f)
                AddQuad(indices, baseIndex + 0, baseIndex + 1, baseIndex + 2, baseIndex + 3);
            else
                AddQuad(indices, baseIndex + 0, baseIndex + 3, baseIndex + 2, baseIndex + 1);
        }

        // 3) Corner patches — fill the open polygon where 3+ face slabs meet.
        foreach (KeyValuePair<Vector3, List<Vector3>> pair in normalsAtPos)
        {
            List<Vector3> normals = pair.Value;
            if (normals.Count < 3)
                continue;

            Vector3 pos = pair.Key;
            Vector3 sumN = Vector3.zero;
            for (int i = 0; i < normals.Count; i++)
                sumN += normals[i];
            Vector3 outward = sumN.sqrMagnitude > 1e-8f ? sumN.normalized : normals[0];

            // Order offset points around the outward axis so the fan doesn't fold.
            Vector3 tangent = Vector3.Cross(outward, Mathf.Abs(outward.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 bitangent = Vector3.Cross(outward, tangent);
            var ordered = new List<Vector3>(normals.Count);
            var angles = new List<float>(normals.Count);
            for (int i = 0; i < normals.Count; i++)
            {
                Vector3 p = pos + normals[i] * width;
                Vector3 d = p - (pos + outward * width);
                float ang = Mathf.Atan2(Vector3.Dot(d, bitangent), Vector3.Dot(d, tangent));
                ordered.Add(p);
                angles.Add(ang);
            }

            for (int i = 0; i < ordered.Count - 1; i++)
            {
                int min = i;
                for (int j = i + 1; j < ordered.Count; j++)
                {
                    if (angles[j] < angles[min])
                        min = j;
                }

                if (min != i)
                {
                    float tmpA = angles[i];
                    angles[i] = angles[min];
                    angles[min] = tmpA;
                    Vector3 tmpP = ordered[i];
                    ordered[i] = ordered[min];
                    ordered[min] = tmpP;
                }
            }

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < ordered.Count; i++)
                centroid += ordered[i];
            centroid /= ordered.Count;

            int cIndex = verts.Count;
            verts.Add(centroid);
            for (int i = 0; i < ordered.Count; i++)
            {
                Vector3 p0 = ordered[i];
                Vector3 p1 = ordered[(i + 1) % ordered.Count];
                Vector3 cross = Vector3.Cross(p0 - centroid, p1 - centroid);
                int b0 = verts.Count;
                verts.Add(p0);
                verts.Add(p1);
                if (Vector3.Dot(cross, outward) >= 0f)
                {
                    indices.Add(cIndex);
                    indices.Add(b0);
                    indices.Add(b0 + 1);
                }
                else
                {
                    indices.Add(cIndex);
                    indices.Add(b0 + 1);
                    indices.Add(b0);
                }
            }
        }

        if (indices.Count == 0)
            return null;

        var mesh = new Mesh { name = "OutlineShellSealed" };
        if (verts.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static void AccumulateFaceNormal(
        Dictionary<Vector3, List<Vector3>> map,
        Vector3 position,
        Vector3 faceNormal)
    {
        Vector3 key = QuantizePos(position);
        if (!map.TryGetValue(key, out List<Vector3> list))
        {
            list = new List<Vector3>(4);
            map[key] = list;
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (Vector3.Dot(list[i], faceNormal) > 0.999f)
                return;
        }

        list.Add(faceNormal);
    }

    private static void RegisterEdgeFace(
        Dictionary<EdgeKey, EdgeFacePair> map,
        Vector3 a,
        Vector3 b,
        int triIndex,
        Vector3 faceNormal)
    {
        EdgeKey key = EdgeKey.Create(a, b);
        if (!map.TryGetValue(key, out EdgeFacePair faces))
        {
            map[key] = new EdgeFacePair(triIndex, faceNormal);
            return;
        }

        if (!faces.hasB && faces.triA != triIndex)
        {
            faces.hasB = true;
            faces.triB = triIndex;
            faces.normalB = faceNormal;
            map[key] = faces;
        }
    }

    private static Vector3 QuantizePos(Vector3 v)
    {
        const float s = 10000f;
        return new Vector3(
            Mathf.Round(v.x * s) / s,
            Mathf.Round(v.y * s) / s,
            Mathf.Round(v.z * s) / s);
    }

    private static Mesh BuildCreaseMesh(Mesh source, float angleDegrees, float width)
    {
        Vector3[] srcVerts = source.vertices;
        int[] tris = source.triangles;
        if (srcVerts == null || tris == null || tris.Length < 3)
            return null;

        // edgeKey -> accumulated face normals (1 or 2)
        var edgeNormals = new Dictionary<EdgeKey, EdgeFaces>(tris.Length);

        for (int t = 0; t < tris.Length; t += 3)
        {
            int i0 = tris[t];
            int i1 = tris[t + 1];
            int i2 = tris[t + 2];

            Vector3 v0 = srcVerts[i0];
            Vector3 v1 = srcVerts[i1];
            Vector3 v2 = srcVerts[i2];
            Vector3 faceNormal = Vector3.Cross(v1 - v0, v2 - v0);
            if (faceNormal.sqrMagnitude < 1e-10f)
                continue;
            faceNormal.Normalize();

            AddEdge(edgeNormals, v0, v1, faceNormal);
            AddEdge(edgeNormals, v1, v2, faceNormal);
            AddEdge(edgeNormals, v2, v0, faceNormal);
        }

        float cosThreshold = Mathf.Cos(angleDegrees * Mathf.Deg2Rad);
        var segments = new List<CreaseSegment>(64);

        foreach (KeyValuePair<EdgeKey, EdgeFaces> pair in edgeNormals)
        {
            EdgeFaces faces = pair.Value;
            bool isHard;
            Vector3 outward;

            if (!faces.hasB)
            {
                // Boundary edge.
                isHard = true;
                outward = faces.normalA;
            }
            else
            {
                float dot = Vector3.Dot(faces.normalA, faces.normalB);
                isHard = dot < cosThreshold;
                outward = (faces.normalA + faces.normalB).normalized;
                if (outward.sqrMagnitude < 0.5f)
                    outward = faces.normalA;
            }

            if (!isHard)
                continue;

            segments.Add(new CreaseSegment(pair.Key.a, pair.Key.b, outward));
        }

        if (segments.Count == 0)
            return null;

        var verts = new List<Vector3>(segments.Count * 8 + 64);
        var indices = new List<int>(segments.Count * 24 + 128);
        float half = width * 0.5f;
        // Overlap neighboring tubes at joints so open box ends don't leave holes.
        float extend = half * 1.15f;

        var cornerOutward = new Dictionary<Vector3, Vector3>(segments.Count * 2);
        var cornerCount = new Dictionary<Vector3, int>(segments.Count * 2);

        for (int s = 0; s < segments.Count; s++)
        {
            CreaseSegment seg = segments[s];
            Vector3 a = seg.a + seg.outward * (width * 0.35f);
            Vector3 b = seg.b + seg.outward * (width * 0.35f);
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 1e-5f)
                continue;
            dir /= len;

            // Stretch past endpoints so tubes swallow corner gaps.
            a -= dir * extend;
            b += dir * extend;

            Vector3 side = Vector3.Cross(dir, seg.outward);
            if (side.sqrMagnitude < 1e-6f)
                side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 1e-6f)
                side = Vector3.Cross(dir, Vector3.right);
            side.Normalize();

            Vector3 n = Vector3.Cross(side, dir).normalized;

            Vector3 a0 = a + (-side - n) * half;
            Vector3 a1 = a + (side - n) * half;
            Vector3 a2 = a + (side + n) * half;
            Vector3 a3 = a + (-side + n) * half;
            Vector3 b0 = b + (-side - n) * half;
            Vector3 b1 = b + (side - n) * half;
            Vector3 b2 = b + (side + n) * half;
            Vector3 b3 = b + (-side + n) * half;

            int baseIndex = verts.Count;
            verts.Add(a0); verts.Add(a1); verts.Add(a2); verts.Add(a3);
            verts.Add(b0); verts.Add(b1); verts.Add(b2); verts.Add(b3);

            AddQuad(indices, baseIndex + 0, baseIndex + 1, baseIndex + 5, baseIndex + 4);
            AddQuad(indices, baseIndex + 1, baseIndex + 2, baseIndex + 6, baseIndex + 5);
            AddQuad(indices, baseIndex + 2, baseIndex + 3, baseIndex + 7, baseIndex + 6);
            AddQuad(indices, baseIndex + 3, baseIndex + 0, baseIndex + 4, baseIndex + 7);
            // End caps close the tube mouths at joints.
            AddQuad(indices, baseIndex + 0, baseIndex + 3, baseIndex + 2, baseIndex + 1);
            AddQuad(indices, baseIndex + 4, baseIndex + 5, baseIndex + 6, baseIndex + 7);

            AccumulateCorner(cornerOutward, cornerCount, QuantizePos(seg.a), seg.outward);
            AccumulateCorner(cornerOutward, cornerCount, QuantizePos(seg.b), seg.outward);
        }

        // Corner plugs — fill residual wedges where several creases meet.
        foreach (KeyValuePair<Vector3, int> pair in cornerCount)
        {
            if (pair.Value < 2)
                continue;

            Vector3 outward = cornerOutward[pair.Key];
            if (outward.sqrMagnitude > 1e-8f)
                outward.Normalize();
            else
                outward = Vector3.up;

            Vector3 center = pair.Key + outward * (width * 0.35f);
            AddCornerBox(verts, indices, center, half * 1.25f);
        }

        if (indices.Count == 0)
            return null;

        var mesh = new Mesh { name = "OutlineCreases" };
        if (verts.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static void AccumulateCorner(
        Dictionary<Vector3, Vector3> outwardSum,
        Dictionary<Vector3, int> counts,
        Vector3 key,
        Vector3 outward)
    {
        if (outwardSum.TryGetValue(key, out Vector3 sum))
            outwardSum[key] = sum + outward;
        else
            outwardSum[key] = outward;

        if (counts.TryGetValue(key, out int c))
            counts[key] = c + 1;
        else
            counts[key] = 1;
    }

    private static void AddCornerBox(List<Vector3> verts, List<int> indices, Vector3 center, float half)
    {
        int b = verts.Count;
        verts.Add(center + new Vector3(-half, -half, -half));
        verts.Add(center + new Vector3(half, -half, -half));
        verts.Add(center + new Vector3(half, half, -half));
        verts.Add(center + new Vector3(-half, half, -half));
        verts.Add(center + new Vector3(-half, -half, half));
        verts.Add(center + new Vector3(half, -half, half));
        verts.Add(center + new Vector3(half, half, half));
        verts.Add(center + new Vector3(-half, half, half));

        AddQuad(indices, b + 0, b + 1, b + 2, b + 3); // -Z
        AddQuad(indices, b + 5, b + 4, b + 7, b + 6); // +Z
        AddQuad(indices, b + 4, b + 0, b + 3, b + 7); // -X
        AddQuad(indices, b + 1, b + 5, b + 6, b + 2); // +X
        AddQuad(indices, b + 3, b + 2, b + 6, b + 7); // +Y
        AddQuad(indices, b + 4, b + 5, b + 1, b + 0); // -Y
    }

    private static void AddQuad(List<int> indices, int i0, int i1, int i2, int i3)
    {
        indices.Add(i0);
        indices.Add(i1);
        indices.Add(i2);
        indices.Add(i0);
        indices.Add(i2);
        indices.Add(i3);
    }

    private static void AddEdge(
        Dictionary<EdgeKey, EdgeFaces> map,
        Vector3 a,
        Vector3 b,
        Vector3 faceNormal)
    {
        EdgeKey key = EdgeKey.Create(a, b);
        if (!map.TryGetValue(key, out EdgeFaces faces))
        {
            map[key] = new EdgeFaces(faceNormal);
            return;
        }

        if (!faces.hasB)
        {
            faces.hasB = true;
            faces.normalB = faceNormal;
            map[key] = faces;
        }
    }

    private struct EdgeKey
    {
        public Vector3 a;
        public Vector3 b;

        public static EdgeKey Create(Vector3 p0, Vector3 p1)
        {
            // Quantize to avoid float noise splitting the same edge.
            Vector3 q0 = Quantize(p0);
            Vector3 q1 = Quantize(p1);
            if (q0.x < q1.x || (Mathf.Approximately(q0.x, q1.x) && q0.y < q1.y) ||
                (Mathf.Approximately(q0.x, q1.x) && Mathf.Approximately(q0.y, q1.y) && q0.z < q1.z))
                return new EdgeKey { a = q0, b = q1 };
            return new EdgeKey { a = q1, b = q0 };
        }

        private static Vector3 Quantize(Vector3 v)
        {
            const float s = 10000f;
            return new Vector3(
                Mathf.Round(v.x * s) / s,
                Mathf.Round(v.y * s) / s,
                Mathf.Round(v.z * s) / s);
        }

        public override int GetHashCode()
        {
            return a.GetHashCode() * 73856093 ^ b.GetHashCode() * 19349663;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is EdgeKey other)) return false;
            return a == other.a && b == other.b;
        }
    }

    private struct EdgeFacePair
    {
        public int triA;
        public int triB;
        public Vector3 normalA;
        public Vector3 normalB;
        public bool hasB;

        public EdgeFacePair(int tri, Vector3 normal)
        {
            triA = tri;
            triB = -1;
            normalA = normal;
            normalB = Vector3.zero;
            hasB = false;
        }
    }

    private struct EdgeFaces
    {
        public Vector3 normalA;
        public Vector3 normalB;
        public bool hasB;

        public EdgeFaces(Vector3 normal)
        {
            normalA = normal;
            normalB = Vector3.zero;
            hasB = false;
        }
    }

    private readonly struct CreaseSegment
    {
        public readonly Vector3 a;
        public readonly Vector3 b;
        public readonly Vector3 outward;

        public CreaseSegment(Vector3 a, Vector3 b, Vector3 outward)
        {
            this.a = a;
            this.b = b;
            this.outward = outward;
        }
    }
}
