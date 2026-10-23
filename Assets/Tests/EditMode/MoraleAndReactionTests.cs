using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Abilities;
using FriendshipWarriors.Battle.AI;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Battle.Reactions;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;
using FriendshipWarriors.Units.Status;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class MoraleAndReactionTests
    {
        private readonly List<Object> _created = new();
        private JobDefinition _job;

        [SetUp]
        public void SetUp()
        {
            _job = ScriptableObject.CreateInstance<JobDefinition>();
            _job.Define("Job", "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), 4, 3, 0,
                new JobRequirement[0], Color.gray);
            _job.SetEquipment(new[] { WeaponType.Sword, WeaponType.Rifle }, canUseShields: true);
            _created.Add(_job);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private Unit UnitWith(int morale = Unit.DefaultMorale, int resonance = Unit.DefaultResonance, int pa = 6, int hp = 200,
            Team team = Team.Player) =>
            new("U", team, _job, new RawStats(hp * 16384, 229376, 8 * 16384, pa * 16384, 65536), morale, resonance);

        private T Create<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            _created.Add(obj);
            return obj;
        }

        private EquipmentDefinition Sword(int wp = 4, int weaponEvasion = 0)
        {
            var sword = Create<EquipmentDefinition>();
            sword.DefineWeapon("Sword", "", WeaponType.Sword, wp, 1, 1, 3);
            sword.SetWeaponEvasion(weaponEvasion);
            return sword;
        }

        private AbilityDefinition Reaction(ReactionEffect effect)
        {
            var reaction = Create<AbilityDefinition>();
            reaction.Define(effect.ToString(), "", AbilityKind.Reaction, 0);
            reaction.SetReaction(effect);
            if (effect == ReactionEffect.CounterStrike)
                reaction.SetAction(AbilityEffect.Damage, AbilityTargeting.Standing, 1, 1, 3, randomMultiplier: new Vector2Int(1, 4), knockback: true);
            return reaction;
        }

        private static Unit Equip(Unit unit, AbilityDefinition reaction)
        {
            ForceLearn(unit, reaction);
            unit.TryEquip(reaction);
            return unit;
        }

        /// <summary>Test helper: teaches an ability through a throwaway job that offers it for free.</summary>
        private static void ForceLearn(Unit unit, AbilityDefinition ability)
        {
            var teacher = ScriptableObject.CreateInstance<JobDefinition>();
            teacher.SetAbilities("Teacher", new[] { ability });
            unit.TryLearn(ability, teacher);
            Object.DestroyImmediate(teacher);
        }

        // ------------------------------------------------------------------ Morale formulas

        [TestCase(70, 1.0)]
        [TestCase(100, 1.15)]
        [TestCase(0, 0.65)]
        public void MeleeMoraleFactor_HitsTheAnchors(int morale, double expected)
        {
            Assert.That(CombatFormulas.MeleeMoraleFactor(UnitWith(morale)), Is.EqualTo(expected).Within(1e-9));
        }

        [TestCase(70, 1.0)]
        [TestCase(100, 0.85)]
        [TestCase(0, 1.35)]
        public void DefenseMoraleFactor_HitsTheAnchors(int morale, double expected)
        {
            Assert.That(CombatFormulas.DefenseMoraleFactor(UnitWith(morale)), Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void HighMorale_HitsHarderInMelee_ButNotWithRifles()
        {
            var bold = UnitWith(morale: 100);
            var target = UnitWith(team: Team.Enemy);
            bold.TryEquipItem(Sword(wp: 5));

            // 6 PA × 5 WP = 30, × 1.15 = 34.5 → 34.
            Assert.That(CombatFormulas.AttackDamage(bold, target), Is.EqualTo(34));

            var rifle = Create<EquipmentDefinition>();
            rifle.DefineWeapon("Rifle", "", WeaponType.Rifle, 5, 1, 5, 99, twoHanded: true);
            bold.TryEquipItem(rifle);
            // ⌊(6 + 8) / 2⌋ × 5 = 35, no Morale bonus at range.
            Assert.That(CombatFormulas.AttackDamage(bold, target), Is.EqualTo(35));
        }

        [Test]
        public void HighMoraleTargets_ShrugOffPhysicalDamage()
        {
            var attacker = UnitWith();
            var steady = UnitWith(morale: 100, team: Team.Enemy);

            Assert.That(CombatFormulas.PhysicalDamage(attacker, steady, 40, melee: false), Is.EqualTo(34)); // 40 × 0.85
        }

        [Test]
        public void BareHands_UseTheUnitsMorale()
        {
            var unit = UnitWith(morale: 50, pa: 10);

            Assert.That(CombatFormulas.AttackFactors(unit).Damage, Is.EqualTo(10 * 50 / 100 * 10));
        }

        // ------------------------------------------------------------------ Panic

        [Test]
        public void MoraleBelowTen_CausesPanic_AndRecoveringToTenEndsIt()
        {
            var unit = UnitWith(morale: 12);
            Assert.That(unit.HasStatus(StatusEffect.Panic), Is.False);

            unit.AdjustMorale(-3);
            Assert.That(unit.Morale, Is.EqualTo(9));
            Assert.That(unit.HasStatus(StatusEffect.Panic), Is.True);
            Assert.That(unit.CanAct, Is.False);
            Assert.That(unit.CanMove, Is.True, "a panicking unit still runs");

            unit.AdjustMorale(1);
            Assert.That(unit.HasStatus(StatusEffect.Panic), Is.False);
        }

        [Test]
        public void PanicCantBeAddedOrCuredDirectly()
        {
            var unit = UnitWith(morale: 5);

            Assert.That(unit.RemoveStatus(StatusEffect.Panic), Is.False);
            Assert.That(unit.HasStatus(StatusEffect.Panic), Is.True);
            Assert.That(UnitWith().AddStatus(StatusEffect.Panic), Is.False);
        }

        [Test]
        public void PanickingUnits_TakeHalfAgainAsMuch()
        {
            var attacker = UnitWith();
            var nervous = UnitWith(team: Team.Enemy);
            nervous.AdjustMorale(-61); // 9: panicking, but compare at the same defense factor below

            double defense = CombatFormulas.DefenseMoraleFactor(nervous);
            int expected = (int)System.Math.Floor(20 * defense * 1.5 + 1e-9);
            Assert.That(CombatFormulas.PhysicalDamage(attacker, nervous, 20, melee: false), Is.EqualTo(expected));
        }

        // ------------------------------------------------------------------ Permanent Morale

        [Test]
        public void Commendations_RaiseMoralePermanently_DefeatsLowerIt()
        {
            var unit = UnitWith(morale: 60);

            unit.AwardCommendation("Held the Bridge");
            Assert.That(unit.BaseMorale, Is.EqualTo(60 + Unit.CommendationMoraleBonus));

            unit.SufferDefeat();
            Assert.That(unit.BaseMorale, Is.EqualTo(60 + Unit.CommendationMoraleBonus - Unit.DefeatMoralePenalty));

            unit.AdjustMorale(-30);
            unit.ClearBattleModifiers();
            Assert.That(unit.Morale, Is.EqualTo(unit.BaseMorale), "in-battle swings don't stick");
        }

        [Test]
        public void MoraleAndResonance_StayWithinZeroToHundred()
        {
            var unit = UnitWith(morale: 95, resonance: 3);

            unit.AdjustBaseMorale(50);
            unit.AdjustBaseResonance(-50);

            Assert.That(unit.Morale, Is.EqualTo(100));
            Assert.That(unit.Resonance, Is.Zero);
        }

        [Test]
        public void GenericUnits_RollMoraleAndResonanceInTheMiddleBand()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 50; i++)
            {
                var unit = UnitFactory.CreateGeneric("G", Team.Enemy, _job, 1, rng);
                Assert.That(unit.Morale, Is.InRange(UnitFactory.SpiritMin, UnitFactory.SpiritMax));
                Assert.That(unit.Resonance, Is.InRange(UnitFactory.SpiritMin, UnitFactory.SpiritMax));
            }
        }

        // ------------------------------------------------------------------ Resonance

        [Test]
        public void MagicFactor_MultipliesBothResonances()
        {
            var caster = UnitWith(resonance: 80);
            var faithful = UnitWith(resonance: 90, team: Team.Enemy);
            var grounded = UnitWith(resonance: 5, team: Team.Enemy);

            Assert.That(CombatFormulas.MagicFactor(caster, faithful), Is.EqualTo(0.72).Within(1e-9));
            Assert.That(CombatFormulas.MagicFactor(caster, grounded), Is.EqualTo(0.04).Within(1e-9));
        }

        // ------------------------------------------------------------------ Reactions

        private static int Trials(System.Func<System.Random, bool> trial, int count = 4000)
        {
            var rng = new System.Random(11);
            int hits = 0;
            for (int i = 0; i < count; i++)
            {
                if (trial(rng))
                    hits++;
            }
            return hits;
        }

        [Test]
        public void SpeedUp_TriggersAtTheReactorsMoraleRate()
        {
            var grid = TestGrids.Flat(3, 3);
            var reaction = Reaction(ReactionEffect.SpeedUp);
            var attacker = UnitWith(team: Team.Enemy);

            int fired = Trials(rng =>
            {
                var reactor = Equip(UnitWith(morale: 40), reaction);
                return ReactionResolver.React(reactor, attacker, true, 10, grid, new[] { reactor, attacker }, null, rng).Triggered;
            });

            Assert.That(fired / 4000.0, Is.InRange(0.37, 0.43));
        }

        [Test]
        public void Reactions_NeedDamage_AndAReactorWhoCanReact()
        {
            var grid = TestGrids.Flat(3, 3);
            var reactor = Equip(UnitWith(morale: 100), Reaction(ReactionEffect.SpeedUp));
            var attacker = UnitWith(team: Team.Enemy);
            var rng = new System.Random(1);

            Assert.That(ReactionResolver.React(reactor, attacker, true, 0, grid, new[] { reactor, attacker }, null, rng).Triggered, Is.False);
            reactor.AddStatus(StatusEffect.Sleep);
            Assert.That(ReactionResolver.React(reactor, attacker, true, 10, grid, new[] { reactor, attacker }, null, rng).Triggered, Is.False);
            reactor.RemoveStatus(StatusEffect.Sleep);
            Assert.That(ReactionResolver.React(reactor, attacker, true, 10, grid, new[] { reactor, attacker }, null, rng).Triggered, Is.True);
            Assert.That(reactor.SpeedBonus, Is.EqualTo(1));
        }

        [Test]
        public void CounterStrike_HitsBackAdjacentPhysicalAttackers_Only()
        {
            var grid = TestGrids.Flat(5, 1);
            var reactor = Equip(UnitWith(morale: 100), Reaction(ReactionEffect.CounterStrike));
            reactor.Coord = new GridCoord(1, 0);
            reactor.Facing = Direction.East;
            var attacker = UnitWith(team: Team.Enemy);
            attacker.Coord = new GridCoord(2, 0);
            attacker.Facing = Direction.West;
            var units = new[] { reactor, attacker };

            Assert.That(ReactionResolver.React(reactor, attacker, physical: false, 10, grid, units, null, new System.Random(1)).Triggered,
                Is.False, "not a physical attack");

            int before = attacker.CurrentHp;
            var result = ReactionResolver.React(reactor, attacker, physical: true, 10, grid, units, null, new System.Random(1));
            Assert.That(result.Triggered, Is.True);
            Assert.That(attacker.CurrentHp, Is.LessThan(before));

            attacker.Coord = new GridCoord(4, 0); // out of reach
            Assert.That(ReactionResolver.React(reactor, attacker, physical: true, 10, grid, units, null, new System.Random(1)).Triggered, Is.False);
        }

        [Test]
        public void AutoHeal_UsesTheWeakestTonicInStock()
        {
            var grid = TestGrids.Flat(3, 3);
            var tonic = Create<AbilityDefinition>();
            tonic.Define("Tonic", "", AbilityKind.Action, 0);
            tonic.SetAction(AbilityEffect.RestoreHp, AbilityTargeting.Standing, 0, 1, 99, amount: 30, isItem: true);
            var strong = Create<AbilityDefinition>();
            strong.Define("Strong Tonic", "", AbilityKind.Action, 0);
            strong.SetAction(AbilityEffect.RestoreHp, AbilityTargeting.Standing, 0, 1, 99, amount: 70, isItem: true);
            var inventory = new Inventory();
            inventory.Add(strong, 1);
            inventory.Add(tonic, 2);

            var reactor = Equip(UnitWith(morale: 100), Reaction(ReactionEffect.AutoHeal));
            reactor.TakeDamage(50);
            var attacker = UnitWith(team: Team.Enemy);

            var result = ReactionResolver.React(reactor, attacker, true, 50, grid, new[] { reactor, attacker }, inventory, new System.Random(1));

            Assert.That(result.Triggered, Is.True);
            Assert.That(inventory.Count(tonic), Is.EqualTo(1));
            Assert.That(inventory.Count(strong), Is.EqualTo(1));
            Assert.That(reactor.CurrentHp, Is.EqualTo(reactor.Stats.MaxHp - 20));
        }

        [Test]
        public void GainRegen_GivesRegen()
        {
            var grid = TestGrids.Flat(3, 3);
            var reactor = Equip(UnitWith(morale: 100), Reaction(ReactionEffect.GainRegen));
            var attacker = UnitWith(team: Team.Enemy);

            ReactionResolver.React(reactor, attacker, true, 10, grid, new[] { reactor, attacker }, null, new System.Random(1));

            Assert.That(reactor.HasStatus(StatusEffect.Regen), Is.True);
        }

        [Test]
        public void Parry_AddsWeaponEvasionFromFrontAndSide_NotBehind()
        {
            var target = Equip(UnitWith(team: Team.Enemy), Reaction(ReactionEffect.Parry));
            target.TryEquipItem(Sword(weaponEvasion: 20));
            target.Coord = new GridCoord(5, 5);
            target.Facing = Direction.North;
            var attacker = UnitWith();

            attacker.Coord = new GridCoord(5, 6);
            Assert.That(CombatFormulas.HitChance(attacker, target), Is.EqualTo(80), "front");
            attacker.Coord = new GridCoord(6, 5);
            Assert.That(CombatFormulas.HitChance(attacker, target), Is.EqualTo(80), "side");
            attacker.Coord = new GridCoord(5, 4);
            Assert.That(CombatFormulas.HitChance(attacker, target), Is.EqualTo(100), "back");
        }

        [Test]
        public void AiAvoidsSwingingIntoACounter_WhenAnotherTargetIsAsGood()
        {
            var grid = TestGrids.Flat(5, 3);
            var actor = UnitWith(team: Team.Enemy, pa: 5);
            actor.TryEquipItem(Sword(wp: 2));
            actor.Coord = new GridCoord(2, 1);
            var counterer = Equip(UnitWith(morale: 100), Reaction(ReactionEffect.CounterStrike));
            counterer.Coord = new GridCoord(1, 1);
            counterer.Facing = Direction.West;
            var plain = UnitWith();
            plain.Coord = new GridCoord(3, 1);
            plain.Facing = Direction.East;

            double risky = AiPlanner.AttackScore(actor, counterer);
            double safe = AiPlanner.AttackScore(actor, plain);

            Assert.That(safe, Is.GreaterThan(risky));
        }

        [Test]
        public void PanickingAi_RunsAndDoesntAttack()
        {
            var grid = TestGrids.Flat(7, 1);
            var actor = UnitWith(team: Team.Enemy, morale: 5);
            actor.TryEquipItem(Sword());
            actor.Coord = new GridCoord(2, 0);
            var foe = UnitWith();
            foe.Coord = new GridCoord(1, 0);

            var plan = AiPlanner.Plan(grid, actor, new[] { actor, foe }, MovementRules.Default, new System.Random(1));

            Assert.That(plan.Target, Is.Null);
            Assert.That(plan.MoveBefore, Is.EqualTo(new GridCoord(6, 0)));
        }

        [Test]
        public void DisabledAi_StillApproachesButDoesntAttack()
        {
            var grid = TestGrids.Flat(7, 1);
            var actor = UnitWith(team: Team.Enemy);
            actor.TryEquipItem(Sword());
            actor.Coord = new GridCoord(5, 0);
            actor.AddStatus(StatusEffect.Disable);
            var foe = UnitWith();
            foe.Coord = new GridCoord(0, 0);

            var plan = AiPlanner.Plan(grid, actor, new[] { actor, foe }, MovementRules.Default, new System.Random(1));

            Assert.That(plan.Target, Is.Null);
            Assert.That(plan.MoveBefore, Is.Not.Null);
        }
    }
}
