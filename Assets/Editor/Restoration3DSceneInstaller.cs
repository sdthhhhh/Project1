#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Restoration3DSceneInstaller
{
    [InitializeOnLoadMethod]private static void InstallAfterCompile(){EditorApplication.delayCall+=()=>{if(!Application.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode&&GameObject.Find("MovableItems")!=null)Install();};}
    [MenuItem("Tools/Object Inspection/Install Restoration 3D Inspection")]
    public static void Install()
    {
        if(Application.isPlaying)return;GameObject movableRoot=GameObject.Find("MovableItems"),correctRoot=GameObject.Find("Deskroom");if(movableRoot==null||correctRoot==null)return;
        Dictionary<string,Transform> sources=FindNumbered(movableRoot.transform),targets=FindNumbered(correctRoot.transform);
        for(int n=2;n<=6;n++)
        {
            string id=n.ToString();if(!sources.TryGetValue(id,out Transform source)||!targets.TryGetValue(id,out Transform target))continue;
            EnsureCollider(source.gameObject);EnsureCollider(target.gameObject);
            InspectableObject sourceInspect=source.GetComponent<InspectableObject>();if(sourceInspect==null){sourceInspect=Undo.AddComponent<InspectableObject>(source.gameObject);sourceInspect.ConfigurePreview(source.gameObject,"A misplaced object marked "+id+".",Vector3.zero);}sourceInspect.SetCanInspect(true);
            RestorationInspectablePickup pickup=source.GetComponent<RestorationInspectablePickup>();if(pickup==null)pickup=Undo.AddComponent<RestorationInspectablePickup>(source.gameObject);pickup.Configure(id);
            RestorationPickup oldPickup=source.GetComponent<RestorationPickup>();if(oldPickup!=null)Undo.DestroyObjectImmediate(oldPickup);
            InspectableObject targetInspect=target.GetComponent<InspectableObject>();if(targetInspect==null){targetInspect=Undo.AddComponent<InspectableObject>(target.gameObject);targetInspect.ConfigurePreview(target.gameObject,"The restored position for item "+id+".",Vector3.zero);}targetInspect.SetCanInspect(false);
            RestorationPlace place=target.GetComponent<RestorationPlace>();if(place==null)place=Undo.AddComponent<RestorationPlace>(target.gameObject);place.Configure(id);
            EditorUtility.SetDirty(sourceInspect);EditorUtility.SetDirty(targetInspect);EditorUtility.SetDirty(pickup);EditorUtility.SetDirty(place);
        }
        foreach(GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())if(go.scene.IsValid()&&go.name=="RestorationInventory")Undo.DestroyObjectImmediate(go);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();Debug.Log("Restoration 3D inspection installed; legacy inventory UI removed.");
    }
    private static Dictionary<string,Transform> FindNumbered(Transform root){var result=new Dictionary<string,Transform>();foreach(Transform t in root.GetComponentsInChildren<Transform>(true)){int length=0;while(length<t.name.Length&&char.IsDigit(t.name[length]))length++;if(length==0)continue;if(int.TryParse(t.name.Substring(0,length),out int n)&&n>=2&&n<=6&&!result.ContainsKey(n.ToString()))result[n.ToString()]=FindVisual(t,root);}return result;}
    private static Transform FindVisual(Transform numbered,Transform root){if(numbered.GetComponentInChildren<Renderer>(true)!=null)return numbered;Transform current=numbered.parent;while(current!=null&&current!=root){if(current.GetComponent<Renderer>()!=null)return current;current=current.parent;}return numbered;}
    private static void EnsureCollider(GameObject go){if(go.GetComponentInChildren<Collider>(true)==null)Undo.AddComponent<BoxCollider>(go);}
}
#endif
