using UnityEngine;
using MysticJourney.Core.Services;

// Initializes a new default instance of the SettingsService class.
public class SettingsService
{
    // Executes core business logic for instance.
    public static SettingsService Instance { get; private set; } = new();

    // Executes core business logic for master volume.
    public float MasterVolume { get; private set; } = 1f;
    // Executes core business logic for music volume.
    public float MusicVolume { get; private set; } = 1f;
    // Executes core business logic for sfx volume.
    public float SfxVolume { get; private set; } = 1f;
    // Executes core business logic for is muted.
    public bool IsMuted { get; private set; }

    // Executes core business logic for display mode index.
    public int DisplayModeIndex { get; private set; }
    // Executes core business logic for resolution index.
    public int ResolutionIndex { get; private set; }
    // Executes core business logic for has session graphics settings.
    public bool HasSessionGraphicsSettings { get; private set; }
    // Executes core business logic for show damage numbers.
    public bool ShowDamageNumbers { get; private set; } = true;

    private const string KeyMasterVolume = "mj_setting_master_vol";
    private const string KeyMusicVolume = "mj_setting_music_vol";
    private const string KeySfxVolume = "mj_setting_sfx_vol";
    private const string KeyMuted = "mj_setting_muted";
    private const string LegacyKeyDisplayMode = "mj_setting_display_mode";
    private const string LegacyKeyResolution = "mj_setting_resolution";
    private const string KeyDamageNumbers = "mj_setting_damage_numbers";

    // Initializes a new default instance of the SettingsService class.
    private SettingsService() { }

    // Executes core business logic for load.
    public void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat(KeyMasterVolume, 1f);
        MusicVolume = PlayerPrefs.GetFloat(KeyMusicVolume, 1f);
        SfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, 1f);
        IsMuted = PlayerPrefs.GetInt(KeyMuted, 0) == 1;
        ShowDamageNumbers = PlayerPrefs.GetInt(KeyDamageNumbers, 1) == 1;

        RemoveLegacyGraphicsPreferences();
    }

    // Executes core business logic for save.
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

    // Executes core business logic for set master volume.
    public void SetMasterVolume(float value)
    {
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        MasterVolume = Mathf.Clamp01(value);
        ApplyVolume();
    }

    // Executes core business logic for set music volume.
    public void SetMusicVolume(float value)
    {
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        MusicVolume = Mathf.Clamp01(value);
        ApplyVolume();
    }

    // Executes core business logic for set sfx volume.
    public void SetSfxVolume(float value)
    {
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        SfxVolume = Mathf.Clamp01(value);
        ApplyVolume();
    }

    // Executes core business logic for set muted.
    public void SetMuted(bool muted)
    {
        IsMuted = muted;
        ApplyVolume();
    }

    // Executes core business logic for set display mode.
    public void SetDisplayMode(int index)
    {
        DisplayModeIndex = Mathf.Max(0, index);
        HasSessionGraphicsSettings = true;
    }

    // Executes core business logic for set resolution.
    public void SetResolution(int index)
    {
        ResolutionIndex = Mathf.Max(0, index);
        HasSessionGraphicsSettings = true;
    }

    // Executes core business logic for initialize session graphics.
    public void InitializeSessionGraphics(int displayModeIndex, int resolutionIndex)
    {
        if (HasSessionGraphicsSettings)
            return;

        DisplayModeIndex = Mathf.Max(0, displayModeIndex);
        ResolutionIndex = Mathf.Max(0, resolutionIndex);
        HasSessionGraphicsSettings = true;
    }

    // Executes core business logic for set show damage numbers.
    public void SetShowDamageNumbers(bool show)
    {
        ShowDamageNumbers = show;
    }

    // Executes core business logic for apply volume.
    private void ApplyVolume()
    {
        AudioManager.Instance.ApplyVolumesFromSettings();
    }

    // Executes core business logic for get effective master volume.
    public float GetEffectiveMasterVolume() => IsMuted ? 0f : MasterVolume;

    // Executes core business logic for remove legacy graphics preferences.
    private static void RemoveLegacyGraphicsPreferences()
    {
        PlayerPrefs.DeleteKey(LegacyKeyDisplayMode);
        PlayerPrefs.DeleteKey(LegacyKeyResolution);
    }
}
