using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle;
using FriendshipWarriors.Battle.AI;
using FriendshipWarriors.Battle.Magic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Status;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    /// <summary>Move confirm/cancel, spells locked onto a unit or a tile, and moving or acting while a spell charges.</summary>
    public class BattleUxTests
    {
        private readonly List<Object> _created = new();
        private JobDefinition _job;

        [SetUp]
        public void SetUp()
        {
            _job = ScriptableObject.CreateInstance<JobDefinition>();
            _job.Define("Mage", "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), 4, 3, 5,
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

        private Unit UnitAt(GridCoord coord, Team team = Team.Player, int speed = 5, int hp = 50) =>
            new($"{team} {coord}", team, _job, new RawStats(hp * 16384, 60 * 16384, speed * 16384, 4 * 16384, 10 * 16384)) { Coord = coord };

        private AbilityDefinition Spell(int speed = 25, int radius = 1, int mp = 6, AbilityEffect effect = AbilityEffect.Damage)
        {
            var spell = ScriptableObject.CreateInstance<AbilityDefinition>();
            _created.Add(spell);
            spell.Define("Spell", "", AbilityKind.Action, 50);
            spell.SetAction(effect, AbilityTargeting.Standing, 0, 4, 99, mpCost: mp);
            spell.SetSpell(14, speed, radius, 1, SpellElement.None, null, 0);
            return spell;
        }

        // ------------------------------------------------------------------ Move confirm / cancel

        [Test]
        public void TentativeMove_StandsOnTheDestination_WithoutSpendingTheMove()
        {
            var unit = UnitAt(new GridCoord(1, 1));
            unit.Facing = Direction.North;
            var turn = new TurnState();

            var move = new TentativeMove(unit, turn, new GridCoord(3, 1), Direction.East);

            Assert.That(unit.Coord, Is.EqualTo(new GridCoord(3, 1)));
            Assert.That(unit.Facing, Is.EqualTo(Direction.East));
            Assert.That(turn.MoveSpent, Is.False);
            Assert.That(turn.CanMove, Is.True);
            Assert.That(move.IsSettled, Is.False);
        }

        [Test]
        public void CancellingAMove_PutsTheUnitBack_AndKeepsTheMove()
        {
            var unit = UnitAt(new GridCoord(1, 1));
            unit.Facing = Direction.North;
            var turn = new TurnState();

            new TentativeMove(unit, turn, new GridCoord(3, 1), Direction.East).Cancel();

            Assert.That(unit.Coord, Is.EqualTo(new GridCoord(1, 1)));
            Assert.That(unit.Facing, Is.EqualTo(Direction.North));
            Assert.That(turn.MoveSpent, Is.False);
            Assert.That(turn.MovesTaken, Is.Zero);
            Assert.That(TurnOrder.TurnCost(turn.MoveSpent, turn.ActSpent), Is.EqualTo(60), "a cancelled move costs nothing");
        }

        [Test]
        public void ConfirmingAMove_SpendsIt_AndCanOnlyHappenOnce()
        {
            var unit = UnitAt(new GridCoord(1, 1));
            var turn = new TurnState();
            var move = new TentativeMove(unit, turn, new GridCoord(3, 1), Direction.East);

            move.Confirm();

            Assert.That(turn.MoveSpent, Is.True);
            Assert.That(turn.CanMove, Is.False);
            Assert.That(unit.Coord, Is.EqualTo(new GridCoord(3, 1)));
            Assert.Throws<System.InvalidOperationException>(() => move.Cancel());
            Assert.Throws<System.InvalidOperationException>(() => new TentativeMove(unit, turn, new GridCoord(4, 1), Direction.East));
        }

        [Test]
        public void DoubleTime_GetsAPromptForEachMove()
        {
            var unit = UnitAt(new GridCoord(0, 0));
            var turn = new TurnState(canTradeActForMove: true);

            new TentativeMove(unit, turn, new GridCoord(2, 0), Direction.East).Confirm();
            var second = new TentativeMove(unit, turn, new GridCoord(4, 0), Direction.East);
            second.Cancel();

            Assert.That(unit.Coord, Is.EqualTo(new GridCoord(2, 0)), "only the second move is taken back");
            Assert.That(turn.CanMove, Is.True, "the traded second move is still there");
            Assert.That(turn.ActSpent, Is.False);
        }

        // ------------------------------------------------------------------ Spell lock (FFT: a spell aimed at a unit follows it)

        [Test]
        public void UnitLockedSpell_FollowsTheUnit_WhereverItGoes()
        {
            var caster = UnitAt(new GridCoord(0, 0));
            var target = UnitAt(new GridCoord(3, 0), Team.Enemy);
            var order = new TurnOrder(new[] { caster, target });

            var charge = order.BeginCharge(caster, Spell(), target.Coord, lockOn: target);
            target.Coord = new GridCoord(12, 9); // far outside the spell's range of 4

            Assert.That(charge.Lock, Is.EqualTo(SpellLock.Unit));
            Assert.That(charge.Target, Is.EqualTo(new GridCoord(12, 9)), "range isn't checked again when it lands");
        }

        [Test]
        public void TileLockedSpell_StaysPut()
        {
            var caster = UnitAt(new GridCoord(0, 0));
            var target = UnitAt(new GridCoord(3, 0), Team.Enemy);
            var order = new TurnOrder(new[] { caster, target });

            var charge = order.BeginCharge(caster, Spell(), target.Coord);
            target.Coord = new GridCoord(5, 5);

            Assert.That(charge.Lock, Is.EqualTo(SpellLock.Tile));
            Assert.That(charge.Target, Is.EqualTo(new GridCoord(3, 0)));
        }

        [Test]
        public void UnitLockedSpell_LandsWhereItsUnitWasLost()
        {
            var caster = UnitAt(new GridCoord(0, 0));
            var target = UnitAt(new GridCoord(3, 0), Team.Enemy);
            var order = new TurnOrder(new[] { caster, target });
            var charge = order.BeginCharge(caster, Spell(), target.Coord, lockOn: target);

            target.Coord = new GridCoord(4, 2);
            order.Remove(target);
            target.Coord = new GridCoord(9, 9); // whatever happens to the removed unit no longer matters

            Assert.That(charge.Lock, Is.EqualTo(SpellLock.Tile));
            Assert.That(charge.Target, Is.EqualTo(new GridCoord(4, 2)));
        }

        [Test]
        public void TurnOrderEntries_CarryTheChargeAndItsLock()
        {
            var caster = UnitAt(new GridCoord(0, 0), speed: 1);
            var target = UnitAt(new GridCoord(2, 0), Team.Enemy, speed: 1);
            var order = new TurnOrder(new[] { caster, target });
            var charge = order.BeginCharge(caster, Spell(), target.Coord, lockOn: target);

            var entry = order.PreviewEntries(1)[0];

            Assert.That(entry.Charge, Is.SameAs(charge));
            Assert.That(entry.Charge.LockedUnit, Is.SameAs(target));
        }

        [Test]
        public void AiLocks_SingleTargetSpellsOnTheUnit_AndAreaSpellsOnTheTile()
        {
            var foe = UnitAt(new GridCoord(1, 1), Team.Enemy);

            Assert.That(AiSpellLock.For(Spell(radius: 0), foe), Is.EqualTo(SpellLock.Unit));
            Assert.That(AiSpellLock.For(Spell(radius: 1), foe), Is.EqualTo(SpellLock.Tile));
            Assert.That(AiSpellLock.For(Spell(radius: 0), null), Is.EqualTo(SpellLock.Tile), "nobody on the tile");
            Assert.That(AiSpellLock.For(Spell(speed: 0, radius: 0), foe), Is.EqualTo(SpellLock.Tile), "instant spells don't charge");
        }

        // ------------------------------------------------------------------ Moving and acting around a charge

        [Test]
        public void CastingFirst_StillAllowsTheMove_AndCostsAFullTurn()
        {
            var caster = UnitAt(new GridCoord(0, 0));
            var order = new TurnOrder(new[] { caster });
            var turn = new TurnState();

            turn.RecordAct();
            order.BeginCharge(caster, Spell(), new GridCoord(2, 0));
            turn.StopActing();

            Assert.That(turn.CanMove, Is.True);
            Assert.That(turn.CanAct, Is.False);
            new TentativeMove(caster, turn, new GridCoord(0, 2), Direction.South).Confirm();
            Assert.That(turn.IsFinished, Is.True);
            Assert.That(TurnOrder.TurnCost(turn.MoveSpent, turn.ActSpent), Is.EqualTo(100), "60 + 20 moved + 20 acted");
            Assert.That(order.ChargeOf(caster), Is.Not.Null, "moving keeps the spell");
        }

        [Test]
        public void StartingACharge_RulesOutASecondActEvenWithRapidFire()
        {
            var turn = new TurnState(canTradeMoveForAct: true);
            turn.RecordAct();
            turn.StopActing();

            Assert.That(turn.CanAct, Is.False, "a second act would only cancel the spell just started");
            Assert.That(turn.CanMove, Is.True);
        }

        [Test]
        public void ACharger_GetsItsNextTurn_AndMovingKeepsTheSpell()
        {
            var caster = UnitAt(new GridCoord(0, 0), speed: 10);
            var order = new TurnOrder(new[] { caster });
            var charge = order.BeginCharge(caster, Spell(speed: 1), new GridCoord(2, 0));

            var nextTurn = new TurnState(moveBlocked: !caster.CanMove, actBlocked: !caster.CanAct);
            Assert.That(nextTurn.CanMove && nextTurn.CanAct, Is.True);
            new TentativeMove(caster, nextTurn, new GridCoord(1, 1), Direction.South).Confirm();

            Assert.That(order.ChargeOf(caster), Is.SameAs(charge));
            Assert.That(charge.Target, Is.EqualTo(new GridCoord(2, 0)), "a tile-locked spell stays on its tile");
            Assert.That(caster.HasStatus(StatusEffect.Charging), Is.True);
        }

        [Test]
        public void ActingWhileCharging_CancelsTheSpell_AndTheMpStaysSpent()
        {
            var caster = UnitAt(new GridCoord(0, 0));
            var order = new TurnOrder(new[] { caster });
            var spell = Spell(mp: 6);
            SpellCasting.Begin(caster, spell, new GridCoord(2, 0), order, null, new[] { caster }, new System.Random(1), out var charge);
            int mpAfterCast = caster.CurrentMp;

            // What BattleController does before any act resolves.
            order.CancelCharge(caster);

            Assert.That(charge, Is.Not.Null);
            Assert.That(order.ChargeOf(caster), Is.Null);
            Assert.That(order.Charging, Is.Empty);
            Assert.That(caster.HasStatus(StatusEffect.Charging), Is.False);
            Assert.That(caster.CurrentMp, Is.EqualTo(mpAfterCast), "the MP isn't refunded");
            Assert.That(caster.CurrentMp, Is.EqualTo(caster.Stats.MaxMp - 6));
        }

        [Test]
        public void WaitingWhileCharging_KeepsTheSpell()
        {
            var caster = UnitAt(new GridCoord(0, 0), speed: 10);
            var order = new TurnOrder(new[] { caster });
            var charge = order.BeginCharge(caster, Spell(speed: 1), new GridCoord(2, 0));

            order.EndTurn(caster, moved: false, acted: false);

            Assert.That(order.ChargeOf(caster), Is.SameAs(charge));
        }

        [Test]
        public void ASpellLockedOnItsCaster_FollowsTheCastersMove()
        {
            var caster = UnitAt(new GridCoord(0, 0));
            var order = new TurnOrder(new[] { caster });
            var turn = new TurnState();
            turn.RecordAct();
            var charge = order.BeginCharge(caster, Spell(effect: AbilityEffect.RestoreHp), caster.Coord, lockOn: caster);
            turn.StopActing();

            new TentativeMove(caster, turn, new GridCoord(2, 3), Direction.East).Confirm();

            Assert.That(charge.Target, Is.EqualTo(new GridCoord(2, 3)));
        }

        [Test]
        public void AiChargers_ActOnlyForAKnockOut()
        {
            var charger = UnitAt(new GridCoord(0, 0));
            var sturdy = UnitAt(new GridCoord(1, 0), Team.Enemy, hp: 999);
            var nearlyDown = UnitAt(new GridCoord(1, 0), Team.Enemy, hp: 1);

            Assert.That(AiChargeRules.WorthCancelling(charger, null, sturdy), Is.False);
            Assert.That(AiChargeRules.WorthCancelling(charger, null, nearlyDown), Is.True);
            Assert.That(AiChargeRules.WorthCancelling(charger, Spell(), nearlyDown), Is.False, "never a new spell over the one charging");
            Assert.That(AiChargeRules.WorthCancelling(charger, null, null), Is.False);
        }
    }
}
