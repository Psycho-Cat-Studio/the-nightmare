using UnityEngine;
using UnityEngine.SceneManagement;

// Local presentation of the server's existing capture progress; never changes match rules.
public class RoomLighting : MonoBehaviour
{
    public static float ExposureOffset { get; private set; }
    public float CandleFraction { get; private set; } = 1f;
    Room _room;
    float _ambient = 1f;
    float _baseline;
    bool _ownsAmbient;

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // RenderSettings now belongs to the new scene; don't restore the previous scene's value.
        _ownsAmbient = false;
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
        _ownsAmbient = false;
        CandleFraction = 1f;
        _ambient = 1f;
        ExposureOffset = 0f;
    }
    void Update()
    {
        var manager = GameManager.Instance;
        Room room = manager == null ? null : manager.CurrentRoom;
        var phase = manager == null ? GameManager.MatchState.None : manager.CurrentMachState;
        bool fighting = phase == GameManager.MatchState.Room1Fight || phase == GameManager.MatchState.Room2Fight || phase == GameManager.MatchState.Room3Fight;
        bool lost = phase == GameManager.MatchState.Lose;
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
        }
        float fraction = 1f;
        foreach (var area in room.CaptureAreas)
            if (area != null && area.CandleCount > 0)
                fraction = Mathf.Min(fraction, area.LitCandleCount / (float)area.CandleCount);
        CandleFraction = fraction;
        float t = 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime);
        _ambient = Mathf.Lerp(_ambient, Mathf.Lerp(0.75f, 1f, fraction), t);
        ExposureOffset = Mathf.Lerp(ExposureOffset, -0.45f * (1f - fraction), t);
        // ambientProbe's getter includes environment intensity; writing it back applies
        // that intensity again. Leave the probe owned by Unity and scale intensity only.
        RenderSettings.ambientIntensity = QualitySettings.activeColorSpace == ColorSpace.Gamma
            ? Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(_baseline) * _ambient)
            : _baseline * _ambient;
    }
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ResetLighting();
    }
}
