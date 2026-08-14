using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Matches MovableItems 2-6 to PlacementSlots 2-6. Items are collected through
/// the shared inspection UI, then placed into the matching yellow outline with E.
/// </summary>
public sealed class ItemRestorationSystem : MonoBehaviour
{
    public static ItemRestorationSystem Instance { get; private set; }

    [Header("Reveal On Complete")]
    [SerializeField, Tooltip("Activated when all numbered books/items (2–6) are restored (e.g. 日记碎片3).")]
    private GameObject[] revealOnComplete;

    private readonly Dictionary<string, State> states = new Dictionary<string, State>();
    private Drawer rewardDrawer;
    private GameObject medicalReport;
    private bool completed;

    private sealed class State
    {
        public string id;
        public string displayName;
        public bool collected;
        public bool placed;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        GameObject oldInventory=GameObject.Find("RestorationInventory");if(oldInventory!=null)Destroy(oldInventory);
    }

    private IEnumerator Start()
    {
        yield return null;
        Setup();
    }

    private void Setup()
    {
        GameObject movableRoot = GameObject.Find("MovableItems");
        GameObject correctRoot = GameObject.Find("PlacementSlots");
        if (movableRoot == null || correctRoot == null)
        {
            Debug.LogError("Item restoration: MovableItems or PlacementSlots was not found. Check the hierarchy names and save the scene.");
            InteractionUI.Instance?.ShowStatus("Restoration setup error: folders not found");
            return;
        }

        Dictionary<string, Transform> movable = FindNumbered(movableRoot.transform);
        Dictionary<string, Transform> correct = FindNumbered(correctRoot.transform);
        for(int number=2;number<=6;number++)states[number.ToString()]=new State{id=number.ToString(),displayName="Item "+number};

        for (int number = 2; number <= 6; number++)
        {
            string id = number.ToString();
            State state = states[id];
            state.displayName = GetDisplayName(movable, id);

            if (!movable.TryGetValue(id, out Transform source) || !correct.TryGetValue(id, out Transform target))
            {
                Debug.LogError($"物品还原：编号 {id} 缺少初始物品或正确位置物品。");
                continue;
            }

            EnsureCollider(source.gameObject);
            InspectableObject sourceInspect=source.GetComponent<InspectableObject>();
            if(sourceInspect==null){sourceInspect=source.gameObject.AddComponent<InspectableObject>();sourceInspect.ConfigurePreview(source.gameObject,$"A misplaced object marked {id}.",Vector3.zero);}
            sourceInspect.SetCanInspect(true);
            RestorationInspectablePickup pickup=source.GetComponent<RestorationInspectablePickup>();if(pickup==null)pickup=source.gameObject.AddComponent<RestorationInspectablePickup>();pickup.Configure(id);

            // PlacementSlots contains the numbered target instances (yellow hint until placed).
            EnsureCollider(target.gameObject);
            InspectableObject targetInspect=target.GetComponent<InspectableObject>();if(targetInspect==null){targetInspect=target.gameObject.AddComponent<InspectableObject>();targetInspect.ConfigurePreview(target.gameObject,$"The restored position for item {id}.",Vector3.zero);}targetInspect.SetCanInspect(false);
            RestorationPlace place=target.GetComponent<RestorationPlace>();if(place==null)place=target.gameObject.AddComponent<RestorationPlace>();place.Configure(id);
        }

        rewardDrawer = FindDrawerBottom();
        medicalReport = FindSceneObject("Medical Report");
        if (medicalReport != null) medicalReport.SetActive(false);

        EnsureRevealOnCompleteDefaults();
        SetRevealOnCompleteActive(false);
    }

    /// <summary>Wire 日记碎片3 by name when the inspector list is empty (runtime-added component).</summary>
    private void EnsureRevealOnCompleteDefaults()
    {
        if (revealOnComplete != null && revealOnComplete.Length > 0)
            return;

        // Text diary page (not the cover puzzle piece DiaryFragment03).
        GameObject frag = FindSceneObject("日记碎片3");
        if (frag == null)
            frag = FindSceneObject("DiaryFragment03");
        if (frag != null)
            revealOnComplete = new[] { frag };
    }

    private void SetRevealOnCompleteActive(bool active)
    {
        if (revealOnComplete == null)
            return;
        for (int i = 0; i < revealOnComplete.Length; i++)
        {
            if (revealOnComplete[i] != null)
                revealOnComplete[i].SetActive(active);
        }
    }

