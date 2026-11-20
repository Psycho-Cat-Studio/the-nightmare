using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>Tracks authoritative candle occupation/progress and replicates capture state for match rules and presentation.</summary>
public class CaptureArea : NetworkBehaviour, IMatchParticipant
{
    [SerializeField] private string _stableId;
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }
    /// <summary>Persistent authored capture-area identity; the coordinate fallback exists only for unmigrated scenes.</summary>
    public string Id => string.IsNullOrEmpty(_stableId) ? "legacy:" + gameObject.scene.name + ":" + transform.position.ToString("R") : _stableId;

    [SyncVar] public bool _isCaptured = false;
    /// <summary>Replicated candle progress in percent, initially 100; authority decides drain and area loss.</summary>
    [SyncVar] public float _progress = 100;
    [SyncVar] [SerializeField] private float _step = 0.2f;
    [SyncVar] private bool _capturing = false;
    [SyncVar] private int _enemyNum = 0;
    public int EnemyNum { get { return _enemyNum; } }
    private int candlesToLight;
    private List<GameObject> _candles = new List<GameObject>();
    public int CandleCount { get { return transform.GetChild(0).childCount; } }
    public int LitCandleCount { get { return Nightmare.Core.World.CaptureRules.LitCandles(_progress, CandleCount); } }
    private Sprite[] _sprites = new Sprite[2];
    private SpriteRenderer _renderer;
    private readonly HashSet<EnemyControllerServer> _enemiesInside = new HashSet<EnemyControllerServer>();

    /// <summary>Area-loss flag observed by match resolution; assigning it does not itself broadcast a match result.</summary>
    public bool IsCaptured
    {
        get => _isCaptured;
        set
        {
            _isCaptured = value;
        }
    }

    void Start()
    {
        transform.tag = GameTags.CaptureArea;
        _progress = 100;
        _step = 0.05f;
        _sprites[0] = (Sprite) Resources.Load(IndicatorSprites.Red, typeof(Sprite));
        _sprites[1] = (Sprite) Resources.Load(IndicatorSprites.Green, typeof(Sprite));
        _renderer = GetComponent<SpriteRenderer>();
        _renderer.sprite = _sprites[1];
        for (int i = 0; i < transform.GetChild(0).childCount; i++)
        {
            _candles.Add(transform.GetChild(0).GetChild(i).gameObject);
            if(isServer) RpcActivateCandle(i, true);
        }
    }

    private void OnTriggerEnter(Collider other) { TryCapture(other); }
    private void OnTriggerStay(Collider other) { TryCapture(other); }

    private void TryCapture(Collider other)
    {
        // Client physics can overlap a newly spawned enemy before its server controller's
        // Start disables that component. Only the server may stop AI or capture a candle area.
        if (!isServer || _game == null || !_game.CanCapture(this)) return;
        if (other.CompareTag(GameTags.EnemyLegs))
        {
            EnemyControllerServer enemy = other.GetComponentInParent<EnemyControllerServer>();
            if (CanAcceptEnemy(enemy))
            {
                enemy.CurrentState = EnemyControllerServer.EnemyState.Blocked;
                enemy.TurnOnWalking(false);
                enemy.SetAnim("blocked", false);
                enemy.SetAnim("running", false);
                enemy.IsTriggerLocked = true;
                _enemiesInside.Add(enemy);
                _enemyNum = _enemiesInside.Count;
                _capturing = true;
            }
        }
    }

    private bool CanAcceptEnemy(EnemyControllerServer enemy) => enemy != null && enemy.isActiveAndEnabled &&
        !enemy.IsTriggerLocked && !enemy._isDying && !_enemiesInside.Contains(enemy) &&
        enemy.CurrentState == EnemyControllerServer.EnemyState.Running;

    private void OnTriggerExit(Collider other)
    {
        if (!isServer) return;
        if (other.CompareTag(GameTags.EnemyLegs))
        {
            EnemyControllerServer enemy = other.GetComponentInParent<EnemyControllerServer>();
            if (enemy != null && _enemiesInside.Remove(enemy))
            {
                _enemyNum = _enemiesInside.Count;
                if (_enemyNum <= 0)
                    _capturing = false;
                if (enemy.isActiveAndEnabled && !enemy._isDying)
                {
                    enemy.TurnOnWalking(true);
                    enemy.CurrentState = EnemyControllerServer.EnemyState.Running;
                    enemy.SetAnim("running", true);
                }
            }
        }
    }

    // Enemies that die inside the area simply drop out of the set (their colliders are disabled,
    // so no OnTriggerExit). The old counter was decremented on death even for enemies that were
    // never counted, and could go negative or stay too high.
    void Update()
    {
        if (isServer && !_isCaptured && _game != null && _game.CanCapture(this))
        {
            _enemiesInside.RemoveWhere(enemy => enemy == null || enemy._isDying);
            _enemyNum = _enemiesInside.Count;
            _capturing = _enemyNum > 0;
            if (_capturing)
            {
                _progress -= Nightmare.Core.World.CaptureRules.DamagePerSecond(_enemyNum) * Time.deltaTime;
                if (_progress <= 0)
                {
                    _progress = 0;
                    IsCaptured = true;
                    RpcChangeSprite(0); // the old check compared against the wrong sprite, so it never turned red
                }
                
                candlesToLight = LitCandleCount;

                for (int i = 0; i < candlesToLight; i++)
                {
                    if (!_candles[i].transform.GetChild(0).gameObject.activeSelf)
                        RpcActivateCandle(i, true);
                }
                for (int i = candlesToLight; i < _candles.Count; i++)
                {
                    if (_candles[i].transform.GetChild(0).gameObject.activeSelf)
                        RpcActivateCandle(i, false);
                }
            }
        }
    }

    [ClientRpc]
    void RpcChangeSprite(int index)
    {
        _renderer.sprite = _sprites[index];
    }

    [ClientRpc]
    void RpcActivateCandle(int index, bool isOn)
    {
        _candles[index].transform.GetChild(0).gameObject.SetActive(isOn);
    }
}
