using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Mirror;
using UnityEngine.AI;

public class PlayerShoot : NetworkBehaviour {
    public Camera Cam { get; set; }

    public PlayerEquipment Equipment { get; set; }

    private PlayerController _playerController;
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
    private float normalFOV;
    private float zoomFOV;
    [SerializeField] private GameObject _grenadePrefab;
    private float _grenadeTimer = 0f;
    public int _maxGrenades = 3;
    public int _grenades;

    public TextMeshProUGUI _grenadesTM;


    public bool IsBuildingOnFly { get; set; }
    // True while the hide/show animation of a weapon swap plays; the weapon must not fire meanwhile.
    public bool IsSwitchingWeapon { get; private set; }
    private const float BURST_RECOIL_SCALE = 0.5f;
    // Headshots deal 3x; anything above this is rejected by the server.
    private const float DAMAGE_MULTIPLIER_LIMIT = 3f;
    public bool WasBuilt { get; set; }
    private Animator weaponAnimator;
    private static readonly int IsAiming = Animator.StringToHash("isAiming");
    private static readonly int IsReloading = Animator.StringToHash("isReloading");
    private static readonly int IsSprinting = Animator.StringToHash("isSprinting");
    private static readonly int IsHidden = Animator.StringToHash("isHidden");
    private float currentRecoil;
    public float changeWeaponCooldown = 0;
    private Weapon activeWeapon = null;
    private bool _initialized;
    private Animator _playerAnimator;
    


    // Start is called before the first frame update
    IEnumerator Start() {
        _grenades = 0;
        source = GetComponent<AudioSource>();
        // Network-spawned players do not guarantee that PlayerSetup.Start has equipped
        // the weapon yet. Wait for that dependency instead of relying on script order.
        while (Equipment == null || Equipment.Weapon1 == null) yield return null;
        activeWeapon = Equipment.Weapon1;
        if (Cam == null) enabled = false;
        else {
            Cross = GameObject.Find("cross");
            IsBuildingOnFly = false;
        }

        if(transform.GetChild(0).GetChild(0).gameObject.activeSelf)
            _playerAnimator = transform.GetChild(0).GetChild(0).GetComponent<Animator>();
        else
            _playerAnimator = transform.GetChild(0).GetChild(1).GetComponent<Animator>();
        _playerController = GetComponent<PlayerController>();

        if (isLocalPlayer)
            _grenadesTM = GameObject.Find("Grenades").GetComponent<TextMeshProUGUI>();

        currentRecoil = activeWeapon.Recoil;

        if (!isLocalPlayer) activeWeapon.GetComponent<Animator>().enabled = false;
        _initialized = true;
    }


