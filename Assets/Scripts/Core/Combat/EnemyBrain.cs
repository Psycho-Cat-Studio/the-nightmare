namespace Nightmare.Core.Combat
{
    /// <summary>Navigation, contact and presentation effects requested by the engine-free enemy decisions.</summary>
    public interface IEnemyWorld
    {
        void FollowDestination();
        bool IsNearDestination { get; }
        bool ShouldScream { get; }
        void BeginScream();
        void EndScream();
        void BeginRunning();
        /// <summary>Attempts to enter valid melee contact and stop pursuit; returns whether a fighting target was acquired.</summary>
        bool TryBeginContactFight();
        /// <summary>Applies authority-side contact damage for elapsed seconds and reports whether valid contact remains.</summary>
        bool ApplyMelee(float deltaTime);
        void ResumePursuit();
    }

    /// <summary>Enemy state decisions, independent of navigation, collision, transport and animation.</summary>
    public sealed class EnemyBrain
    {
        private float _screamTimer = CombatRules.InitialScreamDelaySeconds;
        /// <summary>Current decision state; the adapter also assigns externally triggered trap and capture states.</summary>
        public EnemyActivity Activity { get; set; } = EnemyActivity.Walking;
        /// <summary>Advances enemy decisions using elapsed seconds and world callbacks; the caller owns simulation authority.</summary>
        public void Tick(float deltaTime, IEnemyWorld world)
        {
            switch (Activity)
            {
                case EnemyActivity.Walking:
                    world.FollowDestination();
                    _screamTimer -= deltaTime;
                    if (world.IsNearDestination && _screamTimer <= 0f)
                    {
                        if (world.ShouldScream)
                        {
                            Activity = EnemyActivity.Screaming;
                            _screamTimer = 0f;
                            world.BeginScream();
                        }
                        else { Activity = EnemyActivity.Running; world.BeginRunning(); }
                    }
                    break;
                case EnemyActivity.Running:
                    world.FollowDestination();
                    break;
                case EnemyActivity.Screaming:
                    _screamTimer += deltaTime;
                    if (_screamTimer >= CombatRules.ScreamDurationSeconds)
                    {
                        world.EndScream();
                        Activity = EnemyActivity.Running;
                        if (world.TryBeginContactFight()) Activity = EnemyActivity.Fighting;
                        else world.BeginRunning();
                    }
                    break;
                case EnemyActivity.Fighting:
                    if (!world.ApplyMelee(deltaTime))
                    {
                        Activity = EnemyActivity.Running;
                        world.ResumePursuit();
                    }
                    break;
            }
        }
    }
}
