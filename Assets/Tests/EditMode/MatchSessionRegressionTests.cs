using Nightmare.Core.Match;
using NUnit.Framework;

namespace Nightmare.Tests
{
    /// <summary>Protects session-owned timing, readiness, final results and successful-stage cleanup decisions.</summary>
    public class MatchSessionRegressionTests
    {
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void LossHasPriorityOverAnExpiredDefense(bool playersDown, bool areaLost)
        {
            var session = new MatchSession();
            session.Apply(new MatchProgress(MatchPhase.Defend, 1), 0f);
            Assert.That(session.Resolve(playersDown, areaLost, 3), Is.EqualTo(new MatchProgress(MatchPhase.Lost, 1)));
        }
        [Test]
        public void OldAreaCannotLosePreparationAndFinalResultCannotAdvance()
        {
            var session = new MatchSession();
            session.Apply(new MatchProgress(MatchPhase.Prepare, 1), 60f);
            Assert.That(session.Resolve(false, true, 3), Is.EqualTo(session.Progress));
            session.Apply(new MatchProgress(MatchPhase.Won, 2), 0f);
            Assert.That(session.Resolve(true, true, 3), Is.EqualTo(session.Progress));
        }
        [TestCase(MatchPhase.Prepare, 1, true)]
        [TestCase(MatchPhase.Won, 0, true)]
        [TestCase(MatchPhase.Lost, 0, false)]
        [TestCase(MatchPhase.Defend, 0, false)]
        [TestCase(MatchPhase.Prepare, 0, false)]
        public void OnlySuccessfulDefenseTransitionsRetireTheHorde(MatchPhase phase, int index, bool completes)
        {
            var session = new MatchSession();
            session.Apply(new MatchProgress(MatchPhase.Defend), 120f);
            Assert.That(session.CompletesStage(new MatchProgress(phase, index)), Is.EqualTo(completes));
        }
        [Test]
        public void DuplicateProgressDoesNotRestartTheClock()
        {
            var session = new MatchSession();
            var progress = new MatchProgress(MatchPhase.Prepare);
            session.Apply(progress, 60f);
            session.Clock.Tick(7f);
            Assert.That(session.Apply(progress, 60f), Is.False);
            Assert.That(session.Clock.Remaining, Is.EqualTo(53f));
        }
        [Test]
        public void UniqueReadinessTracksDisconnectsAndCannotRestartTheCampaign()
        {
            var session = new MatchSession();
            session.Apply(new MatchProgress(MatchPhase.Lobby), 0f);
            Assert.That(session.RegisterReady("one"), Is.True);
            Assert.That(session.RegisterReady("one"), Is.False);
            Assert.That(session.CanBegin(2), Is.False);
            session.RegisterReady("two"); session.RemoveReady("one");
            Assert.That(session.CanBegin(1), Is.True);
            session.Apply(new MatchProgress(MatchPhase.Prepare), 60f);
            Assert.That(session.CanBegin(1), Is.False);
        }
    }
}
