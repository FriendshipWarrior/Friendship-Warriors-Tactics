using FriendshipWarriors.Battle;
using FriendshipWarriors.Battle.Combat;
using NUnit.Framework;

namespace FriendshipWarriors.Tests
{
    public class BattleClockTests
    {
        [Test]
        public void RunsWithRealTime_WhenNotPaused()
        {
            var clock = new BattleClock();
            Assert.That(clock.Now(3f), Is.EqualTo(3f));
            Assert.That(clock.Now(7.5f), Is.EqualTo(7.5f));
        }

        [Test]
        public void StopsWhilePaused_AndResumesWhereItWas()
        {
            var clock = new BattleClock();
            clock.Pause(2f);
            Assert.That(clock.Now(2f), Is.EqualTo(2f));
            Assert.That(clock.Now(50f), Is.EqualTo(2f), "frozen while paused");

            clock.Resume(50f);
            Assert.That(clock.Now(50f), Is.EqualTo(2f), "picks up where it stopped");
            Assert.That(clock.Now(51f), Is.EqualTo(3f));
        }

        [Test]
        public void SeveralPauses_AddUp_AndRepeatedCallsAreIgnored()
        {
            var clock = new BattleClock();
            clock.Pause(1f);
            clock.Pause(4f);   // already paused: ignored
            clock.Resume(5f);  // 4 s paused
            clock.Resume(9f);  // not paused: ignored
            clock.Pause(10f);
            clock.Resume(12f); // 2 s paused

            Assert.That(clock.Now(20f), Is.EqualTo(14f));
        }

        [Test]
        public void TimedPressWindow_SurvivesAPauseLongerThanTheWindow()
        {
            var rules = TimedPressRules.Default;
            var clock = new BattleClock();
            float start = clock.Now(10f);

            float pausedAt = 10f + rules.Duration * 0.25f;
            clock.Pause(pausedAt);
            clock.Resume(pausedAt + rules.Duration * 4f);

            float elapsed = clock.Now(pausedAt + rules.Duration * 4f) - start;
            Assert.That(elapsed, Is.EqualTo(rules.Duration * 0.25f).Within(1e-4f));
            Assert.That(elapsed, Is.LessThan(rules.Duration), "the press is still open after resuming");

            // Pressing at the perfect moment after the pause still scores as a perfect press.
            float perfectReal = pausedAt + rules.Duration * 4f + (rules.PerfectTime - elapsed);
            Assert.That(rules.Accuracy(clock.Now(perfectReal) - start), Is.EqualTo(1f).Within(1e-3f));
        }
    }
}
