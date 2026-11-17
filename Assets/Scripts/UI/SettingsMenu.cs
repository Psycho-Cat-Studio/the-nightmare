using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Persistent controller; the uGUI view is a reusable prefab with the game's menu artwork/font.
public class SettingsMenu : MonoBehaviour
{
    public static bool IsOpen { get { return _instance != null && _instance._open; } }
    private static SettingsMenu _instance;
    private static int _escapeFrame = -1;
    private bool _open;
    private SettingsMenuView _view;
    private GameObject _previousSelection;
    private Resolution[] _resolutions;
    private int _resolutionIndex;
    private bool _fullscreen;
    private bool _displayTrial;
    private float _displayDeadline;
    private int _oldWidth, _oldHeight;
    private FullScreenMode _oldMode;
    public Resolution[] Resolutions { get { return _resolutions; } }
    public int ResolutionIndex { get { return _resolutionIndex; } set { _resolutionIndex = value; } }
    public bool Fullscreen { get { return _fullscreen; } set { _fullscreen = value; } }
    public bool DisplayTrial { get { return _displayTrial; } }
    public int DisplaySeconds { get { return Mathf.Max(0, Mathf.CeilToInt(_displayDeadline - Time.realtimeSinceStartup)); } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (_instance != null) return;
        var go = new GameObject("SettingsMenu");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SettingsMenu>();
    }
    private void OnEnable()
    {
        _instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddMenuButtons();
    }
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        CloseWindow();
    }
    private void OnApplicationFocus(bool focused) { if (!focused) GameSettings.Save(); }
    private void OnApplicationQuit() { GameSettings.Save(); }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CloseWindow();
        AddMenuButtons();
    }
    private void AddMenuButtons()
    {
        var sprite = Resources.Load<Sprite>("UI/settingsButton");
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            var credits = GameObject.Find("CreditsButton");
            var quit = GameObject.Find("QuitButton");
            if (credits == null || quit == null || sprite == null || GameObject.Find("SettingsButton") != null) return;
            var settings = Instantiate(credits, credits.transform.parent, false);
            settings.name = "SettingsButton";
            ((RectTransform)settings.transform).anchoredPosition = ((RectTransform)quit.transform).anchoredPosition;
            ((RectTransform)quit.transform).anchoredPosition += Vector2.down * 190f;
            var button = settings.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(OpenWindow);
            button.transition = Selectable.Transition.ColorTint;
            foreach (Image image in settings.GetComponentsInChildren<Image>(true))
                if (image.sprite != null && image.sprite.name.ToLowerInvariant().Contains("credit")) image.sprite = sprite;
            button.image.sprite = sprite;
            return;
        }
        // PauseMenu can be inactive when the controller is recreated or a scene is loaded.
        var pause = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "PauseMenu" && g.scene.IsValid());
        if (pause == null || pause.transform.Find("SettingsButton") != null) return;
        var go = new GameObject("SettingsButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(pause.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(230f, 64f);
        rect.anchoredPosition = new Vector2(0, -230);
        go.GetComponent<Image>().sprite = sprite;
        go.GetComponent<Image>().preserveAspect = true;
        var pauseButton = go.GetComponent<Button>();
        pauseButton.targetGraphic = go.GetComponent<Image>();
        pauseButton.onClick.AddListener(OpenWindow);
    }
    private bool CanOpen
    {
        get
        {
            string scene = SceneManager.GetActiveScene().name;
            return scene == "MainMenu" || scene == "LobbyTests" || PauseGame.menuActive;
        }
    }
    public void OpenWindow()
    {
        if (!CanOpen || _open) return;
        if (_view == null)
        {
            var prefab = Resources.Load<SettingsMenuView>("UI/SettingsPanel");
            if (prefab == null) { Debug.LogError("Missing UI/SettingsPanel prefab"); return; }
            _view = Instantiate(prefab, transform, false);
            _view.Initialize(this);
        }
        _previousSelection = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        _open = true;
        if (GameManager.LocalPlayer != null) PauseGame.menuActive = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        var available = Screen.resolutions.ToList();
        available.Add(new Resolution { width = Screen.width, height = Screen.height });
        int maxWidth = Mathf.Max(Screen.currentResolution.width, Screen.width);
        int maxHeight = Mathf.Max(Screen.currentResolution.height, Screen.height);
        foreach (var size in new[] { new Vector2Int(640,360), new Vector2Int(854,480), new Vector2Int(960,540),
            new Vector2Int(1024,576), new Vector2Int(1280,720), new Vector2Int(1280,800), new Vector2Int(1600,900), new Vector2Int(1920,1080) })
            if (size.x <= maxWidth && size.y <= maxHeight) available.Add(new Resolution { width = size.x, height = size.y });
        _resolutions = available.Where(r => r.width > 0 && r.height > 0)
            .GroupBy(r => new { r.width, r.height }).Select(g => g.First()).OrderBy(r => r.width).ThenBy(r => r.height).ToArray();
        _resolutionIndex = System.Array.FindIndex(_resolutions, r => r.width == Screen.width && r.height == Screen.height);
        if (_resolutionIndex < 0) _resolutionIndex = 0;
        _fullscreen = Screen.fullScreen;
        _view.gameObject.SetActive(true);
        _view.ShowTab(0);
    }
    public void CloseWindow()
    {
        RevertDisplay();
        _open = false;
        if (_view != null) _view.gameObject.SetActive(false);
        if (_previousSelection != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_previousSelection);
        GameSettings.Save();
    }
    // Both Update and PauseGame call this; consumption is independent of script execution order.
    public static bool ConsumeEscape()
    {
        if (_escapeFrame == Time.frameCount) return true;
        if (!IsOpen) return false;
        _escapeFrame = Time.frameCount;
        _instance.CloseWindow();
        return true;
    }
    private void RevertDisplay()
    {
        if (!_displayTrial) return;
        Screen.SetResolution(_oldWidth, _oldHeight, _oldMode);
        _displayTrial = false;
    }
    private void ConfirmDisplay()
    {
        if (!_displayTrial) return;
        GraphicsPreferences.SaveDisplay(Screen.width, Screen.height, Screen.fullScreenMode);
        _displayTrial = false;
    }
    private void ApplyDisplay(int width, int height, bool fullscreen)
    {
        if (!_displayTrial)
        {
            _oldWidth = Screen.width; _oldHeight = Screen.height; _oldMode = Screen.fullScreenMode;
            // A quit/crash during an unconfirmed first trial must not become the next launch's resolution.
            if (!PlayerPrefs.HasKey("display.width")) GraphicsPreferences.SaveDisplay(_oldWidth, _oldHeight, _oldMode);
        }
        _displayTrial = true;
        _displayDeadline = Time.realtimeSinceStartup + 15;
        Screen.SetResolution(width, height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    public void ApplySelectedDisplay()
    {
        Resolution resolution = _resolutions[_resolutionIndex];
        ApplyDisplay(resolution.width, resolution.height, _fullscreen);
    }
    public void KeepDisplay() { ConfirmDisplay(); }
    public void CancelDisplay() { RevertDisplay(); }
    private void Update()
    {
        if (_displayTrial && Time.realtimeSinceStartup >= _displayDeadline) RevertDisplay();
        if (_open && !CanOpen) CloseWindow();
        if (Input.GetKeyDown(KeyCode.Escape)) ConsumeEscape();
        if (_open && _view != null) _view.UpdateConfirmation();
    }
    private void OnDestroy() { if (_instance == this) _instance = null; }
}
