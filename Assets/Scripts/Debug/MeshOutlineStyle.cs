using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Mesh outline (component = who gets outlines; generated shells are Never saved into git).
/// Edit Tool Generate and Play Mode both use sealed Rebuild(); Play spreads builds across frames.
/// </summary>
[DisallowMultipleComponent]
public sealed class MeshOutlineStyle : MonoBehaviour
{
    public const string OutlinePipeline = "sealed-runtime-v2";

    private const int MaxSealedShellTris = 80000;

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
    [SerializeField, Range(0.05f, 0.25f), Tooltip("Outline cannot exceed this fraction of the cap axis (thickness, or average size for thin sheets).")]
    private float maxRelativeToMinAxis = 0.12f;
    [SerializeField, Range(0f, 0.02f), Tooltip("Minimum outline width in WORLD units so tiny/thin meshes stay visible. 0 = default 0.003.")]
    private float minWorldOutlineWidth = 0.003f;
    [SerializeField, Range(0f, 0.2f), Tooltip("If min/max bounds axis is below this, treat as a thin sheet (cards/photos): scale-inflate outline instead of sealed extrusion. 0 = default 0.12.")]
    private float thinSheetAspectThreshold = 0.12f;
    [SerializeField, Tooltip("Force thin-sheet inflate outline (photos/cards), ignoring aspect ratio.")]
    private bool forceThinSheetOutline;
    [SerializeField] private Color bodyColor = new Color(0.09f, 0.09f, 0.1f, 1f);
    [SerializeField, Tooltip("Rebuild outline shells when Play starts (generated helpers are DontSave).")]
    private bool buildOnAwake = true;
    [SerializeField] private Material[] cachedOriginalMaterials;

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
    private bool builtThisPlaySession;
    private bool shellUsesShaderExtrusion;

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

    /// <summary>Remove generated shells and comic body; restore cached originals when possible.</summary>
    public void ClearGenerated()
    {
        MeshRenderer sourceRenderer = GetComponent<Renderer>() as MeshRenderer;
        Cleanup();
        if (sourceRenderer != null)
            RestoreOriginalMaterials(sourceRenderer);
    }

    /// <summary>
    /// Keep OutlineShell/Creases visible forever (e.g. inspect puzzle clones).
    /// Clears internal refs and renames helpers so OnDestroy Cleanup will not delete them.
    /// Note: shell/creases fields are non-serialized — Instantiated clones often have the
    /// child GOs but null refs, so we resolve by name before renaming.
    /// </summary>
    public void DetachGeneratedHelpersKeepVisible()
    {
        MeshOutlinePlayBuilder.Cancel(this);

        if (shell == null)
        {
            Transform shellTf = transform.Find("OutlineShell");
            if (shellTf != null)
                shell = shellTf.gameObject;
        }

        if (creases == null)
        {
            Transform creaseTf = transform.Find("OutlineCreases");
            if (creaseTf != null)
                creases = creaseTf.gameObject;
        }

        if (shell != null)
        {
            shell.SetActive(true);
            MeshRenderer shellRenderer = shell.GetComponent<MeshRenderer>();
            if (shellRenderer != null)
                shellRenderer.enabled = true;
            shell.name = "OutlineShell_Detached";
            shell = null;
        }

        if (creases != null)
        {
            creases.SetActive(true);
            MeshRenderer creaseRenderer = creases.GetComponent<MeshRenderer>();
            if (creaseRenderer != null)
                creaseRenderer.enabled = true;
            creases.name = "OutlineCreases_Detached";
            creases = null;
        }

        // Drop ownership only — materials stay on the renderers.
        shellMesh = null;
        creaseMesh = null;
        bodyMat = null;
        shellMat = null;
        creaseMat = null;
        enabled = false;
    }

    private static float CharacteristicSize(Vector3 size)
    {
        return (size.x + size.y + size.z) / 3f;
    }

