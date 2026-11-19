using UnityEngine;
using UnityEngine.Rendering;

// Replays the original authored explosion unchanged, with a separate world-radius indicator.
public class ExplosionFeedback : MonoBehaviour
{
    [SerializeField] Material _ringMaterial;
    ParticleSystem[] _systems;
    Vector3 _authoredScale;
    AudioSource _sound;
    MeshRenderer _ring;
    MaterialPropertyBlock _ringProperties;
    readonly RaycastHit[] _groundHits = new RaycastHit[16];
    static Mesh _ringMesh;
    float _lifetime;
    public double Started { get; private set; }
    public float Radius { get; private set; }
    public Vector3 Center { get; private set; }

    void Awake() { Initialize(); }

    void Initialize()
    {
        if (_systems != null) return;
        _authoredScale = transform.localScale;
        _systems = GetComponentsInChildren<ParticleSystem>(true);
        _sound = GetComponent<AudioSource>();
        _ringProperties = new MaterialPropertyBlock();
        if (_ringMesh == null) _ringMesh = CreateRing();
        var ringObject = new GameObject("Blast radius", typeof(MeshFilter), typeof(MeshRenderer));
        ringObject.transform.SetParent(transform, false);
        ringObject.GetComponent<MeshFilter>().sharedMesh = _ringMesh;
        _ring = ringObject.GetComponent<MeshRenderer>();
        _ring.sharedMaterial = _ringMaterial;
        _ring.shadowCastingMode = ShadowCastingMode.Off;
        _ring.receiveShadows = false;
        _ring.lightProbeUsage = LightProbeUsage.Off;
        _ring.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    public void Begin(Vector3 center, float radius, Quaternion rotation)
    {
        Initialize();
        gameObject.SetActive(false);
        StopEffect();
        transform.SetPositionAndRotation(center, rotation);
        // Shared ratio: grenade reference radius 3.52 m, enlarged by 50% (20% above the previous visual size).
        transform.localScale = _authoredScale * (1.5f * radius / 3.52f);
        Center = center;
        Radius = radius;
        Started = Time.timeAsDouble;
        _lifetime = 3f; // Same lifetime as the original explosion prefabs.
        Vector3 ground, normal;
        bool hasGround = FindGround(center, radius, out ground, out normal);
        _ring.gameObject.SetActive(hasGround);
        if (hasGround)
        {
            _ring.transform.SetPositionAndRotation(ground + normal * 0.015f, Quaternion.FromToRotation(Vector3.up, normal));
            // Original effects retain their authored scale; the range ring is world-sized.
            Vector3 scale = transform.lossyScale;
            _ring.transform.localScale = new Vector3(radius / Mathf.Abs(scale.x), radius / Mathf.Abs(scale.y), radius / Mathf.Abs(scale.z));
            SetRingAlpha(0.8f);
        }
        // Native OnEnable reproduces the original prefab's autoplay/subemitter/audio behavior.
        gameObject.SetActive(true);
    }

    bool FindGround(Vector3 center, float radius, out Vector3 point, out Vector3 normal)
    {
        point = center; normal = Vector3.up;
        int count = Physics.RaycastNonAlloc(center + Vector3.up * 0.1f, Vector3.down, _groundHits,
            radius + 0.6f, 1 << 0, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = _groundHits[i];
            if (!hit.collider.tag.StartsWith("Floor") || hit.distance >= nearest) continue;
            nearest = hit.distance; point = hit.point; normal = hit.normal;
        }
        return !float.IsPositiveInfinity(nearest);
    }

    void Update()
    {
        float age = (float)(Time.timeAsDouble - Started);
        if (age >= _lifetime) { gameObject.SetActive(false); return; }
        if (_ring.gameObject.activeSelf)
        {
            if (age >= 0.45f) _ring.gameObject.SetActive(false);
            else SetRingAlpha(0.8f * (1f - age / 0.45f));
        }
    }

    void SetRingAlpha(float alpha)
    {
        _ringProperties.SetColor("_BaseColor", new Color(1f, 0.52f, 0.08f, alpha));
        _ring.SetPropertyBlock(_ringProperties);
    }

    void StopEffect()
    {
        if (_systems != null)
            foreach (var system in _systems)
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_sound != null) _sound.Stop();
    }

    void OnDisable() { StopEffect(); }

    static Mesh CreateRing()
    {
        const int segments = 64;
        var vertices = new Vector3[(segments + 1) * 2];
        var triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[i * 2] = direction * 0.98f;
            vertices[i * 2 + 1] = direction;
            if (i == segments) continue;
            int n = i * 6, v = i * 2;
            triangles[n] = v; triangles[n + 1] = v + 2; triangles[n + 2] = v + 1;
            triangles[n + 3] = v + 1; triangles[n + 4] = v + 2; triangles[n + 5] = v + 3;
        }
        var mesh = new Mesh { name = "Shared blast radius ring" };
        mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateBounds();
        return mesh;
    }
}
