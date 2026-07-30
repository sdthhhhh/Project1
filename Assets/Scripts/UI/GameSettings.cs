using UnityEngine;

/// <summary>Persistent audio / look settings used by StartScene and gameplay.</summary>
public static class GameSettings
{
    private const string VolumeKey = "settings.masterVolume";
    private const string SensitivityKey = "settings.mouseSensitivity";

    public const float DefaultVolume = 1f;
    public const float DefaultSensitivity = 2f;

    public static float MasterVolume { get; private set; } = DefaultVolume;
    public static float MouseSensitivity { get; private set; } = DefaultSensitivity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Load();
        ApplyAudio();
    }

    public static void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
        MouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
    }

    public static void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
        ApplyAudio();
    }

    public static void SetMouseSensitivity(float value)
    {
        MouseSensitivity = Mathf.Clamp(value, 0.2f, 8f);
        PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
        PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
        PlayerPrefs.Save();
    }

    public static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume;
    }
}
