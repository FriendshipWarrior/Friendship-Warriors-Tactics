using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;
using FriendshipWarriors.Units.Status;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class StatusTests
    {
        private readonly List<Object> _created = new();
        private JobDefinition _job;

        [SetUp]
        public void SetUp()
        {
            _job = ScriptableObject.CreateInstance<JobDefinition>();
            _job.Define("Job", "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), 4, 3, 10,
                new JobRequirement[0], Color.gray);
            _job.SetEquipment(new[] { WeaponType.Sword }, canUseShields: true);
            _created.Add(_job);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        /// <summary>A unit whose shown HP, P.Atk and Speed are the given values (100% multipliers).</summary>
        private Unit UnitWith(int hp = 80, int pa = 6, int speed = 8, Team team = Team.Player, int morale = Unit.DefaultMorale) =>
            new("U", team, _job, new RawStats(hp * 16384, 229376, speed * 16384, pa * 16384, 65536), morale);

        // ------------------------------------------------------------------ Status set

        [Test]
        public void Durations_CountDownAndExpire()
        {
            var unit = UnitWith();
            unit.AddStatus(StatusEffect.Stop);

            Assert.That(unit.Statuses.Remaining(StatusEffect.Stop), Is.EqualTo(20));
            unit.Statuses.Tick(19);
            Assert.That(unit.HasStatus(StatusEffect.Stop), Is.True);
            var expired = unit.Statuses.Tick();
            Assert.That(expired, Is.EquivalentTo(new[] { StatusEffect.Stop }));
            Assert.That(unit.HasStatus(StatusEffect.Stop), Is.False);
        }

        [TestCase(StatusEffect.Poison, 36)]
        [TestCase(StatusEffect.Regen, 32)]
        [TestCase(StatusEffect.Protect, 32)]
        [TestCase(StatusEffect.Shell, 32)]
        [TestCase(StatusEffect.Haste, 32)]
        [TestCase(StatusEffect.Slow, 32)]
        [TestCase(StatusEffect.Stop, 20)]
        [TestCase(StatusEffect.Sleep, 60)]
        [TestCase(StatusEffect.Disable, 24)]
        [TestCase(StatusEffect.Immobilize, 24)]
        [TestCase(StatusEffect.Blind, StatusRules.UntilCured)]
        public void DefaultDurations_FollowFft(StatusEffect status, int ticks)
        {
            Assert.That(StatusRules.DefaultDuration(status), Is.EqualTo(ticks));
        }

        [Test]
        public void Silence_LastsTwoOfTheUnitsOwnTurns_NotClockTicks()
        {
            var unit = UnitWith();
            unit.AddStatus(StatusEffect.Silence);

            Assert.That(unit.Statuses.IsTurnBased(StatusEffect.Silence), Is.True);
            unit.Statuses.Tick(10000);
            Assert.That(unit.HasStatus(StatusEffect.Silence), Is.True, "the clock alone never ends it");

            unit.Statuses.TickTurn();
            Assert.That(unit.HasStatus(StatusEffect.Silence), Is.True, "one turn left");
            Assert.That(unit.Statuses.TickTurn(), Is.EqualTo(new[] { StatusEffect.Silence }));
            Assert.That(unit.HasStatus(StatusEffect.Silence), Is.False);
        }

        [Test]
        public void TurnTicks_LeaveClockTimedStatusesAlone()
        {
            var unit = UnitWith();
            unit.AddStatus(StatusEffect.Poison);

            unit.Statuses.TickTurn();
            unit.Statuses.TickTurn();

            Assert.That(unit.Statuses.Remaining(StatusEffect.Poison), Is.EqualTo(36));
        }

        [Test]
        public void AnyDamage_WakesASleepingUnit()
        {
            var unit = UnitWith();
            unit.AddStatus(StatusEffect.Sleep);

            unit.TakeDamage(1);

            Assert.That(unit.HasStatus(StatusEffect.Sleep), Is.False);
        }

        [Test]
        public void ZeroDamage_DoesNotWake()
        {
            var unit = UnitWith();
            unit.AddStatus(StatusEffect.Sleep);

            unit.TakeDamage(0);

            Assert.That(unit.HasStatus(StatusEffect.Sleep), Is.True);
        }

        [Test]
        public void KnockOut_ClearsEveryStatus()
        {
            var unit = UnitWith();
            unit.AddStatus(StatusEffect.Poison);
            unit.AddStatus(StatusEffect.Silence);
            unit.AddStatus(StatusEffect.Protect);

            unit.TakeDamage(99999);

            Assert.That(unit.Statuses.Count, Is.Zero);
            Assert.That(unit.Statuses.IsTurnBased(StatusEffect.Silence), Is.False);
        }

        [Test]
        public void PoisonIsGentlerThanRegen()
        {
            Assert.That(StatusRules.PoisonDivisor, Is.EqualTo(16));
            Assert.That(StatusRules.RegenDivisor, Is.EqualTo(8));
        }

        [Test]
        public void UntilCuredStatuses_NeverRunOut()
        {
            var unit = UnitWith();
            unit.AddStatus(StatusEffect.Blind);

            unit.Statuses.Tick(10000);

            Assert.That(unit.HasStatus(StatusEffect.Blind), Is.True);
        }

        [Test]
        public void ReapplyingRefreshesToTheLongerDuration()
        {
            var unit = UnitWith();
            unit.AddStatus(StatusEffect.Poison);
            unit.Statuses.Tick(30);

            unit.AddStatus(StatusEffect.Poison);
            Assert.That(unit.Statuses.Remaining(StatusEffect.Poison), Is.EqualTo(36));

            unit.AddStatus(StatusEffect.Poison, 5);
            Assert.That(unit.Statuses.Remaining(StatusEffect.Poison), Is.EqualTo(36));
        }

        [Test]
        public void HasteAndSlow_CancelEachOther()
        {
            var unit = UnitWith();
            unit.AddStatus(StatusEffect.Slow);

            unit.AddStatus(StatusEffect.Haste);

            Assert.That(unit.HasStatus(StatusEffect.Slow), Is.False);
            Assert.That(unit.HasStatus(StatusEffect.Haste), Is.False, "Haste on a slowed unit only cancels the Slow");
        }

        [Test]
        public void KnockOut_ClearsStatuses_AndTheDownedTakeNoMore()
        {
            var unit = UnitWith();
            unit.AddStatus(StatusEffect.Poison);
            unit.AddStatus(StatusEffect.Protect);

            unit.TakeDamage(9999);

            Assert.That(unit.Statuses.Count, Is.Zero);
            Assert.That(unit.AddStatus(StatusEffect.Regen), Is.False);
        }

        [Test]
        public void BattleEnd_ClearsStatusesAndBoosts()
        {
            var unit = UnitWith();
            unit.AddStatus(StatusEffect.Haste);
            unit.AddSpeedBonus(2);
            unit.AdjustMorale(-20);

            unit.ClearBattleModifiers();

            Assert.That(unit.Statuses.Count, Is.Zero);
            Assert.That(unit.SpeedBonus, Is.Zero);
            Assert.That(unit.Morale, Is.EqualTo(Unit.DefaultMorale));
        }

        // ------------------------------------------------------------------ What statuses prevent

        [TestCase(StatusEffect.Stop, false, false)]
        [TestCase(StatusEffect.Sleep, false, false)]
        [TestCase(StatusEffect.Disable, true, false)]
        [TestCase(StatusEffect.Immobilize, false, true)]
        [TestCase(StatusEffect.Poison, true, true)]
        public void Statuses_LimitMovingAndActing(StatusEffect status, bool canMove, bool canAct)
        {
            var unit = UnitWith();
            unit.AddStatus(status);

            Assert.That(unit.CanMove, Is.EqualTo(canMove), "move");
            Assert.That(unit.CanAct, Is.EqualTo(canAct), "act");
        }

        [Test]
        public void TurnState_RespectsBlockedMoveAndAct()
        {
            var immobile = new TurnState(canTradeActForMove: true, moveBlocked: true);
            Assert.That(immobile.CanMove, Is.False);
            Assert.That(immobile.CanAct, Is.True);

            var disabled = new TurnState(canTradeMoveForAct: true, actBlocked: true);
            Assert.That(disabled.CanAct, Is.False);
            Assert.That(disabled.CanMove, Is.True);
        }

        // ------------------------------------------------------------------ Turn order

        [Test]
        public void Haste_Slow_AndStop_ChangeHowFastCtFills()
        {
            var normal = UnitWith(speed: 8);
            var hasted = UnitWith(speed: 8);
            var slowed = UnitWith(speed: 8);
            var stopped = UnitWith(speed: 8);
            hasted.AddStatus(StatusEffect.Haste);
            slowed.AddStatus(StatusEffect.Slow);
            stopped.AddStatus(StatusEffect.Stop);

            Assert.That(TurnOrder.ChargeRate(normal), Is.EqualTo(8));
            Assert.That(TurnOrder.ChargeRate(hasted), Is.EqualTo(12));
            Assert.That(TurnOrder.ChargeRate(slowed), Is.EqualTo(4));
            Assert.That(TurnOrder.ChargeRate(stopped), Is.Zero);
        }

        [Test]
        public void HastedUnits_TakeTurnsSooner()
        {
            var normal = UnitWith(speed: 8);
            var hasted = UnitWith(speed: 8);
            hasted.AddStatus(StatusEffect.Haste);

            Assert.That(new TurnOrder(new[] { normal, hasted }).NextTurn(), Is.SameAs(hasted));
        }

        [Test]
        public void StatusTimers_RunOnTheBattleClock()
        {
            var unit = UnitWith(speed: 10);
            unit.AddStatus(StatusEffect.Stop);
            var order = new TurnOrder(new[] { unit });

            // Stopped for 20 ticks, then 10 more ticks at Speed 10 to reach 100 CT.
            Assert.That(order.NextTurn(), Is.SameAs(unit));
            Assert.That(order.ClockTick, Is.EqualTo(30));
            Assert.That(unit.HasStatus(StatusEffect.Stop), Is.False);
        }

        // ------------------------------------------------------------------ Cures

        private AbilityDefinition Cure(params StatusEffect[] cures)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            _created.Add(ability);
            ability.Define("Cure", "", AbilityKind.Action, 0);
            ability.SetAction(AbilityEffect.CureStatus, AbilityTargeting.Standing, 0, 1, 3);
            ability.SetCures(cures);
            return ability;
        }

        [Test]
        public void CureAbilities_RemoveOnlyTheirStatuses()
        {
            var firstAid = Cure(StatusEffect.Blind, StatusEffect.Silence); // FFT Squire Heal
            var user = UnitWith();
            var target = UnitWith();
            target.AddStatus(StatusEffect.Blind);
            target.AddStatus(StatusEffect.Poison);

            var outcome = Battle.Abilities.AbilityResolver.Apply(
                Battle.Abilities.AbilityResolver.Resolve(firstAid, user, target, new System.Random(1)), target);

            Assert.That(outcome.CuredCount, Is.EqualTo(1));
            Assert.That(target.HasStatus(StatusEffect.Blind), Is.False);
            Assert.That(target.HasStatus(StatusEffect.Poison), Is.True);
            Assert.That(firstAid.IsSupportAction, Is.True, "cures earn support EXP");
        }

        [Test]
        public void Remedy_CuresSeveralAtOnce()
        {
            var remedy = Cure(StatusEffect.Poison, StatusEffect.Blind, StatusEffect.Silence, StatusEffect.Sleep);
            var target = UnitWith();
            foreach (var status in new[] { StatusEffect.Poison, StatusEffect.Blind, StatusEffect.Sleep, StatusEffect.Haste })
                target.AddStatus(status);

            var outcome = Battle.Abilities.AbilityResolver.Apply(
                Battle.Abilities.AbilityResolver.Resolve(remedy, target, target, new System.Random(1)), target);

            Assert.That(outcome.CuredCount, Is.EqualTo(3));
            Assert.That(target.Statuses.Active, Is.EquivalentTo(new[] { StatusEffect.Haste }));
        }

        // ------------------------------------------------------------------ Combat effects

        private (Unit attacker, Unit target) BehindDuel()
        {
            var target = UnitWith(team: Team.Enemy);
            target.Coord = new GridCoord(5, 5);
            target.Facing = Direction.North;
            var attacker = UnitWith();
            attacker.Coord = new GridCoord(5, 4); // behind: no evasion in play
            return (attacker, target);
        }

        [Test]
        public void Protect_CutsPhysicalDamageToTwoThirds()
        {
            var (attacker, target) = BehindDuel();
            int plain = CombatFormulas.PhysicalDamage(attacker, target, 30, melee: false);
            target.AddStatus(StatusEffect.Protect);

            Assert.That(plain, Is.EqualTo(30));
            Assert.That(CombatFormulas.PhysicalDamage(attacker, target, 30, melee: false), Is.EqualTo(20));
        }

        [TestCase(StatusEffect.Sleep)]
        [TestCase(StatusEffect.Stop)]
        public void SleepingOrStoppedTargets_CantEvade(StatusEffect status)
        {
            var target = UnitWith(team: Team.Enemy);
            target.Coord = new GridCoord(5, 5);
            target.Facing = Direction.North;
            var attacker = UnitWith();
            attacker.Coord = new GridCoord(5, 6); // front: class evasion 10% normally applies

            Assert.That(CombatFormulas.HitChance(attacker, target), Is.EqualTo(90));
            target.AddStatus(status);
            Assert.That(CombatFormulas.HitChance(attacker, target), Is.EqualTo(100));
        }

        [Test]
        public void SleepingTargets_TakeHalfAgainAsMuch()
        {
            var (attacker, target) = BehindDuel();
            target.AddStatus(StatusEffect.Sleep);

            Assert.That(CombatFormulas.PhysicalDamage(attacker, target, 20, melee: false), Is.EqualTo(30));
        }

        [Test]
        public void Blind_HalvesTheAttackersHitChance()
        {
            var (attacker, target) = BehindDuel();
            attacker.AddStatus(StatusEffect.Blind);

            Assert.That(CombatFormulas.HitChance(attacker, target), Is.EqualTo(50));
        }

        [Test]
        public void Shell_CutsTheMagicFactorToTwoThirds()
        {
            var caster = UnitWith();
            var target = UnitWith(team: Team.Enemy);
            double plain = CombatFormulas.MagicFactor(caster, target);
            target.AddStatus(StatusEffect.Shell);

            Assert.That(CombatFormulas.MagicFactor(caster, target), Is.EqualTo(plain * 2.0 / 3.0).Within(1e-9));
        }
    }
}
