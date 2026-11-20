using Nightmare.Core.Combat;
using Nightmare.Core.Match;
using Nightmare.Core.World;
using NUnit.Framework;

namespace Nightmare.Tests
{
    /// <summary>Verifies engine-independent combat, match, capture and identity invariants.</summary>
    public class CoreRulesTests
    {
        [Test]
        public void RepeatedReloadDoesNotCreateRoundsAndPartialReserveSurvives()
        {
            var ammo = new AmmoState(31, 155, 46, 1);
            for (int i = 0; i < 20; i++) ammo.Reload();
            Assert.That(ammo.Magazine, Is.EqualTo(31));
            Assert.That(ammo.Reserve, Is.EqualTo(15));
            Assert.That(ammo.FullReserveMagazines, Is.Zero);
            for (int i = 0; i < 31; i++) Assert.That(ammo.TryConsume(), Is.True);
            Assert.That(ammo.TryConsume(), Is.False);
            ammo.Reload();
            Assert.That(ammo.Magazine, Is.EqualTo(15));
            Assert.That(ammo.Total, Is.EqualTo(15));
        }
        [Test]
        public void PickupDuringReloadPreservesMagazineAndCompletesOnce()
        {
            var ammo = new AmmoState(31, 155, 18, 1);
            var operation = new WeaponOperation();
            int completions = 0;
            operation.Completed += _ => completions++;
            operation.Begin(WeaponActivity.Reloading, 2.2f);
            operation.Tick(1f, ammo); ammo.Refill(); operation.Tick(1.3f, ammo); operation.Tick(10f, ammo);
            Assert.That(ammo.Total, Is.EqualTo(155));
            Assert.That(ammo.Magazine, Is.EqualTo(31));
            Assert.That(completions, Is.EqualTo(1));
        }
        [Test]
        public void CancelledReloadCannotCompleteAfterDeathOrWeaponRemoval()
        {
            var ammo = new AmmoState(31, 155, 50, 2);
            var operation = new WeaponOperation(); operation.Begin(WeaponActivity.Reloading, 2.2f);
            operation.Tick(1f, ammo); operation.Cancel(); operation.Tick(10f, ammo);
            Assert.That(ammo.Magazine, Is.EqualTo(2));
        }
        [TestCase(1)] [TestCase(3)] [TestCase(4)] [TestCase(10)]
        public void CampaignSupportsAnyNumberOfStages(int count)
        {
            var state = new MatchProgress(MatchPhase.Prepare);
            for (int i = 0; i < count; i++)
            {
                Assert.That(state.StageIndex, Is.EqualTo(i));
                state = state.Next(count); Assert.That(state.Phase, Is.EqualTo(MatchPhase.Defend));
                state = state.Next(count);
            }
            Assert.That(state.Phase, Is.EqualTo(MatchPhase.Won));
            Assert.That(state.CanDamagePlayers, Is.False);
        }
        [TestCase(EnemyActivity.Walking, true)] [TestCase(EnemyActivity.Running, true)]
        [TestCase(EnemyActivity.Screaming, false)] [TestCase(EnemyActivity.Blocked, false)] [TestCase(EnemyActivity.InZone, false)]
        public void MeleeWorksWhileWalkingWithoutCancellingScreamOrTrap(EnemyActivity activity, bool expected)
            => Assert.That(EnemyContactRules.CanBeginMelee(activity), Is.EqualTo(expected));
        [TestCase(0, 0f)] [TestCase(1, 2.5f)] [TestCase(2, 5f)] [TestCase(3, 5f)] [TestCase(4, 7.5f)]
        public void CandleCaptureKeepsOriginalRate(int enemies, float expected)
            => Assert.That(CaptureRules.DamagePerSecond(enemies), Is.EqualTo(expected));
        [Test]
        public void LobbyImmunityAndHealthClampAreIndependentOfPresentation()
        {
            Assert.That(HealthRules.ApplyDamage(100, 45, false), Is.EqualTo(100));
            Assert.That(HealthRules.ApplyDamage(100, 45, true), Is.EqualTo(55));
            Assert.That(HealthRules.ApplyDamage(55, 100, true), Is.Zero);
            Assert.That(HealthRules.ApplyDamage(55, float.NaN, true), Is.EqualTo(55));
        }
        [Test]
        public void ReadyPlayersAreUniqueAndEmptySessionsCannotStart()
        {
            var roster = new ReadyRoster();
            Assert.That(roster.CanStart(0), Is.False);
            Assert.That(roster.Add("a"), Is.True);
            Assert.That(roster.Add("a"), Is.False);
            Assert.That(roster.CanStart(2), Is.False);
            roster.Add("b"); Assert.That(roster.CanStart(2), Is.True);
            roster.Remove("b"); Assert.That(roster.Count, Is.EqualTo(1));
        }
        [Test]
        public void GridIdentityIsIndependentOfSystemCulture()
        {
            var previous = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                var id = new GridCellId("room-2", -4, 12);
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("pl-PL");
                string polish = id.ToString();
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                Assert.That(id.ToString(), Is.EqualTo(polish));
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
        }
    }
}
