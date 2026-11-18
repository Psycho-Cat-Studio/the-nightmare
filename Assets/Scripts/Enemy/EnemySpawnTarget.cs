using UnityEngine;

// Optional first destination only. Subsequent targeting uses the normal nearest-area rule.
public class EnemySpawnTarget : MonoBehaviour
{
    [SerializeField] private CaptureArea _initialArea;
    public CaptureArea InitialArea { get { return _initialArea; } }
}
