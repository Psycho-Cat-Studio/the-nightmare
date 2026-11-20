using UnityEngine;
using System;
using Nightmare.Core.Combat;

/// <summary>Applies authored weapon settings and owns ammunition, fire modes and timed firing/reload operations.</summary>
public class Weapon : MonoBehaviour {

    [SerializeField] private Nightmare.Configuration.WeaponDefinition _definition;
    public Nightmare.Configuration.WeaponDefinition Definition => _definition;
    [SerializeField] protected int _id;
    public int Id { get { return _id; } set { _id = value; } }

    [SerializeField] protected string _name;
    public string Name {
        get { return _name; }
        set { _name = value; }
    }

    [SerializeField] protected int _damage;
    public int Damage {
        get { return _damage; }
        set { _damage = value; }
    }
    
    /// <summary>Loaded rounds. Assignments are clamped to magazine capacity and available total ammunition.</summary>
    [SerializeField] protected int _currentMagAmmo;
    public int CurrentMagAmmo {
        get { EnsureAmmo(); return _ammo.Magazine; }
        set { EnsureAmmo(); _ammo.Restore(_ammo.Total, value); SaveAmmo(); }
    }

    [SerializeField] protected int _maxMagAmmo;
    public int MaxMagAmmo {
        get { return _maxMagAmmo; }
        set { _maxMagAmmo = value; }
    }

    /// <summary>Total ammunition including loaded rounds; increasing it preserves the loaded magazine.</summary>
    [SerializeField] protected int _currentAmmo;
    public int CurrentAmmo {
        get { EnsureAmmo(); return _ammo.Total; }
        set { EnsureAmmo(); _ammo.Restore(value, _ammo.Magazine); SaveAmmo(); }
    }

    [SerializeField] protected int _maxAmmo;
    public int MaxAmmo {
        get { return _maxAmmo; }
        set { _maxAmmo = value; }
    }

    [SerializeField] protected float _range;
    public float Range {
        get { return _range; }
        set { _range = value; }
    }

    [SerializeField] protected float _fireRate;
    public float FireRate {
        get { return _fireRate; }
        set { _fireRate = value; }
    }

    [SerializeField] protected float _reloadTime;
    public float ReloadTime {
        get { return _reloadTime; }
        set { _reloadTime = value; }
    }

    [SerializeField] protected float _accuracyFacor;
    public float AccuracyFacor {
        get { return _accuracyFacor; }
        set { _accuracyFacor = value; }
    }

    [SerializeField] protected float _recoil;
    public float Recoil {
        get { return _recoil; }
        set { _recoil = value; }
    }

    public ParticleSystem Flash { get; set; }

    [SerializeField] private GameObject _hitEffect;
    public GameObject HitEffect {
        get { return _hitEffect; }
        set { _hitEffect = value; }
    }

    [SerializeField] private GameObject _smoke;
    public GameObject Smoke {
        get { return _smoke; }
        set { _smoke = value; }
    }

    private AmmoState _ammo;
    private bool _initialized;
    private readonly WeaponOperation _operation = new WeaponOperation();
    /// <summary>Raised for the specific weapon whose reload completed; subscribers must detach when abandoning that operation.</summary>
    public event Action<Weapon> ReloadCompleted;
    /// <summary>Unloaded rounds, excluding CurrentMagAmmo.</summary>
    public int ReserveAmmo { get { EnsureAmmo(); return _ammo.Reserve; } }
    /// <summary>Complete spare magazines used by HUD icons; a partial spare does not add an icon.</summary>
    public int FullReserveMagazines { get { EnsureAmmo(); return _ammo.FullReserveMagazines; } }

    private void EnsureAmmo()
    {
        if (_ammo == null) _ammo = new AmmoState(_maxMagAmmo, _maxAmmo, _currentAmmo, _currentMagAmmo);
    }
    private void SaveAmmo() { _currentAmmo = _ammo.Total; _currentMagAmmo = _ammo.Magazine; }

    public enum WeaponState {
        idle,
        shooting,
        reloading
    };

    [SerializeField] private WeaponState _state;

    public WeaponState State {
        get { return _state; }
        set { _state = value; if (value == WeaponState.idle) _operation.Cancel(); }
    }

