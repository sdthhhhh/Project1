using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Play Mode sealed outline builder — same Rebuild() as Tool Generate, throttled.
/// One coordinator avoids 770 coroutines racing at Play start.
/// Requires outline meshes to have Read/Write enabled (Tools → Mesh Outline).
/// </summary>
public sealed class MeshOutlinePlayBuilder : MonoBehaviour
{
    [SerializeField, Min(1)] private int buildsPerFrame = 3;
    [SerializeField, Min(0)] private int settleFrames = 30;

    private static MeshOutlinePlayBuilder instance;
    private static readonly List<MeshOutlineStyle> Pending = new List<MeshOutlineStyle>(512);
    private static readonly HashSet<int> PendingIds = new HashSet<int>();
    private static readonly HashSet<int> RetriedIds = new HashSet<int>();

    private int framesWaited;
    private int meshWaitFrames;
    private int rescanCooldown;
    private bool startedDrain;
    private bool meshDataValidated;

    public static int PendingCount => Pending.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying)
            return;

        EnsureInstance();
        EnqueueMissing();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        Pending.Clear();
        PendingIds.Clear();
        RetriedIds.Clear();
    }

    public static void Enqueue(MeshOutlineStyle style)
    {
        if (style == null || !Application.isPlaying)
            return;
        if (style.gameObject.name == "OutlineShell" || style.gameObject.name == "OutlineCreases")
            return;

        int id = style.GetInstanceID();
        if (!PendingIds.Add(id))
            return;

        Pending.Add(style);
        EnsureInstance();
    }

    public static void Cancel(MeshOutlineStyle style)
    {
        if (style == null)
            return;

        int id = style.GetInstanceID();
        if (!PendingIds.Remove(id))
            return;

        for (int i = Pending.Count - 1; i >= 0; i--)
        {
            if (Pending[i] == null || Pending[i].GetInstanceID() == id)
                Pending.RemoveAt(i);
        }
    }

    private static void EnqueueMissing()
    {
        var styles = Object.FindObjectsOfType<MeshOutlineStyle>(true);
        for (int i = 0; i < styles.Length; i++)
        {
            MeshOutlineStyle style = styles[i];
            if (style == null)
                continue;
            if (style.transform.Find("OutlineShell") != null)
                continue;
            Enqueue(style);
        }
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        var go = new GameObject("MeshOutlinePlayBuilder");
        Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<MeshOutlinePlayBuilder>();
    }

    private void Update()
    {
        if (!startedDrain)
        {
            framesWaited++;
            if (framesWaited < settleFrames)
                return;

            if (!meshDataValidated)
            {
                meshWaitFrames++;
                if (!SampleMeshesHaveCpuPositions())
                {
                    // Safety: after long wait, still try — readable meshes should pass quickly.
                    if (meshWaitFrames < 300)
                        return;
                }
                meshDataValidated = true;
                Debug.Log("MeshOutlinePlayBuilder: starting sealed builds (wait +" + meshWaitFrames +
                          ", queued " + Pending.Count + ").");
            }

            startedDrain = true;
            ResortByCameraDistance();
        }

        rescanCooldown--;
        if (Pending.Count == 0 || rescanCooldown <= 0)
        {
            rescanCooldown = 60;
            EnqueueMissing();
            if (Pending.Count > 0)
                ResortByCameraDistance();
        }

        if (Pending.Count == 0)
            return;

        int built = 0;
        while (Pending.Count > 0 && built < buildsPerFrame)
        {
            MeshOutlineStyle style = Pending[0];
            Pending.RemoveAt(0);
            if (style == null)
                continue;

            PendingIds.Remove(style.GetInstanceID());

            try
            {
                style.Rebuild();
                Transform shellTf = style.transform.Find("OutlineShell");
                MeshFilter shellMf = shellTf != null ? shellTf.GetComponent<MeshFilter>() : null;
                bool sealedOk = shellMf != null && shellMf.sharedMesh != null &&
                                shellMf.sharedMesh.name == "OutlineShellSealed";
                if (sealedOk)
                {
                    MeshOutlineStyle.NotePlaySealedBuilt();
                }
                else
                {
                    int id = style.GetInstanceID();
                    if (RetriedIds.Add(id))
                    {
                        Enqueue(style);
                    }
                    else
                    {
                        if (shellTf == null)
                            style.RebuildLightPlaceholder();
                        MeshOutlineStyle.NotePlayLightFallback();
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("MeshOutlinePlayBuilder failed on " + style.name + ": " + e.Message, style);
                try
                {
                    style.RebuildLightPlaceholder();
                    MeshOutlineStyle.NotePlayLightFallback();
                }
                catch
                {
                    // ignore
                }
            }

            built++;
        }
    }

    private static bool SampleMeshesHaveCpuPositions()
    {
        int checkedMeshes = 0;
        int withPositions = 0;
        for (int i = 0; i < Pending.Count && checkedMeshes < 12; i++)
        {
            MeshOutlineStyle style = Pending[i];
            if (style == null)
                continue;
            MeshFilter filter = style.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.vertexCount < 3)
                continue;

            checkedMeshes++;
            if (!mesh.isReadable)
                continue;

            var verts = new List<Vector3>(mesh.vertexCount);
            mesh.GetVertices(verts);
            if (verts.Count < 3)
                continue;

            float maxSqr = 0f;
            int limit = Mathf.Min(verts.Count, 64);
            for (int v = 0; v < limit; v++)
                maxSqr = Mathf.Max(maxSqr, verts[v].sqrMagnitude);

            if (maxSqr > 1e-12f)
                withPositions++;
        }

        return checkedMeshes > 0 && withPositions >= Mathf.Max(1, checkedMeshes - 1);
    }

    private static void ResortByCameraDistance()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 origin = cam.transform.position;
        Pending.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            float da = (a.transform.position - origin).sqrMagnitude;
            float db = (b.transform.position - origin).sqrMagnitude;
            return da.CompareTo(db);
        });
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
