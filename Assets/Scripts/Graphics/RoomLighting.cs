using UnityEngine;
using UnityEngine.SceneManagement;

// Local presentation of the server's existing capture progress; never changes match rules.
/// <summary>Derives local ambient, exposure and fog darkening from candle state and restores scene baselines between rooms.</summary>
public class RoomLighting : MonoBehaviour, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    public static float ExposureOffset { get; private set; }
    public float CandleFraction { get; private set; } = 1f;
    const float TransitionRate = 3f;
    const float MinimumAmbientMultiplier = 0.75f;
    const float MaximumExposureReduction = 0.45f;
    const float MaximumFogDensity = 0.12f;
    Room _room;
    float _ambient = 1f;
    float _baseline;
    bool _ownsAmbient;
    bool _ownsFog;
    Color _fogColor;
    float _fogDensity;
    static readonly Color DarkFogColor = new Color(0.008f, 0.008f, 0.012f, 1f);

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // RenderSettings now belongs to the new scene; don't restore the previous scene's value.
        _ownsAmbient = false;
        _ownsFog = false;
        _room = null;
        ExposureOffset = 0f;
        CandleFraction = 1f;
        _ambient = 1f;
    }
    void ResetLighting()
    {
        if (_ownsAmbient)
        {
            RenderSettings.ambientIntensity = _baseline;
        }
        if (_ownsFog)
        {
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogDensity = _fogDensity;
        }
        _ownsFog = false;
        _ownsAmbient = false;
        CandleFraction = 1f;
        _ambient = 1f;
        ExposureOffset = 0f;
    }
    void Update()
    {
        var manager = _game;
        Room room = manager == null ? null : manager.World.CurrentRoom;
        var phase = manager == null ? Nightmare.Core.Match.MatchPhase.None : manager.Phase;
        bool fighting = phase == Nightmare.Core.Match.MatchPhase.Defend;
        bool lost = phase == Nightmare.Core.Match.MatchPhase.Lost;
        if (room != _room || (!fighting && !lost))
        {
            ResetLighting();
            _room = room;
        }
        if ((!fighting && !lost) || room == null) return;
        if (!_ownsAmbient)
        {
            _baseline = RenderSettings.ambientIntensity;
            _ownsAmbient = true;
            // Only extend the scene's existing Exp2 fog; menus and other fog modes stay authored.
            _ownsFog = RenderSettings.fog && RenderSettings.fogMode == FogMode.ExponentialSquared;
            _fogColor = RenderSettings.fogColor;
            _fogDensity = RenderSettings.fogDensity;
        }
        float fraction = 1f;
        foreach (var area in room.CaptureAreas)
            if (area != null && area.CandleCount > 0)
                fraction = Mathf.Min(fraction, area.LitCandleCount / (float)area.CandleCount);
        CandleFraction = fraction;
        float t = 1f - Mathf.Exp(-TransitionRate * Time.unscaledDeltaTime);
        // Ease out: half the candles lost gives 75% of the mood, with the same endpoints.
        float candleDarkness = 1f - fraction * fraction;
        _ambient = Mathf.Lerp(_ambient, Mathf.Lerp(1f, MinimumAmbientMultiplier, candleDarkness), t);
        ExposureOffset = Mathf.Lerp(ExposureOffset, -MaximumExposureReduction * candleDarkness, t);
        // ambientProbe's getter includes environment intensity; writing it back applies
        // that intensity again. Leave the probe owned by Unity and scale intensity only.
        RenderSettings.ambientIntensity = QualitySettings.activeColorSpace == ColorSpace.Gamma
            ? Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(_baseline) * _ambient)
            : _baseline * _ambient;
        if (_ownsFog)
        {
            float darkness = Mathf.Clamp01((1f - _ambient) / (1f - MinimumAmbientMultiplier));
            RenderSettings.fogColor = Color.Lerp(_fogColor, DarkFogColor, darkness);
            RenderSettings.fogDensity = Mathf.Lerp(_fogDensity, Mathf.Max(_fogDensity, MaximumFogDensity), darkness);
        }
    }
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ResetLighting();
    }
}
