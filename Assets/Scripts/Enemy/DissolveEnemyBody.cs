using UnityEngine;

/// <summary>Animates local material dissolution and requests final entity removal only on authority.</summary>
public class DissolveEnemyBody : MonoBehaviour {

    private static readonly int DissolveAmountProperty = Shader.PropertyToID("_DissolveAmount");
    private Material mat;
    private IEntityLifetime _lifetime;
    [SerializeField, Min(0.01f)] private float _duration = 3f;
    [SerializeField, Min(0f)] private float _despawnDelay;
    private float _completedAt = -1f;
    private float _dissolveAmount = 0f;
    public bool Enabled { get; set; }
    public float DissolveAmount { get => _dissolveAmount; }

    private void Awake() {
        mat = GetComponent<Renderer>().material;
        _lifetime = GetComponentInParent<IEntityLifetime>();
    }

    private void Update() {
        if (!Enabled) return;
        if (_dissolveAmount < 1f)
        {
            _dissolveAmount = Mathf.Min(1f, _dissolveAmount + Time.deltaTime / Mathf.Max(0.01f, _duration));
            mat.SetFloat(DissolveAmountProperty, DissolveAmount);
            if (_dissolveAmount >= 1f) _completedAt = Time.time;
        }
        // Keep an invisible Ghost briefly so its existing spatial death voice can finish.
        // Only the server despawns the network object, including after this cosmetic delay.
        if (_completedAt >= 0 && Time.time >= _completedAt + _despawnDelay && _lifetime != null && _lifetime.IsAuthority)
            _lifetime.Despawn();
    }
}
