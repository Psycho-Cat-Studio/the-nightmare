using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

// Cosmetic only. Damage, navigation and the enemy state machine remain server-owned elsewhere.
/// <summary>Replicates confirmed enemy hit, attack and death audiovisual feedback.</summary>
public class EnemyFeedback : NetworkBehaviour
{
    [SerializeField] AudioClip[] _hurtClips = new AudioClip[0];
    [SerializeField] AudioClip[] _attackClips = new AudioClip[0];
    [SerializeField] AudioClip[] _deathClips = new AudioClip[0];
    [SerializeField] Material _impactMaterial;
    [FormerlySerializedAs("_groundDeathVisual")]
    [SerializeField] bool _spectralDeath;
    [SerializeField] ParticleSystem _spectralDeathEffect;
    [SerializeField] GhostLevitation _levitation;
    const float HurtDuration = 0.3f;
    const float HurtCooldown = 0.25f;
    const float HurtVoiceCooldown = 0.9f;
    [SerializeField] float _attackDuration = 1.5f / 1.7f;
    [SerializeField] int _attackVariants = 3;
    [SerializeField] string _attackStateName = "Base Layer.Punching";
    [SerializeField] string _hurtStateName = "Hit feedback.Hit";
    static readonly int AttackVariant = Animator.StringToHash("attackVariant");
    int _attackState, _hurtState;
    Animator _animator;
    EnemyControllerServer _enemy;
    AudioSource _voice;
    Renderer[] _body;
    int _hurtLayer;
    int _lastHurt = -1, _lastAttackSound = -1, _lastAttack = -1, _attackCycle = -1;
    double _nextHurt, _nextHurtVoice, _painUntil, _hurtStarted = double.NegativeInfinity, _attackSoundAt = -1;
    int _pendingAttackSound;
    float _pendingPitch;
    bool _dead;
    bool _deathPlayed;
    static int _lastDeath = -1;

    void Awake()
    {
        _attackState = Animator.StringToHash(_attackStateName);
        _hurtState = Animator.StringToHash(_hurtStateName);
        _enemy = GetComponent<EnemyControllerServer>();
        _animator = GetComponentInChildren<Animator>(true);
        _body = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        _hurtLayer = _animator != null ? _animator.GetLayerIndex("Hit feedback") : -1;
        _voice = gameObject.AddComponent<AudioSource>();
        _voice.playOnAwake = false;
        _voice.spatialBlend = 1;
        _voice.dopplerLevel = 0;
        _voice.minDistance = 3f;
        _voice.maxDistance = 22f;
        _voice.rolloffMode = AudioRolloffMode.Linear;
        _voice.volume = 1f;
        _voice.priority = 150;
    }

    public bool IsValidImpact(Vector3 localPoint, Vector3 localNormal)
    {
        if (!Finite(localPoint) || !Finite(localNormal) || localNormal.sqrMagnitude < 0.25f || localNormal.sqrMagnitude > 4f) return false;
        Vector3 point = transform.TransformPoint(localPoint);
        foreach (var renderer in _body)
        {
            if (renderer == null) continue;
            var bounds = renderer.bounds; bounds.Expand(1f);
            if (bounds.Contains(point)) return true;
        }
        return false;
    }

