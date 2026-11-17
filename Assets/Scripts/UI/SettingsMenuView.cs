using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The serialized shell is shared by main menu, lobby and pause. Rows bind to existing preferences.
public class SettingsMenuView : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private RectTransform _content;
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private Button[] _tabs;
    [SerializeField] private Button _close;
    [SerializeField] private Button _keep;
    [SerializeField] private Button _revert;
    [SerializeField] private TMP_Text _confirmation;
    [SerializeField] private GameObject _confirmationRoot;
    private SettingsMenu _owner;
    private GameObject _lastSelection;
    private int _tab;
    private static readonly Color Ink = new Color(0.91f, 0.87f, 0.77f);
    private static readonly Color Surface = new Color(0.14f, 0.13f, 0.12f, 0.96f);
    private static readonly Color Accent = new Color(0.54f, 0.38f, 0.24f);

    public void Initialize(SettingsMenu owner)
    {
        _owner = owner;
        for (int i = 0; i < _tabs.Length; i++)
        {
            int tab = i;
            _tabs[i].onClick.AddListener(() => ShowTab(tab));
        }
        _close.onClick.AddListener(owner.CloseWindow);
        _keep.onClick.AddListener(owner.KeepDisplay);
        _revert.onClick.AddListener(owner.CancelDisplay);
    }
    public void ShowTab(int tab)
    {
        _tab = tab;
        foreach (Transform child in _content)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        for (int i = 0; i < _tabs.Length; i++)
        {
            var colors = _tabs[i].colors;
            colors.normalColor = i == tab ? Accent : Surface;
            _tabs[i].colors = colors;
        }
        if (tab == 0) Gameplay();
        else if (tab == 1) Display();
        else Effects();
        Canvas.ForceUpdateCanvases();
        _scroll.verticalNormalizedPosition = 1;
        _tabs[tab].Select();
        UpdateConfirmation();
        ConfigureNavigation();
    }
    public void UpdateConfirmation()
    {
        bool active = _owner.DisplayTrial;
        bool changed = _confirmationRoot.activeSelf != active;
        _confirmationRoot.SetActive(active);
        if (active) _confirmation.text = "Keep this resolution? Reverting in " + _owner.DisplaySeconds + "s";
        if (changed) ConfigureNavigation();
    }
    private void Gameplay()
    {
        var row = Row("Player name");
        var field = Box(row, "Name", 300, 44);
        var text = Label(field, GameSettings.PlayerName, 24);
        text.font = TMP_Settings.defaultFontAsset;
        Stretch(text.rectTransform, 12, 4, 12, 4);
        var input = field.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = field.GetComponent<Image>();
        input.textViewport = (RectTransform)field;
        input.textComponent = text;
        input.characterLimit = GameSettings.MAX_NAME_LENGTH;
        input.customCaretColor = true; input.caretColor = Ink;
        input.selectionColor = new Color(0.54f, 0.38f, 0.24f, 0.7f);
        input.text = GameSettings.PlayerName;
        input.onEndEdit.AddListener(value => { GameSettings.PlayerName = value; input.text = GameSettings.PlayerName; });
        Note("Player name applies when joining the next game.");
        SliderRow("Mouse sensitivity", 0.1f, 3f, GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => v.ToString("0.00") + "x");
        ToggleRow("Invert mouse Y", GameSettings.InvertY, v => GameSettings.InvertY = v);
        SliderRow("Field of view", GameSettings.MIN_FOV, GameSettings.MAX_FOV, GameSettings.FieldOfView, v => GameSettings.FieldOfView = Mathf.Round(v), v => Mathf.RoundToInt(v).ToString());
        SliderRow("Master volume", 0, 1, GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, v => Mathf.RoundToInt(v * 100) + "%");
    }
    private void Display()
    {
        int[] fps = GameSettings.MAX_FPS_OPTIONS;
        Choice("Frame limit", Array.ConvertAll(fps, n => n == 0 ? "Unlimited" : n + " FPS"),
            Mathf.Max(0, Array.IndexOf(fps, GameSettings.MaxFps)), i => GameSettings.MaxFps = fps[i]);
        Choice("Rendering quality", QualitySettings.names, GameSettings.QualityLevel, i => GameSettings.QualityLevel = i);
        Choice(_owner.Fullscreen ? "Render resolution" : "Window resolution", Array.ConvertAll(_owner.Resolutions, r => r.width + " x " + r.height), _owner.ResolutionIndex, i => _owner.ResolutionIndex = i);
        ToggleRow("Borderless fullscreen", _owner.Fullscreen, v => { _owner.Fullscreen = v; ShowTab(_tab); });
        Note("Borderless keeps the interface at desktop resolution; windowed mode resizes the game window.");
        var row = Row("");
        MakeButton(row, "Apply resolution", 300).onClick.AddListener(_owner.ApplySelectedDisplay);
    }
    private void Effects()
    {
        Choice("Anti-aliasing", new[] { "Off", "FXAA" }, GraphicsPreferences.AntiAliasing, i => GraphicsPreferences.AntiAliasing = i);
        ToggleRow("Bloom", GraphicsPreferences.Bloom, v => GraphicsPreferences.Bloom = v);
        ToggleRow("Vignette", GraphicsPreferences.Vignette, v => GraphicsPreferences.Vignette = v);
        ToggleRow("Automatic exposure", GraphicsPreferences.AutoExposure, v => { GraphicsPreferences.AutoExposure = v; ShowTab(_tab); }, SystemInfo.supportsAsyncGPUReadback);
        if (!GraphicsPreferences.AutoExposure || !SystemInfo.supportsAsyncGPUReadback)
            SliderRow("Exposure", -3, 3, GraphicsPreferences.ManualExposure, v => GraphicsPreferences.ManualExposure = v, v => v.ToString("0.0") + " EV");
        if (!SystemInfo.supportsAsyncGPUReadback) Note("Automatic exposure is unavailable on this device; manual exposure is used.");
        ToggleRow("Ambient occlusion", GraphicsPreferences.AmbientOcclusion, v => GraphicsPreferences.AmbientOcclusion = v);
        ToggleRow("Depth of field", GraphicsPreferences.DepthOfField, v => GraphicsPreferences.DepthOfField = v);
        ToggleRow("Motion blur", GraphicsPreferences.MotionBlur, v => GraphicsPreferences.MotionBlur = v);
        ToggleRow("Neutral tonemapping", GraphicsPreferences.Tonemapping, v => GraphicsPreferences.Tonemapping = v);
        ToggleRow("Chromatic aberration", GraphicsPreferences.ChromaticAberration, v => GraphicsPreferences.ChromaticAberration = v);
        ToggleRow("Dithering", GraphicsPreferences.Dithering, v => GraphicsPreferences.Dithering = v);
    }
    private RectTransform Row(string title)
    {
        var row = Rect(_content, title.Length == 0 ? "Action" : title);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16; layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true; layout.childForceExpandWidth = false;
        layout.childControlHeight = true; layout.childForceExpandHeight = false;
        row.gameObject.AddComponent<LayoutElement>().minHeight = 52;
        var label = Label(row, title, 26);
        // Nevermore lacks the hyphen; keep the decorative words and render only '-' in the UI font.
        if (title == "Anti-aliasing")
            label.text = "Anti<font=\"" + TMP_Settings.defaultFontAsset.name + "\">-</font>aliasing";
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        return row;
    }
    private void Note(string message)
    {
        var text = Label(_content, message, 19);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = new Color(0.72f, 0.69f, 0.62f);
        text.gameObject.AddComponent<LayoutElement>().minHeight = 52;
    }
    private void ToggleRow(string title, bool value, Action<bool> set, bool enabled = true)
    {
        var row = Row(title);
        var control = Box(row, title + " toggle", 300, 42);
        var label = Label(control, value ? "On" : "Off", 25, TextAlignmentOptions.Center);
        var toggle = control.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = control.GetComponent<Image>();
        toggle.isOn = value;
        toggle.interactable = enabled;
        toggle.onValueChanged.AddListener(v => { label.text = v ? "On" : "Off"; set(v); });
    }
    private void SliderRow(string title, float min, float max, float value, Action<float> set, Func<float, string> format)
    {
        var row = Row(title);
        var area = Rect(row, title + " control");
        var size = area.gameObject.AddComponent<LayoutElement>(); size.preferredWidth = 300; size.preferredHeight = 44;
        var readout = Label(area, format(value), 23, TextAlignmentOptions.MidlineRight);
        readout.font = TMP_Settings.defaultFontAsset;
        readout.rectTransform.anchorMin = new Vector2(0.72f, 0); readout.rectTransform.anchorMax = Vector2.one;
        var track = Box(area, "Slider", 0, 0);
        Stretch(track, 0, 17, 94, 17);
        var handle = Box(track, "Handle", 18, 32);
        handle.GetComponent<Image>().color = Ink;
        var slider = track.gameObject.AddComponent<Slider>();
        slider.targetGraphic = handle.GetComponent<Image>(); slider.handleRect = handle;
        slider.minValue = min; slider.maxValue = max; slider.value = value;
        slider.onValueChanged.AddListener(v => { set(v); readout.text = format(v); });
    }
    private void Choice(string title, string[] options, int selected, Action<int> set)
    {
        if (options.Length == 0) return;
        selected = Mathf.Clamp(selected, 0, options.Length - 1);
        var row = Row(title);
        var left = MakeButton(row, "<", 44);
        var text = Label(row, options[selected], 24, TextAlignmentOptions.Center);
        text.font = TMP_Settings.defaultFontAsset;
        var size = text.gameObject.AddComponent<LayoutElement>(); size.preferredWidth = 180;
        var right = MakeButton(row, ">", 44);
        Action<int> change = direction => { selected = (selected + direction + options.Length) % options.Length; text.text = options[selected]; set(selected); };
        left.onClick.AddListener(() => change(-1)); right.onClick.AddListener(() => change(1));
    }
    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Tab) || EventSystem.current == null) return;
        var controls = GetComponentsInChildren<Selectable>().Where(s => s.IsActive() && s.IsInteractable()).ToArray();
        if (controls.Length == 0) return;
        int index = Array.FindIndex(controls, s => s.gameObject == EventSystem.current.currentSelectedGameObject);
        int direction = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
        controls[(index + direction + controls.Length) % controls.Length].Select();
    }
    private void ConfigureNavigation()
    {
        var controls = GetComponentsInChildren<Selectable>().Where(s => s.IsActive() && s.IsInteractable()).ToArray();
        for (int i = 0; i < controls.Length; i++)
        {
            var previous = controls[(i + controls.Length - 1) % controls.Length];
            var next = controls[(i + 1) % controls.Length];
            // Explicit links keep arrow navigation inside this modal. Sliders retain left/right
            // value changes; Up/Down and Tab move focus between controls.
            controls[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = controls[i] is Scrollbar ? null : previous,
                selectOnDown = controls[i] is Scrollbar ? null : next,
                selectOnLeft = controls[i] is Slider ? null : previous,
                selectOnRight = controls[i] is Slider ? null : next
            };
        }
    }
    // Keep keyboard-selected controls visible without interfering with mouse-wheel scrolling.
    private void LateUpdate()
    {
        var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        if (selected == null || selected == _lastSelection) return;
        _lastSelection = selected;
        if (!selected.transform.IsChildOf(_content)) return;
        Canvas.ForceUpdateCanvases();
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_scroll.viewport, selected.transform);
        var rect = _scroll.viewport.rect;
        float delta = bounds.min.y < rect.yMin ? rect.yMin - bounds.min.y : bounds.max.y > rect.yMax ? rect.yMax - bounds.max.y : 0;
        var position = _content.anchoredPosition;
        position.y = Mathf.Clamp(position.y + delta, 0, Mathf.Max(0, _content.rect.height - rect.height));
        _content.anchoredPosition = position;
    }
    private static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    private static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }
    private static RectTransform Box(Transform parent, string name, float width, float height)
    {
        var rect = Rect(parent, name);
        rect.gameObject.AddComponent<Image>().color = Surface;
        if (width > 0) { var layout = rect.gameObject.AddComponent<LayoutElement>(); layout.preferredWidth = width; layout.preferredHeight = height; }
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private TMP_Text Label(Transform parent, string text, float size, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var rect = Rect(parent, "Label"); Stretch(rect);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font; label.text = text; label.fontSize = size; label.color = Ink;
        label.alignment = alignment; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }
    private Button MakeButton(Transform parent, string caption, float width)
    {
        var rect = Box(parent, caption, width, 46);
        var label = Label(rect, caption, 26, TextAlignmentOptions.Center);
        // Decorative Nevermore lacks these glyphs. Reuse the project's regular UI font for arrows.
        if (caption == "<" || caption == ">") label.font = TMP_Settings.defaultFontAsset;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        rect.GetComponent<Image>().color = Color.white;
        var colors = button.colors; colors.normalColor = Surface; colors.highlightedColor = Accent;
        colors.selectedColor = Accent; colors.pressedColor = new Color(0.75f, 0.58f, 0.36f); button.colors = colors;
        return button;
    }
    // Called by the editor prefab builder, not during normal gameplay.
    public void Build(TMP_FontAsset font, Sprite background, Sprite returnSprite)
    {
        _font = font;
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        var backdrop = Box(transform, "Modal background", 0, 0); Stretch(backdrop);
        backdrop.GetComponent<Image>().sprite = background; backdrop.GetComponent<Image>().color = Color.white;
        var panel = Box(transform, "Panel", 0, 0); Stretch(panel, 100, 24, 100, 24);
        panel.GetComponent<Image>().color = new Color(0.035f, 0.03f, 0.025f, 0.9f);
        var title = Label(panel, "SETTINGS", 44, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.pivot = new Vector2(0.5f, 1); title.rectTransform.sizeDelta = new Vector2(0, 70);
        var tabs = Rect(panel, "Tabs"); tabs.anchorMin = new Vector2(0, 1); tabs.anchorMax = Vector2.one;
        tabs.pivot = new Vector2(0.5f, 1); tabs.anchoredPosition = new Vector2(0, -76); tabs.sizeDelta = new Vector2(-48, 48);
        var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>(); tabLayout.spacing = 12;
        tabLayout.childAlignment = TextAnchor.MiddleCenter;
        tabLayout.childControlWidth = true; tabLayout.childForceExpandWidth = true;
        _tabs = new[] { MakeButton(tabs, "Gameplay", 200), MakeButton(tabs, "Display", 200), MakeButton(tabs, "Effects", 200) };
        var viewport = Box(panel, "Scroll viewport", 0, 0); Stretch(viewport, 28, 142, 28, 150);
        viewport.GetComponent<Image>().color = Color.clear; viewport.gameObject.AddComponent<RectMask2D>();
        _scroll = viewport.gameObject.AddComponent<ScrollRect>(); _scroll.horizontal = false; _scroll.viewport = viewport;
        _content = Rect(viewport, "Content"); _content.anchorMin = new Vector2(0, 1); _content.anchorMax = Vector2.one;
        _content.pivot = new Vector2(0.5f, 1); _content.sizeDelta = Vector2.zero;
        var rows = _content.gameObject.AddComponent<VerticalLayoutGroup>(); rows.spacing = 10; rows.padding = new RectOffset(4, 4, 4, 16);
        rows.childControlWidth = true; rows.childForceExpandWidth = true; rows.childControlHeight = true; rows.childForceExpandHeight = false;
        _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; _scroll.content = _content;
        var bar = Box(panel, "Scrollbar", 0, 0);
        bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one;
        bar.offsetMin = new Vector2(-22, 150); bar.offsetMax = new Vector2(-12, -142);
        var handle = Box(bar, "Handle", 0, 0); Stretch(handle); handle.GetComponent<Image>().color = Ink;
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
        _scroll.verticalScrollbar = scrollbar; _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        var footer = Rect(panel, "Display confirmation"); footer.anchorMin = Vector2.zero; footer.anchorMax = new Vector2(1, 0);
        footer.pivot = new Vector2(0.5f, 0); footer.anchoredPosition = new Vector2(0, 70); footer.sizeDelta = new Vector2(-56, 76);
        _confirmationRoot = footer.gameObject;
        _confirmation = Label(footer, "Keep this resolution?", 23, TextAlignmentOptions.Center);
        _confirmation.rectTransform.anchorMin = new Vector2(0, 0.5f);
        var actions = Rect(footer, "Actions"); Stretch(actions); actions.anchorMax = new Vector2(1, 0.5f);
        var actionLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>(); actionLayout.spacing = 16; actionLayout.childAlignment = TextAnchor.MiddleCenter; actionLayout.childForceExpandWidth = false;
        _keep = MakeButton(actions, "Keep", 160); _revert = MakeButton(actions, "Revert", 160);
        _confirmationRoot.SetActive(false);
        var close = Rect(panel, "Close area"); close.anchorMin = new Vector2(0.5f, 0); close.anchorMax = close.anchorMin; close.anchoredPosition = new Vector2(0, 32); close.sizeDelta = new Vector2(180, 52);
        _close = MakeButton(close, "Back", 180); Stretch((RectTransform)_close.transform);
        if (returnSprite != null)
        {
            _close.image.sprite = returnSprite; _close.image.preserveAspect = true;
            _close.GetComponentInChildren<TMP_Text>().text = "Back";
            var colors = _close.colors; colors.normalColor = Color.white; colors.highlightedColor = Ink; colors.selectedColor = Ink; _close.colors = colors;
        }
    }
}
