using UnityEngine;

/// <summary>Keeps crosshair geometry legible across screen sizes while applying weapon spread.</summary>
public class AimScaler : MonoBehaviour
{
    private Transform _parent;
    private Vector3 _scale;
    private RectTransform _rect;
    private Canvas _canvas;
    private const float MinimumThicknessPixels = 2f;

    void Start()
    {
        _scale = transform.localScale;
        _parent = transform.parent;
        _rect = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
    }

    void LateUpdate()
    {
        // PlayerShoot updates spread in Update; keep line size independent of that spread.
        Vector3 size = new Vector3(_scale.x / _parent.localScale.x,
            _scale.y / _parent.localScale.y, _scale.z / _parent.localScale.z);
        transform.localScale = size;
        if (_rect == null || _canvas == null || _canvas.renderMode != RenderMode.ScreenSpaceOverlay) return;

        // Overlay world units are screen pixels. Thin subpixel quads can disappear at 720p.
        float width = Mathf.Abs(_rect.rect.width * _rect.lossyScale.x);
        float height = Mathf.Abs(_rect.rect.height * _rect.lossyScale.y);
        if (width <= 0f || height <= 0f) return;
        if (width < height) size.x *= Mathf.Max(1f, MinimumThicknessPixels / width);
        else size.y *= Mathf.Max(1f, MinimumThicknessPixels / height);
        transform.localScale = size;
    }
}
