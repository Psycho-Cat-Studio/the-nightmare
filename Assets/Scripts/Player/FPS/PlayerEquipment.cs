using TMPro;
using UnityEngine;

/// <summary>Owns local pickup interactions and the two-slot loadout, plus presentation of replicated remote weapons.</summary>
public class PlayerEquipment : MonoBehaviour, IPlayerLoadout, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    public GameObject ActiveWeapon => getActiveWeapon() != null ? getActiveWeapon().gameObject : null;
    public bool IsSwitchingWeapon => _shoot != null && _shoot.IsSwitchingWeapon;
    private IEquipmentRequests _requests;
    private PlayerAuthority _authority;
    public bool isLocalPlayer => _authority != null && _authority.IsLocalPlayer;
    public void InitializeEquipment(IEquipmentRequests requests) { _requests = requests; }
    void Awake() { _authority = GetComponent<PlayerAuthority>(); _rig = GetComponent<PlayerRigBindings>(); }
    public AudioSource WeaponSound { get; set; }
    public Weapon Weapon1 { get; set; }
    public Weapon Weapon2 { get; set; }
    [SerializeField] private Camera _cam;
    [SerializeField] private LayerMask _mask;
    private TextMeshProUGUI pickUp;
    private PlayerShoot _shoot;
    private PlacementController _controller;
    [SerializeField, Min(0f)] private float _interactionDistance = 0.75f;
    private PlayerRigBindings _rig;
    private const float ImpactLifetimeSeconds = 2f;
    private AudioSource _source;
    [SerializeField] private AudioClip _pick;
    [SerializeField] private AudioClip _pickRifle;

    private void Start() {
        if (isLocalPlayer) {
            pickUp = _game.SceneBindings.Interaction;
        }
        else {
            _rig.BuildingRoot.SetActive(false);//turn off camera
            if (_rig.RemoteArrow != null) _rig.RemoteArrow.SetActive(false);//turn off arrow
        }

        _shoot = GetComponent<PlayerShoot>();
        _controller = GetComponent<PlacementController>();
        Weapon2 = null;
        _source = GetComponent<AudioSource>();
    }

    public Weapon getActiveWeapon() {
        if (Weapon1 == null) return null;
        if (Weapon1.gameObject.activeSelf)
            return Weapon1;
        return Weapon2;
    }

    private void Update()
    {
        if (!isLocalPlayer || SettingsMenu.IsOpen) return;
        if (Physics.Raycast(_cam.transform.position, _cam.transform.forward, out var hit, _interactionDistance, _mask))
            HandleInteraction(hit.collider);
        else pickUp.enabled = false;
    }

    private void HandleInteraction(Collider target)
    {
        if (target.CompareTag(GameTags.RemovableChairs))
        {
            ShowPrompt("Press E to Remove Chairs");
            if (Input.GetKeyDown(KeyCode.E)) _requests.RemoveChairs(target.transform.position);
        }
        else if (target.CompareTag(GameTags.Weapon)) HandleWeaponPickup(target);
        else if (target.CompareTag(GameTags.Reviving))
        {
            ShowPrompt("Press E to revive");
            if (Input.GetKeyDown(KeyCode.E)) _requests.Revive(target.GetComponentInParent<PlayerManager>().transform.name);
        }
        else if (target.CompareTag(GameTags.Chest)) HandleChestPickup(target);
        else pickUp.enabled = false;
    }

    private void ShowPrompt(string message)
    {
        pickUp.enabled = true;
        pickUp.text = message;
    }

    private void HandleWeaponPickup(Collider target)
    {
        var spawn = target.GetComponentInParent<GunSpawnPoint>();
        int index = spawn != null ? spawn.WeaponId : 1;
        var prefabs = _game.Weapons;
        if (index < 0 || index >= prefabs.Length) return;
        var weapon = prefabs[index].GetComponent<Weapon>();
        string displayName = weapon.Definition != null ? weapon.Definition.DisplayName : weapon.Name;
        ShowPrompt("Press E to pick up " + displayName);
        if (Input.GetKeyDown(KeyCode.E)) TryCollectWeapon(index);
    }

    private void HandleChestPickup(Collider target)
    {
        var chest = target.GetComponentInParent<ChestAlwaysFull>();
        pickUp.enabled = chest.active && !chest.alreadyPicked;
        if (!pickUp.enabled) return;
        pickUp.text = "Press E to pick up collectibles";
        if (Input.GetKeyDown(KeyCode.E)) CollectChest(chest);
    }

    /// <summary>Applies authored local chest grants with placeable capacity limits and a same-frame duplicate guard.</summary>
    /// <remarks>Chests intentionally become collectible again on subsequent updates.</remarks>
    public void CollectChest(ChestAlwaysFull chest)
    {
        if (!isLocalPlayer || chest == null || !chest.active || chest.alreadyPicked) return;
        _source.PlayOneShot(_pick);
        _shoot._grenades += chest.grenades;
        var catalog = _game.Content;
        foreach (var grant in chest.GetPlaceableGrants(catalog))
        {
            int index = System.Array.IndexOf(catalog.Placeables, grant.Item);
            if (index < 0 || index >= _controller.placeableCount.Length) continue;
            if (_controller.hasNoPlaceable() && _shoot.IsBuildingOnFly && chest.grenades == 0)
                _controller.InstantietePlaceable(index);
            _controller.placeableCount[index] = Mathf.Min(
                _controller.maxPlaceable[index], _controller.placeableCount[index] + grant.Amount);
        }
        chest.alreadyPicked = true;
    }

    /// <summary>Collects a catalog weapon into the two-slot loadout, or refills the same weapon while preserving loaded rounds.</summary>
    /// <returns>Whether local ownership and current weapon-operation state permitted collection.</returns>
    public bool TryCollectWeapon(int index)
    {
        var prefabs = _game.Weapons;
        var active = getActiveWeapon();
        if (!isLocalPlayer || active == null || active.State != Weapon.WeaponState.idle
            || _shoot.IsSwitchingWeapon || index < 0 || index >= prefabs.Length) return false;
        _source.PlayOneShot(_pickRifle);
        var existing = FindCarriedWeapon(index);
        if (existing != null)
        {
            existing.CurrentAmmo = existing.MaxAmmo;
            return true;
        }
        if (Weapon2 != null) Destroy(Weapon2.gameObject);
        var instance = Instantiate(prefabs[index], _rig.WeaponHolder);
        Weapon2 = instance.GetComponent<Weapon>();
        Weapon1.transform.localPosition = Weapon1.Definition != null ? Weapon1.Definition.FirstPersonPosition : new Vector3(.02f, .03f, -.22f);
        Weapon1.transform.localRotation = Quaternion.identity;
        Weapon1.gameObject.SetActive(false);
        CmdSetActiveWeapon(index);
        return true;
    }

    private Weapon FindCarriedWeapon(int id)
    {
        if (Weapon1.Id == id) return Weapon1;
        if (Weapon2 != null && Weapon2.Id == id) return Weapon2;
        return null;
    }

    public void PlayerShooting() {
        getActiveWeapon().Flash.Play();
        GameObject smokeEffect =
            Instantiate(getActiveWeapon().Smoke, getActiveWeapon().transform.GetChild(0).position,
                Quaternion.Euler(-90, 0, 0));
        Destroy(smokeEffect, ImpactLifetimeSeconds);
    }

    // Which weapon a player holds is shown to everybody else. Before, remote players always held the
    // pistol model (the old RpcChangeWeapon was commented out because it indexed hand children that
    // did not exist yet), so muzzle flashes and smoke also came from the wrong gun.
    public void CmdSetActiveWeapon(int weaponId) { _requests.SetActiveWeapon(weaponId); }

    public void ShowRemoteWeapon(int weaponId) {
        if (isLocalPlayer || Weapon1 == null) return;

        if (weaponId == Weapon1.Id) {
            Weapon1.gameObject.SetActive(true);
            if (Weapon2 != null) Weapon2.gameObject.SetActive(false);
            return;
        }

        if (Weapon2 == null || Weapon2.Id != weaponId) {
            if (Weapon2 != null) Destroy(Weapon2.gameObject);
            Weapon2 = CreateRemoteWeapon(_game.Weapons[weaponId]);
        }
        Weapon2.gameObject.SetActive(true);
        Weapon1.gameObject.SetActive(false);
    }

    // Third-person weapon held by another player's model; used for its visuals and muzzle effects only.
    /// <summary>Creates presentation-only equipment at the authored model hand, using the weapon definition pose.</summary>
    public Weapon CreateRemoteWeapon(GameObject weaponPrefab) {
        GameObject weaponObject = Instantiate(weaponPrefab, _rig.RemoteWeaponHand);
        MatchBindings.BindHierarchy(weaponObject, _game);
        Weapon weapon = weaponObject.GetComponent<Weapon>();
        if (weapon.Definition != null)
        {
            weaponObject.transform.localPosition = weapon.Definition.RemotePosition;
            weaponObject.transform.localRotation = Quaternion.Euler(weapon.Definition.RemoteEuler);
        }
        else if (weapon.Name.Equals("Rifle")) {
            weaponObject.transform.localPosition = new Vector3(-0.061f, -0.442f, 0.308f);
            weaponObject.transform.localRotation = Quaternion.Euler(-106.591f, 62.645f, 25.95499f);
        }
        else {
            weaponObject.transform.localPosition = new Vector3(-0.115f, -0.407f, 0.333f);
            weaponObject.transform.localRotation = Quaternion.Euler(-101.359f, 19.54199f, 79.521f);
        }
        weaponObject.GetComponent<Animator>().enabled = false;
        TransformLayers.SetRecursively(weaponObject, GameLayers.LocalPlayer);
        return weapon;
    }

    public void PlayRemoteShot() {
        if (!isLocalPlayer) {
            _shoot.playSootSound();
            getActiveWeapon().Flash.Play();
            GameObject smokeEffect = Instantiate(getActiveWeapon().Smoke,
                getActiveWeapon().transform.GetChild(0).position,
                Quaternion.Euler(-90, 0, 0));
            Destroy(smokeEffect, ImpactLifetimeSeconds);
        }
    }

    public void DoHitEffect(Vector3 hitPoint, Vector3 normal) {
        GameObject hitEffect = Instantiate(getActiveWeapon().HitEffect, hitPoint, Quaternion.LookRotation(normal));
        Destroy(hitEffect, ImpactLifetimeSeconds);
    }

    public void PlayRemoteImpact(Vector3 hitPoint, Vector3 normal) {
        if (!isLocalPlayer) {
            GameObject hitEffect = Instantiate(getActiveWeapon().HitEffect, hitPoint, Quaternion.LookRotation(normal));
            Destroy(hitEffect, ImpactLifetimeSeconds);
        }
    }
}
