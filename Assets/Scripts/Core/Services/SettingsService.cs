using UnityEngine;
using MysticJourney.Core.Services;

public class SettingsService
{
    public static SettingsService Instance { get; private set; } = new();

    // Audio
    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;
    public bool IsMuted { get; private set; }

    // Graphics
    public int DisplayModeIndex { get; private set; }
    public int ResolutionIndex { get; private set; }
    public bool HasSessionGraphicsSettings { get; private set; }
    public bool ShowDamageNumbers { get; private set; } = true;

    private const string KeyMasterVolume = "mj_setting_master_vol";
    private const string KeyMusicVolume = "mj_setting_music_vol";
    private const string KeySfxVolume = "mj_setting_sfx_vol";
    private const string KeyMuted = "mj_setting_muted";
    private const string LegacyKeyDisplayMode = "mj_setting_display_mode";
    private const string LegacyKeyResolution = "mj_setting_resolution";
    private const string KeyDamageNumbers = "mj_setting_damage_numbers";

    private SettingsService() { }

    public void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat(KeyMasterVolume, 1f);
        MusicVolume = PlayerPrefs.GetFloat(KeyMusicVolume, 1f);
        SfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, 1f);
        IsMuted = PlayerPrefs.GetInt(KeyMuted, 0) == 1;
        ShowDamageNumbers = PlayerPrefs.GetInt(KeyDamageNumbers, 1) == 1;

        RemoveLegacyGraphicsPreferences();
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(KeyMasterVolume, MasterVolume);
        PlayerPrefs.SetFloat(KeyMusicVolume, MusicVolume);
        PlayerPrefs.SetFloat(KeySfxVolume, SfxVolume);
        PlayerPrefs.SetInt(KeyMuted, IsMuted ? 1 : 0);
        PlayerPrefs.SetInt(KeyDamageNumbers, ShowDamageNumbers ? 1 : 0);
        RemoveLegacyGraphicsPreferences();
        PlayerPrefs.Save();
        Debug.Log("[SettingsService] Persistent settings saved. Graphics remain session-only.");
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        ApplyVolume();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        ApplyVolume();
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        ApplyVolume();
    }

    public void SetMuted(bool muted)
    {
        IsMuted = muted;
        ApplyVolume();
    }

    public void SetDisplayMode(int index)
    {
        DisplayModeIndex = Mathf.Max(0, index);
        HasSessionGraphicsSettings = true;
    }

    public void SetResolution(int index)
    {
        ResolutionIndex = Mathf.Max(0, index);
        HasSessionGraphicsSettings = true;
    }

    public void InitializeSessionGraphics(int displayModeIndex, int resolutionIndex)
    {
        if (HasSessionGraphicsSettings)
            return;

        DisplayModeIndex = Mathf.Max(0, displayModeIndex);
        ResolutionIndex = Mathf.Max(0, resolutionIndex);
        HasSessionGraphicsSettings = true;
    }

    public void SetShowDamageNumbers(bool show)
    {
        ShowDamageNumbers = show;
    }

    private void ApplyVolume()
    {
        // Route qua AudioManager (điều khiển volume TỪNG source: music/sfx) thay vì
        // AudioListener.volume — biến global đó tắt cả nhạc map lẫn mọi âm thanh, và
        // là nguyên nhân "mất nhạc khi mở Settings" (master slider serialize = 0).
        AudioManager.Instance.ApplyVolumesFromSettings();
    }

    public float GetEffectiveMasterVolume() => IsMuted ? 0f : MasterVolume;

    private static void RemoveLegacyGraphicsPreferences()
    {
        PlayerPrefs.DeleteKey(LegacyKeyDisplayMode);
        PlayerPrefs.DeleteKey(LegacyKeyResolution);
    }
}
