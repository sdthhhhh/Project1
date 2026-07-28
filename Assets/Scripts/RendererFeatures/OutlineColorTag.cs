using UnityEngine;

/// <summary>
/// Assigns this object (and optionally child renderers) to an outline Layer
/// so ScreenSpaceOutlines can draw it in the matching color group.
/// </summary>
[DisallowMultipleComponent]
public sealed class OutlineColorTag : MonoBehaviour
{
    public enum OutlineColorKind
    {
        None = 0,
        White = 1,
        Red = 2,
        Yellow = 3
    }

    public const string LayerWhite = "Outline_White";
    public const string LayerRed = "Outline_Red";
    public const string LayerYellow = "Outline_Yellow";

    [SerializeField] private OutlineColorKind color = OutlineColorKind.White;
    [SerializeField, Tooltip("Also set layer on child objects that have a Renderer. Turn OFF if children need different outline colors.")]
    private bool applyToChildrenWithRenderers = false;

    public OutlineColorKind Color
    {
        get => color;
        set
        {
            if (color == value) return;
            color = value;
            Apply();
        }
    }

    private void OnEnable() => Apply();

#if UNITY_EDITOR
    private void OnValidate() => Apply();
#endif

    public void Apply()
    {
        int layer = ResolveLayer(color);
        if (layer < 0)
        {
            if (color != OutlineColorKind.None)
            {
                Debug.LogWarning(
                    $"OutlineColorTag: layer for '{color}' is missing. Add layers Outline_White / Outline_Red / Outline_Yellow.",
                    this);
            }
            return;
        }

        SetLayerRecursive(gameObject, layer, applyToChildrenWithRenderers);
    }

    public static int ResolveLayer(OutlineColorKind kind)
    {
        switch (kind)
        {
            case OutlineColorKind.White:
                return LayerMask.NameToLayer(LayerWhite);
            case OutlineColorKind.Red:
                return LayerMask.NameToLayer(LayerRed);
            case OutlineColorKind.Yellow:
                return LayerMask.NameToLayer(LayerYellow);
            default:
                return LayerMask.NameToLayer("Default");
        }
    }

    public static string ResolveLayerName(OutlineColorKind kind)
    {
        switch (kind)
        {
            case OutlineColorKind.White: return LayerWhite;
            case OutlineColorKind.Red: return LayerRed;
            case OutlineColorKind.Yellow: return LayerYellow;
            default: return "Default";
        }
    }

    private static void SetLayerRecursive(GameObject root, int layer, bool includeChildRenderers)
    {
        root.layer = layer;
        if (!includeChildRenderers) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].gameObject.layer = layer;
        }
    }
}
