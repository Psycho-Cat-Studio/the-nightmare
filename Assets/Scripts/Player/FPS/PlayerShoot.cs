using System;
using System.Collections;
using TMPro;
using UnityEngine;
using Nightmare.Core.Combat;
using Nightmare.Configuration;

/// <summary>Coordinates local weapon input, operation presentation and requests to authoritative combat adapters.</summary>
public class PlayerShoot : MonoBehaviour, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    public Camera Cam { get; set; }

    public PlayerEquipment Equipment { get; set; }

    private IPlayerCombatRequests _requests;
    public void InitializeCombat(IPlayerCombatRequests requests) { _requests = requests; }
    public GameObject GrenadePrefab => _grenadePrefab;
    public bool isLocalPlayer => _playerController != null && _playerController.isLocalPlayer;
    private PlayerController _playerController;
    private PlayerMotor _motor;
    private WeaponPresentation _presentation;
    public AudioSource source { get; set; }
    public AudioClip shot;
    [SerializeField] private AudioClip _rifleShot;
    private AudioSource[] _rifleVoices;
    private int _nextRifleVoice;
    public AudioClip hitMarker;
    public AudioClip reload;
    public AudioClip pistol;
    public AudioClip rifle;
    public AudioClip nono;

    public GameObject Cross;

    [SerializeField] private LayerMask _mask;
    private bool _shootingDone = false;
    private float crossAccuracy = 1;
    [SerializeField] private GameObject _grenadePrefab;
    private float _grenadeTimer = 0f;
    public int _maxGrenades = 3;
    public int _grenades;

    public TextMeshProUGUI _grenadesTM;

    /// <summary>Whether FPS placement preview is active; this does not change the match to overhead building mode.</summary>
    public bool IsBuildingOnFly { get; set; }
    /// <summary>Whether the hide/show swap operation is in progress and firing must remain blocked.</summary>
    public bool IsSwitchingWeapon { get; private set; }
    private const float BURST_RECOIL_SCALE = 0.5f;
    /// <summary>Accepted-placement latch cleared when releasing fire exits FPS placement mode.</summary>
    public bool WasBuilt { get; set; }
    [SerializeField] private WeaponHandlingSettings _handling = new WeaponHandlingSettings();
    private const float ScrollThreshold = 0.01f;
    private const float BurstIntervalScale = 0.8f;
    private const int BurstShotCount = 3;
    private float currentRecoil;
    public float changeWeaponCooldown = 0;
    private Weapon _reloadingWeapon;
    private GameObject _swapFrom, _swapTo;
    private bool _initialized;
    private Animator _playerAnimator;

    IEnumerator Start() {
        _playerController = GetComponent<PlayerController>();
        _grenades = 0;
        source = GetComponent<AudioSource>();
        // Network-spawned players do not guarantee that PlayerSetup.Start has equipped
        // the weapon yet. Wait for that dependency instead of relying on script order.
        while (Equipment == null || Equipment.Weapon1 == null) yield return null;
        var activeWeapon = Equipment.Weapon1;
        if (Cam == null) enabled = false;
        else {
            Cross = _game.SceneBindings.Crosshair;
            IsBuildingOnFly = false;
        }

        _playerAnimator = GetComponent<PlayerRigBindings>().ActiveAnimator;
        _playerController = GetComponent<PlayerController>();
        _motor = GetComponent<PlayerMotor>();
        if (isLocalPlayer)
        {
            var holder = GetComponent<PlayerRigBindings>().WeaponHolder;
            _presentation = holder.GetComponent<WeaponPresentation>();
            if (_presentation == null) _presentation = holder.gameObject.AddComponent<WeaponPresentation>();
            _presentation.Initialize(_playerController, _motor, Equipment);
        }

        if (isLocalPlayer)
            _grenadesTM = _game.SceneBindings.Grenades;

        currentRecoil = activeWeapon.Recoil;

        if (!isLocalPlayer) activeWeapon.GetComponent<Animator>().enabled = false;
        _initialized = true;
    }

    void Update()
    {
        if (!_initialized || !isLocalPlayer) return;
        if (!_playerController.CanControl)
        {
            SuspendInput();
            return;
        }
        if (!ButtonsControll.screensOver || WinLoseScreens.winLoseActive || Equipment.getActiveWeapon() == null) return;

        currentRecoil = Equipment.getActiveWeapon().Recoil * (_playerController.IsAiming ? _handling.AdsRecoilMultiplier : 1f);
        float normalFov = GameSettings.FieldOfView;
        // Keep the original order. Swap coroutines can change the active weapon before their first yield.
        UpdateGrenadeHud();
        UpdateCrosshair();
        HandleWeaponSwitch();
        HandleFireMode();
        HandleReloadInput();
        HandleGrenades();
        HandleFireInput();
        UpdateAiming(normalFov);
    }

    private void SuspendInput()
    {
        _shootingDone = Input.GetButton(GameInput.Fire);
        _grenadeTimer = 0f;
        var weapon = Equipment.getActiveWeapon();
        if (weapon != null) weapon.GetComponent<Animator>().SetBool(AnimationParameters.Aiming, false);
    }

    private void UpdateGrenadeHud()
    {
        if (_grenades > _maxGrenades) _grenades = _maxGrenades;
        _grenadesTM.text = _grenades.ToString();
    }

    private void UpdateCrosshair()
    {
        // Continuous equivalent of the original fixed-frame recovery; keep both threshold comparisons.
        if (crossAccuracy > _handling.CrosshairRestThreshold)
            crossAccuracy = (crossAccuracy + _handling.CrosshairRecoveryBias)
                * Mathf.Pow(_handling.CrosshairRecoveryPerFrame, Time.deltaTime * _handling.ReferenceFrameRate)
                - _handling.CrosshairRecoveryBias;
        if (crossAccuracy < _handling.CrosshairRestThreshold) crossAccuracy = 1f;
        Cross.transform.GetChild(0).localScale = Vector3.one * crossAccuracy;
    }

    private void HandleWeaponSwitch()
    {
        if (changeWeaponCooldown > 0f) changeWeaponCooldown -= Time.deltaTime;
        if (changeWeaponCooldown > 0f || Math.Abs(Input.GetAxis(GameInput.Scroll)) <= ScrollThreshold
            || Equipment.getActiveWeapon().State != Weapon.WeaponState.idle || IsBuildingOnFly || IsSwitchingWeapon) return;
        changeWeaponCooldown = _handling.SwapCooldownSeconds;
        if (Equipment.Weapon2 == null) return;
        var from = Equipment.Weapon1.gameObject.activeSelf ? Equipment.Weapon1 : Equipment.Weapon2;
        var to = from == Equipment.Weapon1 ? Equipment.Weapon2 : Equipment.Weapon1;
        StartCoroutine(HideWeapon(from.gameObject, to.gameObject));
    }

    private void HandleFireMode()
    {
        if (Input.GetKeyDown(KeyCode.B) && !IsSwitchingWeapon) Equipment.getActiveWeapon().changeFireMode();
    }

    private void HandleReloadInput()
    {
        var weapon = Equipment.getActiveWeapon();
        if (Input.GetKeyDown(KeyCode.R) && weapon.State != Weapon.WeaponState.reloading && !IsSwitchingWeapon
            && weapon.CurrentMagAmmo < weapon.MaxMagAmmo && weapon.CurrentAmmo > weapon.CurrentMagAmmo)
            StartCoroutine(Reload());
    }

    private void HandleGrenades()
    {
        if (Input.GetKey(KeyCode.G) && _grenadeTimer <= GrenadeThrowRules.MaximumChargeSeconds)
            _grenadeTimer += Time.deltaTime;
        if (!Input.GetKeyUp(KeyCode.G)) return;
        if (_grenades > 0)
        {
            _grenades--;
            _requests.ThrowGrenade(transform.position, transform.rotation, Cam.transform.forward,
                GrenadeThrowRules.ForceFromCharge(_grenadeTimer));
            _playerAnimator.SetTrigger(AnimationParameters.Throwing);
        }
        else playSound(nono);
        _grenadeTimer = 0f;
    }

    private void HandleFireInput()
    {
        var weapon = Equipment.getActiveWeapon();
        if (Input.GetButton(GameInput.Fire) && weapon.State == Weapon.WeaponState.idle && !PauseGame.menuActive
            && weapon.CurrentMagAmmo >= 1 && !IsBuildingOnFly && !IsSwitchingWeapon) Shoot();
        if (!Input.GetButtonUp(GameInput.Fire)) return;
        _shootingDone = false;
        if (!WasBuilt) return;
        IsBuildingOnFly = false;
        WasBuilt = false;
    }

    private void UpdateAiming(float normalFov)
    {
        var weapon = Equipment.getActiveWeapon();
        bool aiming = Input.GetButton(GameInput.Aim)
            && (weapon.State == Weapon.WeaponState.idle || weapon.State == Weapon.WeaponState.shooting)
            && !PauseGame.menuActive && !IsSwitchingWeapon;
        currentRecoil = weapon.Recoil * (aiming ? _handling.AdsRecoilMultiplier : 1f);
        var animator = weapon.GetComponent<Animator>();
        if (aiming) animator.SetBool(AnimationParameters.Sprinting, false);
        animator.SetBool(AnimationParameters.Aiming, aiming);
        float targetFov = aiming ? normalFov - GameSettings.AIM_FOV_REDUCTION : normalFov;
        Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, targetFov, 1f - Mathf.Exp(-_handling.AdsDamping * Time.deltaTime));
        Cross.SetActive(!aiming);
        _playerController.SensitivityScale = aiming ? _playerController.ZoomSensitivity : _playerController.NonZoomSensitivity;
    }

    public IEnumerator HideWeapon(GameObject toHide, GameObject toShow) {
        IsSwitchingWeapon = true;
        _swapFrom = toHide;
        _swapTo = toShow;
        toHide.gameObject.SetActive(true);
        toShow.gameObject.SetActive(true);

        toHide.transform.GetChild(0).gameObject.SetActive(true);
        toShow.transform.GetChild(0).gameObject.SetActive(false);

        toHide.transform.GetComponent<Animator>().SetBool(AnimationParameters.Hidden, true);
        toShow.transform.GetComponent<Animator>().SetBool(AnimationParameters.Hidden, true);
        yield return new WaitForSeconds(_handling.SwapHalfSeconds);
        toHide.transform.GetChild(0).gameObject.SetActive(false);
        toShow.transform.GetChild(0).gameObject.SetActive(true);
        if(toShow.transform.GetComponent<Weapon>().Id == 1)
            playSound(rifle);
        else
            playSound(pistol);
        yield return new WaitForSeconds(_handling.SwapHalfSeconds);
        ApplyFirstPersonPose(toShow.GetComponent<Weapon>());

        toShow.transform.GetComponent<Animator>().SetBool(AnimationParameters.Hidden, false);
        toHide.transform.GetComponent<Animator>().SetBool(AnimationParameters.Hidden, false);
        toHide.gameObject.SetActive(false);
        toShow.gameObject.SetActive(true);
        IsSwitchingWeapon = false;
        _swapFrom = _swapTo = null;
        Equipment.CmdSetActiveWeapon(toShow.GetComponent<Weapon>().Id);
    }

    private static void ApplyFirstPersonPose(Weapon weapon)
    {
        if (weapon.Definition != null) weapon.transform.localPosition = weapon.Definition.FirstPersonPosition;
        else weapon.transform.localPosition = weapon.Id == 0 ? new Vector3(.02f, .03f, -.22f) : new Vector3(.08f, -.02f, -.17f);
        weapon.transform.localRotation = Quaternion.identity;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (_reloadingWeapon != null)
        {
            _reloadingWeapon.ReloadCompleted -= OnReloadCompleted;
            _reloadingWeapon.CancelReload();
            _reloadingWeapon.GetComponent<Animator>().SetBool(AnimationParameters.Reloading, false);
            _reloadingWeapon = null;
        }
        if (_swapFrom != null)
        {
            _swapFrom.SetActive(true);
            _swapFrom.transform.GetChild(0).gameObject.SetActive(true);
            _swapFrom.GetComponent<Animator>().SetBool(AnimationParameters.Hidden, false);
        }
        if (_swapTo != null)
        {
            _swapTo.GetComponent<Animator>().SetBool(AnimationParameters.Hidden, false);
            _swapTo.SetActive(false);
        }
        _swapFrom = _swapTo = null;
        IsSwitchingWeapon = false;
        _grenadeTimer = 0f;
        _shootingDone = false;
    }

    IEnumerator Reload()
    {
        Weapon weapon = Equipment.getActiveWeapon();
        if (weapon == null || !weapon.TryReload()) yield break;
        _reloadingWeapon = weapon;
        weapon.ReloadCompleted += OnReloadCompleted;
        weapon.GetComponent<Animator>().SetBool(AnimationParameters.Reloading, true);
        playSound(reload);
        yield break;
    }

    private void OnReloadCompleted(Weapon weapon)
    {
        weapon.ReloadCompleted -= OnReloadCompleted;
        weapon.GetComponent<Animator>().SetBool(AnimationParameters.Reloading, false);
        if (_reloadingWeapon == weapon) _reloadingWeapon = null;
    }

    // Burst of three with half recoil. The old code halved Weapon.Recoil around StartCoroutine, which
    // had no effect (recoil is read from currentRecoil), and the rest of a burst could be fired
    // from a different weapon after a swap.
    IEnumerator TripleShot() {
        Weapon weapon = Equipment.getActiveWeapon();
        for (int i = 0; i < BurstShotCount; i++) {
            if (!_playerController.CanControl || !isActiveAndEnabled || Equipment.getActiveWeapon() != weapon || IsSwitchingWeapon || weapon.State == Weapon.WeaponState.reloading)
                yield break;
            PerformWeaponFire(BURST_RECOIL_SCALE);
            yield return new WaitForSeconds(weapon.FireRate * BurstIntervalScale);
        }
    }

    void Shoot() {
        Equipment.getActiveWeapon().GetComponent<Animator>().SetBool(AnimationParameters.Sprinting, false);

        if (Equipment.getActiveWeapon().Mode == Weapon.FireMode.single && !_shootingDone) {
            PerformWeaponFire();
            _shootingDone = true;
        }
        else if (Equipment.getActiveWeapon().Mode == Weapon.FireMode.triple && !_shootingDone) {
            StartCoroutine(TripleShot());
            _shootingDone = true;
        }
        else if (Equipment.getActiveWeapon().Mode == Weapon.FireMode.continous) {
            PerformWeaponFire();
        }
    }

    void PerformWeaponFire(float recoilScale = 1f)
    {
        if (!_playerController.CanControl || IsSwitchingWeapon || IsBuildingOnFly) return;
        if (!Equipment.getActiveWeapon().TryFire()) return;
        // Capture aim before presentation adds this shot's recoil.
        _motor.ApplyView();
        Vector3 origin = Cam.transform.position;
        Vector3 direction = Cam.transform.forward;
        PresentShot(recoilScale);
        TraceShot(origin, direction);
        var weapon = Equipment.getActiveWeapon();
        if (weapon.CurrentMagAmmo == 0 && weapon.CurrentAmmo >= 1) StartCoroutine(Reload());
    }

    private void PresentShot(float recoilScale)
    {
        crossAccuracy += 2f - crossAccuracy * 0.5f;
        _presentation?.Shot(recoilScale * (_playerController.IsAiming ? _handling.AdsRecoilMultiplier : 1f));
        Equipment.PlayerShooting();
        playSootSound();
        _motor.IncreaseRecoil(currentRecoil * recoilScale);
        _requests.Shot();
    }

    private void TraceShot(Vector3 origin, Vector3 direction)
    {
        if (!Physics.Raycast(origin, direction, out var hit, Equipment.getActiveWeapon().Range,
            _mask, QueryTriggerInteraction.Ignore)) return;
        bool enemyHit = IsEnemyCollider(hit.collider);
        bool confirmedEffect = enemyHit && (hit.collider.GetComponentInParent<IEnemyHitTarget>()?.UsesConfirmedHitEffects ?? false);
        if (hit.collider.CompareTag(GameTags.Player)) HitPlayer(hit.collider);
        else if (enemyHit) HitEnemy(hit);
        else if (hit.collider.CompareTag(GameTags.Barrel))
            _requests.ExplodeBarrel(hit.collider.GetComponent<IExplosiveHitTarget>().EntityId);
        if (confirmedEffect) return;
        Equipment.DoHitEffect(hit.point, hit.normal);
        _requests.EnvironmentHit(hit.point, hit.normal);
    }

    private static bool IsEnemyCollider(Collider collider)
    {
        return collider.CompareTag(GameTags.EnemyHead) || collider.CompareTag(GameTags.EnemyBody)
            || collider.CompareTag(GameTags.EnemyLegs);
    }

    private void HitPlayer(Collider collider)
    {
        var target = collider.GetComponentInParent<IPlayerHitTarget>();
        if (target == null || target.IsDead || !GetComponent<PlayerAuthority>().Match.CanDamagePlayers) return;
        StartCoroutine(ShowHitmarker());
        _requests.PlayerHit(target.EntityId, Equipment.getActiveWeapon().Damage);
    }

    private void HitEnemy(RaycastHit hit)
    {
        StartCoroutine(ShowHitmarker());
        var target = hit.collider.GetComponentInParent<IEnemyHitTarget>();
        if (target == null) return;
        float multiplier = hit.collider.CompareTag(GameTags.EnemyHead) ? CombatRules.HeadDamageMultiplier
            : hit.collider.CompareTag(GameTags.EnemyBody) ? CombatRules.BodyDamageMultiplier : CombatRules.LegDamageMultiplier;
        _requests.EnemyHit(target.EntityId, multiplier * Equipment.getActiveWeapon().Damage,
            target.Root.InverseTransformPoint(hit.point), target.Root.InverseTransformDirection(hit.normal));
    }

    private void playSound(AudioClip clip)
    {
        source.clip = clip;
        source.PlayOneShot(source.clip);
    }

    public void playSootSound()
    {
        if (Equipment == null || Equipment.getActiveWeapon() == null || Equipment.getActiveWeapon().Id != 1 || _rifleShot == null)
        {
            playSound(shot);
            return;
        }
        if (_rifleVoices == null)
        {
            _rifleVoices = new AudioSource[2];
            for (int i = 0; i < _rifleVoices.Length; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.clip = _rifleShot;
                voice.spatialBlend = source.spatialBlend;
                voice.rolloffMode = source.rolloffMode;
                voice.minDistance = source.minDistance;
                voice.maxDistance = source.maxDistance;
                if (source.rolloffMode == AudioRolloffMode.Custom)
                    voice.SetCustomCurve(AudioSourceCurveType.CustomRolloff, source.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
                voice.outputAudioMixerGroup = source.outputAudioMixerGroup;
                voice.priority = source.priority;
                voice.volume = source.volume * 0.9f;
                voice.dopplerLevel = 0;
                voice.bypassReverbZones = true;
                _rifleVoices[i] = voice;
            }
        }
        // Two bounded dry voices retain the shot's bass body without stacking long echoes.
        var next = _rifleVoices[_nextRifleVoice];
        _nextRifleVoice = (_nextRifleVoice + 1) % _rifleVoices.Length;
        next.Stop();
        next.Play();
    }

    IEnumerator ShowHitmarker() {
        playSound(hitMarker);
        Cross.transform.GetChild(1).gameObject.SetActive(true);
        yield return new WaitForSeconds(_handling.HitmarkerSeconds);
        Cross.transform.GetChild(1).gameObject.SetActive(false);
    }

    // Compatibility entry point used by existing verification tools.
    public void InvokeCmdPlayerShoot(string playerId, float damage) { _requests.PlayerHit(playerId, damage); }
}