    void Update() {
        if (!_initialized || !isLocalPlayer || SettingsMenu.IsOpen) return;
        if (ButtonsControll.screensOver && !WinLoseScreens.winLoseActive && Equipment.getActiveWeapon() != null)
        { 
            currentRecoil = Equipment.getActiveWeapon().Recoil;
            normalFOV = GameSettings.FieldOfView;
            zoomFOV = normalFOV - GameSettings.AIM_FOV_REDUCTION;

            if (_grenades > _maxGrenades)
                _grenades = _maxGrenades;
            if (isLocalPlayer)
                _grenadesTM.text = _grenades.ToString();
            // Tuned for the old fixed 50 FPS cap; scaled by frame time so it decays the same at any frame rate.
            if (crossAccuracy > 1.02) crossAccuracy -= (crossAccuracy * 0.05f + 0.02f) * Time.deltaTime * 50f;
            if (crossAccuracy < 1.02f) crossAccuracy = 1f;
            Cross.transform.GetChild(0).transform.localScale = new Vector3(crossAccuracy, crossAccuracy, crossAccuracy);

            //changing weapon
            if (changeWeaponCooldown > 0) changeWeaponCooldown -= Time.deltaTime;
            if (changeWeaponCooldown <= 0 && Math.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.01 &&
                Equipment.getActiveWeapon().State == Weapon.WeaponState.idle && !IsBuildingOnFly && !IsSwitchingWeapon) {
                changeWeaponCooldown = 2;
                if (Equipment.Weapon2 != null) {
                    if (Equipment.Weapon1.gameObject.activeSelf) {
                        StartCoroutine(HideWeapon(Equipment.Weapon1.gameObject, Equipment.Weapon2.gameObject));    //show rifle
                    }
                    else {
                        StartCoroutine(HideWeapon(Equipment.Weapon2.gameObject, Equipment.Weapon1.gameObject));    //show pistol
                    }
                }
            }


            //fire mode
            if (Input.GetKeyDown(KeyCode.B) && !IsSwitchingWeapon) Equipment.getActiveWeapon().changeFireMode();

            //reloading: only with spare rounds left (CurrentAmmo counts the magazine too)
            Weapon weapon = Equipment.getActiveWeapon();
            if (Input.GetKeyDown(KeyCode.R) && weapon.State != Weapon.WeaponState.reloading && !IsSwitchingWeapon &&
                weapon.CurrentMagAmmo < weapon.MaxMagAmmo && weapon.CurrentAmmo > weapon.CurrentMagAmmo) {
                StartCoroutine(Reload());
            }

            if (Input.GetKey(KeyCode.G)) {
                if (_grenadeTimer <= 3f) {
                    _grenadeTimer += Time.deltaTime;
                }
            }
            if (Input.GetKeyUp(KeyCode.G)) {
                if (_grenades > 0)
                {
                    _grenades--;
                    // Thrown where the player looks (it used to always fly flat along the body's forward).
                    CmdSpawnGrenade(transform.position, transform.rotation, Cam.transform.forward,
                        (_grenadeTimer + 0.5f) / 3);
                    _playerAnimator.SetTrigger("throwing");
                    _grenadeTimer = 0f;
                }
                else
                {
                    source.clip = nono;
                    source.PlayOneShot(source.clip);
                    _grenadeTimer = 0f; // otherwise the next throw starts fully charged
                }
            }
            

            //fireing
            if (Input.GetButton("Fire1") && Equipment.getActiveWeapon().State == Weapon.WeaponState.idle &&
                !PauseGame.menuActive && Equipment.getActiveWeapon().CurrentMagAmmo >= 1 && !IsBuildingOnFly && !IsSwitchingWeapon) {
                Shoot();
            }

            if (Input.GetButtonUp("Fire1")) {
                _shootingDone = false;
                if (WasBuilt) {
                    IsBuildingOnFly = false;
                    WasBuilt = false;
                }
            }

            //aiming
            if (Input.GetButton("Fire2") &&
                (Equipment.getActiveWeapon().State == Weapon.WeaponState.idle ||
                 Equipment.getActiveWeapon().State == Weapon.WeaponState.shooting) &&
                !PauseGame.menuActive && !IsSwitchingWeapon) {
                currentRecoil = Equipment.getActiveWeapon().Recoil * 0.35f;
                if (isLocalPlayer) {
                    Equipment.getActiveWeapon().GetComponent<Animator>().SetBool(IsSprinting, false);
                    Equipment.getActiveWeapon().GetComponent<Animator>().SetBool(IsAiming, true);
                }

                Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, zoomFOV, 0.6f);
                Cross.gameObject.SetActive(false);
                _playerController.SensitivityScale = _playerController.ZoomSensitivity;
            }
            else {
                currentRecoil = Equipment.getActiveWeapon().Recoil;
                if (isLocalPlayer) Equipment.getActiveWeapon().GetComponent<Animator>().SetBool(IsAiming, false);
                Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, normalFOV, 0.6f);
                Cross.gameObject.SetActive(true);
                _playerController.SensitivityScale = _playerController.NonZoomSensitivity;
            }
        }
    }

    public IEnumerator HideWeapon(GameObject toHide, GameObject toShow) {
        IsSwitchingWeapon = true;
        toHide.gameObject.SetActive(true);
        toShow.gameObject.SetActive(true);
        
        toHide.transform.GetChild(0).gameObject.SetActive(true);
        toShow.transform.GetChild(0).gameObject.SetActive(false);

        toHide.transform.GetComponent<Animator>().SetBool(IsHidden, true);
        toShow.transform.GetComponent<Animator>().SetBool(IsHidden, true);
        yield return new WaitForSeconds(0.5f);    
        toHide.transform.GetChild(0).gameObject.SetActive(false);
        toShow.transform.GetChild(0).gameObject.SetActive(true);
        if(toShow.transform.GetComponent<Weapon>().Id == 1)
            playSound(rifle);
        else
            playSound(pistol);
        yield return new WaitForSeconds(0.5f); 
        if (toShow.transform.GetComponent<Weapon>().Id == 0) {
            toShow.transform.localPosition = new Vector3(0.02f, 0.03f, -0.22f);
            toShow.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
        else {
            toShow.transform.localPosition = new Vector3(0.08f, -0.02f, -0.17f);
            toShow.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
        
        toShow.transform.GetComponent<Animator>().SetBool(IsHidden, false);
        toHide.transform.GetComponent<Animator>().SetBool(IsHidden, false);
        toHide.gameObject.SetActive(false);
        toShow.gameObject.SetActive(true);
        IsSwitchingWeapon = false;
        Equipment.CmdSetActiveWeapon(toShow.GetComponent<Weapon>().Id);
    }

    private void OnDisable() {
        // Death or a blocked weapon disables this component mid-swap; don't stay locked afterwards.
        IsSwitchingWeapon = false;
    }
    
    IEnumerator Reload() {
        if (isLocalPlayer) Equipment.getActiveWeapon().GetComponent<Animator>().SetBool(IsReloading, true);
        //Blackened = true;
        Equipment.getActiveWeapon().reload();
        playSound(reload);
        yield return new WaitForSeconds(Equipment.getActiveWeapon().ReloadTime);
        //Blackened = false;
        if (isLocalPlayer) Equipment.getActiveWeapon().GetComponent<Animator>().SetBool(IsReloading, false);
    }

    // Burst of three with half recoil. The old code halved Weapon.Recoil around StartCoroutine, which
    // had no effect (recoil is read from currentRecoil), and the rest of a burst could be fired
    // from a different weapon after a swap.
    IEnumerator TripleShot() {
        Weapon weapon = Equipment.getActiveWeapon();
        for (int i = 0; i < 3; i++) {
            if (Equipment.getActiveWeapon() != weapon || IsSwitchingWeapon || weapon.State == Weapon.WeaponState.reloading)
                yield break;
            PerformWeaponFire(BURST_RECOIL_SCALE);
            yield return new WaitForSeconds(weapon.FireRate * 0.8f);
        }
    }


    void Shoot() {
        crossAccuracy += 2f - crossAccuracy * 0.5f;
        Equipment.getActiveWeapon().GetComponent<Animator>().SetBool(IsSprinting, false);


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


    void PerformWeaponFire(float recoilScale = 1f) {
        if (Equipment.getActiveWeapon().CurrentMagAmmo >= 1) {
            Equipment.PlayerShooting();
            playSootSound();
            Equipment.getActiveWeapon().shoot();
            gameObject.GetComponent<PlayerMotor>().IncreaseRecoil(currentRecoil * recoilScale);
            CmdPlayerShooting();
            RaycastHit hit;
            if (Physics.Raycast(Cam.transform.position, Cam.transform.forward, out hit,
                Equipment.getActiveWeapon().Range,
                _mask, QueryTriggerInteraction.Ignore)) {
                bool enemyHit = hit.collider.CompareTag("EnemyHead") || hit.collider.CompareTag("EnemyBody") || hit.collider.CompareTag("EnemyLegs");
                bool usesEnemyFeedback = enemyHit && hit.collider.GetComponentInParent<EnemyFeedback>() != null;
                if (hit.collider.CompareTag("Player")) {
                    var target = hit.collider.GetComponentInParent<PlayerManager>();
                    if (target != null && !target.IsDead && GameManager.Instance.CanDamagePlayers) {
                        StartCoroutine(ShowHitmarker());
                        CmdPlayerShoot(target.transform.name, Equipment.getActiveWeapon().Damage);
                    }
                }
                else if (enemyHit) {
                    StartCoroutine(ShowHitmarker());
                    var enemy = hit.collider.GetComponentInParent<EnemyControllerServer>();
                    if (enemy != null) {
                        float multiplier = hit.collider.CompareTag("EnemyHead") ? 3f : hit.collider.CompareTag("EnemyBody") ? 2f : 1f;
                        CmdEnemyShoot(enemy.transform.name, multiplier * Equipment.getActiveWeapon().Damage,
                            enemy.transform.InverseTransformPoint(hit.point), enemy.transform.InverseTransformDirection(hit.normal));
                    }
                }
                else if (hit.collider.tag == "Barrel") {
                    Barrel barrel = hit.collider.GetComponent<Barrel>();
                    //CmdSetAuthAndExplode(barrel.netId, GetComponent<NetworkIdentity>());
                    CmdExplodeBarrel(barrel.netId);
                //barrel.Explode();
                    //barrel.CmdExplodeBarrel(barrel.InitialPosAndTag);

                }


                // Enemy hits use their server-confirmed green effect, once on every peer.
                if (!usesEnemyFeedback) {
                    Equipment.DoHitEffect(hit.point, hit.normal);
                    CmdOnHit(hit.point, hit.normal);
                }
            }

            if (Equipment.getActiveWeapon().CurrentMagAmmo == 0 && Equipment.getActiveWeapon().CurrentAmmo >= 1)
                StartCoroutine(Reload());
        }
    }

    [Command]
    public void CmdExplodeBarrel(uint objectId)
    {
        // Another shot or a chain reaction may already have destroyed it.
        NetworkIdentity barrelIdentity;
        if (!NetworkServer.spawned.TryGetValue(objectId, out barrelIdentity) || barrelIdentity == null) return;
        Barrel barrel = barrelIdentity.GetComponent<Barrel>();
        if (barrel != null) barrel.Explode();
    }

    private void playSound(AudioClip clip)
    {
//        if (clip == hitMarker)
//            source.volume = 1.0f;
//        else
//            source.volume = 0.7f;
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
        yield return new WaitForSeconds(0.15f);
        Cross.transform.GetChild(1).gameObject.SetActive(false);
    }

    [Command]
    void CmdPlayerShooting() {
        Equipment.RpcPlayerShooting();
    }

    [Command]
    void CmdOnHit(Vector3 hitPoint, Vector3 normal) {
        Equipment.RpcDoHitEffect(hitPoint, normal);
    }

    // The client reports hits (no lag compensation yet), so at least reject impossible damage.
    [Server]
    private bool IsPlausibleDamage(float damage) {
        PlayerManager self = GetComponent<PlayerManager>();
        if (self == null || self.IsDead || damage <= 0f) return false;
        float maxDamage = 0f;
        foreach (GameObject weaponPrefab in GameManager.Instance.Weapons) {
            Weapon weapon = weaponPrefab != null ? weaponPrefab.GetComponent<Weapon>() : null;
            if (weapon != null) maxDamage = Mathf.Max(maxDamage, weapon.Damage);
        }
        return damage <= maxDamage * DAMAGE_MULTIPLIER_LIMIT;
    }

    [Command]
    void CmdEnemyShoot(string shootEnemyId, float damage, Vector3 localPoint, Vector3 localNormal) {
        EnemyControllerServer enemy = GameManager.GetEnemy(shootEnemyId);
        if (enemy == null || enemy._isDying || !IsPlausibleDamage(damage)) return;
        var feedback = enemy.GetComponent<EnemyFeedback>();
        // Impact validation is cosmetic; it does not replace the existing damage validation.
        bool validImpact = feedback != null && feedback.IsValidImpact(localPoint, localNormal);
        enemy.TakeDamage(damage);
        if (validImpact) feedback.BulletHit(localPoint, localNormal);
    }


    public void InvokeCmdPlayerShoot(string shootPlayerId, float damage) {
        CmdPlayerShoot(shootPlayerId, damage);
    }

    [Command]
    void CmdPlayerShoot(string shootPlayerId, float damage) {
        PlayerManager target = GameManager.GetPlayer(shootPlayerId);
        if (target != null && IsPlausibleDamage(damage)) target.TakeDamage(damage);
    }

    [Command]
    void CmdSpawnGrenade(Vector3 playerPos, Quaternion playerRot, Vector3 aimDirection, float force) {
        // Charge is (0.5..3.5)/3 on the client; clamp what a client may send.
        force = Mathf.Clamp(force, 0f, 3.5f / 3f);
        aimDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : transform.forward;
        // Spawn in front of the body at chest height, independent of the aim pitch, so it never starts in the floor.
        Vector3 flatForward = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = transform.forward;
        GameObject grenade = Instantiate(_grenadePrefab, playerPos + flatForward.normalized + Vector3.up / 2, playerRot);
        Rigidbody rb = grenade.GetComponent<Rigidbody>();
        rb.linearVelocity = force * (15 * aimDirection + 5 * Vector3.up);
        NetworkServer.Spawn(grenade);
    }
}