using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Mirror;

public class PlacementController : NetworkBehaviour
{
    public static float GridTileSize = 1f;
    private const float PlacementRadiusInTiles = 2f;
    [SerializeField] private List<GameObject> _placeableObject;
    [SerializeField] private List<GameObject> _placeableObjectModels;
    //public GameObject PlaceableObject {get { return _placeableObject; } set { _placeableObject = value; }}
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
    private float _buildingCameraAngle = 0f;
    private Camera _actionCamera;
    private Camera _currentCamera;
    [SerializeField] private GameObject _gridPointPrefab;
    [SerializeField] private float _scrollBorderThickness = 0.005f;  // percentage of screen height
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
    private CustomNetworkManager _customNetworkManager;

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
        if (isLocalPlayer)
        {
            //_customNetworkManager = GameObject.Find("NetworkManager").GetComponent<CustomNetworkManager>();
            _placeableTM[0] = GameObject.Find("SnaresCount").GetComponent<TextMeshProUGUI>();
            _placeableTM[1] = GameObject.Find("TeddyBearCount").GetComponent<TextMeshProUGUI>();
            _placeableTM[2] = GameObject.Find("BarrelsCount").GetComponent<TextMeshProUGUI>();
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
        if(isLocalPlayer)
            for (int i = 0; i < 3; i++)
            {
                _placeableTM[i].text = placeableCount[i].ToString();
            }
            
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
        int tempIndex = _placeableIndex;
        while (true)
        {
            tempIndex++;
            if (tempIndex > _placeableObject.Count - 1) tempIndex = 0;
            if (placeableCount[tempIndex] != 0)
            {
                return tempIndex;
            }
            if (tempIndex == _placeableIndex) return _placeableIndex;
        }
    }


    int returnFirstAvaialbleSmallerIndex()
    {
        int tempIndex = _placeableIndex;
        while (true)
        {
            tempIndex--;
            if (tempIndex < 0) tempIndex = _placeableObject.Count - 1;
            if (placeableCount[tempIndex] != 0)
            {
                return tempIndex;
            }
            if (tempIndex == _placeableIndex) return _placeableIndex;
        }

    }


