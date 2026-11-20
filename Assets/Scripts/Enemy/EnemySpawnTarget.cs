using UnityEngine;

// Optional first destination only. Subsequent targeting uses the normal nearest-area rule.
/// <summary>Authors the preferred first candle target for one spawn without changing later proximity-based targeting.</summary>
public class EnemySpawnTarget : MonoBehaviour
{
    [SerializeField] private CaptureArea _initialArea;
    public CaptureArea InitialArea { get { return _initialArea; } }
}