    static bool Finite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y)
            && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }

    [Server]
    public void BulletHit(Vector3 localPoint, Vector3 localNormal)
    {
        int hurt = -1;
        bool react = !_enemy._isDying && NetworkTime.time >= _nextHurt;
        if (react)
        {
            _nextHurt = NetworkTime.time + HurtCooldown;
            if (NetworkTime.time >= _nextHurtVoice)
            {
                hurt = Next(_hurtClips.Length, ref _lastHurt);
                _nextHurtVoice = NetworkTime.time + HurtVoiceCooldown;
            }
        }
        RpcBulletHit(localPoint, localNormal.normalized, react, hurt, Random.Range(0.95f, 1.05f));
    }

    [ClientRpc]
    void RpcBulletHit(Vector3 localPoint, Vector3 localNormal, bool react, int hurt, float pitch)
    {
        EnemyHitParticles.Emit(_impactMaterial, transform.TransformPoint(localPoint), transform.TransformDirection(localNormal).normalized);
        if (!react || IsDead()) return;
        // Mirror's interpolated timeline trails the server. A hit must start on receipt,
        // rather than wait for that movement buffer to catch up with the event timestamp.
        _hurtStarted = Time.unscaledTimeAsDouble;
        if (_hurtLayer >= 0)
            _animator.Play(_hurtState, _hurtLayer, 0);
        _attackSoundAt = -1;
        if (PlayVoice(_hurtClips, hurt, pitch, 1f, 128))
            _painUntil = Time.unscaledTimeAsDouble + _voice.clip.length / pitch;
    }

    void Update()
    {
        if (!isServer || _animator == null || IsDead()) return;
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        bool combat = _enemy.CurrentState == EnemyControllerServer.EnemyState.Fighting
            || (_enemy.CurrentState == EnemyControllerServer.EnemyState.Blocked && !_animator.GetBool("blocked"));
        bool canBeginAttackCycle = combat && state.IsTag("EnemyAttack") &&
            !_animator.GetBool("blocked") && !_animator.IsInTransition(0);
        if (!canBeginAttackCycle)
        {
            _attackCycle = -1;
            return;
        }
        int cycle = Mathf.FloorToInt(state.normalizedTime);
        if (_attackCycle == cycle) return;
        _attackCycle = cycle;
        int variant = Next(_attackVariants, ref _lastAttack);
        int sound = Next(_attackClips.Length, ref _lastAttackSound);
        RpcAttack(variant, sound, Random.Range(0.95f, 1.05f), NetworkTime.localTime);
    }

    [ClientRpc]
    void RpcAttack(int variant, int sound, float pitch, double started)
    {
        if (IsDead() || _animator == null || _animator.GetBool("blocked")) return;
        _animator.SetFloat(AttackVariant, variant);
        // Predicted time estimates the server clock without the snapshot buffer. Bound
        // catch-up so a delayed/early estimate never skips an entire visual attack.
        float age = isServer ? 0f : Mathf.Clamp((float)(NetworkTime.predictedTime - NetworkTime.rtt * 0.5 - started), 0f, _attackDuration * 0.5f);
        if (!isServer)
            _animator.Play(_attackState, 0, age / _attackDuration);
        _pendingAttackSound = sound;
        _pendingPitch = pitch;
        _attackSoundAt = Time.unscaledTimeAsDouble + Mathf.Max(0f, _attackDuration * 0.2f - age);
    }

    void LateUpdate()
    {
        if (_levitation != null)
        {
            if (IsDead()) _levitation.Freeze();
            else _levitation.Present(NetworkTime.time, netId);
        }
        if (_animator == null) return;
        if (IsDead())
        {
            if (!_dead) { _dead = true; _voice.Stop(); }
            _attackSoundAt = -1;
            if (_hurtLayer >= 0) _animator.SetLayerWeight(_hurtLayer, 0);
            return;
        }
        float age = (float)(Time.unscaledTimeAsDouble - _hurtStarted);
        if (_hurtLayer >= 0)
            _animator.SetLayerWeight(_hurtLayer, Mathf.Clamp01(Mathf.Min(age / 0.04f, (HurtDuration - age) / 0.08f)));
        if (_attackSoundAt < 0) return;
        if (!_animator.GetCurrentAnimatorStateInfo(0).IsTag("EnemyAttack") || _animator.GetBool("blocked"))
        {
            _attackSoundAt = -1;
            return;
        }
        if (Time.unscaledTimeAsDouble < _attackSoundAt) return;
        _attackSoundAt = -1;
        if (Time.unscaledTimeAsDouble >= _painUntil && age >= HurtDuration)
            PlayVoice(_attackClips, _pendingAttackSound, _pendingPitch, 0.85f, 150);
    }

    [Server]
    public void ServerDied()
    {
        RpcDied(Next(_deathClips.Length, ref _lastDeath), Random.Range(0.95f, 1.05f));
    }

    [ClientRpc]
    void RpcDied(int clip, float pitch)
    {
        if (_deathPlayed) return;
        _deathPlayed = true;
        if (_levitation != null) _levitation.Freeze();
        _dead = true;
        _attackSoundAt = -1;
        if (_animator != null && _hurtLayer >= 0) _animator.SetLayerWeight(_hurtLayer, 0);
        if (_spectralDeath && _animator != null)
        {
            // Freeze the current pose: no fall or root displacement before the fast dissolve.
            _animator.enabled = false;
            if (_spectralDeathEffect != null)
            {
                // Replay the existing spawn effect in place, without overlapping its old particles.
                _spectralDeathEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (var particles in _spectralDeathEffect.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particles.main;
                    main.startDelay = 0f;
                    main.simulationSpeed = 1.5f;
                }
                _spectralDeathEffect.Play(true);
            }
        }
        // The old charge scream is a separate source and may still have a long tail.
        var scream = GetComponent<AudioSource>();
        if (scream != null && scream != _voice) scream.Stop();
        _voice.Stop();
        _voice.minDistance = 5f;
        _voice.maxDistance = 24f;
        PlayVoice(_deathClips, clip, pitch, 0.9f, 64);
    }

    bool IsDead() { return _dead || _enemy == null || _enemy._isDying || (_animator != null && _animator.GetBool("die")); }

    bool PlayVoice(AudioClip[] clips, int index, float pitch, float volume, int priority)
    {
        if (index < 0 || index >= clips.Length || clips[index] == null) return false;
        _voice.Stop();
        _voice.clip = clips[index];
        _voice.pitch = pitch;
        _voice.volume = volume;
        _voice.priority = priority;
        _voice.Play();
        return true;
    }

    static int Next(int count, ref int previous)
    {
        if (count == 0) return -1;
        int next = Random.Range(0, count > 1 && previous >= 0 ? count - 1 : count);
        if (count > 1 && next >= previous && previous >= 0) next++;
        previous = next;
        return next;
    }

    void OnDisable()
    {
        if (_voice != null) _voice.Stop();
        if (_animator != null && _hurtLayer >= 0) _animator.SetLayerWeight(_hurtLayer, 0);
        _attackSoundAt = -1;
    }
}
