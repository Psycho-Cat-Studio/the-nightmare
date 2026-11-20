using UnityEngine;

/// <summary>Caches shared Animator parameter hashes used by player movement and life presentation.</summary>
public static class AnimationParameters
{
    public static readonly int Running = Animator.StringToHash("running");
    public static readonly int Throwing = Animator.StringToHash("throwing");
    public static readonly int Die = Animator.StringToHash("die");
    public static readonly int Revive = Animator.StringToHash("revive");
    public static readonly int Hidden = Animator.StringToHash("isHidden");
    public static readonly int Sprinting = Animator.StringToHash("isSprinting");
    public static readonly int Aiming = Animator.StringToHash("isAiming");
    public static readonly int Reloading = Animator.StringToHash("isReloading");
}
