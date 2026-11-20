using System;
using TMPro;
using UnityEngine;
using Mirror;
using Random = System.Random;

/// <summary>Owns replicated player life state and applies downed, revival and action/building presentation.</summary>
public class PlayerManager : NetworkBehaviour, Nightmare.Core.Ports.IHealth, IPlayerHitTarget, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }


    [SyncVar] private bool _isDead = false;
    /// <summary>Replicated downed state; the player remains present and can be revived.</summary>
    public bool IsDead { get { return _isDead; }  protected set { _isDead = value; } }

    public float _maxHealth = 100;
    public string EntityId => transform.name;
    public Transform Root => transform;
    public float CurrentHealth => _currentHealth;
    public float MaximumHealth => _maxHealth;

    private const int MAX_NAME_LENGTH = 16;
    [SyncVar] private string _displayName = "";
    public string DisplayName { get { return string.IsNullOrEmpty(_displayName) ? transform.name : _displayName; } }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _isDead = false;
        _currentHealth = _maxHealth;
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        CmdSetDisplayName(GameSettings.PlayerName);
    }

    [Command]
    private void CmdSetDisplayName(string requested)
    {
        string name = (requested ?? "").Replace("\n", " ").Replace("<", "").Replace(">", "").Trim();
        if (name.Length > MAX_NAME_LENGTH) name = name.Substring(0, MAX_NAME_LENGTH);
        _displayName = name;
    }

    [SyncVar] public float _currentHealth;

    [SerializeField] private Behaviour[] _disableOnDeath;
    private bool[] _wasEnabled;

    private PlacementController _placementController;
    private GameObject _cross;
    private Rigidbody _rigidbody;
    private Animator _playerAnimator;
    [SerializeField] private GameObject _cameraReviving;
    private GameObject _camera;
    [SerializeField] private GameObject _aliveCollider;
    [SerializeField] private GameObject _reviveCollider;

    /// <summary>Applies overhead-building body/camera state and, locally, hides action controls and changes floor collision/rendering.</summary>
    public void SetBuildingMode()
    {
        _rigidbody.useGravity = false;
        foreach (var root in GetComponent<PlayerRigBindings>().ActionRoots) root.SetActive(false);
        if (isLocalPlayer)
        {
            foreach (GameObject floor in _game.FloorsToDisable)
            {
                floor.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                floor.GetComponent<MeshCollider>().enabled = false;
            }
            GetComponent<PlayerRigBindings>().BuildingRoot.SetActive(true);
            for (int i = 0; i < _disableOnDeath.Length; i++)
                _disableOnDeath[i].enabled = !_wasEnabled[i];
            _cross.SetActive(false);
        }
    }

    /// <summary>Restores action-mode gravity, roots and locally owned controls/floor collision after building.</summary>
    public void SetActionMode()
    {
        _rigidbody.useGravity = true;
        foreach (var root in GetComponent<PlayerRigBindings>().ActionRoots) root.SetActive(true);
        if (isLocalPlayer)
        {
            foreach (GameObject floor in _game.FloorsToDisable)
            {
                floor.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                floor.GetComponent<MeshCollider>().enabled = true;
            }
            for (int i = 0; i < _disableOnDeath.Length; i++)
                _disableOnDeath[i].enabled = _wasEnabled[i];
            _cross.SetActive(true);
        }
    }

    public void Setup()
    {
        
        var rig = GetComponent<PlayerRigBindings>();
        _playerAnimator = rig.ActiveAnimator;
        if (isLocalPlayer) TransformLayers.SetRecursively(rig.GraphicsRoot, LayerMask.NameToLayer(GameLayers.UnrenderedPlayer));
        // The weapon mask includes RemotePlayer but deliberately excludes our own body.
        int hitLayer = LayerMask.NameToLayer(isLocalPlayer ? GameLayers.LocalPlayer : GameLayers.RemotePlayer);
        foreach (var collider in GetComponentsInChildren<Collider>(true))
            if (collider.CompareTag(GameTags.Player)) collider.gameObject.layer = hitLayer;
        _rigidbody = GetComponent<Rigidbody>();
        _wasEnabled = new bool[_disableOnDeath.Length];

        for (int i = 0; i < _wasEnabled.Length; i++) _wasEnabled[i] = _disableOnDeath[i].enabled;
        
        _camera = GetComponent<PlayerSetup>().ActionCamera.gameObject;
        _placementController = GetComponent<PlacementController>();
        if (isLocalPlayer) _cross = _game.SceneBindings.Crosshair;
        SetActionMode();
    }

    void Update() {
        if (!isLocalPlayer) return;
        if (ButtonsControll.screensOver)
        {
            if (PauseGame.menuActive || WinLoseScreens.winLoseActive)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (!SettingsMenu.IsOpen && Input.GetKeyDown(KeyCode.Backspace) && CustomNetworkManager.Instance != null)
            CustomNetworkManager.Instance.LeaveToMainMenu();
        
    }

    /// <summary>Restores maximum health, living colliders and the behaviours captured during Setup.</summary>
    public void SetDefaults()
    {
        _isDead = false;

        _currentHealth = _maxHealth;

        for (int i = 0; i < _disableOnDeath.Length; i++)
            _disableOnDeath[i].enabled = _wasEnabled[i];

        SwitchColliders(true);
    }

    // Die/Revive already run on every peer (via RPC), so colliders are switched locally.
    // This used to be a Command, which non-owning clients are not allowed to send.
    private void SwitchColliders(bool isAlive)
    {
        _aliveCollider.SetActive(isAlive);
        _reviveCollider.SetActive(!isAlive);
    }

    // Health is server-authoritative and reaches clients through the SyncVar. Damage used to be a
    // ClientRpc that every client applied on its own copy, and enemies sent one every frame.
    /// <summary>Applies authoritative damage only in allowed match phases; reaching zero downs the player.</summary>
    [Server]
    public void TakeDamage(float damage)
    {
        if (_isDead || damage <= 0f || _game == null || !_game.CanDamagePlayers) return;

        _currentHealth = Nightmare.Core.Combat.HealthRules.ApplyDamage(_currentHealth, damage, true);
        if (_currentHealth <= 0f)
        {
            _isDead = true;
            RpcDie();
        }
    }

    [ClientRpc]
    private void RpcDie()
    {
        Die();
    }

    private void Die()
    {
        _playerAnimator.SetTrigger(AnimationParameters.Die);
        _playerAnimator.SetBool(AnimationParameters.Revive, false);
        _isDead = true;
        for (int i = 0; i < _disableOnDeath.Length; i++)
            _disableOnDeath[i].enabled = false;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        _rigidbody.isKinematic = true;

        SwitchColliders(false);
        _game.Players.DeactivatePlayer(transform.name);
        ChangeCamera(true);
        
        transform.GetComponent<PlayerEquipment>().getActiveWeapon().transform.GetComponent<Animator>().SetBool(AnimationParameters.Hidden, false);
        transform.GetComponent<PlayerEquipment>().getActiveWeapon().transform.GetComponent<Animator>().SetBool(AnimationParameters.Sprinting, false);
        transform.GetComponent<PlayerEquipment>().getActiveWeapon().transform.GetComponent<Animator>().SetBool(AnimationParameters.Aiming, false);
        transform.GetComponent<PlayerEquipment>().getActiveWeapon().transform.GetComponent<Animator>().SetBool(AnimationParameters.Reloading, false);
    }

    private void ChangeCamera(bool isRevivng)
    {
        if (isLocalPlayer)
        {
            _camera.SetActive(!isRevivng);
            _cameraReviving.SetActive(isRevivng);
        }
    }
    
    /// <summary>Restores the downed copy health, colliders, controls and weapon pose; the backend coordinates application on peers.</summary>
    public void Revive()
    {
        _rigidbody.isKinematic = false;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        ChangeCamera(false);
        _playerAnimator.SetBool(AnimationParameters.Revive, true);
        SetDefaults();
        // Preserve Mirror spawn-index/RNG bookkeeping even though revival does not teleport here.
        _ = NetworkManager.singleton.GetStartPosition();
        _game.Players.ActivatePlayer(transform.name, this);

        if (transform.GetComponent<PlayerEquipment>().getActiveWeapon().Id == 0)
        {
            transform.GetComponent<PlayerEquipment>().getActiveWeapon().transform.localPosition = new Vector3(0.02f, 0.03f, -0.22f);
            transform.GetComponent<PlayerEquipment>().getActiveWeapon().transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
        else
        {
            transform.GetComponent<PlayerEquipment>().getActiveWeapon().transform.localPosition = new Vector3(0.08f, -0.02f, -0.17f);
            transform.GetComponent<PlayerEquipment>().getActiveWeapon().transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }
}