    private static Dictionary<string, Transform> FindNumbered(Transform root)
    {
        var result = new Dictionary<string, Transform>();
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (!TryReadNumber(child.name, out int n) || n < 2 || n > 6)
                continue;

            string key = n.ToString();
            Transform visual = FindVisualObject(child, root);
            if (!result.TryGetValue(key, out Transform existing))
            {
                result.Add(key, visual);
                continue;
            }

            // Prefer active PlacementSlots instances over leftover inactive shelf copies.
            bool existingActive = existing != null && existing.gameObject.activeInHierarchy;
            bool candidateActive = visual != null && visual.gameObject.activeInHierarchy;
            if (!existingActive && candidateActive)
                result[key] = visual;
        }
        return result;
    }

    private static bool TryReadNumber(string objectName, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(objectName)) return false;
        int length = 0;
        while (length < objectName.Length && char.IsDigit(objectName[length])) length++;
        return length > 0 && int.TryParse(objectName.Substring(0, length), out number);
    }

    private static Transform FindVisualObject(Transform numbered, Transform folderRoot)
    {
        if (numbered.GetComponent<Renderer>() != null || numbered.GetComponentInChildren<Renderer>(true) != null)
            return numbered;
        Transform current = numbered.parent;
        while (current != null && current != folderRoot)
        {
            if (current.GetComponent<Renderer>() != null) return current;
            current = current.parent;
        }
        return numbered;
    }

    private static string GetDisplayName(Dictionary<string, Transform> items, string id)
    {
        if (!items.TryGetValue(id, out Transform item)) return "Item " + id;
        foreach (Transform child in item.GetComponentsInChildren<Transform>(true))
            if (child != item && !int.TryParse(child.name, out _)) return child.name;
        return "Item " + id;
    }

    public bool CanPlace(string id) => states.TryGetValue(id, out State s) && s.collected && !s.placed;

    public void Collect(string id, GameObject source)
    {
        if (!states.TryGetValue(id, out State state) || state.collected) return;
        state.collected = true;
        source.SetActive(false);
        InteractionUI.Instance?.ShowStatus(state.displayName+" collected");
    }

    public void Place(string id, GameObject target)
    {
        if (!CanPlace(id)) return;
        State state = states[id]; state.placed = true;
        RestorationPlace place = target.GetComponent<RestorationPlace>();
        if (place != null)
        {
            place.RevealPlaced();
            Destroy(place);
        }
        else
            SetVisible(target, true);
        InspectableObject inspectable = target.GetComponent<InspectableObject>();
        if (inspectable != null) inspectable.SetCanInspect(false);
        InteractionUI.Instance?.ShowStatus(state.displayName + " restored");
        CheckComplete();
    }

    private void CheckComplete()
    {
        if (completed) return;
        foreach (State state in states.Values) if (!state.placed) return;
        completed = true;
        if (medicalReport != null) medicalReport.SetActive(true);
        SetRevealOnCompleteActive(true);
        if (rewardDrawer != null) rewardDrawer.UnlockAndOpen();
        else InteractionUI.Instance?.ShowStatus("Drawer is open");
    }

    private static void SetVisible(GameObject root, bool visible)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
    }

    private static void EnsureCollider(GameObject obj)
    {
        if (obj.GetComponentInChildren<Collider>(true) == null) obj.AddComponent<BoxCollider>();
    }

    private static Drawer FindDrawerBottom()
    {
        GameObject drawer = GameObject.Find("DrawerBottom");
        return drawer != null ? drawer.GetComponentInParent<Drawer>() : null;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
            if (obj.scene.IsValid() && obj.name == objectName) return obj;
        return null;
    }
}

public sealed class RestorationInspectablePickup : MonoBehaviour,IInspectableCollectible
{
    [SerializeField]private string id;
    public void Configure(string value)=>id=value;
    public void CollectFromInspection()=>ItemRestorationSystem.Instance?.Collect(id,gameObject);
}

public sealed class RestorationPlace : MonoBehaviour
{
    private string id;
    private MeshOutlineStyle outlineStyle;
    private readonly List<Renderer> ghostRenderers = new List<Renderer>(8);
    private readonly List<Material[]> cachedMaterials = new List<Material[]>(8);
    private readonly List<bool> cachedRendererEnabled = new List<bool>(8);
    private readonly List<Material> runtimeGhostMaterials = new List<Material>(8);
    private MeshOutlineStyle.OutlineTone cachedTone;
    private bool cachedPreserveOriginalMaterials;
    private bool cachedDrawOutlineShell;
    private bool cachedDrawHardEdges;
    private bool cachedOutlineEnabled;
    private bool ghostApplied;

    private static readonly Color PlacementOutlineColor = new Color(1f, .86f, .32f, 1f);
    private static readonly Color PlacementGhostColor = new Color(1f, .86f, .32f, .035f);

    public void Configure(string value)
    {
        id = value;
        if (Application.isPlaying)
            ApplyGhostInstance();
    }

