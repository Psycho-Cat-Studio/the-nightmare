using System;

namespace Nightmare.Core.Combat
{
    public enum WeaponActivity { Idle, Firing, Reloading }

    /// <summary>Single owner of the operation deadline; animation observes completion.</summary>
    public sealed class WeaponOperation
    {
        public WeaponActivity Activity { get; private set; }
        /// <summary>Seconds until the active operation completes; zero after cancellation or completion.</summary>
        public float Remaining { get; private set; }
        /// <summary>Raised synchronously by Tick after completion; cancellation does not raise this event.</summary>
        public event Action<WeaponActivity> Completed;
        /// <summary>Replaces the pending operation and sets its remaining duration in seconds.</summary>
        public void Begin(WeaponActivity activity, float seconds)
        {
            Activity = activity;
            Remaining = Math.Max(0f, seconds);
        }
        /// <summary>Discards the operation without granting ammunition or emitting Completed.</summary>
        public void Cancel() { Activity = WeaponActivity.Idle; Remaining = 0f; }
        /// <summary>Advances by nonnegative elapsed seconds, reloads when due, then notifies completion once.</summary>
        public void Tick(float deltaTime, AmmoState ammo)
        {
            if (Activity == WeaponActivity.Idle) return;
            Remaining -= Math.Max(0f, deltaTime);
            if (Remaining > 0f) return;
            var completed = Activity;
            Cancel();
            if (completed == WeaponActivity.Reloading) ammo.Reload();
            Completed?.Invoke(completed);
        }
    }
}