    void RotateWheel()
    {
        if (_placementPending) return;
        _mouseWheelRotation = 0;
        _mouseWheelRotation += Input.mouseScrollDelta.y;
        
        if (_mouseWheelRotation > 0.5)
        {

            //if (_placeableIndex > _placeableObject.Count - 1) _placeableIndex = 0;
            _placeableIndex = returnFirstAvailableBiggerIndex();
            UpdatePlaceable();
            //_currentObject.transform.Rotate(Vector3.up, 90);
        }
        else if (_mouseWheelRotation < -0.5)
        {
            _placeableIndex = returnFirstAvaialbleSmallerIndex();
            //if (_placeableIndex < 0) _placeableIndex = _placeableObject.Count - 1;
            UpdatePlaceable();
            //_currentObject.transform.Rotate(Vector3.up, -90);
        }
    }


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
            string posAndTag = _currentObject.transform.position.x.ToString() + "_" + _currentObject.transform.position.z.ToString() + "_" + _currentTag;
            if(placeableCount[_placeableIndex] > 0)
            {
                if (GameManager.Instance.BuildingPoints.ContainsKey(posAndTag)
                    && GameManager.Instance.BuildingPoints[posAndTag].Buildable
                    )
                {
                    if (GameManager.CurrentState == GameManager.GameState.Fighting)
                        GameManager.TurnOnGridRenders(false);

                    Vector3 position = _currentObject.transform.position;
                    Quaternion rotation = _currentObject.transform.rotation;
                    ClearPreviewPoint();
                    RestorePreviewMaterials();
                    _placementPending = true;
                    CmdPlaceEntity(position, rotation, _currentTag, _placeableIndex);
                }
                else
                    RejectPlacement();
            }
            else 
                RejectPlacement();
        }
    }

    [Command]
    void CmdPlaceEntity(Vector3 pos, Quaternion rot, string tag, int placeableIndex)
    {
        string posAndTag = pos.x.ToString() + "_" + pos.z.ToString() + "_" + tag;
        GridPoint point;
        if (placeableIndex < 0 || placeableIndex >= _placeableObject.Count
            || !IsWithinPlacementRange(pos)
            || !GameManager.Instance.BuildingPoints.TryGetValue(posAndTag, out point) || !point.Buildable)
        {
            TargetPlacementResult(connectionToClient, placeableIndex, false);
            return;
        }
        GameObject placeableObject = Instantiate(_placeableObject[placeableIndex], pos, rot);
        switch (placeableIndex)
        {
            case INDEX_OF_SNARES:
                Snares snares = placeableObject.GetComponent<Snares>();
                snares.InitialPosAndTag = posAndTag;
                break;
            case INDEX_OF_BEAR:
                TeddyBearServer teddyBearServer = placeableObject.GetComponent<TeddyBearServer>();
                teddyBearServer.InitialPosAndTag = posAndTag;
                teddyBearServer.GetComponentInChildren<SpriteRenderer>().enabled = false;
                break;
            case INDEX_OF_BARREL:
                Barrel barrel = placeableObject.GetComponentInChildren<Barrel>();
                barrel.InitialPosAndTag = posAndTag;
                break;
        }
        //NetworkServer.SpawnWithClientAuthority(placeableObject, connectionToClient);
        NetworkServer.Spawn(placeableObject);
        point.Buildable = false;
        RpcPlaceEntity(posAndTag);
        TargetPlacementResult(connectionToClient, placeableIndex, true);
    }

    [ClientRpc]
    void RpcPlaceEntity(string posAndTag)
    {
        GameManager.Instance.BuildingPoints[posAndTag].Buildable = false;
    }

    [TargetRpc]
    void TargetPlacementResult(NetworkConnectionToClient target, int index, bool accepted)
    {
        _placementPending = false;
        if (!accepted)
        {
            if (_currentObject != null && GameManager.CurrentState == GameManager.GameState.Fighting)
                GameManager.TurnOnGridRenders(true);
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

    bool IsWithinPlacementRange(Vector3 position)
    {
        Vector3 delta = position - transform.position;
        float radius = PlacementRadiusInTiles * GridTileSize;
        return delta.x * delta.x + delta.z * delta.z <= radius * radius;
    }

    void MoveToMouse()
    {
        if (_currentObject != null)
        {
            _currentCamera = GameManager.CurrentState == GameManager.GameState.Building ? _buildingCamera : _actionCamera;
            // Locked-pointer coordinates can be outside a scaled Game view on Linux.
            // FPS placement follows the crosshair; the overhead view uses the pointer.
            Ray ray = GameManager.CurrentState == GameManager.GameState.Fighting
                ? _currentCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0))
                : _currentCamera.ScreenPointToRay(Input.mousePosition);

            RaycastHit hitInfo;
            if (Physics.Raycast(ray, out hitInfo, 100f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
            {
                _x = Mathf.Round(hitInfo.point.x * _reverseGrid) / _reverseGrid;
                _y = Mathf.Round(hitInfo.point.z * _reverseGrid) / _reverseGrid;
                _currentObject.transform.position = new Vector3(_x, hitInfo.transform.position.y, _y);
                if (!IsWithinPlacementRange(_currentObject.transform.position))
                {
                    ClearPreviewPoint();
                    _currentObject.SetActive(false);
                    return;
                }
                _currentTag = hitInfo.collider.tag;
                string key = _x.ToString() + "_" + _y.ToString() + "_" + _currentTag;
                GridPoint point;
                if (GameManager.Instance.BuildingPoints.TryGetValue(key, out point))
                {
                    if (_previewPoint != point)
                    {
                        ClearPreviewPoint();
                        _previewPoint = point;
                        point.SetPlacementPreview(true);
                    }
                    _currentObject.SetActive(true);
                    return;
                }
                // Keep the original model preview over a floor even where no grid cell
                // exists (e.g. the capture zone). It remains impossible to place there.
                foreach (var room in GameManager.Instance.Rooms)
                    if (room.CompareTag(_currentTag))
                    {
                        ClearPreviewPoint();
                        _currentObject.SetActive(true);
                        return;
                    }
                //_currentObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
            }
            ClearPreviewPoint();
            _currentObject.SetActive(false);
        }
    }

    void ClearPreviewPoint()
    {
        if (_previewPoint != null) _previewPoint.SetPlacementPreview(false);
        _previewPoint = null;
    }

    void OnDisable()
    {
        ErasePlaceable();
        if (_playerShoot != null && isLocalPlayer && GameManager.Instance != null) GameManager.TurnOnGridRenders(false);
    }

    void HandleKeys()
    {
        if (_placementPending)
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                ErasePlaceable();
                GameManager.TurnOnGridRenders(false);
            }
            return;
        }
        switch (GameManager.CurrentState)
        {
            case GameManager.GameState.Fighting:

                if (Input.GetKeyDown(KeyCode.T))
                {
                    if (!_playerShoot.IsBuildingOnFly)
                    {
                        GameManager.TurnOnGridRenders(true);
                        if (!hasNoPlaceable())
                        {
                            if (placeableCount[_placeableIndex] <= 0) _placeableIndex = returnFirstAvailableBiggerIndex();
                            UpdatePlaceable();
                        }
                        _playerShoot.IsBuildingOnFly = true;
                    }
                    else
                    {
                        GameManager.TurnOnGridRenders(false);
                        ErasePlaceable();
                    }

                }
                if (Input.GetKeyDown(KeyCode.Return) && GameManager.IsListeningForReady)
                {
                    ErasePlaceable();
                    GameManager.IsListeningForReady = false;
                    CmdRegisterBeingReady();

                }
                break;

            case GameManager.GameState.Building:
            
                float xDelta = 0f, zDelta = 0f, rotationDelta = 0f;

                if (!_isPlacing)
                {
                    float zoomDelta = Input.GetAxis("Mouse ScrollWheel");
                    if (zoomDelta != 0f) AdjustZoom(zoomDelta);                    
                }
                


                if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.Comma)) rotationDelta++;
                if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Period)) rotationDelta--;

                if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || Input.mousePosition.y >= Screen.height - _scrollBorderThickness) zDelta++;
                if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || Input.mousePosition.y <= _scrollBorderThickness) zDelta--;
                if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) || Input.mousePosition.x >= Screen.width - _scrollBorderThickness) xDelta++;
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) || Input.mousePosition.x <= _scrollBorderThickness) xDelta--;

                if (xDelta != 0f || zDelta != 0f) AdjustPositionMouse(xDelta, zDelta);
                if (rotationDelta != 0f) AdjustRotationKeyboard(rotationDelta);

                if (Input.GetKeyDown(KeyCode.T))
                {
                    if (!hasNoPlaceable())
                    {
                        if (_currentObject == null)
                        {
                            if (placeableCount[_placeableIndex] <= 0) _placeableIndex = returnFirstAvailableBiggerIndex();
                            UpdatePlaceable();
                            _isPlacing = true;
                        }
                        else
                        {
                            ErasePlaceable();
                        }
                    }
                }

                
                break;
        }
    }

    [Command]
    void CmdRegisterBeingReady()
    {
        GameManager.Instance.ReadyPlayersCnt++;
        // >= : a player leaving the lobby after others got ready used to block the start forever.
        if (GameManager.Instance.ReadyPlayersCnt >= GameManager.Players.Count)
           RpcRegisterLocalPlayerBeingReady(); // rpc bedzie wywolywana na obiketach symbolizujacych ostatniego playera ktory zadeklarowal sie 'ready'
    }

    [ClientRpc]
    void RpcRegisterLocalPlayerBeingReady() 
    {
        if (GameManager.Instance.CurrentMachState != GameManager.MatchState.Lobby) return;
        /*PlacementController localPlayer = GameManager.LocalPlayer.GetComponent<PlacementController>(); // dlatego musimy wyluskac local playera
        localPlayer._currentCamera = localPlayer._actionCamera;
        ClockManager.canCount = true;
        if (localPlayer._currentObject != null) Destroy(localPlayer._currentObject);
        GameManager.CurrentState = GameManager.GameState.Fighting;*/
        GameManager.Instance.TeleportToRoom1();
        
    }

    void AdjustPositionMouse(float xDelta, float zDelta)
    {
        Vector3 direction = _buildingCameraHolder.localRotation * new Vector3(xDelta, 0f, zDelta).normalized;
        float distance = Mathf.Lerp(_moveSpeedMinZoom, _moveSpeedMaxZoom, _zoom) * Time.deltaTime;
        Vector3 position = _buildingCameraHolder.localPosition;
        position += direction * distance;
        //transform.localPosition = ClampPosition(position);
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
