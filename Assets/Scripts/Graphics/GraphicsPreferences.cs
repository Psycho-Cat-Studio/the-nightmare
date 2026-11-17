using UnityEngine;

// Preferences are per player. Runtime rendering always works on cloned assets.
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
    static bool Flag(string key, bool fallback) { return PlayerPrefs.GetInt("graphics." + key, fallback ? 1 : 0) != 0; }
    static void Flag(string key, bool value, bool fallback)
    {
        if (Flag(key, fallback) == value) return;
        PlayerPrefs.SetInt("graphics." + key, value ? 1 : 0);
        Changed();
    }
    public static void Changed() { Revision++; }
    public static bool Bloom { get { return Flag("bloom", true); } set { Flag("bloom", value, true); } }
    public static bool Vignette { get { return Flag("vignette", true); } set { Flag("vignette", value, true); } }
    public static bool AutoExposure { get { return Flag("autoExposure", true); } set { Flag("autoExposure", value, true); } }
    public static bool DepthOfField { get { return Flag("depthOfField", false); } set { Flag("depthOfField", value, false); } }
    public static bool MotionBlur { get { return Flag("motionBlur", false); } set { Flag("motionBlur", value, false); } }
    // Keep the existing preference key; this switch controls Neutral tonemapping, not a grading LUT.
    public static bool Tonemapping { get { return Flag("colorGrading", false); } set { Flag("colorGrading", value, false); } }
    public static bool ChromaticAberration { get { return Flag("chromatic", false); } set { Flag("chromatic", value, false); } }
    public static bool Dithering { get { return Flag("dithering", false); } set { Flag("dithering", value, false); } }
    public static bool AmbientOcclusion { get { return Flag("ambientOcclusion", false); } set { Flag("ambientOcclusion", value, false); } }
    public static int AntiAliasing
    {
        // Old TAA selections become FXAA: URP TAA is incompatible with our camera stack.
        get { return Mathf.Clamp(PlayerPrefs.GetInt("graphics.aa", 1), 0, 1); }
        set { if (AntiAliasing != value) { PlayerPrefs.SetInt("graphics.aa", Mathf.Clamp(value, 0, 1)); Changed(); } }
    }
    public static float ManualExposure
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat("graphics.exposure", 1f), -3f, 3f); }
        set { if (!Mathf.Approximately(ManualExposure, value)) { PlayerPrefs.SetFloat("graphics.exposure", Mathf.Clamp(value, -3f, 3f)); Changed(); } }
    }
    public static void SaveDisplay(int width, int height, FullScreenMode mode)
    {
        PlayerPrefs.SetInt("display.width", width);
        PlayerPrefs.SetInt("display.height", height);
        PlayerPrefs.SetInt("display.mode", (int)mode);
        PlayerPrefs.SetInt("display.renderWidth", Mathf.RoundToInt(width * RenderScale));
        PlayerPrefs.SetInt("display.renderHeight", Mathf.RoundToInt(height * RenderScale));
        PlayerPrefs.Save();
    }
    public static void ApplySavedDisplay()
    {
        if (!PlayerPrefs.HasKey("display.width") || !PlayerPrefs.HasKey("display.height")) return;
        int width = Mathf.Clamp(PlayerPrefs.GetInt("display.width"), 640, 16384);
        int height = Mathf.Clamp(PlayerPrefs.GetInt("display.height"), 360, 16384);
        // A confirmed mode from another monitor must not reopen a window larger than this display.
        float scale = Mathf.Min(1f, Mathf.Max(640, Screen.currentResolution.width) / (float)width,
            Mathf.Max(360, Screen.currentResolution.height) / (float)height);
        width = Mathf.Max(640, Mathf.RoundToInt(width * scale));
        height = Mathf.Max(360, Mathf.RoundToInt(height * scale));
        var mode = (FullScreenMode)PlayerPrefs.GetInt("display.mode", (int)FullScreenMode.FullScreenWindow);
        if (mode != FullScreenMode.Windowed) mode = FullScreenMode.FullScreenWindow;
        if (mode == FullScreenMode.FullScreenWindow)
        {
            width = Screen.currentResolution.width;
            height = Screen.currentResolution.height;
            SetRenderScale(Mathf.Min(PlayerPrefs.GetInt("display.renderWidth", width) / (float)width,
                PlayerPrefs.GetInt("display.renderHeight", height) / (float)height));
        }
        else SetRenderScale(1f);
        Screen.SetResolution(width, height, mode);
    }
}
