using System.Collections.Generic;
using UnityEngine;

// Scene-owned, local presentation only. Damage is never limited by this pool.
public class ExplosionFeedbackPool : MonoBehaviour
{
    const int CapacityPerPrefab = 4;
    static ExplosionFeedbackPool _instance;
    Transform _staging;

    void Awake()
    {
        var staging = new GameObject("Inactive explosion warmup");
        staging.transform.SetParent(transform, false);
        staging.SetActive(false);
        _staging = staging.transform;
    }
    readonly Dictionary<GameObject, List<ExplosionFeedback>> _groups = new Dictionary<GameObject, List<ExplosionFeedback>>();

    public static float WorldRadius(SphereCollider collider)
    {
        Vector3 scale = collider.transform.lossyScale;
        return collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    public static void Play(GameObject prefab, Vector3 center, float radius)
    {
        Play(prefab, center, radius, Quaternion.identity);
    }

    public static void Play(GameObject prefab, Vector3 center, float radius, Quaternion rotation)
    {
        if (prefab == null) return;
        if (_instance == null)
            _instance = new GameObject("Explosion effects (pooled)").AddComponent<ExplosionFeedbackPool>();
        _instance.Burst(prefab, center, radius, rotation);
    }

    void Burst(GameObject prefab, Vector3 center, float radius, Quaternion rotation)
    {
        List<ExplosionFeedback> group;
        if (!_groups.TryGetValue(prefab, out group))
        {
            group = new List<ExplosionFeedback>(CapacityPerPrefab);
            for (int i = 0; i < CapacityPerPrefab; i++)
            {
                var effect = Instantiate(prefab, _staging).GetComponent<ExplosionFeedback>();
                effect.gameObject.SetActive(false);
                effect.transform.SetParent(transform, false);
                group.Add(effect);
            }
            _groups.Add(prefab, group);
        }
        var selected = group[0];
        foreach (var effect in group)
        {
            if (!effect.gameObject.activeSelf) { selected = effect; break; }
            if (effect.Started < selected.Started) selected = effect;
        }
        selected.Begin(center, radius, rotation);
    }

    void OnDestroy() { if (_instance == this) _instance = null; }
}
