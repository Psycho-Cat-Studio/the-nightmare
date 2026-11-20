using System.Collections.Generic;
using TMPro;
using Nightmare.Core.World;
using UnityEngine;

/// <summary>Coordinates local placement input, preview feedback and accepted or rejected inventory updates.</summary>
public class PlacementController : MonoBehaviour, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    private IPlacementRequests _requests;
    private PlayerAuthority _authority;
    public bool isLocalPlayer => _authority != null && _authority.IsLocalPlayer;
    public void InitializePlacement(IPlacementRequests requests) { _requests = requests; }
    public IReadOnlyList<GameObject> PlaceablePrefabs => _placeableObject;
    void Awake() { _authority = GetComponent<PlayerAuthority>(); }
    public static float GridTileSize = 1f;
    private const float PlacementRadiusInTiles = 2f;
    private const float PreviewRayDistance = 100f;
    [SerializeField] private List<GameObject> _placeableObject;
    [SerializeField] private List<GameObject> _placeableObjectModels;
    private float _mouseWheelRotation;
    private float _x = 0, _y = 0, _reverseGrid, _camMinZoom, _camMaxZoom;
    private GameObject _currentObject;
    private GridPoint _previewPoint;
    private bool _placementPending;
    [SerializeField] private Material _invalidPreviewMaterial;
    private Renderer[] _previewRenderers;
    private Material[][] _previewMaterials;
    private Material[][] _invalidMaterials;
    private float _invalidUntil;

    private string _currentTag;
    private Transform _buildingCameraHolder;
    private Camera _buildingCamera;
    private Camera _actionCamera;
    private Camera _currentCamera;
    [SerializeField] private float _scrollBorderThickness = 0.005f;  // Legacy pixel threshold, despite the original percentage description.
    [SerializeField] private float _moveSpeedMinZoom = 30f;
    [SerializeField] private float _moveSpeedMaxZoom = 30f;
    [SerializeField] private float _rotationSpeedKeyboard = 150f;
    private static float _zoom = 1f;
    private int _placeableIndex = 0;
    private bool _isPlacing = false;
    public int[] maxPlaceable = {3, 1, 3};
    public int[] placeableCount = new int[3];
    private TextMeshProUGUI[] _placeableTM = new TextMeshProUGUI[3];
    public const int INDEX_OF_SNARES = 0;
    public const int INDEX_OF_BEAR = 1;
    public const int INDEX_OF_BARREL = 2;

    private PlayerShoot _playerShoot;
    private PlayerManager _playerManager;

    private AudioSource _source;
    [SerializeField] private AudioClip bop;
    [SerializeField] private AudioClip nono;

    public GameObject CurrentObject
    {
        get => _currentObject;
        set => _currentObject = value;
    }

    public bool hasNoPlaceable()
    {
        foreach (int i in placeableCount)
        {
            if (i != 0) return false;
        }
        return true;
    }

    public void InstantietePlaceable(int index)
    {
        if (_placementPending) return;
        _placeableIndex = index;
        UpdatePlaceable();

    }

    private void Start()
    {
        _source = GetComponent<AudioSource>();
        var content = _game.Content;
        if (content != null && content.Placeables != null)
        {
            _placeableObject = new List<GameObject>();
            _placeableObjectModels = new List<GameObject>();
            maxPlaceable = new int[content.Placeables.Length];
            if (placeableCount.Length != maxPlaceable.Length) System.Array.Resize(ref placeableCount, maxPlaceable.Length);
            for (int i = 0; i < content.Placeables.Length; i++)
            {
                var item = content.Placeables[i];
                _placeableObject.Add(item.Prefab); _placeableObjectModels.Add(item.PreviewPrefab); maxPlaceable[i] = item.InventoryLimit;
            }
        }
        if (isLocalPlayer)
        {
            _placeableTM = _game.SceneBindings.PlaceableCounts;
        }

        _playerShoot = GetComponent<PlayerShoot>();
        _playerManager = GetComponent<PlayerManager>();
        _reverseGrid = 1f / GridTileSize;
        _buildingCameraHolder = gameObject.transform.Find("BuildingCameraHolder");
        _buildingCamera = _buildingCameraHolder.GetComponentInChildren<Camera>();
        _camMaxZoom = _buildingCamera.transform.position.y;
        _camMinZoom = 1f;
        _currentCamera = _actionCamera = gameObject.transform.Find("PlayerCamera").GetComponent<Camera>();

    }

    private void Update()
    {
        if (!isLocalPlayer) return;
        if (SettingsMenu.IsOpen || PauseGame.menuActive)
        {
            ClearPreviewPoint();
            if (_currentObject != null) _currentObject.SetActive(false);
            return;
        }
        UpdateInventoryHud();

        HandleKeys();
        if (_currentObject != null)
        {
            if (!_placementPending)
            {
                RotateWheel();
                MoveToMouse();
                ReleaseOnClick();
            }
            UpdateInvalidPreview();
        }

    }

    private void PlaySound(AudioClip clip)
    {
        _source.clip = clip;
        _source.PlayOneShot(_source.clip);
    }

    int returnFirstAvailableBiggerIndex()
    {
        return InventorySelection.FindAvailable(placeableCount, _placeableIndex, 1, _placeableObject.Count);
    }

    int returnFirstAvaialbleSmallerIndex()
    {
        return InventorySelection.FindAvailable(placeableCount, _placeableIndex, -1, _placeableObject.Count);
    }

    private void UpdateInventoryHud()
    {
        int count = Mathf.Min(placeableCount.Length, _placeableTM.Length);
        for (int i = 0; i < count; i++)
            if (_placeableTM[i] != null) _placeableTM[i].text = placeableCount[i].ToString();
    }

    void RotateWheel()
    {
        if (_placementPending) return;
        _mouseWheelRotation = 0;
        _mouseWheelRotation += Input.mouseScrollDelta.y;

        if (_mouseWheelRotation > 0.5)
        {
            _placeableIndex = returnFirstAvailableBiggerIndex();
            UpdatePlaceable();
        }
        else if (_mouseWheelRotation < -0.5)
        {
            _placeableIndex = returnFirstAvaialbleSmallerIndex();
            UpdatePlaceable();
        }
    }

    /// <summary>Clears local preview state and building-on-the-fly mode; does not despawn an already placed network item.</summary>
    public void ErasePlaceable()
    {
            ClearPreviewPoint();
            RestorePreviewMaterials();
            if (_currentObject != null) Destroy(_currentObject);
            _currentObject = null;
            _isPlacing = false;
            if (_playerShoot != null) _playerShoot.IsBuildingOnFly = false;

    }

    void UpdatePlaceable()
    {
        ClearPreviewPoint();
        RestorePreviewMaterials();
        Destroy(_currentObject);
        _currentObject = Instantiate(_placeableObjectModels[_placeableIndex]);
        // A preview must not catch its own placement ray or participate in physics.
        foreach (var collider in _currentObject.GetComponentsInChildren<Collider>()) collider.enabled = false;
        foreach (var body in _currentObject.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
        CachePreviewMaterials();
        MoveToMouse();
    }

    void CachePreviewMaterials()
    {
        _invalidUntil = 0;
        _previewRenderers = _currentObject.GetComponentsInChildren<Renderer>(true);
        _previewMaterials = new Material[_previewRenderers.Length][];
        _invalidMaterials = new Material[_previewRenderers.Length][];
        for (int i = 0; i < _previewRenderers.Length; i++)
        {
            _previewMaterials[i] = _previewRenderers[i].sharedMaterials;
            _invalidMaterials[i] = new Material[_previewMaterials[i].Length];
            for (int j = 0; j < _invalidMaterials[i].Length; j++)
                _invalidMaterials[i][j] = _invalidPreviewMaterial;
        }
    }

    void RestorePreviewMaterials()
    {
        _invalidUntil = 0;
        if (_previewRenderers == null) return;
        for (int i = 0; i < _previewRenderers.Length; i++)
            if (_previewRenderers[i] != null) _previewRenderers[i].sharedMaterials = _previewMaterials[i];
    }

    void RejectPlacement()
    {
        PlaySound(nono);
        _invalidUntil = Time.unscaledTime + 0.35f;
        UpdateInvalidPreview();
    }

    void UpdateInvalidPreview()
    {
        if (_invalidUntil <= 0 || _invalidPreviewMaterial == null || _previewRenderers == null) return;
        if (Time.unscaledTime >= _invalidUntil) { RestorePreviewMaterials(); return; }
        bool red = Mathf.FloorToInt((0.35f - (_invalidUntil - Time.unscaledTime)) / 0.0875f) % 2 == 0;
        for (int i = 0; i < _previewRenderers.Length; i++)
            if (_previewRenderers[i] != null)
                _previewRenderers[i].sharedMaterials = red ? _invalidMaterials[i] : _previewMaterials[i];
    }

    void ReleaseOnClick()
    {
        if (Input.GetMouseButtonDown(0) && _currentObject.activeSelf && !_placementPending)
        {
            if (_previewPoint == null || !IsWithinPlacementRange(_currentObject.transform.position)) { RejectPlacement(); return; }
            GridPoint candidate = _game.World.FindBuildingPoint(_currentObject.transform.position, _currentTag);
            if(placeableCount[_placeableIndex] > 0)
            {
                if (candidate != null && candidate.Buildable
                    )
                {
                    if (_game.Players.ControlMode == PlayerControlMode.Fighting)
                        _game.World.TurnOnGridRenders(false);

                    Vector3 position = _currentObject.transform.position;
                    Quaternion rotation = _currentObject.transform.rotation;
                    ClearPreviewPoint();
                    RestorePreviewMaterials();
                    _placementPending = true;
                    _requests.Place(position, rotation, candidate.Id, _placeableIndex);
                }
                else
                    RejectPlacement();
            }
            else
                RejectPlacement();
        }
    }

    /// <summary>Applies the owning player acknowledgement: spend one item on acceptance, otherwise retain inventory and show rejection.</summary>
    public void ResolvePlacement(int index, bool accepted)
    {
        _placementPending = false;
        if (!accepted)
        {
            if (_currentObject != null && _game.Players.ControlMode == PlayerControlMode.Fighting)
                _game.World.TurnOnGridRenders(true);
            RejectPlacement();
            return;
        }
        _playerShoot.WasBuilt = true;
        PlaySound(bop);
        RestorePreviewMaterials();
        if (_currentObject != null) Destroy(_currentObject);
        _currentObject = null;
        placeableCount[index]--;
        if (_placeableIndex == index && !hasNoPlaceable() && placeableCount[index] == 0)
            _placeableIndex = returnFirstAvailableBiggerIndex();
    }

    /// <summary>Checks the authored horizontal two-tile radius around this player; height does not extend range.</summary>
    public bool IsWithinPlacementRange(Vector3 position)
    {
        Vector3 delta = position - transform.position;
        float radius = PlacementRadiusInTiles * GridTileSize;
        return delta.x * delta.x + delta.z * delta.z <= radius * radius;
    }

    void MoveToMouse()
    {
        if (_currentObject == null) return;
        _currentCamera = _game.Players.ControlMode == PlayerControlMode.Building ? _buildingCamera : _actionCamera;
        Ray ray = _game.Players.ControlMode == PlayerControlMode.Fighting
            ? _currentCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0f))
            : _currentCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, PreviewRayDistance, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
        {
            HidePreview();
            return;
        }
        _x = Mathf.Round(hit.point.x * _reverseGrid) / _reverseGrid;
        _y = Mathf.Round(hit.point.z * _reverseGrid) / _reverseGrid;
        _currentObject.transform.position = new Vector3(_x, hit.transform.position.y, _y);
        if (!IsWithinPlacementRange(_currentObject.transform.position))
        {
            HidePreview();
            return;
        }
        _currentTag = hit.collider.tag;
        var point = _game.World.FindBuildingPoint(new Vector3(_x, 0f, _y), _currentTag);
        if (point != null) ShowGridPreview(point);
        else if (IsRoomSurface(_currentTag))
        {
            // Keep the authored preview outside cells (e.g. a capture area), without allowing placement.
            ClearPreviewPoint();
            _currentObject.SetActive(true);
        }
        else HidePreview();
    }

    private void ShowGridPreview(GridPoint point)
    {
        if (_previewPoint != point)
        {
            ClearPreviewPoint();
            _previewPoint = point;
            point.SetPlacementPreview(true);
        }
        _currentObject.SetActive(true);
    }

    private bool IsRoomSurface(string surfaceTag)
    {
        foreach (var room in _game.World.Rooms)
            if (room.CompareTag(surfaceTag)) return true;
        return false;
    }

    private void HidePreview()
    {
        ClearPreviewPoint();
        _currentObject.SetActive(false);
    }

    void ClearPreviewPoint()
    {
        if (_previewPoint != null) _previewPoint.SetPlacementPreview(false);
        _previewPoint = null;
    }

    void OnDisable()
    {
        ErasePlaceable();
        if (_playerShoot != null && isLocalPlayer && _game != null) _game.World.TurnOnGridRenders(false);
    }

    void HandleKeys()
    {
        if (_placementPending)
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                ErasePlaceable();
                _game.World.TurnOnGridRenders(false);
            }
            return;
        }
        switch (_game.Players.ControlMode)
        {
            case PlayerControlMode.Fighting: HandleCombatKeys(); break;
            case PlayerControlMode.Building: HandleBuildingKeys(); break;
        }
    }

    private void HandleCombatKeys()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (!_playerShoot.IsBuildingOnFly)
            {
                _game.World.TurnOnGridRenders(true);
                ShowAvailablePreview();
                _playerShoot.IsBuildingOnFly = true;
            }
            else
            {
                _game.World.TurnOnGridRenders(false);
                ErasePlaceable();
            }
        }
        if (Input.GetKeyDown(KeyCode.Return) && _game.Players.IsListeningForReady)
        {
            ErasePlaceable();
            _game.Players.IsListeningForReady = false;
            CmdRegisterBeingReady();
        }
    }

    private void ShowAvailablePreview()
    {
        if (hasNoPlaceable()) return;
        if (placeableCount[_placeableIndex] <= 0) _placeableIndex = returnFirstAvailableBiggerIndex();
        UpdatePlaceable();
    }

    private void HandleBuildingKeys()
    {
        HandleBuildingCamera();
        if (!Input.GetKeyDown(KeyCode.T) || hasNoPlaceable()) return;
        if (_currentObject != null)
        {
            ErasePlaceable();
            return;
        }
        ShowAvailablePreview();
        _isPlacing = true;
    }

    private void HandleBuildingCamera()
    {
        if (!_isPlacing)
        {
            float zoomDelta = Input.GetAxis(GameInput.Scroll);
            if (zoomDelta != 0f) AdjustZoom(zoomDelta);
        }
        float xDelta = 0f, zDelta = 0f, rotationDelta = 0f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.Comma)) rotationDelta++;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Period)) rotationDelta--;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || Input.mousePosition.y >= Screen.height - _scrollBorderThickness) zDelta++;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || Input.mousePosition.y <= _scrollBorderThickness) zDelta--;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) || Input.mousePosition.x >= Screen.width - _scrollBorderThickness) xDelta++;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) || Input.mousePosition.x <= _scrollBorderThickness) xDelta--;
        if (xDelta != 0f || zDelta != 0f) AdjustPositionMouse(xDelta, zDelta);
        if (rotationDelta != 0f) AdjustRotationKeyboard(rotationDelta);
    }

    void CmdRegisterBeingReady() { _requests.Ready(); }

    void AdjustPositionMouse(float xDelta, float zDelta)
    {
        Vector3 direction = _buildingCameraHolder.localRotation * new Vector3(xDelta, 0f, zDelta).normalized;
        float distance = Mathf.Lerp(_moveSpeedMinZoom, _moveSpeedMaxZoom, _zoom) * Time.deltaTime;
        Vector3 position = _buildingCameraHolder.localPosition;
        position += direction * distance;
        _buildingCameraHolder.localPosition = position;
    }

    void AdjustZoom(float delta)
    {
        delta *= -1f;
        _zoom = Mathf.Clamp01(_zoom + delta);

       float distance = Mathf.Lerp(_camMinZoom, _camMaxZoom, _zoom);
       _buildingCamera.transform.localPosition = new Vector3(_buildingCamera.transform.localPosition.x, distance, _buildingCamera.transform.localPosition.z);

    }

    void AdjustRotationKeyboard(float angle)
    {
        angle *= _rotationSpeedKeyboard * Time.deltaTime;
        _buildingCameraHolder.rotation = Quaternion.Euler(0f, _buildingCameraHolder.localEulerAngles.y + angle, 0f);
    }
}
