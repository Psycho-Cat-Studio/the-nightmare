// Persisted identifiers: changing a value requires a saved-preference migration.
/// <summary>Defines persisted graphics and display keys whose values must remain compatible with saved preferences.</summary>
internal static class GraphicsPreferenceKeys
{
    public const string Bloom = "graphics.bloom";
    public const string Vignette = "graphics.vignette";
    public const string AutoExposure = "graphics.autoExposure";
    public const string DepthOfField = "graphics.depthOfField";
    public const string MotionBlur = "graphics.motionBlur";
    public const string Tonemapping = "graphics.colorGrading";
    public const string Chromatic = "graphics.chromatic";
    public const string Dithering = "graphics.dithering";
    public const string AmbientOcclusion = "graphics.ambientOcclusion";
    public const string AntiAliasing = "graphics.aa";
    public const string Exposure = "graphics.exposure";
    public const string DisplayWidth = "display.width";
    public const string DisplayHeight = "display.height";
    public const string DisplayMode = "display.mode";
    public const string RenderWidth = "display.renderWidth";
    public const string RenderHeight = "display.renderHeight";
}
