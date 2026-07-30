using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D diary assembly: player places collected FBX fragments into user-authored sockets on the diary.
/// Uses ObjectInspectionCanvas overlay for dimming + Q/Esc close (same layer as item inspect).
/// </summary>
[DisallowMultipleComponent]
public sealed class DiaryAssemblyController : MonoBehaviour
{
    public static DiaryAssemblyController Instance { get; private set; }

    [Header("UI (same layer as item inspect)")]
    [SerializeField] private InspectableUIController inspectUI;

    [Header("Sockets")]
    [SerializeField, Tooltip("If empty, auto-finds DiaryAssemblySocket under this object.")]
    private DiaryAssemblySocket[] sockets;

    [Header("Piece visuals (optional overrides)")]
    [SerializeField] private GameObject piece1Prefab;
    [SerializeField] private GameObject piece2Prefab;
    [SerializeField] private GameObject piece3Prefab;
    [SerializeField] private GameObject piece4Prefab;

    [Header("Raycast while assembling")]
    [SerializeField, Min(0.5f)] private float placeDistance = 8f;
    [SerializeField] private LayerMask socketLayers = ~0;

    [Header("Prompts")]
    [SerializeField, TextArea(2, 4)] private string assemblyDescription =
        "Click a highlighted groove to place the matching diary fragment.\nQ / Esc — Put Back";

    public bool IsOpen { get; private set; }

    private readonly HashSet<int> placedIds = new HashSet<int>();
    private FirstPersonMovement movement;
    private FirstPersonLook look;
    private PlayerInteraction playerInteraction;
    private bool controlsLocked;
    private System.Action onCompleted;

    private void Awake()
    {
        if (IsInspectPreviewClone())
            return;
        Instance = this;
        if (sockets == null || sockets.Length == 0)
            sockets = GetComponentsInChildren<DiaryAssemblySocket>(true);
        CachePlayer();
        AssignPrefabOverrides();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private static bool IsInspectPreviewClone(Component c)
    {
        Transform t = c.transform;
        while (t != null)
        {
            string n = t.name;
            if (n.StartsWith("PreviewModel")
                || n.Contains("InspectedModelPivot")
                || n.Contains("CollectibleModelPivot")
                || n.Contains("ObjectInspection3DStudio")
                || n.Contains("CollectibleInspection3DStudio")
                || n == "DiaryInspectPuzzleRoot")
                return true;
            t = t.parent;
        }
        return false;
    }

    private bool IsInspectPreviewClone() => IsInspectPreviewClone(this);

    public void Configure(InspectableUIController ui, System.Action completedCallback)
    {
        inspectUI = ui;
        onCompleted = completedCallback;
    }

    public void BeginAssembly()
    {
        if (IsOpen)
            return;

        if (sockets == null || sockets.Length == 0)
            sockets = GetComponentsInChildren<DiaryAssemblySocket>(true);

        if (sockets == null || sockets.Length == 0)
        {
            Debug.LogError("DiaryAssemblyController: no DiaryAssemblySocket regions. Add sockets under the diary and size their BoxColliders.", this);
            return;
        }

        IsOpen = true;
        CachePlayer();
        LockControls();

        if (inspectUI == null)
            inspectUI = FindObjectOfType<InspectableUIController>();

        if (inspectUI != null)
            inspectUI.ShowUtilityOverlay(assemblyDescription, "Put Back");
        else
            Debug.LogWarning("DiaryAssemblyController: InspectableUIController missing — Esc/Q close overlay unavailable.", this);

        InteractionUI.Instance?.ShowStatus("Place fragments into the diary grooves.");
    }

    public void CloseAssembly()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        if (inspectUI != null && inspectUI.IsOpen)
            inspectUI.Hide();
        UnlockControls();
    }

    /// <summary>Called from InspectableRaycaster while assembly UI is open.</summary>
    public void HandleAssemblyClick()
    {
        if (!IsOpen)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, placeDistance, socketLayers, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            InteractionUI.Instance?.ShowStatus("Aim at a diary groove.");
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        DiaryAssemblySocket socket = null;
        for (int i = 0; i < hits.Length; i++)
        {
            socket = hits[i].collider.GetComponentInParent<DiaryAssemblySocket>();
            if (socket != null)
                break;
        }

        if (socket == null)
        {
            InteractionUI.Instance?.ShowStatus("Aim at a diary groove.");
            return;
        }

        if (socket.IsFilled)
        {
            InteractionUI.Instance?.ShowStatus("That groove is already filled.");
            return;
        }

        if (DiaryManager.Instance == null || !DiaryManager.Instance.HasFragment(socket.CorrectFragmentId))
        {
            InteractionUI.Instance?.ShowStatus("You don't have that fragment yet.");
            return;
        }

        if (placedIds.Contains(socket.CorrectFragmentId))
        {
            InteractionUI.Instance?.ShowStatus("That fragment is already placed.");
            return;
        }

        if (!socket.TryPlace(socket.CorrectFragmentId))
            return;

        placedIds.Add(socket.CorrectFragmentId);
        InteractionUI.Instance?.ShowStatus($"Placed fragment {socket.CorrectFragmentId} ({placedIds.Count}/{RequiredCount()})");

        if (AllSocketsFilled())
            CompleteAssembly();
    }

    private int RequiredCount()
    {
        if (sockets == null || sockets.Length == 0)
            return DiaryManager.Instance != null ? DiaryManager.Instance.TotalFragments : 4;
        return sockets.Length;
    }

    private bool AllSocketsFilled()
    {
        if (sockets == null || sockets.Length == 0)
            return false;
        for (int i = 0; i < sockets.Length; i++)
        {
            if (sockets[i] == null || !sockets[i].IsFilled)
                return false;
        }
        return true;
    }

    private void CompleteAssembly()
    {
        DiaryManager.Instance?.MarkPuzzleCompleted();
        CloseAssembly();
        onCompleted?.Invoke();
        InteractionUI.Instance?.ShowStatus("The diary cover is complete.");
    }

    private void AssignPrefabOverrides()
    {
        if (sockets == null)
            return;
        for (int i = 0; i < sockets.Length; i++)
        {
            if (sockets[i] == null)
                continue;
            GameObject prefab = PrefabForId(sockets[i].CorrectFragmentId);
            if (prefab != null)
                sockets[i].Configure(sockets[i].CorrectFragmentId, prefab);
        }
    }

    private GameObject PrefabForId(int id)
    {
        switch (id)
        {
            case 1: return piece1Prefab;
            case 2: return piece2Prefab;
            case 3: return piece3Prefab;
            case 4: return piece4Prefab;
            default: return null;
        }
    }

    private void CachePlayer()
    {
        if (movement == null) movement = FindObjectOfType<FirstPersonMovement>();
        if (look == null) look = FindObjectOfType<FirstPersonLook>();
        if (playerInteraction == null) playerInteraction = FindObjectOfType<PlayerInteraction>();
    }

    private void LockControls()
    {
        if (controlsLocked)
            return;
        controlsLocked = true;
        if (movement != null) movement.enabled = false;
        if (look != null) look.enabled = false;
        if (playerInteraction != null) playerInteraction.enabled = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void UnlockControls()
    {
        if (!controlsLocked)
            return;
        controlsLocked = false;
        if (movement != null) movement.enabled = true;
        if (look != null) look.enabled = true;
        if (playerInteraction != null) playerInteraction.enabled = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
