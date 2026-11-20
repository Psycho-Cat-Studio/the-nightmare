using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Mirror;

/// <summary>Owns authoritative bear navigation, contact damage, target lifetime and occupied-cell release.</summary>
public class TeddyBearServer : NetworkBehaviour, IPlacedItem, IEntityLifetime, IMatchParticipant
{
    public override void OnStartClient() { MirrorMatchBinding.Bind(gameObject); base.OnStartClient(); }

    public override void OnStartServer() { MirrorMatchBinding.Bind(gameObject); base.OnStartServer(); }

    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    public bool IsAuthority => isServer;
    public void Despawn()
    {
        if (!isServer) return;
        _game.World.ReleaseBuildingPoint(InitialPosAndTag);
        RpcDie(InitialPosAndTag);
        NetworkServer.Destroy(gameObject);
    }
    public void InitializeCell(string cellId) { InitialPosAndTag = cellId; var sprite = GetComponentInChildren<SpriteRenderer>(); if (sprite != null) sprite.enabled = false; }

    private const float EntryAnimationSeconds = 2.97f;
    private const float DeathAnimationSeconds = 3.9f;
    private const string NO_DESTINATION = "";
    public NavMeshAgent Agent { get; set; }
    private EnemyControllerServer _damageDest;
    private EnemyControllerServer _previousDamageDest;
    [SerializeField] private float _damage = 60f;
    public Transform Dest { get; set; }
    private Animator _animator;
    public string InitialPosAndTag { get; set; }
    private int _beatenEnemies = 0;
    [SerializeField] private int _beatenEnemiesToDeath = 5;
    SpriteRenderer _spriteRenderer;
    public SpriteRenderer SpriteRenderer { get { return _spriteRenderer; } }

    public enum BearState
    {
        Running,
        Fighting,
        Waiting,
    };

    private BearState _currentState = BearState.Waiting;
    public BearState CurrentState
    {
        get => _currentState;
        set
        {
            _currentState = value;
        }
    }

    private List<EnemyControllerServer> _enemies = new List<EnemyControllerServer>();
    private Transform _bearTransform;

    void Start()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _game.World.RegisterGridSprite(_spriteRenderer);
        if (!isServer)
        {
            enabled = false;
        }
        else
        {
            _animator = GetComponentInChildren<Animator>();
            _bearTransform = transform.GetChild(1);
            Agent = transform.GetChild(1).GetComponent<NavMeshAgent>();
            StartCoroutine(WaitForEndOfEntryAnim());
        }
    }

    void Update()
    {
        RemoveInvalidTargets();
        switch (_currentState)
        {
            case BearState.Fighting: UpdateFighting(); break;
            case BearState.Running: UpdateRunning(); break;
        }
    }

    private void RemoveInvalidTargets()
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
            if (_enemies[i] == null || _enemies[i]._isDying) _enemies.RemoveAt(i);
    }
    private void UpdateFighting()
    {
        if (_damageDest == null || _damageDest._isDying || !_damageDest.gameObject.activeSelf)
        {
            _enemies.Remove(_damageDest);
            _damageDest = null;
            _currentState = BearState.Running;
            TurnOnWalking(true);
            SetClosestEnemy();
            SetAnim("fighting", false);
            if (_beatenEnemies >= _beatenEnemiesToDeath)
            {
                Death();
                return;
            }
        }
        else _damageDest.TakeDamage(Time.deltaTime * _damage);
    }

    private void UpdateRunning()
    {
        if (Dest != null && Dest.gameObject.activeSelf) Agent.SetDestination(Dest.position);
        else SetClosestEnemy();
    }

    public void EntryDetected(Collider other)
    {
        _enemies.Add(other.GetComponentInParent<EnemyControllerServer>());

    }

    public void ExitDetected(Collider other)
    {
        _enemies.Remove(other.GetComponentInParent<EnemyControllerServer>());

    }

    public void DamageEntryDetected(Collider other)
    {
        if (enabled && _currentState == BearState.Running)
        {
            TurnOnWalking(false);
            _damageDest = other.GetComponentInParent<EnemyControllerServer>();
            if (_previousDamageDest != _damageDest) _beatenEnemies++;
            _currentState = BearState.Fighting;
            SetAnim("fighting", true);
        }
    }

    public void DamageExitDetected(Collider other)
    {
        if (enabled && _currentState == BearState.Fighting)
        {
            TurnOnWalking(true);
            _previousDamageDest = _damageDest;
            _damageDest = null;
            SetAnim("fighting", false);
            _currentState = BearState.Running;
        }
    }

    private IEnumerator WaitForEndOfEntryAnim()
    {
        TurnOnWalking(false);
        SetAnim("waiting", true);
        yield return new WaitForSeconds(EntryAnimationSeconds);
        TurnOnWalking(true);
        _currentState = BearState.Running;
    }

    private bool _dying;

    // Server only (was a [Command] called on the server). Guarded: it could start several times.
    [Server]
    public void Death()
    {
        if (_dying) return;
        _dying = true;
        _currentState = BearState.Waiting;
        TurnOnWalking(false);
        SetAnim("death", true);
        StartCoroutine(Die());
    }

    private IEnumerator Die()
    {
        yield return new WaitForSeconds(DeathAnimationSeconds);
        Despawn();
    }

    [ClientRpc]
    public void RpcDie(string posAndTag)
    {
        if (_game != null) _game.World.ReleaseBuildingPoint(posAndTag);
    }

    private void OnDestroy()
    {
        _game.World.UnregisterGridSprite(_spriteRenderer);
    }

    public void SetClosestEnemy()
    {
        Transform tMin = null;
        float minDist = Mathf.Infinity;
        Vector3 currentPos = _bearTransform.position;
        foreach (EnemyControllerServer t in _enemies)
        {
            float dist = Vector3.Distance(t.transform.position, currentPos);
            if (dist < minDist)
            {
                tMin = t.transform;
                minDist = dist;
            }
        }

        Dest = tMin;

        if (Dest)
        {
            RpcSendDest(Dest.name);
            SetAnim("waiting", false);
        }
        else
        {
            RpcSendDest(NO_DESTINATION);
            SetAnim("waiting", true);
        }
    }

    [ClientRpc]
    void RpcSendDest(string destId)
    {
        if (!isServer)
        {
            TeddyBearClient teddyClient = GetComponent<TeddyBearClient>();
            EnemyControllerServer enemy = destId == NO_DESTINATION ? null : _game.World.GetEnemy(destId) as EnemyControllerServer;
            if (enemy) teddyClient.Dest = enemy.transform;
            else teddyClient.Dest = null;
        }
    }

    public void TurnOnWalking(bool isOn)
    {
        Agent.enabled = isOn;
        RpcTurnOnWalking(isOn);
    }

    [ClientRpc]
    public void RpcTurnOnWalking(bool isOn)
    {
        if (!isServer)
        {
            GetComponent<TeddyBearClient>().SetWalking(isOn);
        }
    }

    public void SetAnim(string animName, bool isOn)
    {
        _animator.SetBool(animName, isOn);
        RpcSetAnim(animName, isOn);
    }

    [ClientRpc]
    void RpcSetAnim(string animName, bool isOn)
    {
        if (!isServer)
        {
            TeddyBearClient teddyBearClient = GetComponent<TeddyBearClient>();
            teddyBearClient.SetAnim(animName, isOn);
        }
    }

}