    public enum FireMode {
        single,
        triple,
        continous
    };

    [SerializeField] private bool allowTriple;
    [SerializeField] private bool allowContinous;
    [SerializeField] private FireMode _defaultFireMode;
    [SerializeField] private FireMode _fireMode;

    public FireMode Mode {
        get { return _fireMode; }
        set { _fireMode = value; }
    }

    void Awake() { Initialize(); }

    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        if (_definition != null)
        {
            _id = _definition.LegacyIndex; _name = _definition.DisplayName; _damage = _definition.Damage;
            _maxMagAmmo = _definition.MagazineCapacity; _maxAmmo = _definition.MaximumAmmo;
            _range = _definition.Range; _fireRate = _definition.FireInterval; _reloadTime = _definition.ReloadSeconds;
            _recoil = _definition.Recoil; allowTriple = _definition.AllowBurst; allowContinous = _definition.AllowAutomatic;
            _defaultFireMode = (FireMode)_definition.DefaultMode;
            _currentAmmo = _maxAmmo; _currentMagAmmo = _maxMagAmmo;
        }
        EnsureAmmo();
        _operation.Completed += OperationCompleted;
        Flash = GetComponentInChildren<ParticleSystem>();
        _state = WeaponState.idle;
        _fireMode = _defaultFireMode;
    }

    void Start() { Initialize(); }

    void Update() { Tick(Time.deltaTime); }

    /// <summary>Advances this weapon operation by elapsed seconds and emits completion synchronously when due.</summary>
    public void Tick(float deltaTime)
    {
        _operation.Tick(deltaTime, _ammo);
        SaveAmmo();
    }

    private void OperationCompleted(WeaponActivity operation)
    {
        _state = WeaponState.idle;
        SaveAmmo();
        if (operation == WeaponActivity.Reloading) ReloadCompleted?.Invoke(this);
    }

    /// <summary>Starts the single reload operation when the magazine has space and reserve rounds exist.</summary>
    /// <returns>false if already reloading, full, or without reserve ammunition.</returns>
    public bool TryReload()
    {
        EnsureAmmo();
        if (_state == WeaponState.reloading || _ammo.Magazine >= _ammo.Capacity || _ammo.Reserve == 0) return false;
        _state = WeaponState.reloading;
        _operation.Begin(WeaponActivity.Reloading, _reloadTime);
        return true;
    }

    /// <summary>Cancels an unfinished reload without adding rounds or emitting ReloadCompleted.</summary>
    public void CancelReload()
    {
        if (_state != WeaponState.reloading) return;
        _operation.Cancel();
        _state = WeaponState.idle;
    }

    /// <summary>Consumes one loaded round and starts the firing operation unless empty or reloading.</summary>
    /// <remarks>The input controller enforces cadence; this method permits the authored shorter burst intervals.</remarks>
    public bool TryFire()
    {
        EnsureAmmo();
        // Burst intervals intentionally remain .8 of FireRate, as authored by PlayerShoot.
        if (_state == WeaponState.reloading || !_ammo.TryConsume()) return false;
        SaveAmmo();
        _state = WeaponState.shooting;
        _operation.Begin(WeaponActivity.Firing, _fireRate);
        return true;
    }

    // Kept for existing UnityEvents and external verification scripts.
    public void reload() { TryReload(); }
    public void shoot() { TryFire(); }

    // Cycles single -> triple -> continuous, skipping modes this weapon doesn't allow. The old version
    // skipped a disallowed triple straight into continuous without checking allowContinous.
    /// <summary>Cycles authored allowed fire modes while preserving the legacy UnityEvent entry point.</summary>
    public void changeFireMode() {
        for (int i = 0; i < 3; i++) {
            _fireMode = (FireMode) (((int) _fireMode + 1) % 3);
            if (IsAllowed(_fireMode)) return;
        }
    }

    private bool IsAllowed(FireMode mode) {
        return mode == FireMode.single
               || (mode == FireMode.triple && allowTriple)
               || (mode == FireMode.continous && allowContinous);
    }
    public void resetAmmo() {
        EnsureAmmo();
        _ammo.Reset();
        SaveAmmo();
    }
}
