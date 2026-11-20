using UnityEngine;
using Mirror;

[DefaultExecutionOrder(-150)]
[RequireComponent(typeof(Rigidbody))]
public class PlayerMotor : NetworkBehaviour
{
    [SerializeField] private Camera _cam;
    [SerializeField] private float _cameraRotationLimit = 85f;
    private Vector3 _velocity;
    private float _viewYaw;
    private float currentCameraRotationX;
    private float _recoilOffset;
    private Rigidbody _rb;
    private Animator _anim;
    private PlayerController _controller;
    public bool isSpeintingToggle;
    public float ViewYaw => _viewYaw;
    public Vector3 DesiredVelocity => _velocity;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _controller = GetComponent<PlayerController>();
    }

    void Start()
    {
        var graphics = transform.GetChild(0);
        _anim = graphics.GetChild(graphics.GetChild(0).gameObject.activeSelf ? 0 : 1).GetComponent<Animator>();
    }

    void OnEnable()
    {
        _viewYaw = transform.eulerAngles.y;
        _velocity = Vector3.zero;
        _recoilOffset = 0f;
    }

    public void Move(Vector3 velocity) { _velocity = velocity; }
    public void Rotate(Vector3 rotation) { _viewYaw = Mathf.Repeat(_viewYaw + rotation.y, 360f); }
    public void RotateCamera(float cameraRotation)
    {
        currentCameraRotationX = Mathf.Clamp(currentCameraRotationX - cameraRotation,
            -_cameraRotationLimit, _cameraRotationLimit);
    }

    void FixedUpdate()
    {
        if (!_controller.CanControl) return;
        if (_velocity.sqrMagnitude > 0f) _rb.MovePosition(_rb.position + _velocity * Time.fixedDeltaTime);
        _rb.MoveRotation(Quaternion.Euler(0f, _viewYaw, 0f));
        if (_anim != null) _anim.SetBool("running", _velocity.sqrMagnitude > 0.0001f);
    }

    void Update()
    {
        if (!_controller.CanControl) return;
        // Exact continuous equivalent of the old 50 Hz decay (offset * .97 - .01).
        float decay = Mathf.Pow(0.97f, Time.deltaTime * 50f);
        _recoilOffset = Mathf.Max(0f, (_recoilOffset + 1f / 3f) * decay - 1f / 3f);
    }

    void LateUpdate()
    {
        if (_controller.CanControl) ApplyView();
    }

    public void ApplyView()
    {
        if (_cam != null && isLocalPlayer)
            // World rotation cancels the parent's interpolated yaw without moving its physics body.
            _cam.transform.rotation = Quaternion.Euler(currentCameraRotationX - _recoilOffset, _viewYaw, 0f);
    }

    public void IncreaseRecoil(float weaponRecoil)
    {
        _recoilOffset += weaponRecoil - _recoilOffset * 0.05f;
    }

    public void ClearInput()
    {
        _velocity = Vector3.zero;
        _recoilOffset = 0f;
        // Discard any unsimulated yaw when controls are hidden or disabled.
        _viewYaw = transform.eulerAngles.y;
        if (_anim != null) _anim.SetBool("running", false);
    }

    void OnDisable() { ClearInput(); }
}
