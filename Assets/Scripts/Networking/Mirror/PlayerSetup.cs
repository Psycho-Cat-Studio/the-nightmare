using UnityEngine;
using Mirror;

/// <summary>Composes player authority, equipment, cameras and HUD and separates local controls from remote presentation.</summary>
[RequireComponent(typeof(PlayerManager))]
public class PlayerSetup : NetworkBehaviour, IMatchParticipant
{
    public override void OnStartServer() { MirrorMatchBinding.Bind(gameObject); base.OnStartServer(); }

    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    [SerializeField] private Behaviour[] _toDisable;
    [SerializeField] private Camera _buildingCamera;
    [SerializeField] private Camera _actionCamera;
    public Camera ActionCamera { get { return _actionCamera; } }
    private Camera _sceneCamera;
    [SerializeField] private GameObject _weaponObjectPrefab;
    private PlayerEquipment _equipment;
    private BulletsHUD _bulletshud;
    private HealthBar _healthBar;
    private ClipsManager _clipsManager;

    void Start() {
        if (!isLocalPlayer) {
            EquipWeaponNotLocal();
            DisableComponents();
            AssignRemoteLayer();
            Arrow arrow = GetComponentInChildren<Arrow>(true);
            if (arrow != null) arrow.gameObject.SetActive(false);
        }
        else {
            GameSettings.ApplyFrameRate();
            // Physics runs at 50 Hz; interpolate the local body so rendering above that stays smooth.
            GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Interpolate;
            EquipWeapon();
           
            var hud = _game.SceneBindings;
            _bulletshud = hud.Bullets;
            _healthBar = hud.Health;
            _clipsManager = hud.Clips;
            _game.Players.LocalPlayer = GetComponent<PlayerManager>();
                _sceneCamera = hud.LobbyCamera;
                if (_sceneCamera != null)
                    _sceneCamera.gameObject.SetActive(false);
            _bulletshud.Equipment = GetComponent<PlayerEquipment>();
            _clipsManager.player = GetComponent<PlayerEquipment>();
            _healthBar.player = GetComponent<PlayerManager>();
            _bulletshud.playerEnabled = true;
            _healthBar.playerEnabled = true;
            _clipsManager.playerEnabled = true;
            _game.World.SetCameraForBillboards(_actionCamera);
            
            Vector3 lobbySpawn = hud.LobbySpawn.position;
            // Float overload: the int one only returned -1 or 0, so players spawned almost on top of each other.
            Vector3 randomStartPosition = new Vector3(lobbySpawn.x + Random.Range(-1f, 1f), lobbySpawn.y, lobbySpawn.z + Random.Range(-1f, 1f));
            
            Rigidbody rigidbody = GetComponent<Rigidbody>();
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rigidbody.isKinematic = true;
            transform.position = randomStartPosition;
            rigidbody.position = randomStartPosition;
            rigidbody.isKinematic = false;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
        
        GetComponent<PlayerManager>().Setup();
    }

    void EquipWeaponNotLocal() {
        PlayerShoot shoot = GetComponent<PlayerShoot>();
        shoot.source = GetComponent<AudioSource>();
        shoot.Cam = _actionCamera;
        _equipment = GetComponent<PlayerEquipment>();
        _equipment.Weapon1 = _equipment.CreateRemoteWeapon(_weaponObjectPrefab);
        _equipment.WeaponSound = _equipment.Weapon1.GetComponent<AudioSource>();
        shoot.Equipment = _equipment;

        if (GetComponent<PlayerRigBindings>().WeaponCamera != null) GetComponent<PlayerRigBindings>().WeaponCamera.enabled = false;
    }

    public void EquipWeapon() {
        GameObject weaponObject = Instantiate(_weaponObjectPrefab, _actionCamera.transform.GetChild(0));
        PlayerShoot shoot = GetComponent<PlayerShoot>();
        shoot.Cam = _actionCamera;
        _equipment = GetComponent<PlayerEquipment>();
        _equipment.Weapon1 = weaponObject.GetComponent<Weapon>();
        _equipment.WeaponSound = weaponObject.GetComponent<AudioSource>();
        shoot.Equipment = _equipment;
    }

    public override void OnStartClient() {
        MirrorMatchBinding.Bind(gameObject);
        base.OnStartClient();
        var authority = GetComponent<PlayerAuthority>();
        if (authority == null) authority = gameObject.AddComponent<PlayerAuthority>();
        authority.Initialize(isLocalPlayer, isServer);
        authority.BindGame(_game, new LocalUiInputGate());
        authority.BindOptions(new LocalPlayerOptions());
        _game.Players.RegisterPlayer(GetComponent<NetworkIdentity>().netId.ToString(), GetComponent<PlayerManager>());
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        GetComponent<PlayerAuthority>().Initialize(true, isServer);
#if UNITY_ANDROID
        var controller = GetComponent<PlayerController>();
        controller.InitializeInput(new MobilePlayerInput(controller.MovementStick, controller.LookStick));
#endif
    }

    public override void OnStopLocalPlayer()
    {
        if (_game != null) _game.World.SetCameraForBillboards(null);
        base.OnStopLocalPlayer();
    }

    private void AssignRemoteLayer() {
        GetComponent<PlayerRigBindings>().GraphicsRoot.layer = LayerMask.NameToLayer(GameLayers.RemotePlayer);
    }

    private void DisableComponents() {
        for (int i = 0; i < _toDisable.Length; i++)
            _toDisable[i].enabled = false;
        _buildingCamera.gameObject.SetActive(false);
    }

    private void OnDisable() {
        if (isLocalPlayer && _game != null)
            _game.World.SetCameraForBillboards(null);
        if (_game != null && _game.Players.Players.ContainsKey(transform.name))
        {

            if (isLocalPlayer)
            {
                if (_sceneCamera != null)
                    _sceneCamera.gameObject.SetActive(true);
            }

            _game.Players.UnregisterPlayer(transform.name);
        }
    }

    private void OnEnable() {
        if (isLocalPlayer) {
            if (_sceneCamera != null)
                _sceneCamera.gameObject.SetActive(false);
        }
    }
}
