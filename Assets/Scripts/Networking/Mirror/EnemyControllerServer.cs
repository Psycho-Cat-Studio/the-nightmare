using UnityEngine;
using Nightmare.Core.Combat;
using UnityEngine.Serialization;
using Mirror;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>Adapts enemy decisions to authoritative navigation, contact damage, health and replicated feedback.</summary>
public class EnemyControllerServer : NetworkBehaviour, IEnemyWorld, IEnemyHitTarget, IEnemyPerception, IEnemyTrapReceiver, IEntityLifetime, IStageEnemy, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }


    [SerializeField] private Nightmare.Configuration.EnemyDefinition _definition;
    public bool IsAuthority => isServer;
    public bool CanDetectPlayer => isServer && enabled && _init && !_isDying && !IsTriggerLocked;
    public void DetectPlayer(Transform player) { if (CanDetectPlayer) PlayerDetected(player); }
    public void Despawn() { if (isServer) NetworkServer.Destroy(gameObject); }
    /// <inheritdoc cref="IEnemyTrapReceiver.TryFreeze"/>
    public bool TryFreeze(float seconds)
    {
        if (!isServer || !isActiveAndEnabled || _isDying || !_affectedByBricks || CurrentState == EnemyState.Blocked) return false;
        TurnOnWalking(false);
        CurrentState = EnemyState.Blocked;
        SetAnim("blocked", true);
        StartCoroutine(ReleaseTrap(seconds, PreviousState));
        return true;
    }
    private IEnumerator ReleaseTrap(float seconds, EnemyState resume)
    {
        yield return new WaitForSeconds(seconds);
        if (_isDying) yield break;
        _damageDest = null;
        CurrentState = resume;
        if (resume == EnemyState.Running || resume == EnemyState.Walking)
        {
            SetAnim("running", resume == EnemyState.Running);
            SetAnim("blocked", false);
            TurnOnWalking(true);
        }
    }

    public bool UsesConfirmedHitEffects => GetComponent<EnemyFeedback>() != null;
    public string EntityId => transform.name;
    public Transform Root => transform;
    private const string NO_DESTINATION = "";

    private const string ENEMY_ID_PREFIX = "Enemy ";

    private const float DIST_TO_SCREAM = 6f;
    private AudioSource _source;
    public bool IsWalking { get; set; }
    [SyncVar] private float _currentHealth;
    [SerializeField] private float _maxHealth = 50f;
    [SerializeField] private float _walkSpeed = 2f;
    [SerializeField] private float _runSpeed = 3.5f;
    [SerializeField] private float _deathAnimationDelay = 3.2f;
    [FormerlySerializedAs("_consumesTrapCharges")]
    [SerializeField] private bool _affectedByBricks = true;
    public bool AffectedByBricks => _affectedByBricks;
    [SerializeField] private GameObject[] _deathHiddenObjects = new GameObject[0];
    private readonly EnemyBrain _brain = new EnemyBrain();
    private PlayerManager _previousMeleeTarget;
    public NavMeshAgent Agent { get; set; }
    public PlayerManager _damageDest;
    public bool _isDying;
    [SerializeField] private float _damage = 2f;
    [SerializeField] private List<Collider> _colliders;
    public Transform Dest { get; set; }
    private Animator _animator;
    public enum EnemyState { Walking, Screaming, Running, Fighting, Blocked, InZone};
    public EnemyState PreviousState { get; set; }
    /// <summary>Decision state; assignments while walking/running remember that pursuit state for trap recovery.</summary>
    public EnemyState CurrentState { get => (EnemyState)_brain.Activity;
        set
        {
            if (CurrentState == EnemyState.Running || CurrentState == EnemyState.Walking)
                PreviousState = CurrentState;
            _brain.Activity = (EnemyActivity)value;
        }
    }
    public List<AudioClip> clips = new List<AudioClip>();

    public Snares SnaresToDestroy { get; set; }

    private bool _init = false;
    private BoxCollider _meleeTrigger;
    private readonly HashSet<Collider> _playerContacts = new HashSet<Collider>();
    private CaptureArea _initialCaptureArea;

    // Set by the spawning server before Start; never biases subsequent retargeting.
    /// <summary>Assigns the spawning authority preferred first candle area before Start; later retargeting uses proximity.</summary>
    public void SetInitialCaptureArea(CaptureArea area) { _initialCaptureArea = area; }
    public bool IsTriggerLocked { get; set; }

    public bool ShouldScream { get; set; }
    public override void OnStartServer()
    {
        MirrorMatchBinding.Bind(gameObject);
        base.OnStartServer();
        _currentHealth = _maxHealth;
        RegisterIdentity();
    }

    public override void OnStartClient()
    {
        MirrorMatchBinding.Bind(gameObject);
        base.OnStartClient();
        RegisterIdentity();
    }

    private void RegisterIdentity()
    {
        transform.name = ENEMY_ID_PREFIX + netId;
        _game.World.RegisterEnemy(transform.name, this);
    }

    private void Awake()
    {
        if (_definition != null)
        {
            _maxHealth = _definition.Health; _walkSpeed = _definition.WalkSpeed; _runSpeed = _definition.RunSpeed;
            _damage = _definition.DamagePerSecond; _deathAnimationDelay = _definition.DeathAnimationSeconds;
            _affectedByBricks = _definition.AffectedByBricks;
        }
        Agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        _source = GetComponent<AudioSource>();
        _meleeTrigger = GetComponent<BoxCollider>();
    }

    private void Start()
    {
        if (_isDying) return;
        // Hits and teddy-bear targets refer to enemies by name, so the name must be identical on every
        // peer: use the network id instead of a local spawn counter (which differed per client).
        if (!isServer) enabled = false;
        else
        {
            int randInt = Random.Range(0, 5);
            if (randInt == 0) ShouldScream = true;
            _source = GetComponent<AudioSource>();
            IsWalking = true;
            _animator = GetComponentInChildren<Animator>();
            
        }
        Agent = GetComponent<NavMeshAgent>();
        if (Agent != null) Agent.speed = _walkSpeed;
        if (isServer)
        {
            _meleeTrigger = GetComponent<BoxCollider>();
            _init = true;
            SelectCaptureArea(true);
        }
    }

    private void Update()
    {
        if (!_init || _isDying) return;
        _brain.Tick(Time.deltaTime, this);
    }

    void IEnemyWorld.FollowDestination()
    {
        if (!HasValidDestination() || !Agent.SetDestination(Dest.position)) SetClosestCaptureArea();
    }
    bool IEnemyWorld.IsNearDestination => !Agent.pathPending && Agent.hasPath && Agent.remainingDistance < DIST_TO_SCREAM;
    bool IEnemyWorld.ShouldScream => ShouldScream;
    void IEnemyWorld.BeginScream() { Scream(); TurnOnWalking(false); SetAnim("screaming", true); }
    void IEnemyWorld.EndScream() { SetAnim("screaming", false); SetAgentSpeed(_runSpeed); }
    void IEnemyWorld.BeginRunning() { SetAnim("running", true); SetAgentSpeed(_runSpeed); if (Agent.isStopped) TurnOnWalking(true); }
    bool IEnemyWorld.TryBeginContactFight() => TryBeginContactFight();
    bool IEnemyWorld.ApplyMelee(float deltaTime)
    {
        _previousMeleeTarget = _damageDest;
        _damageDest = GetContactTarget();
        if (_damageDest == null) return false;
        _damageDest.TakeDamage(deltaTime * _damage);
        return true;
    }
    void IEnemyWorld.ResumePursuit()
    {
        TurnOnWalking(true);
        if (_previousMeleeTarget != null && !_previousMeleeTarget.IsDead && _previousMeleeTarget.gameObject.activeInHierarchy)
            PlayerDetected(_previousMeleeTarget.transform);
        else SetClosestCaptureArea();
        SetAnim("running", true);
    }

    public void PlayerDetected(Transform playerTransform)
    {
        if (!isServer || !_init || _isDying || CurrentState == EnemyState.Blocked) return;
        var player = playerTransform.GetComponent<PlayerManager>();
        if (player == null || player.IsDead) return;
        Dest = playerTransform;
        
        RpcSendDest(playerTransform.name, false);
        IsTriggerLocked = true;
    }

    private bool HasValidDestination()
    {
        if (Dest == null || !Dest.gameObject.activeInHierarchy) return false;
        var player = Dest.GetComponent<PlayerManager>();
        return player == null || !player.IsDead;
    }

    public void SetClosestCaptureArea() { SelectCaptureArea(false); }

    private void SelectCaptureArea(bool initial)
    {
        IsTriggerLocked = false;
        CaptureArea preferred = initial ? _initialCaptureArea : null;
        if (initial) _initialCaptureArea = null;
        Transform closest = null;
        float minDistance = Mathf.Infinity;
        foreach (var area in _game.World.CurrentCaptureAreas.Values)
        {
            if (area == null || !area.gameObject.activeInHierarchy || area.IsCaptured) continue;
            if (!initial && area.EnemyNum >= 7) continue;
            if (area == preferred) { closest = area.transform; break; }
            float distance = (area.transform.position - transform.position).sqrMagnitude;
            if (distance < minDistance) { closest = area.transform; minDistance = distance; }
        }
        Dest = closest;
        if (Dest)
        {
            string areaId = Dest.GetComponent<CaptureArea>().Id;
            RpcSendDest(areaId, true);
        }
        else RpcSendDest(NO_DESTINATION, true);
    }

    // Called by server-side code only (player hits arrive through PlayerShoot's Command). This was a
    // [Command] invoked on the server, which UNET only tolerated because the game always ran as host.
    /// <summary>Applies server damage once while alive; lethal damage starts death presentation and eventual despawn.</summary>
    [Server]
    public void TakeDamage(float damage)
    {
        if (_isDying) return;
        _currentHealth -= damage;

        if (_currentHealth <= 0 && !_isDying)
        {
            _isDying = true;
            TurnOnWalking(false);
            SetAnim("die", true);
            _playerContacts.Clear();
            GetComponent<EnemyFeedback>()?.ServerDied();
            StartCoroutine(Die());
            CurrentState = EnemyState.Blocked;
        }

    }

    /// <summary>Ends combat immediately after successful defense and dissolves without a kill animation or voice.</summary>
    [Server]
    public void RetireForStageCompletion()
    {
        if (_isDying) return;
        _isDying = true;
        _playerContacts.Clear();
        Dest = null;
        _init = false;
        TurnOnWalking(false);
        CurrentState = EnemyState.Blocked;
        DisableRetiredCollisions();
        RpcRetireForStageCompletion();
    }

    private void DisableRetiredCollisions()
    {
        foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
    }

    [ClientRpc]
    private void RpcRetireForStageCompletion()
    {
        _isDying = true;
        var presentation = GetComponent<EnemyControllerClient>();
        if (presentation != null) { presentation.Dest = null; presentation.SetWalking(false); }
        DisableRetiredCollisions();
        foreach (var source in GetComponentsInChildren<AudioSource>(true)) source.Stop();
        BeginDissolve();
    }

    public IEnumerator Die()
    {
        RpcRemoveEnemy();
        yield return new WaitForSeconds(_deathAnimationDelay);
        RpcDie();
    }

    [ClientRpc]
    void RpcRemoveEnemy()
    {
        foreach (Collider collider in _colliders)
        {
            collider.gameObject.SetActive(false);
        }
        _game.World.UnregisterEnemy(transform.name, this);
    }

    [ClientRpc]
    void RpcDie() { BeginDissolve(); }

    private void BeginDissolve()
    {
        foreach (var hidden in _deathHiddenObjects)
            if (hidden != null) hidden.SetActive(false);
        GetComponentInChildren<DissolveEnemyBody>().Enabled = true;
    }

    [ClientRpc]
    void RpcSendDest(string destId, bool isArea)
    {
        if (!isServer)
        {
            if (isArea)
            {
                EnemyControllerClient enemyControllerClient = GetComponent<EnemyControllerClient>();
                CaptureArea captureArea;
                _game.World.CurrentCaptureAreas.TryGetValue(destId, out captureArea);
                enemyControllerClient.Dest = captureArea != null ? captureArea.transform : null;
            }
            else
            {
                EnemyControllerClient enemyControllerClient = GetComponent<EnemyControllerClient>();
                PlayerManager player = _game.Players.GetPlayer(destId);
                if (player) enemyControllerClient.Dest = player.transform;
                else { enemyControllerClient.Dest = null; }
            }
        }
    }

    public void SetAnim(string animName, bool isOn)
    {
        _animator.SetBool(animName, isOn);
        RpcSetAnim(animName, isOn);
    }

    private void OnDestroy()
    {
        IStageEnemy registered;
        if (_game != null && _game.World.Enemies.TryGetValue(transform.name, out registered) && registered == this)
            _game.World.UnregisterEnemy(transform.name, this);
    }

    [ClientRpc]
    void RpcSetAnim(string animName, bool isOn)
    {
        if (!isServer)
        {
            EnemyControllerClient enemyControllerClient = GetComponent<EnemyControllerClient>();
            enemyControllerClient.SetAnim(animName, isOn);
        }
    }

    private void OnTriggerEnter(Collider other) { TrackPlayerContact(other); }
    private void OnTriggerStay(Collider other) { TrackPlayerContact(other); }

    private void TrackPlayerContact(Collider other)
    {
        if (!CanTrackPlayerContact(other)) return;
        _playerContacts.Add(other);
        // During Screaming we only remember overlaps. The state completes before attacking.
        TryBeginContactFight();
    }

    private bool CanTrackPlayerContact(Collider other) => isServer && enabled && _init &&
        !_isDying && other.CompareTag(GameTags.Player);

    private void OnTriggerExit(Collider other)
    {
        if (isServer) _playerContacts.Remove(other);
        // Fighting rechecks the whole set, so another collider/player can retain contact.
    }

    private static bool InvalidContact(Collider other)
    {
        return other == null || !other.enabled || !other.gameObject.activeInHierarchy;
    }

    private PlayerManager GetContactTarget()
    {
        _playerContacts.RemoveWhere(InvalidContact);
        if (_meleeTrigger == null || !_meleeTrigger.enabled) return null;
        PlayerManager closest = null;
        float minDistance = Mathf.Infinity;
        foreach (var other in _playerContacts)
        {
            var player = other.GetComponentInParent<PlayerManager>();
            if (player == null || player.IsDead) continue;
            // The larger child detector also reports triggers to the Rigidbody root.
            // Only overlap with the actual root attack box may deal damage.
            Vector3 direction; float distance;
            if (!Physics.ComputePenetration(_meleeTrigger, _meleeTrigger.transform.position,
                _meleeTrigger.transform.rotation, other, other.transform.position,
                other.transform.rotation, out direction, out distance)) continue;
            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance < minDistance) { closest = player; minDistance = sqrDistance; }
        }
        return closest;
    }

    private bool TryBeginContactFight()
    {
        if (!EnemyContactRules.CanBeginMelee((EnemyActivity)CurrentState)) return false;
        var player = GetContactTarget();
        if (player == null) return false;
        _damageDest = player;
        IsTriggerLocked = true;
        TurnOnWalking(false);
        CurrentState = EnemyState.Fighting;
        SetAnim("running", false);
        return true;
    }

    public void TurnOnWalking(bool isOn)
    {
        Agent.isStopped = !isOn;
        RpcTurnOnWalking(isOn);
    }

    [ClientRpc]
    public void RpcTurnOnWalking(bool isOn)
    {
        if (!isServer)
        {
            EnemyControllerClient enemyControllerClient = GetComponent<EnemyControllerClient>();
            enemyControllerClient.SetWalking(isOn);
        }
    }

    void Scream()
    {
        int random = Random.Range(0, clips.Count);
        _source.clip = clips[random];
        _source.PlayOneShot(_source.clip);
        RpcScream(random);

    }

    void SetAgentSpeed(float speed)
    {
        Agent.speed = speed;
        RpcSetAgentSpeed(speed);
    }

    [ClientRpc]
    void RpcSetAgentSpeed(float speed)
    {
        if (!isServer)
        {
            EnemyControllerClient enemyControllerClient = GetComponent<EnemyControllerClient>();
            enemyControllerClient.SetAgentSpeed(speed);
        }
    }

    [ClientRpc]
    void RpcScream(int random)
    {
        if (!isServer)
        {
            EnemyControllerClient enemyControllerClient = GetComponent<EnemyControllerClient>();
            enemyControllerClient.Scream(random);
        }
    }

}
