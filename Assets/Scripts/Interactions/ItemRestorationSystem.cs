using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>按 MovableItems/Deskroom 下的同名数字，自动建立六件物品的拾取和归位流程。</summary>
public sealed class ItemRestorationSystem : MonoBehaviour
{
    public static ItemRestorationSystem Instance { get; private set; }

    [Header("Reveal On Complete")]
    [SerializeField, Tooltip("Activated when all numbered books/items (2–6) are restored (e.g. DiaryFragment03).")]
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
        GameObject correctRoot = GameObject.Find("Deskroom");
        if (movableRoot == null || correctRoot == null)
        {
            Debug.LogError("物品还原：找不到 MovableItems 或 Deskroom。请确认名称并保存场景。");
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

            SetVisible(target.gameObject, false);
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

    /// <summary>Wire DiaryFragment03 by name when the inspector list is empty (runtime-added component).</summary>
    private void EnsureRevealOnCompleteDefaults()
    {
        if (revealOnComplete != null && revealOnComplete.Length > 0)
            return;

        GameObject frag = FindSceneObject("DiaryFragment03");
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
            if (TryReadNumber(child.name, out int n) && n >= 2 && n <= 6 && !result.ContainsKey(n.ToString()))
                result.Add(n.ToString(), FindVisualObject(child, root));
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
        SetVisible(target, true);
        InspectableObject inspectable=target.GetComponent<InspectableObject>();if(inspectable!=null)inspectable.SetCanInspect(false);
        RestorationPlace place=target.GetComponent<RestorationPlace>();if(place!=null)Destroy(place);
        InteractionUI.Instance?.ShowStatus(state.displayName+" restored");CheckComplete();
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

public sealed class RestorationPlace : MonoBehaviour, IInteractable
{
    private string id;
    private GameObject dustHint;

    public void Configure(string value)
    {
        id = value;
        if(Application.isPlaying)CreateDustHint();
    }

    public string GetInteractText()
    {
        return ItemRestorationSystem.Instance != null && ItemRestorationSystem.Instance.CanPlace(id)
            ? "Press E to place item"
            : "Something used to be here";
    }

    public void Interact()
    {
        if (ItemRestorationSystem.Instance != null && ItemRestorationSystem.Instance.CanPlace(id))
        {
            ItemRestorationSystem.Instance.Place(id, gameObject);
            if (dustHint != null) Destroy(dustHint);
            enabled=false;
        }
        else
        {
            InteractionUI.Instance?.ShowStatus("Something used to be here.");
        }
    }

    private void CreateDustHint()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        dustHint = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dustHint.name = "PlacementHint_" + id;
        Destroy(dustHint.GetComponent<Collider>());
        dustHint.transform.position = new Vector3(bounds.center.x, bounds.min.y + 0.006f, bounds.center.z);
        dustHint.transform.rotation = transform.rotation;
        dustHint.transform.localScale = new Vector3(
            Mathf.Max(bounds.size.x * 0.9f, 0.08f),
            0.012f,
            Mathf.Max(bounds.size.z * 0.9f, 0.08f));

        Renderer hintRenderer = dustHint.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material material = new Material(shader);
        Color dustColor = new Color(0.34f, 0.27f, 0.16f, 0.28f);
        material.color = dustColor;
        material.SetColor("_BaseColor", dustColor);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        hintRenderer.material = material;
    }
}
