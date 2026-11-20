using Nightmare.Core.Combat;
using NUnit.Framework;

namespace Nightmare.Tests
{
    /// <summary>Characterizes enemy decision transitions through a fake world adapter.</summary>
    public class EnemyBrainTests
    {
        /// <summary>Records enemy-world callbacks and supplies deterministic contact and destination conditions to decision tests.</summary>
        private sealed class World : IEnemyWorld
        {
            public bool IsNearDestination => true;
            public bool ShouldScream => true;
            public bool Contact;
            public bool ScreamEnded;
            public int DamageTicks;
            public void FollowDestination() { }
            public void BeginScream() { }
            public void EndScream() { ScreamEnded = true; }
            public void BeginRunning() { }
            public bool TryBeginContactFight() => Contact;
            public bool ApplyMelee(float deltaTime) { DamageTicks++; return Contact; }
            public void ResumePursuit() { }
        }
        [Test]
        public void ScreamingDoesNotDealDamageAndAlwaysRechecksContactAfterwards()
        {
            var brain = new EnemyBrain(); var world = new World { Contact = true };
            brain.Tick(1.01f, world);
            Assert.That(brain.Activity, Is.EqualTo(EnemyActivity.Screaming));
            brain.Tick(2f, world);
            Assert.That(world.DamageTicks, Is.Zero);
            Assert.That(world.ScreamEnded, Is.False);
            brain.Tick(.21f, world);
            Assert.That(world.ScreamEnded, Is.True);
            // Contact is remembered during the scream and becomes a fight when the grace period ends.
            Assert.That(brain.Activity, Is.EqualTo(EnemyActivity.Fighting));
        }
        [Test]
        public void LosingContactResumesMovementWithoutAStaleDamageTarget()
        {
            var brain = new EnemyBrain { Activity = EnemyActivity.Fighting };
            var world = new World { Contact = false }; brain.Tick(.02f, world);
            Assert.That(brain.Activity, Is.EqualTo(EnemyActivity.Running));
        }
    }
}
