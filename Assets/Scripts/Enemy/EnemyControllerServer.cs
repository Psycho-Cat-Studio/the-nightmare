using UnityEngine;
using UnityEngine.Serialization;
using Mirror;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class EnemyControllerServer : NetworkBehaviour
{

    private const string NO_DESTINATION = "";

    private const string ENEMY_ID_PREFIX = "Enemy ";

    private const float SCREAM_TIME = 2.2f;
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
    private float _screamTimer = 1f;
    public NavMeshAgent Agent { get; set; }
    public PlayerManager _damageDest;
    public bool _isDying;
    [SerializeField] private float _damage = 2f;
    [SerializeField] private List<Collider> _colliders;
    public Transform Dest { get; set; }
    private Animator _animator;
    public enum EnemyState { Walking, Screaming, Running, Fighting, Blocked, InZone};
    private EnemyState _currentState = EnemyState.Walking;
    public EnemyState PreviousState { get; set; }
    public EnemyState CurrentState { get => _currentState;
        set
        {
            if (CurrentState == EnemyState.Running || CurrentState == EnemyState.Walking)
                PreviousState = _currentState;
            _currentState = value;
        }
    }
    public List<AudioClip> clips = new List<AudioClip>();

    public Snares SnaresToDestroy { get; set; }

    private bool _init = false;
    private BoxCollider _meleeTrigger;
    private readonly HashSet<Collider> _playerContacts = new HashSet<Collider>();
    private CaptureArea _initialCaptureArea;

    // Set by the spawning server before Start; never biases subsequent retargeting.
    public void SetInitialCaptureArea(CaptureArea area) { _initialCaptureArea = area; }
    public bool IsTriggerLocked { get; set; }

    public bool ShouldScream { get; set; }
    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentHealth = _maxHealth;
    }

    private void Start()
    {
        // Hits and teddy-bear targets refer to enemies by name, so the name must be identical on every
        // peer: use the network id instead of a local spawn counter (which differed per client).
        transform.name = ENEMY_ID_PREFIX + netId;
        GameManager.Enemies[transform.name] = this;
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
        //Obstacle = GetComponent<NavMeshObstacle>();
        Agent.speed = _walkSpeed;
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
        switch(_currentState)
        {
            case EnemyState.Walking:
                if (HasValidDestination())
                {
                    if (!Agent.SetDestination(Dest.position))
                    {
                        SetClosestCaptureArea();
                    }
                }
                else SetClosestCaptureArea();
                _screamTimer -= Time.deltaTime;
                if (!Agent.pathPending && Agent.hasPath && Agent.remainingDistance < DIST_TO_SCREAM && _screamTimer <= 0f)
                {
                    if (ShouldScream)
                    {
                        _currentState = EnemyState.Screaming;
                        Scream();
                        TurnOnWalking(false);
                        SetAnim("screaming", true);
                        _screamTimer = 0f;
                    }
                    else
                    {
                        SetAnim("running", true);
                        _currentState = EnemyState.Running;
                        SetAgentSpeed(_runSpeed);
                    }
                }
                break;

            case EnemyState.Running:
                if (HasValidDestination())
                {
                    if (!Agent.SetDestination(Dest.position))
                    {
                        SetClosestCaptureArea();
                    }
                }
                else SetClosestCaptureArea();
                break;

            case EnemyState.Screaming:

                _screamTimer += Time.deltaTime;
                if (_screamTimer >= SCREAM_TIME)
                {
                    SetAnim("screaming", false);
                    CurrentState = EnemyState.Running;
                    SetAgentSpeed(_runSpeed);
                    if (!TryBeginContactFight())
                    {
                        SetAnim("running", true);
                        TurnOnWalking(true);
                    }
                }
                break;

            case EnemyState.Fighting:

                var previousTarget = _damageDest;
                _damageDest = GetContactTarget();
                if (_damageDest == null)
                {
                    TurnOnWalking(true);
                    // Leaving melee range resumes the existing chase of a live player.
                    if (previousTarget != null && !previousTarget.IsDead && previousTarget.gameObject.activeInHierarchy)
                        PlayerDetected(previousTarget.transform);
                    else SetClosestCaptureArea();
                    SetAnim("running", true);
                    CurrentState = EnemyState.Running;
                }
                else _damageDest.TakeDamage(Time.deltaTime * _damage);
                

                break;

            case EnemyState.Blocked:

                break;

            case EnemyState.InZone:

                break;
        }
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
        foreach (var area in GameManager.Instance.CurrentCaptureAreas.Values)
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
            string areaId = Dest.position.x.ToString() + "_" + Dest.position.y.ToString() + "_" + Dest.position.z.ToString();
            RpcSendDest(areaId, true);
        }
        else RpcSendDest(NO_DESTINATION, true);
    }

    // Called by server-side code only (player hits arrive through PlayerShoot's Command). This was a
    // [Command] invoked on the server, which UNET only tolerated because the game always ran as host.
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
            _currentState = EnemyState.Blocked;
        }
            
        
    }

    public
    IEnumerator Die()
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
        GameManager.Enemies.Remove(transform.name);
    }

    

    [ClientRpc]
    void RpcDie()
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
                GameManager.Instance.CurrentCaptureAreas.TryGetValue(destId, out captureArea);
                enemyControllerClient.Dest = captureArea != null ? captureArea.transform : null;
            }
            else
            {
                EnemyControllerClient enemyControllerClient = GetComponent<EnemyControllerClient>();
                PlayerManager player = GameManager.GetPlayer(destId);
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
        EnemyControllerServer registered;
        if (GameManager.Enemies.TryGetValue(transform.name, out registered) && registered == this)
            GameManager.Enemies.Remove(transform.name);
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
        if (!isServer || !enabled || !_init || _isDying || !other.CompareTag("Player")) return;
        _playerContacts.Add(other);
        // During Screaming we only remember overlaps. The state completes before attacking.
        TryBeginContactFight();
    }

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
        if (CurrentState != EnemyState.Running) return false;
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
        //Agent.enabled = isOn;
        //Obstacle.enabled = !isOn;
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
