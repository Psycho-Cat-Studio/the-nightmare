namespace Nightmare.Core.Combat
{
    public enum EnemyActivity { Walking, Screaming, Running, Fighting, Blocked, InZone }
    /// <summary>Defines which enemy activity states permit beginning contact combat.</summary>
    public static class EnemyContactRules
    {
        public static bool CanBeginMelee(EnemyActivity activity) => activity == EnemyActivity.Walking || activity == EnemyActivity.Running;
    }
}
