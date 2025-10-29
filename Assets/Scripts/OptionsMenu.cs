using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class OptionsMenu : MonoBehaviour
{
    #if !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern System.IntPtr GetActiveWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(System.IntPtr hwnd, int index, int newStyle);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(System.IntPtr hWnd, System.IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const int GWL_STYLE = -16;
    private const int WS_POPUP = 0x800000;
    private const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
    private const uint SWP_SHOWWINDOW = 0x0040;
    #endif

    // UI references
    public TMPro.TMP_Dropdown displayModeDropdown;
    public TMPro.TMP_Dropdown resolutionDropdown;
    public Toggle vsyncToggle;
    public Toggle subtitlesToggle;
    public TMPro.TMP_Dropdown textureResolutionDropdown;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public Slider voiceVolumeSlider;
    public UnityEngine.Rendering.PostProcessing.PostProcessVolume postProcessVolume;
    public Slider postProcessSlider;
    public TMP_Dropdown languageDropdown;
    public Toggle invertCameraToggle;
    public Slider cameraSensitivitySlider;
    public TMPro.TMP_Dropdown antiAliasingDropdown;
    public Toggle controllerVibrationToggle;
    public Button resetButton;

    private Resolution[] resolutions;
    private bool isPaused = false;
    private bool isInOptionsMenu = false;

    private void Start()
    {
        SetupResolutionDropdown();

        // Set up listeners for sliders and dropdowns
        displayModeDropdown.onValueChanged.AddListener(ChangeDisplayMode);
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
        vsyncToggle.onValueChanged.AddListener(ToggleVSync);
        subtitlesToggle.onValueChanged.AddListener(ToggleSubtitles);
        textureResolutionDropdown.onValueChanged.AddListener(ChangeTextureResolution);
        musicVolumeSlider.onValueChanged.AddListener(ChangeMusicVolume);
        sfxVolumeSlider.onValueChanged.AddListener(ChangeSFXVolume);
        voiceVolumeSlider.onValueChanged.AddListener(ChangeVoiceVolume);
        postProcessSlider.onValueChanged.AddListener(ChangePostProcessWeight);
        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        invertCameraToggle.onValueChanged.AddListener(ToggleCameraInvert);
        cameraSensitivitySlider.onValueChanged.AddListener(ChangeCameraSensitivity);
        antiAliasingDropdown.onValueChanged.AddListener(ChangeAntiAliasing);
        controllerVibrationToggle.onValueChanged.AddListener(ToggleControllerVibration);
        resetButton.onClick.AddListener(ResetSettings);

        InitializeOptions();
    }

    private void SetupResolutionDropdown()
    {
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        // Higher-end resolutions (4K, 2K) and common resolutions
        Resolution[] targetResolutions = new Resolution[]
        {
            new Resolution { width = 3840, height = 2160 }, // 4K
            new Resolution { width = 2560, height = 1440 }, // 2K/QHD
            new Resolution { width = 1920, height = 1080 }, // Full HD
            new Resolution { width = 1600, height = 900 },  // HD+
            new Resolution { width = 1366, height = 768 },  // HD
            new Resolution { width = 1280, height = 720 }   // HD Ready
        };

        foreach (Resolution targetRes in targetResolutions)
        {
            // Check if this resolution is supported by the display
            // Note: We'll always add 4K and 2K even if not detected, for future-proofing
            if (targetRes.width == 3840 || targetRes.width == 2560 || 
                System.Array.Exists(resolutions, res => res.width == targetRes.width && res.height == targetRes.height))
            {
                string option = $"{targetRes.width}x{targetRes.height}";
                options.Add(option);

                if (targetRes.width == Screen.width && targetRes.height == Screen.height)
                {
                    currentResolutionIndex = options.Count - 1;
                }
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    public void SetResolution(int resolutionIndex)
    {
        string[] res = resolutionDropdown.options[resolutionIndex].text.Split('x');
        int width = int.Parse(res[0]);
        int height = int.Parse(res[1]);
        
        Screen.SetResolution(width, height, Screen.fullScreenMode);
    }

    private void InitializeOptions()
    {
        if (Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen)
            displayModeDropdown.value = 0;
        else if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow)
            displayModeDropdown.value = 1;
        else
            displayModeDropdown.value = 2;

            // Initialize the language dropdown with options
        languageDropdown.ClearOptions();
        List<string> languages = new List<string> { "English", "Spanish", "French", "German" }; // Example languages
        languageDropdown.AddOptions(languages);

        // Set default language (you can modify this logic if you use PlayerPrefs or custom logic)
        languageDropdown.value = 0; // Default to English
        languageDropdown.RefreshShownValue();

        bool vsyncEnabled = PlayerPrefs.GetInt("VSyncEnabled", 1) == 1;
        vsyncToggle.isOn = vsyncEnabled;
        QualitySettings.vSyncCount = vsyncEnabled ? 1 : 0;
        subtitlesToggle.isOn = true;
        textureResolutionDropdown.value = (int)QualitySettings.GetQualityLevel();
        musicVolumeSlider.value = AudioListener.volume;
        sfxVolumeSlider.value = 1f;
        voiceVolumeSlider.value = 1f;
        postProcessSlider.value = 1f;
        invertCameraToggle.isOn = false;
        cameraSensitivitySlider.value = 1f;
        SetAABasedOnSystemSpecs();
        controllerVibrationToggle.isOn = true;
    }

    public void OnLanguageChanged(int index)
    {
        string selectedLanguage = languageDropdown.options[index].text;

        switch (selectedLanguage)
        {
            case "English":
                ChangeLanguageToEnglish();
                break;
            case "Spanish":
                ChangeLanguageToSpanish();
                break;
            case "French":
                ChangeLanguageToFrench();
                break;
            case "German":
                ChangeLanguageToGerman();
                break;
            default:
                Debug.LogWarning("Selected language not handled: " + selectedLanguage);
                break;
        }
    }

    private void ChangeLanguageToEnglish()
    {
        // Example: Change in-game text to English
        Debug.Log("Language changed to English");
        // You can call your localization system here
    }

    private void ChangeLanguageToSpanish()
    {
        // Example: Change in-game text to Spanish
        Debug.Log("Language changed to Spanish");
        // You can call your localization system here
    }

    private void ChangeLanguageToFrench()
    {
        // Example: Change in-game text to French
        Debug.Log("Language changed to French");
        // You can call your localization system here
    }

    private void ChangeLanguageToGerman()
    {
        // Example: Change in-game text to German
        Debug.Log("Language changed to German");
        // You can call your localization system here
    }

    public void ResetSettings()
    {
        // Reset Display Mode
        displayModeDropdown.value = 0; // Fullscreen mode as default

        // Reset Resolution
        resolutionDropdown.value = 0; // 1920x1080 as default

        // Reset V-Sync
        vsyncToggle.isOn = true;

        // Reset Subtitles
        subtitlesToggle.isOn = true;

        // Reset Texture Resolution
        textureResolutionDropdown.value = 0; // Max resolution

        // Reset Volume Sliders
        musicVolumeSlider.value = 0.8f;
        sfxVolumeSlider.value = 0.8f;
        voiceVolumeSlider.value = 0.8f;
        postProcessSlider.value = 0.5f; // Default weight for post-processing

        // Reset Camera Settings
        invertCameraToggle.isOn = false; // Not inverted
        cameraSensitivitySlider.value = 1f; // Default sensitivity

        // Reset Anti-Aliasing
        antiAliasingDropdown.value = 3; // 8x

        // Reset Controller Vibration
        controllerVibrationToggle.isOn = true;

        // Optionally save the settings back to PlayerPrefs if needed
        PlayerPrefs.SetInt("VSyncEnabled", vsyncToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("DisplayMode", displayModeDropdown.value);
        PlayerPrefs.SetInt("TextureResolution", textureResolutionDropdown.value);
        PlayerPrefs.SetFloat("MusicVolume", musicVolumeSlider.value);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolumeSlider.value);
        PlayerPrefs.SetFloat("VoiceVolume", voiceVolumeSlider.value);
        PlayerPrefs.SetFloat("Brightness", postProcessSlider.value);

        Debug.Log("Settings have been reset to default values.");
    }

    private void ChangeDisplayMode(int modeIndex)
    {
        Resolution currentResolution = Screen.currentResolution;
        
        switch (modeIndex)
        {
            case 0: // Fullscreen
                Screen.SetResolution(currentResolution.width, currentResolution.height, FullScreenMode.ExclusiveFullScreen);
                PlayerPrefs.SetInt("DisplayMode", 0);
                break;

            case 1: // Borderless Window
                Screen.SetResolution(currentResolution.width, currentResolution.height, FullScreenMode.FullScreenWindow);
                PlayerPrefs.SetInt("DisplayMode", 1);
                break;

            case 2: // Windowed
                int targetWidth = 1920;
                int targetHeight = 1080;
                float targetAspect = 16.0f / 9.0f;
                float windowAspect = (float)targetWidth / (float)targetHeight;
                
                if (windowAspect > targetAspect)
                {
                    targetWidth = Mathf.RoundToInt(targetHeight * targetAspect);
                }
                else
                {
                    targetHeight = Mathf.RoundToInt(targetWidth / targetAspect);
                }

                Screen.fullScreenMode = FullScreenMode.Windowed;
                Screen.SetResolution(targetWidth, targetHeight, false);
                PlayerPrefs.SetInt("DisplayMode", 2);
                break;
        }

        StartCoroutine(VerifyDisplayMode(modeIndex));
    }

    private IEnumerator VerifyDisplayMode(int requestedMode)
    {
        yield return new WaitForSeconds(0.5f);
        Debug.Log($"Current Resolution: {Screen.width}x{Screen.height}");
        Debug.Log($"Is Fullscreen: {Screen.fullScreen}");
        Debug.Log($"Requested Mode: {requestedMode}");
        Debug.Log($"Current Display Mode: {Screen.fullScreenMode}");
    }

    private void SetAABasedOnSystemSpecs()
    {
        long systemMemory = SystemInfo.systemMemorySize;
        int gpuMemory = SystemInfo.graphicsMemorySize;
        bool supportsMSAA = SystemInfo.supportsMultisampleAutoResolve;

        int recommendedAALevel;

        if (!supportsMSAA)
        {
            recommendedAALevel = 0;
        }
        else if (systemMemory >= 16000 && gpuMemory >= 6000)
        {
            recommendedAALevel = 3;
        }
        else if (systemMemory >= 8000 && gpuMemory >= 4000)
        {
            recommendedAALevel = 2;
        }
        else if (systemMemory >= 4000 && gpuMemory >= 2000)
        {
            recommendedAALevel = 1;
        }
        else
        {
            recommendedAALevel = 0;
        }

        antiAliasingDropdown.value = recommendedAALevel;
        ChangeAntiAliasing(recommendedAALevel);
    }

private void ToggleVSync(bool isOn)
{
    QualitySettings.vSyncCount = isOn ? 1 : 0;
    PlayerPrefs.SetInt("VSyncEnabled", isOn ? 1 : 0);
}

    private void ToggleSubtitles(bool isOn)
    {
        Debug.Log("Subtitles " + (isOn ? "Enabled" : "Disabled"));
    }

    public void ChangeTextureResolution(int qualityIndex)
    {
        switch (qualityIndex)
        {
            case 0:
                QualitySettings.globalTextureMipmapLimit = 0;
                break;
                
            case 1:
                QualitySettings.globalTextureMipmapLimit = 1;
                break;
                
            case 2:
                QualitySettings.globalTextureMipmapLimit = 2;
                break;
                
            case 3:
                QualitySettings.globalTextureMipmapLimit = 3;
                break;
        }
        
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
    }

    private void ChangeMusicVolume(float value)
    {
        AudioListener.volume = value;
    }

    private void ChangeSFXVolume(float value)
    {
        // Set SFX volume (you may need an AudioManager)
    }

    private void ChangeVoiceVolume(float value)
    {
        // Set Voice volume (you may need an AudioManager)
    }

    private void ChangePostProcessWeight(float value)
    {
        if (postProcessVolume != null)
        {
            postProcessVolume.weight = value;
        }
    }

    private void ToggleCameraInvert(bool isOn)
    {
        // Invert camera logic, for example, reverse Y-axis of mouse input
    }

    private void ChangeCameraSensitivity(float value)
    {
        Debug.Log("Camera sensitivity changed to: " + value);
    }

    public void ChangeAntiAliasing(int qualityIndex)
    {
        int currentQuality = QualitySettings.GetQualityLevel();
        
        switch (qualityIndex)
        {
            case 0: 
                QualitySettings.antiAliasing = 0;
                break;
            case 1:
                QualitySettings.antiAliasing = 2;
                break;
            case 2:
                QualitySettings.antiAliasing = 4;
                break;
            case 3:
                QualitySettings.antiAliasing = 8;
                break;
        }

        QualitySettings.SetQualityLevel(currentQuality, true);
        
        if (Camera.main != null)
        {
            Camera.main.allowMSAA = true;
            Camera.main.Render();
        }

        UnityEngine.Graphics.ExecuteCommandBuffer(new UnityEngine.Rendering.CommandBuffer());
    }

    private void ToggleControllerVibration(bool isOn)
    {
        // Implement controller vibration settings if needed
    }
}