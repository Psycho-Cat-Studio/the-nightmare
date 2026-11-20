using UnityEngine;

// Input must reach the view before PlayerShoot samples its ray in the same frame.
/// <summary>Reads permitted local input and translates movement, look, sprint and aim intent into motor commands.</summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] public float _speed = 2f;
    [SerializeField] private float _lookSensitivity = 2.5f;
    [SerializeField] private MonoBehaviour move;
    [SerializeField] private MonoBehaviour look;
    private IAnalogPlayerInput _analog;
    public MonoBehaviour MovementStick => move;
    public MonoBehaviour LookStick => look;
    public void InitializeInput(IAnalogPlayerInput input) { _analog = input; }
    public float SensitivityScale { get; set; }
    [SerializeField] private float _nonZoomSensivity = 0.7f;
    [SerializeField] private float _zoomSensivity = 0.1f;
    public float NonZoomSensitivity { get => _nonZoomSensivity; set => _nonZoomSensivity = value; }
    public float ZoomSensitivity { get => _zoomSensivity; set => _zoomSensivity = value; }

    private const float JoystickDeadzone = 0.2f;
    private const float InputReferenceFrameRate = 50f;
    private const float SPRINT_SPEED = 7f;
    private float _speedSlow = 2.5f;
    private float _speedFast = SPRINT_SPEED;
    private PlayerMotor _motor;
    private PlayerAuthority _authority;
    public bool isLocalPlayer => _authority != null && _authority.IsLocalPlayer;
    private Nightmare.Core.Ports.IHealth _playerManager;
    private IPlayerLoadout _equipment;
    public bool IsSprinting { get; private set; }
    public bool IsAiming { get; private set; }
    /// <summary>Whether local ownership, enabled controls, player life and the UI input gate all permit control.</summary>
    public bool CanControl => isLocalPlayer && isActiveAndEnabled && _playerManager != null
        && !_playerManager.IsDead && _authority.CanReadInput;

    void Awake()
    {
        _authority = GetComponent<PlayerAuthority>();
        if (_authority == null) _authority = gameObject.AddComponent<PlayerAuthority>();
        _motor = GetComponent<PlayerMotor>();
        _playerManager = GetComponent<Nightmare.Core.Ports.IHealth>();
        _equipment = GetComponent<IPlayerLoadout>();
        SensitivityScale = _nonZoomSensivity;
    }

    void Update()
    {
        if (!CanControl)
        {
            ClearInput();
            return;
        }
        Weapon weapon = _equipment.ActiveWeapon != null ? _equipment.ActiveWeapon.GetComponent<Weapon>() : null;
        bool switching = _equipment.IsSwitchingWeapon;
        IsAiming = weapon != null && !switching && Input.GetButton(GameInput.Aim)
            && (weapon.State == Weapon.WeaponState.idle || weapon.State == Weapon.WeaponState.shooting);
        SensitivityScale = IsAiming ? _zoomSensivity : _nonZoomSensivity;
        ReadInput(out float xMov, out float zMov, out float yaw, out float pitch);
        var input = Vector3.ClampMagnitude(new Vector3(xMov, 0f, zMov), 1f);
        IsSprinting = input.sqrMagnitude > 0.0001f && Input.GetKey(KeyCode.LeftShift)
            && weapon != null && weapon.State == Weapon.WeaponState.idle && !IsAiming
            && !switching && !Input.GetButton(GameInput.Fire);
        _speed = IsSprinting ? _speedFast : _speedSlow;
        if (weapon != null) weapon.GetComponent<Animator>().SetBool(AnimationParameters.Sprinting, IsSprinting);
        _motor.Rotate(Vector3.up * (yaw * _lookSensitivity));
        _motor.RotateCamera(pitch * _lookSensitivity);
        _motor.Move(Quaternion.Euler(0f, _motor.ViewYaw, 0f) * input * _speed);
        _motor.ApplyView();
    }

    private void ReadInput(out float xMov, out float zMov, out float yaw, out float pitch)
    {
        xMov = zMov = yaw = pitch = 0f;
        if (_analog != null)
        {
            var movement = _analog.Movement;
            xMov = Mathf.Abs(movement.x) >= JoystickDeadzone ? movement.x : 0f;
            zMov = Mathf.Abs(movement.y) >= JoystickDeadzone ? movement.y : 0f;
            var lookInput = _analog.Look;
            // A joystick describes angular speed, unlike the mouse's per-frame delta.
            float scale = SensitivityScale * (_authority.Options != null ? _authority.Options.MouseSensitivity : 1f) * Time.deltaTime * InputReferenceFrameRate;
            yaw = (Mathf.Abs(lookInput.x) >= JoystickDeadzone ? lookInput.x : 0f) * scale;
            pitch = (Mathf.Abs(lookInput.y) >= JoystickDeadzone ? lookInput.y : 0f) * scale;
            if ((_authority.Options != null && _authority.Options.InvertY)) pitch = -pitch;
        }
#if !UNITY_ANDROID
        else
        {
        xMov = Input.GetAxisRaw(GameInput.Horizontal);
        zMov = Input.GetAxisRaw(GameInput.Vertical);
        float sensitivity = SensitivityScale * (_authority.Options != null ? _authority.Options.MouseSensitivity : 1f);
        yaw = Input.GetAxisRaw(GameInput.MouseX) * sensitivity;
        pitch = Input.GetAxisRaw(GameInput.MouseY) * sensitivity * ((_authority.Options != null && _authority.Options.InvertY) ? -1f : 1f);
        }
#endif
    }

    private void ClearInput()
    {
        IsSprinting = IsAiming = false;
        if (_motor != null) _motor.ClearInput();
        var weapon = _equipment != null && _equipment.ActiveWeapon != null ? _equipment.ActiveWeapon.GetComponent<Weapon>() : null;
        if (weapon != null) weapon.GetComponent<Animator>().SetBool(AnimationParameters.Sprinting, false);
    }

    void OnDisable() { ClearInput(); }

    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.CompareTag(GameTags.SlowDownWall)) _speedFast = _speedSlow;
    }

    void OnCollisionExit(Collision col)
    {
        if (col.gameObject.CompareTag(GameTags.SlowDownWall)) _speedFast = SPRINT_SPEED;
    }
}