    private static float MinAxis(Vector3 size)
    {
        return Mathf.Max(1e-6f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
    }

    private static float MaxAxis(Vector3 size)
    {
        return Mathf.Max(1e-6f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
    }

    /// <summary>
    /// Thin sheets (cards, mirrors, posters): min/max aspect is tiny, so capping by thickness
    /// crushes outline width. Use average size as the cap axis instead.
    /// </summary>
    private float ResolveCapAxis(Vector3 size, float minAxis, float avg)
    {
        float maxAxis = MaxAxis(size);
        float aspect = minAxis / maxAxis;
        float threshold = thinSheetAspectThreshold > 1e-8f ? thinSheetAspectThreshold : 0.12f;
        if (aspect < threshold)
            return avg;
        return minAxis;
    }

    private bool IsThinSheet(Mesh sourceMesh)
    {
        if (forceThinSheetOutline)
            return true;
        if (sourceMesh == null)
            return false;
        Vector3 size = sourceMesh.bounds.size;
        float minAxis = MinAxis(size);
        float maxAxis = MaxAxis(size);
        float threshold = thinSheetAspectThreshold > 1e-8f ? thinSheetAspectThreshold : 0.12f;
        return (minAxis / maxAxis) < threshold;
    }

    /// <summary>Mark this object as a photo/card so Rebuild uses thin-sheet inflate outline.</summary>
    public void SetForceThinSheetOutline(bool enabled)
    {
        forceThinSheetOutline = enabled;
    }

    /// <summary>
    /// Uniform scale so Cull-Front backfaces form a visible rim (works face-on for cards/photos).
    /// </summary>
    private float ResolveThinSheetInflateScale(Mesh sourceMesh, float localOutline)
    {
        float maxAxis = MaxAxis(sourceMesh.bounds.size);
        float pad = Mathf.Max(localOutline, 1e-5f) * 2f;
        return 1f + pad / Mathf.Max(1e-6f, maxAxis);
    }

    private float ResolveLocalOutlineWidth(Mesh sourceMesh)
    {
        Vector3 size = sourceMesh.bounds.size;
        float avg = Mathf.Max(1e-6f, CharacteristicSize(size));
        float minAxis = MinAxis(size);
        float capAxis = ResolveCapAxis(size, minAxis, avg);

        float local;
        if (!scaleWidthToBounds)
            local = Mathf.Min(outlineWidth, capAxis * maxRelativeToMinAxis);
        else
        {
            float raw = avg * outlineWidthFactor;
            float cap = capAxis * maxRelativeToMinAxis;
            local = Mathf.Min(raw, cap);
        }

        return ApplyMinWorldWidth(local);
    }

    private float ResolveLocalCreaseWidth(Mesh sourceMesh)
    {
        Vector3 size = sourceMesh.bounds.size;
        float avg = Mathf.Max(1e-6f, CharacteristicSize(size));
        float minAxis = MinAxis(size);
        float capAxis = ResolveCapAxis(size, minAxis, avg);

        float local;
        if (!scaleWidthToBounds)
            local = Mathf.Min(creaseWidth, capAxis * maxRelativeToMinAxis * 0.75f);
        else
        {
            float raw = avg * creaseWidthFactor;
            float cap = capAxis * maxRelativeToMinAxis * 0.75f;
            local = Mathf.Min(raw, cap);
        }

        // Crease caps are already thinner; still apply the same world floor so fine lines stay visible.
        return ApplyMinWorldWidth(local);
    }

    /// <summary>
    /// Tiny/thin meshes get crushed by min-axis caps; floor width in world space so they stay visible.
    /// Missing/0 serialized value (old scene components) defaults to 0.003.
    /// </summary>
    private float ApplyMinWorldWidth(float localWidth)
    {
        float floorWorld = minWorldOutlineWidth > 1e-8f ? minWorldOutlineWidth : 0.003f;

        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Abs(ls.x);
        float sy = Mathf.Abs(ls.y);
        float sz = Mathf.Abs(ls.z);
        float scale = Mathf.Max(1e-6f, Mathf.Min(sx, Mathf.Min(sy, sz)));
        float minLocal = floorWorld / scale;
        return Mathf.Max(localWidth, minLocal);
    }

    [ContextMenu("Generate Outline")]
    public void RebuildFromMenu()
    {
        Rebuild();
    }

    [ContextMenu("Clear Generated Outline")]
    public void ClearGeneratedMenu()
    {
        ClearGenerated();
    }

    /// <summary>Diagnostics: sealed shells successfully created this Play session.</summary>
    public static int PlaySealedBuiltCount { get; private set; }
    /// <summary>Diagnostics: fell back to Cull-Front share-mesh this Play session.</summary>
    public static int PlayLightFallbackCount { get; private set; }

    public static void NotePlaySealedBuilt() { PlaySealedBuiltCount++; }
    public static void NotePlayLightFallback() { PlayLightFallbackCount++; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlayBuildStats()
    {
        PlaySealedBuiltCount = 0;
        PlayLightFallbackCount = 0;
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        Mesh sourceMesh = null;
        MeshFilter sourceFilter = GetComponent<MeshFilter>();
        if (sourceFilter != null)
            sourceMesh = sourceFilter.sharedMesh;

        // Never destroy the object's own mesh asset (light path shares it on OutlineShell).
        if (shell != null)
        {
            MeshFilter shellFilter = shell.GetComponent<MeshFilter>();
            if (shellFilter != null && shellFilter.sharedMesh != null && shellFilter.sharedMesh == sourceMesh)
                shellFilter.sharedMesh = null;
        }

        SafeDestroy(shell);
        SafeDestroy(creases);
        SafeDestroy(bodyMat);
        SafeDestroy(shellMat);
        SafeDestroy(creaseMat);
        SafeDestroy(creaseMesh);
        if (shellMesh != null && shellMesh != sourceMesh)
            SafeDestroy(shellMesh);
        shell = null;
        creases = null;
        bodyMat = null;
        shellMat = null;
        creaseMat = null;
        creaseMesh = null;
        shellMesh = null;
        shellUsesShaderExtrusion = false;
        PurgeGeneratedChildren(transform);
    }

    private static void PurgeGeneratedChildren(Transform root)
    {
        if (root == null) return;

        Mesh sourceMesh = null;
        MeshFilter rootFilter = root.GetComponent<MeshFilter>();
        if (rootFilter != null)
            sourceMesh = rootFilter.sharedMesh;

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
        {
            GameObject go = toDelete[i];
            if (go == null) continue;
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh == sourceMesh)
                mf.sharedMesh = null;
            SafeDestroy(go);
        }
    }

    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        // Immediate in Editor (Edit + Play): deferred Destroy can wipe a brand-new
        // OutlineShell/mesh created later in the same Rebuild() call / frame.
        // Do NOT pass allowDestroyingAssets=true — that can nuke shared mesh assets.
        Object.DestroyImmediate(obj);
        return;
#else
        Object.Destroy(obj);
#endif
    }

    private static bool LooksLikeOutlineBody(Material mat)
    {
        return mat != null && mat.shader != null && mat.shader.name.Contains("OutlineBody");
    }

    private bool HasCachedOriginals()
    {
        return cachedOriginalMaterials != null && cachedOriginalMaterials.Length > 0 && cachedOriginalMaterials[0] != null;
    }

    private void CacheOriginalMaterialsIfNeeded(MeshRenderer sourceRenderer)
    {
        if (HasCachedOriginals() || sourceRenderer == null)
            return;

        Material[] current = sourceRenderer.sharedMaterials;
        if (current == null || current.Length == 0)
            return;

        if (LooksLikeOutlineBody(current[0]))
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                GameObject prefab = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                if (prefab != null)
                {
                    MeshRenderer prefabMr = prefab.GetComponent<MeshRenderer>();
                    if (prefabMr != null && prefabMr.sharedMaterials != null && prefabMr.sharedMaterials.Length > 0
                        && !LooksLikeOutlineBody(prefabMr.sharedMaterials[0]))
                    {
                        cachedOriginalMaterials = (Material[])prefabMr.sharedMaterials.Clone();
                        return;
                    }
                }
            }
#endif
            return;
        }

