#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Restoration23456CleanupInstaller
{
    // Cleanup is intentionally manual. Running it after every script reload used to
    // delete restored scene content and immediately save that deletion.
    [MenuItem("Tools/Restoration/Cleanup And Keep Items 2-6 Only")]
    public static void Cleanup()
    {
        if(Application.isPlaying)return;
        DeleteByName("PlacedFrame");DeleteByName("FramePlacePoint");DeleteByName("CollectedFamilyPhotoStatus");DeleteByName("PhotoPickupInventory");
        GameObject drawerTop=FindSceneObject("DrawerTop");if(drawerTop!=null)for(int i=drawerTop.transform.childCount-1;i>=0;i--)Undo.DestroyObjectImmediate(drawerTop.transform.GetChild(i).gameObject);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();Debug.Log("Restoration cleanup complete: only MovableItems 2-6 are required to unlock DrawerBottom.");
    }
    private static void DeleteByName(string name){GameObject go=FindSceneObject(name);if(go!=null)Undo.DestroyObjectImmediate(go);}
    private static GameObject FindSceneObject(string name){foreach(GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())if(go.scene.IsValid()&&go.name==name)return go;return null;}
}
#endif
