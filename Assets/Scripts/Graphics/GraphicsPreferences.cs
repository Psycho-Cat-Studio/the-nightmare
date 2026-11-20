using UnityEngine;

// Preferences are per player. Runtime rendering always works on cloned assets.
/// <summary>Reads and writes per-player effects and confirmed display preferences while tracking runtime changes.</summary>
public static class GraphicsPreferences
{
    // Runtime display trial; written to preferences only after Keep.
    public static float RenderScale { get; private set; } = 1f;
    public static void SetRenderScale(float scale)
    {
        RenderScale = Mathf.Clamp(scale, 0.1f, 1f);
        Changed();
    }
    public static int Revision { get; private set; }
    static bool Flag(string key, bool fallback) { return PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0; }
    static void Flag(string key, bool value, bool fallback)
    {
        if (Flag(key, fallback) == value) return;
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        Changed();
    }
    public static void Changed() { Revision++; }
    public static bool Bloom { get { return Flag(GraphicsPreferenceKeys.Bloom, true); } set { Flag(GraphicsPreferenceKeys.Bloom, value, true); } }
    public static bool Vignette { get { return Flag(GraphicsPreferenceKeys.Vignette, true); } set { Flag(GraphicsPreferenceKeys.Vignette, value, true); } }
    public static bool AutoExposure { get { return Flag(GraphicsPreferenceKeys.AutoExposure, true); } set { Flag(GraphicsPreferenceKeys.AutoExposure, value, true); } }
    public static bool DepthOfField { get { return Flag(GraphicsPreferenceKeys.DepthOfField, false); } set { Flag(GraphicsPreferenceKeys.DepthOfField, value, false); } }
    public static bool MotionBlur { get { return Flag(GraphicsPreferenceKeys.MotionBlur, false); } set { Flag(GraphicsPreferenceKeys.MotionBlur, value, false); } }
    // Keep the existing preference key; this switch controls Neutral tonemapping, not a grading LUT.
    public static bool Tonemapping { get { return Flag(GraphicsPreferenceKeys.Tonemapping, false); } set { Flag(GraphicsPreferenceKeys.Tonemapping, value, false); } }
    public static bool ChromaticAberration { get { return Flag(GraphicsPreferenceKeys.Chromatic, false); } set { Flag(GraphicsPreferenceKeys.Chromatic, value, false); } }
    public static bool Dithering { get { return Flag(GraphicsPreferenceKeys.Dithering, false); } set { Flag(GraphicsPreferenceKeys.Dithering, value, false); } }
    public static bool AmbientOcclusion { get { return Flag(GraphicsPreferenceKeys.AmbientOcclusion, false); } set { Flag(GraphicsPreferenceKeys.AmbientOcclusion, value, false); } }
    public static int AntiAliasing
    {
        // Old TAA selections become FXAA: URP TAA is incompatible with our camera stack.
        get { return Mathf.Clamp(PlayerPrefs.GetInt(GraphicsPreferenceKeys.AntiAliasing, 1), 0, 1); }
        set { if (AntiAliasing != value) { PlayerPrefs.SetInt(GraphicsPreferenceKeys.AntiAliasing, Mathf.Clamp(value, 0, 1)); Changed(); } }
    }
    public static float ManualExposure
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat(GraphicsPreferenceKeys.Exposure, 1f), -3f, 3f); }
        set { if (!Mathf.Approximately(ManualExposure, value)) { PlayerPrefs.SetFloat(GraphicsPreferenceKeys.Exposure, Mathf.Clamp(value, -3f, 3f)); Changed(); } }
    }
    public static void SaveDisplay(int width, int height, FullScreenMode mode)
    {
        PlayerPrefs.SetInt(GraphicsPreferenceKeys.DisplayWidth, width);
        PlayerPrefs.SetInt(GraphicsPreferenceKeys.DisplayHeight, height);
        PlayerPrefs.SetInt(GraphicsPreferenceKeys.DisplayMode, (int)mode);
        PlayerPrefs.SetInt(GraphicsPreferenceKeys.RenderWidth, Mathf.RoundToInt(width * RenderScale));
        PlayerPrefs.SetInt(GraphicsPreferenceKeys.RenderHeight, Mathf.RoundToInt(height * RenderScale));
        PlayerPrefs.Save();
    }
    public static void ApplySavedDisplay()
    {
        if (!PlayerPrefs.HasKey(GraphicsPreferenceKeys.DisplayWidth) || !PlayerPrefs.HasKey(GraphicsPreferenceKeys.DisplayHeight)) return;
        int width = Mathf.Clamp(PlayerPrefs.GetInt(GraphicsPreferenceKeys.DisplayWidth), 640, 16384);
        int height = Mathf.Clamp(PlayerPrefs.GetInt(GraphicsPreferenceKeys.DisplayHeight), 360, 16384);
        // A confirmed mode from another monitor must not reopen a window larger than this display.
        float scale = Mathf.Min(1f, Mathf.Max(640, Screen.currentResolution.width) / (float)width,
            Mathf.Max(360, Screen.currentResolution.height) / (float)height);
        width = Mathf.Max(640, Mathf.RoundToInt(width * scale));
        height = Mathf.Max(360, Mathf.RoundToInt(height * scale));
        var mode = (FullScreenMode)PlayerPrefs.GetInt(GraphicsPreferenceKeys.DisplayMode, (int)FullScreenMode.FullScreenWindow);
        if (mode != FullScreenMode.Windowed) mode = FullScreenMode.FullScreenWindow;
        if (mode == FullScreenMode.FullScreenWindow)
        {
            width = Screen.currentResolution.width;
            height = Screen.currentResolution.height;
            SetRenderScale(Mathf.Min(PlayerPrefs.GetInt(GraphicsPreferenceKeys.RenderWidth, width) / (float)width,
                PlayerPrefs.GetInt(GraphicsPreferenceKeys.RenderHeight, height) / (float)height));
        }
        else SetRenderScale(1f);
        Screen.SetResolution(width, height, mode);
    }
}
