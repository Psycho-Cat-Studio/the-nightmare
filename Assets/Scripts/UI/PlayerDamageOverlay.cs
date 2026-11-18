using UnityEngine;

// Local presentation of replicated health; no additional damage RPCs or render passes.
public class PlayerDamageOverlay : UnityEngine.UI.MaskableGraphic
{
    HealthBar _healthBar;
    PlayerManager _player;
    float _previousHealth;
    float _opacity;
    float _lastDamageAt;
    float _pulseStrength;

    public static void Create(HealthBar healthBar)
    {
        var canvas = healthBar.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var go = new GameObject("PlayerDamageOverlay", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(canvas.rootCanvas.transform, false);
        go.transform.SetAsLastSibling();
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var overlay = go.AddComponent<PlayerDamageOverlay>();
        overlay._healthBar = healthBar;
        overlay.raycastTarget = false;
        overlay.color = new Color(0.8f, 0.015f, 0.025f, 1f);
        overlay.canvasRenderer.SetAlpha(0f);
    }

    void Update()
    {
        var player = _healthBar != null && _healthBar.playerEnabled ? _healthBar.player : null;
        if (player != _player)
        {
            _player = player;
            _previousHealth = player != null ? player._currentHealth : 0f;
            _opacity = 0f;
        }
        bool hidden = player == null || player.IsDead || !_healthBar.isActiveAndEnabled
            || PauseGame.menuActive || SettingsMenu.IsOpen || WinLoseScreens.winLoseActive
            || !ButtonsControll.screensOver;
        if (hidden)
            _opacity = 0f;
        else if (player._currentHealth < _previousHealth - 0.0001f)
        {
            float danger = 1f - Mathf.Clamp01(player._currentHealth / Mathf.Max(1f, player._maxHealth));
            _pulseStrength = 0.04f + 0.66f * danger * danger;
            _opacity = _pulseStrength;
            _lastDamageAt = Time.unscaledTime;
        }
        else if (player._currentHealth > _previousHealth)
            _opacity = 0f;
        else if (Time.unscaledTime - _lastDamageAt > 0.12f)
            _opacity = Mathf.MoveTowards(_opacity, 0f, _pulseStrength * Time.unscaledDeltaTime / 0.55f);

        if (player != null) _previousHealth = player._currentHealth;
        // CanvasRenderer alpha avoids rebuilding the mesh every fade frame.
        canvasRenderer.SetAlpha(_opacity);
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        var rect = GetPixelAdjustedRect();
        const int columns = 24, rows = 16;
        for (int y = 0; y <= rows; y++)
        {
            for (int x = 0; x <= columns; x++)
            {
                float u = x / (float)columns, v = y / (float)rows;
                float radius = new Vector2(u * 2f - 1f, v * 2f - 1f).magnitude;
                var tint = color;
                tint.a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, radius));
                vh.AddVert(new Vector3(rect.xMin + u * rect.width, rect.yMin + v * rect.height), tint, Vector2.zero);
            }
        }
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                int i = y * (columns + 1) + x;
                vh.AddTriangle(i, i + columns + 1, i + 1);
                vh.AddTriangle(i + 1, i + columns + 1, i + columns + 2);
            }
        }
    }

    protected override void OnDisable()
    {
        _opacity = 0f;
        canvasRenderer.SetAlpha(0f);
        base.OnDisable();
    }
}
