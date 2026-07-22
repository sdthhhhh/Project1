#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PhotoRestoreSceneInstaller
{
    private const string SessionKey = "PhotoRestore.InstallAttempted.v1";

    static PhotoRestoreSceneInstaller()
    {
        EditorApplication.delayCall += InstallOnce;
    }

    private static void InstallOnce()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        Install();
    }

    [MenuItem("Tools/Object Inspection/Install Photo Restoration")]
    public static void Install()
    {
        if (Application.isPlaying) return;

        GameObject photoes = FindSceneObject("Photoes");
        Transform collectibleTransform = photoes != null ? FindDirectChild(photoes.transform, "InteractedPhoto (1)") : null;
        Transform restoreTransform = photoes != null ? FindDirectChild(photoes.transform, "InteractedPhoto") : null;

        if (photoes == null || collectibleTransform == null || restoreTransform == null)
        {
            Debug.LogError("Photo restoration installer requires Photoes/InteractedPhoto (1) and Photoes/InteractedPhoto in the open scene.");
            return;
        }

        GameObject collectiblePhoto = collectibleTransform.gameObject;
        InspectableObject inspectable = collectiblePhoto.GetComponent<InspectableObject>();
        if (inspectable == null) inspectable = Undo.AddComponent<InspectableObject>(collectiblePhoto);
        inspectable.SetCanInspect(true);

        InspectableCollectible collectible = collectiblePhoto.GetComponent<InspectableCollectible>();
        if (collectible == null) collectible = Undo.AddComponent<InspectableCollectible>(collectiblePhoto);
        collectible.Configure(true);

        Collider collectibleCollider = collectiblePhoto.GetComponentInChildren<Collider>(true);
        if (collectibleCollider == null) collectibleCollider = Undo.AddComponent<BoxCollider>(collectiblePhoto);
        collectibleCollider.isTrigger = false;

        Renderer restoreRenderer = restoreTransform.GetComponentInChildren<Renderer>(true);
        Collider restoreCollider = restoreTransform.GetComponentInChildren<Collider>(true);
        if (restoreCollider == null) restoreCollider = Undo.AddComponent<BoxCollider>(restoreTransform.gameObject);
        restoreCollider.isTrigger = false;

        if (restoreRenderer == null)
        {
            Debug.LogError("Photo restoration installer: InteractedPhoto needs a Renderer.");
            return;
        }

        PhotoRestoreController[] controllers = Object.FindObjectsOfType<PhotoRestoreController>(true);
        PhotoRestoreController controller = restoreTransform.GetComponent<PhotoRestoreController>();
        if (controller == null) controller = Undo.AddComponent<PhotoRestoreController>(restoreTransform.gameObject);
        foreach (PhotoRestoreController duplicate in controllers)
            if (duplicate != null && duplicate != controller) Undo.DestroyObjectImmediate(duplicate);

        RemoveLegacyEInteractions(collectibleTransform);
        RemoveLegacyEInteractions(restoreTransform);
        controller.Configure(photoes, collectiblePhoto, restoreRenderer, restoreCollider);

        EditorUtility.SetDirty(inspectable);
        EditorUtility.SetDirty(collectible);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = collectiblePhoto;
        Debug.Log("Photo restoration installed: hand Crosshair -> shared collectible inspection -> Q collect -> hand Crosshair on InteractedPhoto -> reveal Photoes.");
    }

    private static void RemoveLegacyEInteractions(Transform root)
    {
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is IInteractable) Undo.DestroyObjectImmediate(behaviour);
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
            if (child.name == childName) return child;
        return null;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            if (gameObject.scene.IsValid() && gameObject.name == objectName) return gameObject;
        return null;
    }
}
#endif
