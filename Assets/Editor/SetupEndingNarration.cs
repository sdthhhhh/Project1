#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SetupEndingNarration
{
    [MenuItem("BlindGame/Setup EndScene Ending Narration")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Stop Play Mode first.");
            return;
        }

        AssetDatabase.Refresh();
        TextAsset pre = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/Ending/EndDialoguePre.csv");
        TextAsset post = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/Ending/EndDialoguePost.csv");
        if (pre == null || post == null)
        {
            Debug.LogError("Missing EndDialogue CSV under Assets/Data/Ending/");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/EndScene.unity", OpenSceneMode.Single);

        GameObject narrGo = GameObject.Find("IntroNarration");
        if (narrGo == null)
            narrGo = GameObject.Find("EndingNarration");
        if (narrGo == null)
        {
            Debug.LogError("IntroNarration / EndingNarration not found.");
            return;
        }

        TMP_Text speaker = null;
        TMP_Text body = null;
        GameObject speakerRow = null;
        CanvasGroup panelGroup = null;

        IntroNarrationController old = narrGo.GetComponent<IntroNarrationController>();
        if (old != null)
        {
            SerializedObject oldSo = new SerializedObject(old);
            speaker = oldSo.FindProperty("speakerText").objectReferenceValue as TMP_Text;
            body = oldSo.FindProperty("bodyText").objectReferenceValue as TMP_Text;
            speakerRow = oldSo.FindProperty("speakerRow").objectReferenceValue as GameObject;
            panelGroup = oldSo.FindProperty("panelGroup").objectReferenceValue as CanvasGroup;
            Undo.DestroyObjectImmediate(old);
        }

        EndingNarrationController ending = narrGo.GetComponent<EndingNarrationController>();
        if (ending == null)
            ending = Undo.AddComponent<EndingNarrationController>(narrGo);
        narrGo.name = "EndingNarration";

        if (speaker == null || body == null || panelGroup == null)
        {
            Transform panel = FindDeep(scene, "NarrationPanel");
            if (panel != null)
            {
                panelGroup = panel.GetComponent<CanvasGroup>();
                Transform bodyTf = panel.Find("BodyText");
                if (bodyTf != null) body = bodyTf.GetComponent<TMP_Text>();
                Transform row = panel.Find("SpeakerRow");
                if (row != null)
                {
                    speakerRow = row.gameObject;
                    Transform sp = row.Find("SpeakerText");
                    if (sp != null) speaker = sp.GetComponent<TMP_Text>();
                }
            }
        }

        GameObject canvas = GameObject.Find("NarrationCanvas");
        if (canvas == null)
        {
            Debug.LogError("NarrationCanvas missing.");
            return;
        }

        Transform oldChoice = canvas.transform.Find("ChoicePanel");
        if (oldChoice != null)
            Undo.DestroyObjectImmediate(oldChoice.gameObject);

        TMP_FontAsset font = body != null ? body.font : TMP_Settings.defaultFontAsset;
        Color textColor = body != null ? body.color : new Color(0.92f, 0.93f, 0.95f, 1f);

        GameObject choiceGo = new GameObject("ChoicePanel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(choiceGo, "ChoicePanel");
        choiceGo.transform.SetParent(canvas.transform, false);
        RectTransform choiceRt = choiceGo.GetComponent<RectTransform>();
        choiceRt.anchorMin = new Vector2(0.5f, 0f);
        choiceRt.anchorMax = new Vector2(0.5f, 0f);
        choiceRt.pivot = new Vector2(0.5f, 0f);
        choiceRt.anchoredPosition = new Vector2(0f, 240f);
        choiceRt.sizeDelta = new Vector2(720f, 140f);

        Button btnA = CreateChoiceButton(choiceGo.transform, "ChoiceLinFang", "A. LinFang", -160f, font, textColor);
        Button btnB = CreateChoiceButton(choiceGo.transform, "ChoiceSuYu", "B. SuYu", 160f, font, textColor);
        choiceGo.SetActive(false);

        SerializedObject eso = new SerializedObject(ending);
        eso.FindProperty("preChoiceCsv").objectReferenceValue = pre;
        eso.FindProperty("postChoiceCsv").objectReferenceValue = post;
        eso.FindProperty("postChoiceLinFangCsv").objectReferenceValue = null;
        eso.FindProperty("postChoiceSuYuCsv").objectReferenceValue = null;
        eso.FindProperty("speakerText").objectReferenceValue = speaker;
        eso.FindProperty("bodyText").objectReferenceValue = body;
        eso.FindProperty("speakerRow").objectReferenceValue = speakerRow;
        eso.FindProperty("panelGroup").objectReferenceValue = panelGroup;
        eso.FindProperty("choicePanel").objectReferenceValue = choiceGo;
        eso.FindProperty("choiceLinFangButton").objectReferenceValue = btnA;
        eso.FindProperty("choiceSuYuButton").objectReferenceValue = btnB;
        eso.FindProperty("choicePromptText").objectReferenceValue = null;
        eso.FindProperty("choicePrompt").stringValue = "Who is the real killer?";
        eso.FindProperty("choiceLinFangLabel").stringValue = "A. LinFang";
        eso.FindProperty("choiceSuYuLabel").stringValue = "B. SuYu";
        eso.FindProperty("nextSceneName").stringValue = "";
        eso.ApplyModifiedProperties();
        EditorUtility.SetDirty(ending);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("EndScene ending narration + A/B killer choice wired.");
    }

    private static Button CreateChoiceButton(Transform parent, string name, string label, float x, TMP_FontAsset font, Color textColor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, -10f);
        rt.sizeDelta = new Vector2(280f, 56f);

        Image img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.82f);

        Button btn = go.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.85f, 0.88f, 0.92f, 1f);
        cb.pressedColor = new Color(0.7f, 0.72f, 0.75f, 1f);
        btn.colors = cb;

        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        RectTransform lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = label;
        tmp.fontSize = 28f;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return btn;
    }

    private static Transform FindDeep(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeep(roots[i].transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