        cachedOriginalMaterials = (Material[])current.Clone();
    }

    private void RestoreOriginalMaterials(MeshRenderer sourceRenderer)
    {
        if (sourceRenderer == null || !HasCachedOriginals())
            return;
        sourceRenderer.sharedMaterials = cachedOriginalMaterials;
    }

    /// <summary>
    /// Fast Cull-Front outline for Play-Mode streaming (far / pending objects).
    /// Near objects should call Rebuild() for sealed quality.
    /// </summary>
    public void RebuildLightPlaceholder()
    {
        if (gameObject.name == "OutlineShell" || gameObject.name == "OutlineCreases")
            return;

        MeshFilter sourceFilter = GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer = GetComponent<Renderer>() as MeshRenderer;
        if (sourceFilter == null || sourceFilter.sharedMesh == null || sourceRenderer == null)
            return;

        Shader bodyShader = Shader.Find("Custom/URP/OutlineBody");
        Shader shellShader = Shader.Find("Custom/URP/OutlineShell");
        if (bodyShader == null || shellShader == null)
            return;

        CacheOriginalMaterialsIfNeeded(sourceRenderer);
        Cleanup();

        bodyMat = new Material(bodyShader);
        sourceRenderer.sharedMaterial = bodyMat;

        Mesh sourceMesh = sourceFilter.sharedMesh;
        float localOutline = ResolveLocalOutlineWidth(sourceMesh);
        shellUsesShaderExtrusion = true;

        shellMat = new Material(shellShader);
        shellMat.SetFloat("_OutlineWidth", localOutline);
        MarkGenerated(shellMat);
        CreateShellObject(sourceMesh, shellMat);

        ApplyColors(localOutline);
        builtThisPlaySession = Application.isPlaying;
        SyncGeneratedVisibility();
    }

    public void Rebuild()
    {
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

        CacheOriginalMaterialsIfNeeded(sourceRenderer);
        Cleanup();

        // Comic body stays on the renderer (small). Only helper meshes/GOs are DontSave.
        bodyMat = new Material(bodyShader);
        sourceRenderer.sharedMaterial = bodyMat;

        Mesh sourceMesh = sourceFilter.sharedMesh;
        float localOutline = ResolveLocalOutlineWidth(sourceMesh);
        float localCrease = ResolveLocalCreaseWidth(sourceMesh);
        int triCount = CountMeshTriangles(sourceMesh);
        bool dense = triCount > MaxSealedShellTris;
        shellUsesShaderExtrusion = false;

        if (drawSilhouette)
        {
            if (dense)
            {
                // Ultra-dense meshes: Cull-Front share-mesh (avoid multi-GB sealed builds).
                shellMat = new Material(shellShader);
                shellMat.SetFloat("_OutlineWidth", localOutline);
                MarkGenerated(shellMat);
                shellUsesShaderExtrusion = true;
                CreateShellObject(sourceMesh, shellMat);
            }
            else if (IsThinSheet(sourceMesh))
            {
                // Cards / photos: sealed face extrusion is nearly invisible face-on.
                // Share mesh + uniform inflate + Cull Front → reliable rim.
                // Scale around mesh bounds center (not pivot) so off-center pivots stay aligned.
                shellMat = new Material(shellShader);
                shellMat.SetFloat("_OutlineWidth", 0f);
                MarkGenerated(shellMat);
                shellUsesShaderExtrusion = false;
                CreateShellObject(sourceMesh, shellMat);
                float inflate = ResolveThinSheetInflateScale(sourceMesh, localOutline);
                Vector3 center = sourceMesh.bounds.center;
                shell.transform.localScale = new Vector3(inflate, inflate, inflate);
                shell.transform.localPosition = center * (1f - inflate);
            }
            else
            {
                // Same path as Tool Generate: sealed face slabs + fins + corners.
                shellMesh = BuildSealedOutlineShell(sourceMesh, localOutline);
                if (shellMesh != null)
                {
                    MarkGenerated(shellMesh);
                    shellMat = new Material(shellShader);
                    shellMat.SetFloat("_OutlineWidth", 0f);
                    MarkGenerated(shellMat);
                    CreateShellObject(shellMesh, shellMat);
                }
                else
                {
                    shellMat = new Material(shellShader);
                    shellMat.SetFloat("_OutlineWidth", localOutline);
                    MarkGenerated(shellMat);
                    shellUsesShaderExtrusion = true;
                    CreateShellObject(sourceMesh, shellMat);
                }
            }
        }

        // Creases match the pre-oversize path (skip on dense — too heavy).
        if (drawHardEdges && !dense)
        {
            creaseMat = new Material(shellShader);
            creaseMat.renderQueue = (int)RenderQueue.Geometry + 30;
            creaseMat.SetFloat("_OutlineWidth", 0f);
            MarkGenerated(creaseMat);
            creaseMesh = BuildCreaseMesh(sourceMesh, hardEdgeAngleDegrees, localCrease);
            if (creaseMesh != null)
            {
                MarkGenerated(creaseMesh);
                creases = new GameObject("OutlineCreases");
                MarkGenerated(creases);
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
        builtThisPlaySession = Application.isPlaying;
        SyncGeneratedVisibility();
    }

    private void LateUpdate()
    {
        // Photos hide via MeshRenderer.enabled (GO stays active). Shells created later must follow.
        if (shell == null && creases == null)
            return;
        SyncGeneratedVisibility();
    }

    private void OnEnable()
    {
        SyncGeneratedVisibility();
    }

    /// <summary>
    /// Match OutlineShell / OutlineCreases to this object's body renderer + active state.
    /// Safe to call before helpers exist (no-op) and again right after Rebuild.
    /// </summary>
    public void SyncGeneratedVisibility()
    {
        MeshRenderer body = GetComponent<MeshRenderer>();
        bool want = isActiveAndEnabled && body != null && body.enabled;
        ApplyGeneratedVisibility(want);
    }

    private void ApplyGeneratedVisibility(bool want)
    {
        if (shell != null)
        {
            if (shell.activeSelf != want)
                shell.SetActive(want);
            MeshRenderer shellRenderer = shell.GetComponent<MeshRenderer>();
            if (shellRenderer != null && shellRenderer.enabled != want)
                shellRenderer.enabled = want;
        }

        if (creases != null)
        {
            if (creases.activeSelf != want)
                creases.SetActive(want);
            MeshRenderer creaseRenderer = creases.GetComponent<MeshRenderer>();
            if (creaseRenderer != null && creaseRenderer.enabled != want)
                creaseRenderer.enabled = want;
        }
    }

    private void CreateShellObject(Mesh mesh, Material mat)
    {
        shell = new GameObject("OutlineShell");
        MarkGenerated(shell);
        shell.transform.SetParent(transform, false);
        shell.transform.localPosition = Vector3.zero;
        shell.transform.localRotation = Quaternion.identity;
        shell.transform.localScale = Vector3.one;
        shell.layer = gameObject.layer;

        MeshFilter shellFilter = shell.AddComponent<MeshFilter>();
        shellFilter.sharedMesh = mesh;

        MeshRenderer shellRenderer = shell.AddComponent<MeshRenderer>();
        shellRenderer.sharedMaterial = mat;
        shellRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shellRenderer.receiveShadows = false;
    }

    private static void MarkGenerated(Object obj)
    {
        if (obj == null) return;
        // Edit-only: keep Tool preview out of the saved scene.
        // In Play Mode, DontSave / DontSaveInEditor will wipe runtime shells.
#if UNITY_EDITOR
        if (!Application.isPlaying)
            obj.hideFlags |= HideFlags.DontSave;
        else
            obj.hideFlags &= ~(HideFlags.DontSave | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild);
#endif
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
            shellMat.SetFloat("_OutlineWidth", shellUsesShaderExtrusion ? localOutlineWidth : 0f);
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
    /// </summary>
    private static int CountMeshTriangles(Mesh mesh)
    {
        if (mesh == null)
            return 0;

        var tris = new List<int>(64);
        mesh.GetTriangles(tris, 0);
        if (tris.Count >= 3)
            return tris.Count / 3;

        int[] legacy = mesh.triangles;
        return legacy != null ? legacy.Length / 3 : 0;
    }

    private static Mesh BuildSealedOutlineShell(Mesh source, float width)
    {
        if (source == null || width <= 1e-6f)
            return null;

        // Prefer Get* APIs — more reliable than .triangles/.vertices for some Play Mode meshes.
        var srcVertList = new List<Vector3>(source.vertexCount);
        source.GetVertices(srcVertList);
        var triList = new List<int>();
        source.GetTriangles(triList, 0);
        if (srcVertList.Count == 0 || triList.Count < 3)
            return null;

        Vector3[] srcVerts = srcVertList.ToArray();
        int[] tris = triList.ToArray();

        // Play Mode can briefly expose index buffers before vertex positions are ready
        // (all zeros) — that yields an empty sealed mesh and looks like a light fallback.
        float maxSqr = 0f;
        int probe = Mathf.Min(srcVerts.Length, 64);
        for (int i = 0; i < probe; i++)
            maxSqr = Mathf.Max(maxSqr, srcVerts[i].sqrMagnitude);
        if (maxSqr <= 1e-12f)
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