    public void TryPlace()
    {
        if (ItemRestorationSystem.Instance != null && ItemRestorationSystem.Instance.CanPlace(id))
        {
            ItemRestorationSystem.Instance.Place(id, gameObject);
            enabled = false;
        }
        else
        {
            InteractionUI.Instance?.ShowStatus("Something used to be here.");
        }
    }

    /// <summary>Replace the yellow placement hint with the real item look.</summary>
    public void RevealPlaced()
    {
        RestoreSolidInstance();
    }

    private void ApplyGhostInstance()
    {
        if (ghostApplied)
            return;

        outlineStyle = GetComponent<MeshOutlineStyle>();
        if (outlineStyle != null)
        {
            cachedTone = outlineStyle.Tone;
            cachedPreserveOriginalMaterials = outlineStyle.PreserveOriginalMaterials;
            cachedDrawOutlineShell = outlineStyle.DrawOutlineShell;
            cachedDrawHardEdges = outlineStyle.DrawHardEdges;
            cachedOutlineEnabled = outlineStyle.enabled;
            outlineStyle.ClearGenerated();
            outlineStyle.PreserveOriginalMaterials = true;
            outlineStyle.DrawOutlineShell = true;
            outlineStyle.DrawHardEdges = false;
            outlineStyle.Tone = MeshOutlineStyle.OutlineTone.Yellow;
            outlineStyle.enabled = true;
            outlineStyle.Rebuild();
            TintGeneratedOutline();
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        ghostRenderers.Clear();
        cachedMaterials.Clear();
        cachedRendererEnabled.Clear();
        runtimeGhostMaterials.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;
            string n = renderer.gameObject.name;
            if (n == "OutlineShell" || n == "OutlineCreases"
                || n == "OutlineShell_Detached" || n == "OutlineCreases_Detached")
                continue;

            ghostRenderers.Add(renderer);
            cachedMaterials.Add(renderer.sharedMaterials);
            cachedRendererEnabled.Add(renderer.enabled);

            Material ghostMat = new Material(shader);
            ghostMat.name = $"PlacementSlot_{id}_Hint";
            ghostMat.color = PlacementGhostColor;
            if (ghostMat.HasProperty("_BaseColor"))
                ghostMat.SetColor("_BaseColor", PlacementGhostColor);
            ghostMat.SetFloat("_Surface", 1f);
            ghostMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            ghostMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            ghostMat.SetFloat("_ZWrite", 0f);
            ghostMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            ghostMat.renderQueue = 3000;
            runtimeGhostMaterials.Add(ghostMat);

            Material[] slots = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
            for (int s = 0; s < slots.Length; s++)
                slots[s] = ghostMat;
            renderer.sharedMaterials = slots;
            renderer.enabled = true;
        }

        ghostApplied = true;
    }

    private void TintGeneratedOutline()
    {
        if (outlineStyle == null)
            return;

        Transform shell = outlineStyle.transform.Find("OutlineShell");
        if (shell == null)
            return;

        Renderer shellRenderer = shell.GetComponent<Renderer>();
        if (shellRenderer == null)
            return;

        Material[] materials = shellRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null && materials[i].HasProperty("_OutlineColor"))
                materials[i].SetColor("_OutlineColor", PlacementOutlineColor);
        }
    }

    private void RestoreSolidInstance()
    {
        for (int i = 0; i < ghostRenderers.Count; i++)
        {
            if (ghostRenderers[i] == null)
                continue;
            if (i < cachedMaterials.Count && cachedMaterials[i] != null)
                ghostRenderers[i].sharedMaterials = cachedMaterials[i];
            ghostRenderers[i].enabled = i < cachedRendererEnabled.Count
                ? cachedRendererEnabled[i]
                : true;
        }

        for (int i = 0; i < runtimeGhostMaterials.Count; i++)
            if (runtimeGhostMaterials[i] != null) Destroy(runtimeGhostMaterials[i]);

        ghostRenderers.Clear();
        cachedMaterials.Clear();
        cachedRendererEnabled.Clear();
        runtimeGhostMaterials.Clear();
        ghostApplied = false;

        if (outlineStyle == null)
            outlineStyle = GetComponent<MeshOutlineStyle>();
        if (outlineStyle != null)
        {
            outlineStyle.ClearGenerated();
            outlineStyle.PreserveOriginalMaterials = cachedPreserveOriginalMaterials;
            outlineStyle.DrawOutlineShell = cachedDrawOutlineShell;
            outlineStyle.DrawHardEdges = cachedDrawHardEdges;
            outlineStyle.Tone = cachedTone;
            outlineStyle.enabled = cachedOutlineEnabled;
            if (cachedOutlineEnabled)
                outlineStyle.Rebuild();
        }
    }
}
