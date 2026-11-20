using UnityEngine;
using Mirror;

// Input must reach the view before PlayerShoot samples its ray in the same frame.
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerController : NetworkBehaviour
{
    [SerializeField] public float _speed = 2f;
    [SerializeField] private float _lookSensitivity = 2.5f;
    [SerializeField] private Joystick move;
    [SerializeField] private Joystick look;
    public float SensitivityScale { get; set; }
    [SerializeField] private float _nonZoomSensivity = 0.7f;
    [SerializeField] private float _zoomSensivity = 0.1f;
    public float NonZoomSensitivity { get => _nonZoomSensivity; set => _nonZoomSensivity = value; }
    public float ZoomSensitivity { get => _zoomSensivity; set => _zoomSensivity = value; }

    private const float SPRINT_SPEED = 7f;
    private float _speedSlow = 2.5f;
    private float _speedFast = SPRINT_SPEED;
    private PlayerMotor _motor;
    private PlayerManager _playerManager;
    private PlayerEquipment _equipment;
    private PlayerShoot _shoot;
    private static readonly int IsSprintingHash = Animator.StringToHash("isSprinting");
    public bool IsSprinting { get; private set; }
    public bool IsAiming { get; private set; }
    public bool CanControl => isLocalPlayer && isActiveAndEnabled && _playerManager != null
        && !_playerManager.IsDead && ButtonsControll.screensOver && !WinLoseScreens.winLoseActive
        && !PauseGame.menuActive && !SettingsMenu.IsOpen;

    void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        _playerManager = GetComponent<PlayerManager>();
        _equipment = GetComponent<PlayerEquipment>();
        _shoot = GetComponent<PlayerShoot>();
        SensitivityScale = _nonZoomSensivity;
    }

    void Start()
    {
#if UNITY_ANDROID
        move = GameObject.Find("Move").GetComponent<Joystick>();
        look = GameObject.Find("Look").GetComponent<Joystick>();
#endif
    }

    void Update()
    {
        if (!CanControl)
        {
            ClearInput();
            return;
        }
        Weapon weapon = _equipment.getActiveWeapon();
        bool switching = _shoot != null && _shoot.IsSwitchingWeapon;
        IsAiming = weapon != null && !switching && Input.GetButton("Fire2")
            && (weapon.State == Weapon.WeaponState.idle || weapon.State == Weapon.WeaponState.shooting);
        SensitivityScale = IsAiming ? _zoomSensivity : _nonZoomSensivity;
        float xMov = 0f, zMov = 0f, yaw = 0f, pitch = 0f;
#if UNITY_ANDROID
        if (move != null)
        {
            xMov = Mathf.Abs(move.Horizontal) >= 0.2f ? move.Horizontal : 0f;
            zMov = Mathf.Abs(move.Vertical) >= 0.2f ? move.Vertical : 0f;
        }
        if (look != null)
        {
            // A joystick describes angular speed, unlike the mouse's per-frame delta.
            float scale = SensitivityScale * GameSettings.MouseSensitivity * Time.deltaTime * 50f;
            yaw = (Mathf.Abs(look.Horizontal) >= 0.2f ? look.Horizontal : 0f) * scale;
            pitch = (Mathf.Abs(look.Vertical) >= 0.2f ? look.Vertical : 0f) * scale;
            if (GameSettings.InvertY) pitch = -pitch;
        }
#else
        xMov = Input.GetAxisRaw("Horizontal");
        zMov = Input.GetAxisRaw("Vertical");
        float sensitivity = SensitivityScale * GameSettings.MouseSensitivity;
        yaw = Input.GetAxisRaw("Mouse X") * sensitivity;
        pitch = Input.GetAxisRaw("Mouse Y") * sensitivity * (GameSettings.InvertY ? -1f : 1f);
#endif
        var input = Vector3.ClampMagnitude(new Vector3(xMov, 0f, zMov), 1f);
        IsSprinting = input.sqrMagnitude > 0.0001f && Input.GetKey(KeyCode.LeftShift)
            && weapon != null && weapon.State == Weapon.WeaponState.idle && !IsAiming
            && !switching && !Input.GetButton("Fire1");
        _speed = IsSprinting ? _speedFast : _speedSlow;
        if (weapon != null) weapon.GetComponent<Animator>().SetBool(IsSprintingHash, IsSprinting);
        _motor.Rotate(Vector3.up * (yaw * _lookSensitivity));
        _motor.RotateCamera(pitch * _lookSensitivity);
        _motor.Move(Quaternion.Euler(0f, _motor.ViewYaw, 0f) * input * _speed);
        _motor.ApplyView();
    }

    private void ClearInput()
    {
        IsSprinting = IsAiming = false;
        if (_motor != null) _motor.ClearInput();
        var weapon = _equipment != null ? _equipment.getActiveWeapon() : null;
        if (weapon != null) weapon.GetComponent<Animator>().SetBool(IsSprintingHash, false);
    }

    void OnDisable() { ClearInput(); }

    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.CompareTag("SlowDownWall")) _speedFast = _speedSlow;
    }

    void OnCollisionExit(Collision col)
    {
        if (col.gameObject.CompareTag("SlowDownWall")) _speedFast = SPRINT_SPEED;
    }
}
