using UnityEngine;

// One scene-owned emitter for every enemy hit; no network objects or per-shot allocations.
public class EnemyHitParticles : MonoBehaviour
{
    static EnemyHitParticles _instance;
    ParticleSystem _particles;

    public static void Emit(Material material, Vector3 point, Vector3 normal)
    {
        if (material == null) return;
        if (_instance == null)
        {
            var go = new GameObject("Enemy hit particles");
            _instance = go.AddComponent<EnemyHitParticles>();
            _instance.Initialize(material);
        }
        _instance.Burst(point + normal * 0.01f, normal);
    }

    void Initialize(Material material)
    {
        _particles = gameObject.AddComponent<ParticleSystem>();
        _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 128;
        main.startSpeed = 0;
        main.gravityModifier = 0;
        var emission = _particles.emission; emission.enabled = false;
        var shape = _particles.shape; shape.enabled = false;
        var color = _particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var size = _particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        _particles.Play();
    }

    void Burst(Vector3 point, Vector3 normal)
    {
        var emit = new ParticleSystem.EmitParams {
            position = point, velocity = normal * 0.08f,
            startColor = new Color(0.16f, 1f, 0.07f, 0.95f),
            startSize = 0.12f, startLifetime = 0.12f
        };
        _particles.Emit(emit, 1);
        emit.startLifetime = 0.25f;
        emit.startSize = 0.035f;
        for (int i = 0; i < 3; i++)
        {
            emit.velocity = normal * 0.35f + Random.insideUnitSphere * 0.25f;
            _particles.Emit(emit, 1);
        }
    }

    void OnDestroy() { if (_instance == this) _instance = null; }
}
