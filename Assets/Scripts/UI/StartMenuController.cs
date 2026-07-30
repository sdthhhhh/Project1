using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// StartScene menu: Start → IntroScene, plus Settings / Credits panels.
/// </summary>
[DisallowMultipleComponent]
public sealed class StartMenuController : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private string introSceneName = "IntroScene";

    [Header("Root Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private CanvasGroup rootGroup;

    [Header("Main Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;

    [Header("Settings")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text volumeValueText;
    [SerializeField] private TMP_Text sensitivityValueText;
    [SerializeField] private Button settingsBackButton;

    [Header("Credits")]
    [SerializeField] private Button creditsBackButton;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.7f;

    private void Awake()
    {
        GameSettings.Load();
        GameSettings.ApplyAudio();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        WireButtons();
        SyncSettingsUi();
        ShowMain();

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            StartCoroutine(FadeIn());
        }
    }

    private void OnDestroy()
    {
        UnwireButtons();
        GameSettings.Save();
    }

    private void WireButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStart);
            startButton.onClick.AddListener(OnStart);
        }
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(ShowSettings);
            settingsButton.onClick.AddListener(ShowSettings);
        }
        if (creditsButton != null)
        {
            creditsButton.onClick.RemoveListener(ShowCredits);
            creditsButton.onClick.AddListener(ShowCredits);
        }
        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.RemoveListener(ShowMain);
            settingsBackButton.onClick.AddListener(ShowMain);
        }
        if (creditsBackButton != null)
        {
            creditsBackButton.onClick.RemoveListener(ShowMain);
            creditsBackButton.onClick.AddListener(ShowMain);
        }
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }
    }

    private void UnwireButtons()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStart);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(ShowSettings);
        if (creditsButton != null) creditsButton.onClick.RemoveListener(ShowCredits);
        if (settingsBackButton != null) settingsBackButton.onClick.RemoveListener(ShowMain);
        if (creditsBackButton != null) creditsBackButton.onClick.RemoveListener(ShowMain);
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
    }

    private void OnStart()
    {
        GameSettings.Save();
        if (string.IsNullOrWhiteSpace(introSceneName))
        {
            Debug.LogError("StartMenuController: introSceneName is empty.", this);
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(introSceneName))
        {
            Debug.LogError($"StartMenuController: scene '{introSceneName}' is not in Build Settings.", this);
            return;
        }
        SceneManager.LoadScene(introSceneName);
    }

    private void ShowMain()
    {
        SetPanel(mainPanel, true);
        SetPanel(settingsPanel, false);
        SetPanel(creditsPanel, false);
    }

    private void ShowSettings()
    {
        SyncSettingsUi();
        SetPanel(mainPanel, false);
        SetPanel(settingsPanel, true);
        SetPanel(creditsPanel, false);
    }

    private void ShowCredits()
    {
        SetPanel(mainPanel, false);
        SetPanel(settingsPanel, false);
        SetPanel(creditsPanel, true);
    }

    private void SyncSettingsUi()
    {
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        }
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 0.2f;
            sensitivitySlider.maxValue = 8f;
            sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        }
        RefreshValueLabels();
    }

    private void OnVolumeChanged(float value)
    {
        GameSettings.SetMasterVolume(value);
        RefreshValueLabels();
    }

    private void OnSensitivityChanged(float value)
    {
        GameSettings.SetMouseSensitivity(value);
        RefreshValueLabels();
    }

    private void RefreshValueLabels()
    {
        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%";
        if (sensitivityValueText != null)
            sensitivityValueText.text = GameSettings.MouseSensitivity.ToString("0.0");
    }

    private static void SetPanel(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeInDuration));
            u = u * u * (3f - 2f * u);
            rootGroup.alpha = u;
            yield return null;
        }
        rootGroup.alpha = 1f;
    }
}
