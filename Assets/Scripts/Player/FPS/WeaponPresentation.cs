using UnityEngine;

// Local first-person offsets live above the authored weapon animation hierarchy.
public class WeaponPresentation : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _maxSwayDegrees = 1.5f;
    [SerializeField, Min(0f)] private float _maxMovementOffset = 0.01f;
    [SerializeField, Min(0f)] private float _shotBack = 0.015f;
    [SerializeField, Min(0f)] private float _shotPitch = 1.5f;
    [SerializeField, Min(0.01f)] private float _shotReturnTime = 0.18f;
    [SerializeField, Range(0f, 1f)] private float _aimMotionScale = 0.2f;
    private PlayerController _controller;
    private PlayerMotor _motor;
    private PlayerEquipment _equipment;
    private Vector3 _basePosition;
    private Quaternion _baseRotation;
    private Vector3 _lastPosition;
    private Quaternion _lastView;
    private Vector3 _offset;
    private Vector3 _angles;
    private float _gait;
    private float _kick;
    private Weapon _weapon;

    public void Initialize(PlayerController controller, PlayerMotor motor, PlayerEquipment equipment)
    {
        _controller = controller;
        _motor = motor;
        _equipment = equipment;
        _weapon = equipment.getActiveWeapon();
        _basePosition = transform.localPosition;
        _baseRotation = transform.localRotation;
        ResetMotion();
    }

    public void Shot(float scale)
    {
        // The first shot after a swap can arrive before this component's next LateUpdate.
        var weapon = _equipment != null ? _equipment.getActiveWeapon() : null;
        if (_weapon != weapon)
        {
            _weapon = weapon;
            ResetMotion();
        }
        _kick = Mathf.Min(1.5f, _kick + Mathf.Max(0f, scale));
    }

    private void ResetMotion()
    {
        _offset = _angles = Vector3.zero;
        _kick = _gait = 0f;
        if (_controller != null) _lastPosition = _controller.transform.position;
        _lastView = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        transform.localPosition = _basePosition;
        transform.localRotation = _baseRotation;
    }

    void LateUpdate()
    {
        if (_controller == null) return;
        var weapon = _equipment.getActiveWeapon();
        if (!_controller.CanControl || !_motor.isActiveAndEnabled || weapon == null)
        {
            ResetMotion();
            return;
        }
        if (_weapon != weapon)
        {
            _weapon = weapon;
            ResetMotion();
        }
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        Vector3 position = _controller.transform.position;
        Vector3 delta = position - _lastPosition;
        _lastPosition = position;
        // Teleports do not make the viewmodel lurch or advance its gait.
        if (delta.sqrMagnitude > 1f) delta = Vector3.zero;
        Vector3 localVelocity = Quaternion.Inverse(Quaternion.Euler(0f, _motor.ViewYaw, 0f)) * delta / dt;
        float speed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
        float speedN = Mathf.Clamp01(speed / 7f);
        _gait += speed * dt * 4f;
        Quaternion view = transform.parent.rotation;
        Vector3 turn = (Quaternion.Inverse(_lastView) * view).eulerAngles;
        _lastView = view;
        float yawRate = Mathf.DeltaAngle(0f, turn.y) / dt;
        float pitchRate = Mathf.DeltaAngle(0f, turn.x) / dt;
        float motion = _controller.IsAiming ? _aimMotionScale : 1f;
        var targetOffset = new Vector3(Mathf.Sin(_gait) * 0.5f, Mathf.Cos(_gait * 2f) * 0.35f, 0f)
            * (_maxMovementOffset * speedN * motion);
        var targetAngles = new Vector3(Mathf.Clamp(-pitchRate / 180f, -1f, 1f),
            Mathf.Clamp(-yawRate / 180f, -1f, 1f), Mathf.Clamp(-localVelocity.x / 7f, -1f, 1f))
            * (_maxSwayDegrees * motion);
        float blend = 1f - Mathf.Exp(-18f * dt);
        _offset = Vector3.Lerp(_offset, targetOffset, blend);
        _angles = Vector3.Lerp(_angles, targetAngles, blend);
        transform.localPosition = _basePosition + _offset + Vector3.back * (_shotBack * _kick);
        transform.localRotation = _baseRotation * Quaternion.Euler(_angles + Vector3.left * (_shotPitch * _kick));
        _kick *= Mathf.Exp(-3f * dt / _shotReturnTime);
    }

    void OnDisable() { ResetMotion(); }
}
