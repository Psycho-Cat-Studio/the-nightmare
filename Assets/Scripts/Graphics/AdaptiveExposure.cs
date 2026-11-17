using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// PPSv1-like eye adaptation. Meter an unprocessed, world-only camera twice a second; no HUD/weapon
// pixels or feedback from the current exposure enter the luminance measurement. Readback is async.
public class AdaptiveExposure : MonoBehaviour
{
    Camera _meter;
    RenderTexture _target;
    bool _pending;
    float _targetExposure = 1;
    RuntimeGraphics _owner;
    void Awake() { _owner = GetComponent<RuntimeGraphics>(); }
    IEnumerator Start()
    {
        var wait = new WaitForSecondsRealtime(0.5f);
        while (true)
        {
            yield return wait;
            if (!GraphicsPreferences.AutoExposure || !SystemInfo.supportsAsyncGPUReadback || _pending) continue;
            var local = GameManager.LocalPlayer;
            if (local == null) continue;
            var source = local.GetComponent<PlayerSetup>().ActionCamera;
            if (source == null || !source.isActiveAndEnabled) continue;
            if (_meter == null)
            {
                var go = new GameObject("Exposure Meter");
                go.transform.SetParent(transform);
                _meter = go.AddComponent<Camera>();
                _meter.enabled = false;
                _target = new RenderTexture(32, 32, 16, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                _target.Create();
            }
            _meter.CopyFrom(source);
            _meter.enabled = false;
            _meter.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            _meter.aspect = source.aspect;
            _meter.targetTexture = _target;
            _meter.ResetProjectionMatrix();
            var data = UrpAccess.CameraData(_meter);
            UrpAccess.Set(data, "renderType", "Base");
            data.GetType().GetMethod("SetRenderer").Invoke(data, new object[] { 0 });
            UrpAccess.Set(data, "volumeLayerMask", (LayerMask)0);
            UrpAccess.Set(data, "renderPostProcessing", false);
            UrpAccess.Set(data, "antialiasing", "None");
            _meter.Render();
            _pending = true;
            AsyncGPUReadback.Request(_target, 0, TextureFormat.RGBAFloat, OnLuminance);
        }
    }
    void OnLuminance(AsyncGPUReadbackRequest request)
    {
        _pending = false;
        if (this == null || request.hasError) return;
        var pixels = request.GetData<Color>();
        var luminances = new float[pixels.Length];
        int index = 0;
        foreach (var pixel in pixels)
        {
            // PPSv1's histogram deliberately used Max3 rather than Rec.709 luminance.
            float luminance = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
            luminances[index++] = Mathf.Max(0.0001f, luminance);
        }
        System.Array.Sort(luminances);
        int low = Mathf.FloorToInt(luminances.Length * 0.45f);
        int high = Mathf.Max(low + 1, Mathf.FloorToInt(luminances.Length * 0.95f));
        double sum = 0;
        for (int i = low; i < high; i++) sum += luminances[i];
        float average = (float)(sum / (high - low));
        average = Mathf.Clamp(average, Mathf.Pow(2, -5), Mathf.Pow(2, 1));
        // The original Light/Ultra profiles enabled AUTO_KEY_VALUE (EyeAdaptation.shader).
        float key = 1.03f - 2f / (2f + Mathf.Log(average + 1f, 2));
        _targetExposure = Mathf.Log(key / average, 2);
    }
    void Update()
    {
        float current = Mathf.Pow(2, _owner.Exposure);
        float target = Mathf.Pow(2, _targetExposure);
        float speed = target < current ? 2f : 1f;
        float exposure = Mathf.Lerp(current, target, 1 - Mathf.Pow(2, -speed * Time.unscaledDeltaTime));
        _owner.Exposure = Mathf.Log(Mathf.Max(0.0001f, exposure), 2);
    }
    void OnDestroy()
    {
        if (_target != null) { _target.Release(); Destroy(_target); }
        // Meter is a child and is destroyed with its owner.
    }
}
