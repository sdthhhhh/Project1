using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DoorPasswordUIController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button closeButton;
    private DoorPasswordLock currentLock;private Action onClosed;
    public bool IsOpen=>panel!=null&&panel.activeSelf;

    private void Awake(){BindButtons();}
    private void OnEnable(){BindButtons();}

    private void BindButtons()
    {
        if(submitButton!=null){submitButton.onClick.RemoveListener(Submit);submitButton.onClick.AddListener(Submit);}
        if(closeButton!=null){closeButton.onClick.RemoveListener(Hide);closeButton.onClick.AddListener(Hide);}
    }

    public void Configure(GameObject root,TMP_Text title,TMP_InputField input,TMP_Text feedback,Button submit,Button close)
    {
        panel=root;titleText=title;passwordInput=input;feedbackText=feedback;submitButton=submit;closeButton=close;
        BindButtons();
        if(panel!=null)panel.SetActive(false);
    }

    public void Show(DoorPasswordLock target,Action closedCallback)
    {
        if(target==null||panel==null)return;currentLock=target;onClosed=closedCallback;panel.SetActive(true);panel.transform.SetAsLastSibling();
        if(titleText!=null)titleText.text=target.DisplayName;
        if(feedbackText!=null){feedbackText.text="Enter password";feedbackText.color=new Color(.92f,.88f,.78f);}
        if(passwordInput!=null){passwordInput.text=string.Empty;passwordInput.ActivateInputField();passwordInput.Select();}
    }

    private void Update(){if(IsOpen&&(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)))Submit();}

    public void Submit()
    {
        if(currentLock==null)return;
        if(currentLock.TryUnlock(passwordInput!=null?passwordInput.text:string.Empty))
        {if(feedbackText!=null){feedbackText.text="Unlocked";feedbackText.color=new Color(.45f,1f,.55f);}Hide();}
        else
        {if(feedbackText!=null){feedbackText.text="Incorrect password";feedbackText.color=new Color(1f,.35f,.3f);}if(passwordInput!=null){passwordInput.text=string.Empty;passwordInput.ActivateInputField();}}
    }

    public void Hide()
    {
        if(panel!=null)panel.SetActive(false);currentLock=null;Action callback=onClosed;onClosed=null;callback?.Invoke();
    }
}
