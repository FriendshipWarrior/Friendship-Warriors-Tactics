using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle;
using FriendshipWarriors.UI;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class TurnTests
    {
        private readonly List<Object> _created = new();
        private JobDefinition _job;

        [SetUp]
        public void SetUp()
        {
            _job = ScriptableObject.CreateInstance<JobDefinition>();
            _job.Define("Job", "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), 4, 3, 5,
                new JobRequirement[0], Color.gray);
            _created.Add(_job);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        /// <summary>A unit whose shown Speed is exactly <paramref name="speed"/> (raw speed × 100% / 1,638,400).</summary>
        private Unit UnitWithSpeed(string name, int speed) =>
            new(name, Team.Player, _job, new RawStats(491520, 229376, speed * 16384, 81920, 65536));

        // ------------------------------------------------------------------ Turn order (CT)

        [Test]
        public void ChargeTime_FillsBySpeedUntilAUnitReaches100()
        {
            var unit = UnitWithSpeed("A", 7);
            var order = new TurnOrder(new[] { unit });

            Assert.That(order.NextTurn(), Is.SameAs(unit));
            Assert.That(order.ClockTick, Is.EqualTo(15), "7 × 15 = 105 is the first total at or above 100");
            Assert.That(unit.ChargeTime, Is.EqualTo(105));
        }

        [Test]
        public void FasterUnits_ActFirst_AndMoreOften()
        {
            var fast = UnitWithSpeed("Fast", 10);
            var slow = UnitWithSpeed("Slow", 5);
            var order = new TurnOrder(new[] { slow, fast });

            var turns = new List<Unit>();
            for (int i = 0; i < 6; i++)
            {
                var unit = order.NextTurn();
                turns.Add(unit);
                order.EndTurn(unit, moved: true, acted: true);
            }

            Assert.That(turns[0], Is.SameAs(fast));
            Assert.That(turns.Count(u => u == fast), Is.EqualTo(4));
            Assert.That(turns.Count(u => u == slow), Is.EqualTo(2));
        }

        [Test]
        public void HigherChargeTime_GoesFirst_EvenIfLaterInTheRoster()
        {
            var a = UnitWithSpeed("A", 5);
            var b = UnitWithSpeed("B", 5);
            a.ChargeTime = 100;
            b.ChargeTime = 108;

            Assert.That(new TurnOrder(new[] { a, b }).NextTurn(), Is.SameAs(b));
        }

        [Test]
        public void Ties_GoToTheEarlierUnitInTheRoster()
        {
            var a = UnitWithSpeed("A", 6);
            var b = UnitWithSpeed("B", 6);

            Assert.That(new TurnOrder(new[] { a, b }).NextTurn(), Is.SameAs(a));
        }

        [TestCase(false, false, 60)]
        [TestCase(true, false, 80)]
        [TestCase(false, true, 80)]
        [TestCase(true, true, 100)]
        public void EndingATurn_SpendsCtForWhatWasUsed(bool moved, bool acted, int cost)
        {
            var unit = UnitWithSpeed("A", 5);
            unit.ChargeTime = 140;

            new TurnOrder(new[] { unit }).EndTurn(unit, moved, acted);

            Assert.That(unit.ChargeTime, Is.EqualTo(System.Math.Min(140 - cost, TurnOrder.MaxChargeTimeAfterTurn)));
        }

        [Test]
        public void ChargeTimeAfterATurn_IsCapped()
        {
            var unit = UnitWithSpeed("A", 5);
            unit.ChargeTime = 200;

            new TurnOrder(new[] { unit }).EndTurn(unit, false, false);

            Assert.That(unit.ChargeTime, Is.EqualTo(TurnOrder.MaxChargeTimeAfterTurn));
        }

        [Test]
        public void Preview_MatchesTheTurnsThatActuallyHappen_WhenEveryTurnMovesAndActs()
        {
            var units = new[] { UnitWithSpeed("A", 6), UnitWithSpeed("B", 8), UnitWithSpeed("C", 11) };
            var order = new TurnOrder(units);

            var preview = order.Preview(10);
            var actual = new List<Unit>();
            for (int i = 0; i < 10; i++)
            {
                var unit = order.NextTurn();
                actual.Add(unit);
                order.EndTurn(unit, true, true);
            }

            Assert.That(preview, Is.EqualTo(actual));
        }

        [Test]
        public void Preview_DoesNotChangeChargeTime()
        {
            var unit = UnitWithSpeed("A", 6);
            new TurnOrder(new[] { unit }).Preview(5);

            Assert.That(unit.ChargeTime, Is.Zero);
        }

        [Test]
        public void NoUnitWithSpeed_MeansNoTurn()
        {
            Assert.That(new TurnOrder(new[] { UnitWithSpeed("A", 0) }).NextTurn(), Is.Null);
        }

        // ------------------------------------------------------------------ Turn state

        [Test]
        public void ATurn_HasOneMoveAndOneAct_InEitherOrder()
        {
            var actFirst = new TurnState();
            actFirst.RecordAct();
            Assert.That(actFirst.CanMove, Is.True, "FFT lets a unit move after acting");
            Assert.That(actFirst.CanAct, Is.False);
            actFirst.RecordMove();
            Assert.That(actFirst.IsFinished, Is.True);

            var moveFirst = new TurnState();
            moveFirst.RecordMove();
            Assert.That(moveFirst.CanMove, Is.False);
            Assert.That(moveFirst.CanAct, Is.True);
        }

        [Test]
        public void TradingMoveForAct_AllowsTwoActsAndNoMove()
        {
            var turn = new TurnState(canTradeMoveForAct: true);

            turn.RecordAct();
            Assert.That(turn.NextActUsesMove, Is.True);
            turn.RecordAct();

            Assert.That(turn.ActsTaken, Is.EqualTo(2));
            Assert.That(turn.CanMove, Is.False);
            Assert.That(turn.IsFinished, Is.True);
            Assert.That(turn.MoveSpent && turn.ActSpent, Is.True, "both halves are spent, so the turn costs full CT");
        }

        [Test]
        public void TradingActForMove_AllowsTwoMovesAndNoAct()
        {
            var turn = new TurnState(canTradeActForMove: true);

            turn.RecordMove();
            Assert.That(turn.NextMoveUsesAct, Is.True);
            turn.RecordMove();

            Assert.That(turn.MovesTaken, Is.EqualTo(2));
            Assert.That(turn.CanAct, Is.False);
            Assert.That(turn.IsFinished, Is.True);
        }

        [Test]
        public void WithoutATrade_ASecondMoveIsRefused()
        {
            var turn = new TurnState();
            turn.RecordMove();

            Assert.Throws<System.InvalidOperationException>(() => turn.RecordMove());
        }

        // ------------------------------------------------------------------ Abilities

        private AbilityDefinition Ability(AbilityKind kind, int cost, TurnTrade trade = TurnTrade.None)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Define(kind.ToString(), "", kind, cost, trade);
            _created.Add(ability);
            return ability;
        }

        [Test]
        public void LearningSpendsProf_ButJobLevelKeepsCountingEverythingEarned()
        {
            var support = Ability(AbilityKind.Support, 150);
            _job.SetAbilities("Drill", new[] { support });
            var unit = UnitWithSpeed("A", 6);
            unit.GainProficiency(200);

            Assert.That(unit.TryLearn(support, _job), Is.True);
            Assert.That(unit.AvailableProficiency(_job), Is.EqualTo(50));
            Assert.That(unit.JobLevel(_job), Is.EqualTo(2));
            Assert.That(unit.TryLearn(support, _job), Is.False, "already learned");
        }

        [Test]
        public void Learning_NeedsEnoughProfAndAJobThatTeachesIt()
        {
            var support = Ability(AbilityKind.Support, 300);
            var stranger = Ability(AbilityKind.Support, 10);
            _job.SetAbilities("Drill", new[] { support });
            var unit = UnitWithSpeed("A", 6);
            unit.GainProficiency(200);

            Assert.That(unit.TryLearn(support, _job), Is.False, "not enough Prof");
            Assert.That(unit.TryLearn(stranger, _job), Is.False, "this job doesn't teach it");
        }

        [Test]
        public void EquippedAbilities_GrantTurnTrades()
        {
            var doubleTime = Ability(AbilityKind.Movement, 100, TurnTrade.ActForMove);
            _job.SetAbilities("Drill", new[] { doubleTime });
            var unit = UnitWithSpeed("A", 6);
            unit.GainProficiency(100);

            Assert.That(unit.TryEquip(doubleTime), Is.False, "must be learned first");
            unit.TryLearn(doubleTime, _job);
            Assert.That(unit.HasTurnTrade(TurnTrade.ActForMove), Is.False, "learned but not equipped");
            Assert.That(unit.TryEquip(doubleTime), Is.True);
            Assert.That(unit.Movement, Is.SameAs(doubleTime));
            Assert.That(unit.HasTurnTrade(TurnTrade.ActForMove), Is.True);
            Assert.That(unit.HasTurnTrade(TurnTrade.MoveForAct), Is.False);
        }

        [Test]
        public void Secondary_NeedsAnotherJobWithALearnedAction()
        {
            var other = ScriptableObject.CreateInstance<JobDefinition>();
            _created.Add(other);
            var action = Ability(AbilityKind.Action, 50);
            other.SetAbilities("Field Kit", new[] { action });
            var unit = UnitWithSpeed("A", 6);

            Assert.That(unit.TrySetSecondary(_job), Is.False, "can't be the current job");
            Assert.That(unit.TrySetSecondary(other), Is.False, "nothing learned from it yet");

            unit.GainProficiency(50, other);
            unit.TryLearn(action, other);
            Assert.That(unit.TrySetSecondary(other), Is.True);
            Assert.That(unit.SecondaryJob, Is.SameAs(other));
        }

        // ------------------------------------------------------------------ Menu

        [Test]
        public void Menu_SkipsDisabledItemsAndWraps()
        {
            var menu = new MenuModel();
            menu.SetItems(new[] { new MenuItem("move", "Move", false), new MenuItem("act", "Act"), new MenuItem("wait", "Wait") });

            Assert.That(menu.Selected?.Id, Is.EqualTo("act"), "first enabled item");
            menu.MoveSelection(1);
            Assert.That(menu.Selected?.Id, Is.EqualTo("wait"));
            menu.MoveSelection(1);
            Assert.That(menu.Selected?.Id, Is.EqualTo("act"), "wrapped past the disabled Move");
            menu.MoveSelection(-1);
            Assert.That(menu.Selected?.Id, Is.EqualTo("wait"));
        }

        [Test]
        public void Menu_KeepsThePreferredSelectionWhenStillEnabled()
        {
            var menu = new MenuModel();
            menu.SetItems(new[] { new MenuItem("move", "Move"), new MenuItem("wait", "Wait") }, preferredId: "wait");
            Assert.That(menu.Selected?.Id, Is.EqualTo("wait"));

            menu.SetItems(new[] { new MenuItem("move", "Move"), new MenuItem("wait", "Wait", false) }, preferredId: "wait");
            Assert.That(menu.Selected?.Id, Is.EqualTo("move"));
        }

        [Test]
        public void Menu_SelectRefusesDisabledItems()
        {
            var menu = new MenuModel();
            menu.SetItems(new[] { new MenuItem("a", "A"), new MenuItem("b", "B", false) });

            Assert.That(menu.Select(1), Is.False);
            Assert.That(menu.SelectedIndex, Is.Zero);
        }
    }
}
