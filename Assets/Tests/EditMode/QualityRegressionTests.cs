using System;
using Nightmare.Core.Combat;
using Nightmare.Core.World;
using NUnit.Framework;

namespace Nightmare.Tests
{
    /// <summary>Locks authored charge, inventory, spawn and handling defaults during readability refactoring.</summary>
    public class QualityRegressionTests
    {
        [TestCase(0f, 0.16666667f)]
        [TestCase(1.5f, 0.66666667f)]
        [TestCase(3f, 1.16666667f)]
        [TestCase(3.05f, 1.18333333f)]
        public void GrenadeChargeRetainsAuthoredForceIncludingFinalFrameOvershoot(float seconds, float expected)
        {
            Assert.That(GrenadeThrowRules.ForceFromCharge(seconds), Is.EqualTo(expected).Within(.000001f));
        }

        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(.8f, .8f)]
        [TestCase(2f, 1.16666667f)]
        public void ServerGrenadeForceRetainsBounds(float reported, float expected)
        {
            Assert.That(GrenadeThrowRules.ClampReportedForce(reported), Is.EqualTo(expected).Within(.000001f));
        }

        [TestCase(new int[] { 0, 0, 0 }, 1, 1, 1)]
        [TestCase(new int[] { 0, 0, 0 }, 1, -1, 1)]
        [TestCase(new int[] { 1, 0, 0 }, 2, 1, 0)]
        [TestCase(new int[] { 0, 0, 1 }, 0, -1, 2)]
        [TestCase(new int[] { 1, 1, 1 }, 1, 1, 2)]
        [TestCase(new int[] { 1, 1, 1 }, 1, -1, 0)]
        [TestCase(new int[] { 0, 2, 0, 3 }, 1, 1, 3)]
        [TestCase(new int[] { 0, 2, 0, 3 }, 1, -1, 3)]
        [TestCase(new int[] { 0, 0, 2 }, 2, 1, 2)]
        public void InventorySelectionKeepsDirectionWrapAndEmptyBehavior(int[] counts, int start, int direction, int expected)
        {
            Assert.That(InventorySelection.FindAvailable(counts, start, direction, counts.Length), Is.EqualTo(expected));
        }

        [TestCase(0, 3f)]
        [TestCase(1, 8f)]
        [TestCase(2, 6f)]
        [TestCase(3, 4f)]
        [TestCase(4, 3f)]
        [TestCase(10, 3f)]
        public void SpawnCadenceRetainsAllAuthoredBranches(int players, float expected)
        {
            var settings = Activator.CreateInstance(GameTestDriver.Type("MatchSettings"));
            Assert.That(GameTestDriver.Call(settings, "SpawnIntervalMaximum", players), Is.EqualTo(expected));
            Assert.That(GameTestDriver.Get(settings, "SpawnIntervalMinimum"), Is.EqualTo(1f));
        }

        [Test]
        public void SharedCombatValuesPreserveTheExistingBalance()
        {
            Assert.That(CombatRules.HeadDamageMultiplier, Is.EqualTo(3f));
            Assert.That(CombatRules.BodyDamageMultiplier, Is.EqualTo(2f));
            Assert.That(CombatRules.LegDamageMultiplier, Is.EqualTo(1f));
            Assert.That(CombatRules.MaximumDamageMultiplier, Is.EqualTo(3f));
            Assert.That(GrenadeThrowRules.ForwardSpeed, Is.EqualTo(15f));
            Assert.That(GrenadeThrowRules.UpwardSpeed, Is.EqualTo(5f));
        }

        [Test]
        public void HandlingConfigurationStartsWithTheExistingTimingsAndPresentation()
        {
            var settings = Activator.CreateInstance(GameTestDriver.Type("Nightmare.Configuration.WeaponHandlingSettings"));
            Assert.That(GameTestDriver.Get(settings, "SwapHalfSeconds"), Is.EqualTo(.5f));
            Assert.That(GameTestDriver.Get(settings, "SwapCooldownSeconds"), Is.EqualTo(2f));
            Assert.That(GameTestDriver.Get(settings, "HitmarkerSeconds"), Is.EqualTo(.15f));
            Assert.That(GameTestDriver.Get(settings, "AdsRecoilMultiplier"), Is.EqualTo(.35f));
            Assert.That(GameTestDriver.Get(settings, "AdsDamping"), Is.EqualTo(16.643f));
            Assert.That(GameTestDriver.Get(settings, "CrosshairRestThreshold"), Is.EqualTo(1.02f));
            Assert.That(GameTestDriver.Get(settings, "CrosshairRecoveryBias"), Is.EqualTo(.4f));
            Assert.That(GameTestDriver.Get(settings, "CrosshairRecoveryPerFrame"), Is.EqualTo(.95f));
            Assert.That(GameTestDriver.Get(settings, "ReferenceFrameRate"), Is.EqualTo(50f));
        }
    }
}
