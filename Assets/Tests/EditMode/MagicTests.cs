using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle;
using FriendshipWarriors.Battle.Abilities;
using FriendshipWarriors.Battle.AI;
using FriendshipWarriors.Battle.Magic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Status;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class MagicTests
    {
        private readonly List<Object> _created = new();
        private JobDefinition _mage;

        [SetUp]
        public void SetUp()
        {
            _mage = ScriptableObject.CreateInstance<JobDefinition>();
            // 100% multipliers: shown stats equal raw / 16384.
            _mage.Define("Mage", "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), 3, 3, 5,
                new JobRequirement[0], Color.gray);
            _created.Add(_mage);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private Unit UnitWith(int ma = 10, int resonance = 70, int speed = 5, Team team = Team.Player, int hp = 50, int mp = 60,
            int morale = 70, GridCoord coord = default)
        {
            var unit = new Unit($"{team} {coord}", team, _mage, new RawStats(hp * 16384, mp * 16384, speed * 16384, 4 * 16384, ma * 16384),
                morale, resonance) { Coord = coord };
            return unit;
        }

        private AbilityDefinition Spell(AbilityEffect effect, int power, int speed = 25, int radius = 1, int vertical = 1, int range = 4,
            int mp = 6, AbilityTargeting targeting = AbilityTargeting.Standing, StatusEffect[] inflicts = null, int revivePercent = 0,
            StatusEffect[] cures = null)
        {
            var spell = ScriptableObject.CreateInstance<AbilityDefinition>();
            _created.Add(spell);
            spell.Define(effect.ToString(), "", AbilityKind.Action, 50);
            spell.SetAction(effect, targeting, 0, range, 99, mpCost: mp);
            spell.SetSpell(power, speed, radius, vertical, SpellElement.None, inflicts, revivePercent);
            if (cures != null)
                spell.SetCures(cures);
            return spell;
        }

        private void Teach(Unit unit, params AbilityDefinition[] spells)
        {
            _mage.SetAbilities("Magic", _mage.Abilities.Concat(spells).Distinct().ToArray());
            foreach (var spell in spells)
            {
                unit.GainProficiency(spell.LearnCost);
                Assert.That(unit.TryLearn(spell, _mage), Is.True);
            }
        }

        // ------------------------------------------------------------------ Formulas (FFT, Resonance for Faith)

        [Test]
        public void Damage_IsMagAtkTimesPowerTimesBothResonances()
        {
            var caster = UnitWith(ma: 10, resonance: 70);
            var target = UnitWith(resonance: 70, team: Team.Enemy);

            Assert.That(MagicFormulas.Damage(caster, target, 14), Is.EqualTo(68)); // 10 × 14 × 0.7 × 0.7 = 68.6
            caster.AdjustBaseResonance(30);
            target.AdjustBaseResonance(30);
            Assert.That(MagicFormulas.Damage(caster, target, 14), Is.EqualTo(140));
        }

        [Test]
        public void Shell_CutsMagicDamageButNotHealing()
        {
            var caster = UnitWith(ma: 10, resonance: 100);
            var target = UnitWith(resonance: 100, team: Team.Enemy);
            target.AddStatus(StatusEffect.Shell);

            Assert.That(MagicFormulas.Damage(caster, target, 14), Is.EqualTo(93)); // 140 × 2/3
            Assert.That(MagicFormulas.Heal(caster, target, 14), Is.EqualTo(140));
        }

        [Test]
        public void ZeroResonance_OnEitherSide_MeansNothingLands()
        {
            var grounded = UnitWith(resonance: 0);
            var normal = UnitWith(resonance: 80, team: Team.Enemy);

            Assert.That(MagicFormulas.Damage(grounded, normal, 24), Is.Zero);
            Assert.That(MagicFormulas.Damage(normal, grounded, 24), Is.Zero);
            Assert.That(MagicFormulas.Heal(normal, grounded, 30), Is.Zero);
            Assert.That(MagicFormulas.HitChance(normal, grounded, 200), Is.Zero);
        }

        [Test]
        public void StatusHitRate_IsMagAtkPlusPowerTimesResonances_CappedAt100()
        {
            var caster = UnitWith(ma: 10, resonance: 70);
            var target = UnitWith(resonance: 70, team: Team.Enemy);

            Assert.That(MagicFormulas.HitChance(caster, target, 120), Is.EqualTo(63)); // 130 × 0.49 = 63.7
            Assert.That(MagicFormulas.HitChance(caster, target, 200), Is.EqualTo(100)); // 102.9, capped
        }

        // ------------------------------------------------------------------ Area

        [Test]
        public void AreaRadiusOne_IsACrossOfFive_WithinVerticalTolerance()
        {
            var flat = TestGrids.Flat(5, 5);
            Assert.That(SpellArea.Tiles(flat, new GridCoord(2, 2), 1, 1).Count, Is.EqualTo(5));
            Assert.That(SpellArea.Tiles(flat, new GridCoord(2, 2), 0, 1), Is.EqualTo(new[] { new GridCoord(2, 2) }));

            var raised = TestGrids.FromRows(
                "000",
                "002",
                "000");
            // (2,1) is 2h up: outside a 1h tolerance, inside a 2h one.
            Assert.That(SpellArea.Tiles(raised, new GridCoord(1, 1), 1, 1), Has.No.Member(new GridCoord(2, 1)));
            Assert.That(SpellArea.Tiles(raised, new GridCoord(1, 1), 1, 2), Has.Member(new GridCoord(2, 1)));
        }

        [Test]
        public void Spells_LandOnEveryoneInTheArea_FriendAndFoe()
        {
            var grid = TestGrids.Flat(6, 6);
            var caster = UnitWith(ma: 10, resonance: 100, coord: new GridCoord(0, 0));
            var foe = UnitWith(resonance: 100, team: Team.Enemy, coord: new GridCoord(3, 3));
            var friend = UnitWith(resonance: 100, coord: new GridCoord(3, 4));
            var outside = UnitWith(resonance: 100, team: Team.Enemy, coord: new GridCoord(5, 5));
            var fire = Spell(AbilityEffect.Damage, 1);

            var result = SpellResolver.Resolve(caster, fire, new GridCoord(3, 3), grid, new[] { caster, foe, friend, outside }, new System.Random(1));

            Assert.That(result.Targets.Select(t => t.Target), Is.EquivalentTo(new[] { foe, friend }));
            Assert.That(foe.CurrentHp, Is.EqualTo(40));
            Assert.That(friend.CurrentHp, Is.EqualTo(40));
            Assert.That(outside.CurrentHp, Is.EqualTo(50));
        }

        // ------------------------------------------------------------------ Charge time on the CT clock

        [Test]
        public void ChargedSpells_ResolveWhenTheirCtReaches100_BeforeUnitTurnsThatTick()
        {
            var caster = UnitWith(speed: 1);
            var runner = UnitWith(speed: 25, team: Team.Enemy);
            var order = new TurnOrder(new[] { caster, runner });
            var fire = Spell(AbilityEffect.Damage, 14, speed: 25);

            var charge = order.BeginCharge(caster, fire, new GridCoord(1, 1));
            Assert.That(charge.TicksLeft, Is.EqualTo(4));
            Assert.That(caster.HasStatus(StatusEffect.Charging), Is.True);

            var first = order.NextEvent();
            Assert.That(first.Kind, Is.EqualTo(TurnEventKind.SpellResolves), "the spell and the runner both hit 100 on tick 4; the spell goes first");
            Assert.That(first.Charge, Is.SameAs(charge));
            Assert.That(order.ClockTick, Is.EqualTo(4));
            Assert.That(caster.HasStatus(StatusEffect.Charging), Is.False);

            var second = order.NextEvent();
            Assert.That(second.Kind, Is.EqualTo(TurnEventKind.UnitTurn));
            Assert.That(second.Unit, Is.SameAs(runner));
            Assert.That(order.ClockTick, Is.EqualTo(4));
        }

        [Test]
        public void SlowerSpells_TakeMoreTicks()
        {
            var caster = UnitWith(speed: 1);
            var order = new TurnOrder(new[] { caster });
            order.BeginCharge(caster, Spell(AbilityEffect.Damage, 32, speed: 10), new GridCoord(0, 0));

            Assert.That(order.NextEvent().Kind, Is.EqualTo(TurnEventKind.SpellResolves));
            Assert.That(order.ClockTick, Is.EqualTo(10));
        }

        [Test]
        public void ChargingUnits_CantEvadeMoveOrAct()
        {
            var caster = UnitWith();
            var order = new TurnOrder(new[] { caster });
            order.BeginCharge(caster, Spell(AbilityEffect.Damage, 14), new GridCoord(0, 0));

            Assert.That(caster.CanEvade, Is.False);
            Assert.That(caster.CanMove, Is.False);
            Assert.That(caster.CanAct, Is.False);
        }

        [TestCase(StatusEffect.Silence)]
        [TestCase(StatusEffect.Sleep)]
        [TestCase(StatusEffect.Stop)]
        public void Charging_IsInterruptedBy(StatusEffect status)
        {
            var caster = UnitWith();
            var other = UnitWith(speed: 10, team: Team.Enemy);
            var order = new TurnOrder(new[] { caster, other });
            var charge = order.BeginCharge(caster, Spell(AbilityEffect.Damage, 14), new GridCoord(0, 0));

            caster.AddStatus(status);
            var e = order.NextEvent();

            Assert.That(e.Kind, Is.EqualTo(TurnEventKind.SpellInterrupted));
            Assert.That(e.Charge, Is.SameAs(charge));
            Assert.That(order.Charging, Is.Empty);
            Assert.That(caster.HasStatus(StatusEffect.Charging), Is.False);
        }

        [Test]
        public void Charging_IsInterruptedByAKnockOut()
        {
            var caster = UnitWith();
            var other = UnitWith(speed: 10, team: Team.Enemy);
            var order = new TurnOrder(new[] { caster, other });
            order.BeginCharge(caster, Spell(AbilityEffect.Damage, 14), new GridCoord(0, 0));

            caster.TakeDamage(999);

            Assert.That(order.NextEvent().Kind, Is.EqualTo(TurnEventKind.SpellInterrupted));
        }

        [Test]
        public void TurnList_ShowsSpellsResolvingInTheirPlace()
        {
            var caster = UnitWith(speed: 5);
            var other = UnitWith(speed: 10, team: Team.Enemy);
            var order = new TurnOrder(new[] { caster, other });
            var fire = Spell(AbilityEffect.Damage, 14, speed: 25);
            order.BeginCharge(caster, fire, new GridCoord(0, 0));

            var entries = order.PreviewEntries(3);

            // Fire resolves on tick 4, the Speed-10 unit acts on tick 10.
            Assert.That(entries[0].Spell, Is.SameAs(fire));
            Assert.That(entries[0].Unit, Is.SameAs(caster));
            Assert.That(entries[1].Spell, Is.Null);
            Assert.That(entries[1].Unit, Is.SameAs(other));
        }

        [Test]
        public void NextTurn_RefusesToSkipASpell()
        {
            var caster = UnitWith(speed: 1);
            var order = new TurnOrder(new[] { caster });
            order.BeginCharge(caster, Spell(AbilityEffect.Damage, 14), new GridCoord(0, 0));

            Assert.Throws<System.InvalidOperationException>(() => order.NextTurn());
        }

        [Test]
        public void CastingConcludesTheTurn_WithoutChargingForAMove()
        {
            var turn = new TurnState();
            turn.RecordAct();
            turn.Conclude();

            Assert.That(turn.IsFinished, Is.True);
            Assert.That(turn.MoveSpent, Is.False, "only the Act is paid for: 80 CT, as in FFT");
            Assert.That(TurnOrder.TurnCost(turn.MoveSpent, turn.ActSpent), Is.EqualTo(80));
        }

        // ------------------------------------------------------------------ Availability

        [Test]
        public void Spells_NeedMpAndAVoice_ButNotATarget()
        {
            var grid = TestGrids.Flat(5, 5);
            var caster = UnitWith(mp: 10);
            var fire = Spell(AbilityEffect.Damage, 14, mp: 6);
            var big = Spell(AbilityEffect.Damage, 32, mp: 48);
            Teach(caster, fire, big);
            var units = new[] { caster };

            Assert.That(AbilityRules.Unavailable(grid, fire, caster, units, null), Is.Null, "empty ground is a fine target");
            Assert.That(AbilityRules.Unavailable(grid, big, caster, units, null), Is.EqualTo("not enough MP"));
            caster.AddStatus(StatusEffect.Silence);
            Assert.That(AbilityRules.Unavailable(grid, fire, caster, units, null), Is.EqualTo("silenced"));
        }

        // ------------------------------------------------------------------ Revive, cures

        [Test]
        public void Raise_RevivesWithinTheDeathCounterOnly()
        {
            var grid = TestGrids.Flat(4, 4);
            var caster = UnitWith(ma: 20, resonance: 100);
            var fallen = UnitWith(resonance: 100, hp: 60, coord: new GridCoord(1, 0));
            var lost = UnitWith(resonance: 100, coord: new GridCoord(2, 0));
            var raise = Spell(AbilityEffect.Revive, 180, radius: 0, targeting: AbilityTargeting.KnockedOut, revivePercent: 50);
            fallen.TakeDamage(999);
            lost.TakeDamage(999);
            for (int i = 0; i < Unit.DeathCounterStart; i++)
                lost.TickDeathCounter();

            var revived = SpellResolver.Resolve(caster, raise, fallen.Coord, grid, new[] { caster, fallen, lost }, new System.Random(1));
            var tooLate = SpellResolver.Resolve(caster, raise, lost.Coord, grid, new[] { caster, fallen, lost }, new System.Random(1));

            Assert.That(revived.Targets.Single().Revived, Is.True);
            Assert.That(fallen.CurrentHp, Is.EqualTo(30), "half of 60");
            Assert.That(tooLate.Targets, Is.Empty);
            Assert.That(lost.IsKnockedOut, Is.True);
        }

        [Test]
        public void Purge_CuresEveryHarmfulStatusButNotBuffs()
        {
            var grid = TestGrids.Flat(4, 4);
            var caster = UnitWith(ma: 20, resonance: 100);
            var ally = UnitWith(resonance: 100, coord: new GridCoord(1, 0));
            var harmful = new[] { StatusEffect.Poison, StatusEffect.Blind, StatusEffect.Silence, StatusEffect.Sleep, StatusEffect.Slow,
                                  StatusEffect.Stop, StatusEffect.Disable, StatusEffect.Immobilize, StatusEffect.Panic };
            foreach (var status in harmful)
                ally.AddStatus(status);
            ally.AdjustMorale(5 - ally.Morale); // Panic follows Morale
            var buffs = new[] { StatusEffect.Regen, StatusEffect.Protect, StatusEffect.Shell };
            foreach (var status in buffs)
                ally.AddStatus(status);
            var purge = Spell(AbilityEffect.CureStatus, 190, radius: 0, cures: harmful);

            var result = SpellResolver.Resolve(caster, purge, ally.Coord, grid, new[] { caster, ally }, new System.Random(1));

            foreach (var status in harmful)
                Assert.That(ally.HasStatus(status), Is.False, status.ToString());
            foreach (var status in buffs)
                Assert.That(ally.HasStatus(status), Is.True, status.ToString());
            Assert.That(ally.Morale, Is.EqualTo(Unit.PanicThreshold), "Panic is lifted by steadying Morale");
            Assert.That(result.Targets.Single().Removed, Has.Member(StatusEffect.Panic));
        }

        [Test]
        public void Purge_LeavesHasteAlone()
        {
            var grid = TestGrids.Flat(4, 4);
            var caster = UnitWith(ma: 20, resonance: 100);
            var ally = UnitWith(resonance: 100, coord: new GridCoord(1, 0));
            ally.AddStatus(StatusEffect.Haste);
            var purge = Spell(AbilityEffect.CureStatus, 190, radius: 0, cures: new[] { StatusEffect.Slow, StatusEffect.Poison });

            SpellResolver.Resolve(caster, purge, ally.Coord, grid, new[] { caster, ally }, new System.Random(1));

            Assert.That(ally.HasStatus(StatusEffect.Haste), Is.True);
        }

        [Test]
        public void StatusSpells_RollTheirHitRate()
        {
            var grid = TestGrids.Flat(3, 3);
            var caster = UnitWith(ma: 10, resonance: 70);
            var poison = Spell(AbilityEffect.AddStatus, 40, radius: 0, inflicts: new[] { StatusEffect.Poison }); // (10 + 40) × 0.49 = 24%
            var rng = new System.Random(3);
            int landed = 0;
            for (int i = 0; i < 4000; i++)
            {
                var foe = UnitWith(resonance: 70, team: Team.Enemy, coord: new GridCoord(1, 1));
                if (SpellResolver.Resolve(caster, poison, foe.Coord, grid, new[] { caster, foe }, rng, grantRewards: false).AnyHit)
                    landed++;
            }

            Assert.That(landed / 4000.0, Is.InRange(0.21, 0.27));
        }

        [Test]
        public void Rewards_OffensiveSpellsGiveLevelExp_SupportSpellsGiveSupportExp()
        {
            var grid = TestGrids.Flat(4, 4);
            var caster = UnitWith(ma: 10, resonance: 100);
            var foe = UnitWith(resonance: 100, team: Team.Enemy, hp: 999, coord: new GridCoord(1, 0));
            var friend = UnitWith(resonance: 100, coord: new GridCoord(0, 1));
            friend.TakeDamage(10);

            SpellResolver.Resolve(caster, Spell(AbilityEffect.Damage, 1, radius: 0), foe.Coord, grid, new[] { caster, foe, friend }, new System.Random(1));
            int afterFire = caster.Experience;
            SpellResolver.Resolve(caster, Spell(AbilityEffect.RestoreHp, 1, radius: 0), friend.Coord, grid, new[] { caster, foe, friend }, new System.Random(1));

            Assert.That(afterFire, Is.EqualTo(Battle.Combat.BattleRewards.Experience(caster, foe)));
            Assert.That(caster.Experience - afterFire, Is.EqualTo(Battle.Combat.BattleRewards.SupportExperience(caster)));
        }

        // ------------------------------------------------------------------ Counter Magic

        private AbilityDefinition CounterMagicReaction()
        {
            var reaction = ScriptableObject.CreateInstance<AbilityDefinition>();
            _created.Add(reaction);
            reaction.Define("Counter Magic", "", AbilityKind.Reaction, 800);
            reaction.SetReaction(ReactionEffect.CounterMagic);
            return reaction;
        }

        private (Unit reactor, Unit caster, SpellResult incoming, BattleGrid grid) CounterSetup(int morale)
        {
            var grid = TestGrids.Flat(6, 6);
            var caster = UnitWith(ma: 5, resonance: 100, team: Team.Enemy, hp: 500, coord: new GridCoord(0, 0));
            var reactor = UnitWith(ma: 5, resonance: 100, hp: 500, mp: 20, morale: morale, coord: new GridCoord(3, 3));
            var counter = CounterMagicReaction();
            Teach(reactor, counter);
            reactor.TryEquip(counter);
            var fire = Spell(AbilityEffect.Damage, 14, mp: 6, radius: 0);
            var incoming = SpellResolver.Resolve(caster, fire, reactor.Coord, grid, new[] { caster, reactor }, new System.Random(1));
            return (reactor, caster, incoming, grid);
        }

        [Test]
        public void CounterMagic_CastsTheSameSpellBack_PayingItsMp()
        {
            var (reactor, caster, incoming, grid) = CounterSetup(morale: 100);

            var counter = SpellResolver.TryCounterMagic(reactor, incoming, grid, new[] { caster, reactor }, new System.Random(2));

            Assert.That(counter, Is.Not.Null);
            Assert.That(counter.Spell, Is.SameAs(incoming.Spell));
            Assert.That(counter.Targets.Single().Target, Is.SameAs(caster));
            Assert.That(caster.CurrentHp, Is.EqualTo(500 - 70));
            Assert.That(reactor.CurrentMp, Is.EqualTo(14));
        }

        [Test]
        public void CounterMagic_NeedsMoraleAVoiceAndAnEnemyCaster()
        {
            var (reactor, caster, incoming, grid) = CounterSetup(morale: 0);
            Assert.That(SpellResolver.TryCounterMagic(reactor, incoming, grid, new[] { caster, reactor }, new System.Random(2)), Is.Null);

            (reactor, caster, incoming, grid) = CounterSetup(morale: 100);
            reactor.AddStatus(StatusEffect.Silence);
            Assert.That(SpellResolver.TryCounterMagic(reactor, incoming, grid, new[] { caster, reactor }, new System.Random(2)), Is.Null);
        }

        // ------------------------------------------------------------------ AI

        [Test]
        public void Ai_CastsIntoAClusterOfEnemies()
        {
            var grid = TestGrids.Flat(10, 10);
            // Move 3 from (2,2) gets within Fire's range 4 of the cluster, but not next to it to attack.
            var mage = UnitWith(ma: 12, resonance: 80, team: Team.Enemy, coord: new GridCoord(2, 2));
            var fire = Spell(AbilityEffect.Damage, 14, mp: 6);
            Teach(mage, fire);
            var a = UnitWith(resonance: 70, coord: new GridCoord(5, 5));
            var b = UnitWith(resonance: 70, coord: new GridCoord(5, 6));
            var c = UnitWith(resonance: 70, coord: new GridCoord(6, 5));

            var plan = AiPlanner.Plan(grid, mage, new[] { mage, a, b, c }, new MovementRules(2), new System.Random(4));

            Assert.That(plan.Spell, Is.SameAs(fire));
            Assert.That(plan.SpellTile, Is.EqualTo(new GridCoord(5, 5)), "the centre of the cluster catches all three");
        }

        [Test]
        public void Ai_HealsAHurtAlly()
        {
            var grid = TestGrids.Flat(12, 12);
            var medic = UnitWith(ma: 12, resonance: 80, team: Team.Enemy, coord: new GridCoord(0, 0));
            var mend = Spell(AbilityEffect.RestoreHp, 14, mp: 6);
            Teach(medic, mend);
            var hurt = UnitWith(resonance: 80, team: Team.Enemy, hp: 80, coord: new GridCoord(2, 0));
            hurt.TakeDamage(60);
            var farFoe = UnitWith(team: Team.Player, coord: new GridCoord(11, 11));

            var plan = AiPlanner.Plan(grid, medic, new[] { medic, hurt, farFoe }, new MovementRules(2), new System.Random(4));

            Assert.That(plan.Spell, Is.SameAs(mend));
            var area = SpellArea.Tiles(grid, plan.SpellTile, mend.AreaRadius, mend.AreaVertical);
            Assert.That(area, Has.Member(hurt.Coord));
        }

        [Test]
        public void Ai_SilencedCastersDontPlanSpells()
        {
            var mage = UnitWith(team: Team.Enemy);
            Teach(mage, Spell(AbilityEffect.Damage, 14));
            mage.AddStatus(StatusEffect.Silence);

            Assert.That(AiPlanner.CastableSpells(mage), Is.Empty);
        }

        [Test]
        public void Ai_SpellPlansAreDeterministicForASeed()
        {
            var grid = TestGrids.Flat(10, 10);
            AiPlan Run()
            {
                var mage = UnitWith(ma: 12, resonance: 80, team: Team.Enemy, coord: new GridCoord(0, 0));
                Teach(mage, Spell(AbilityEffect.Damage, 14));
                var a = UnitWith(coord: new GridCoord(4, 4));
                var b = UnitWith(coord: new GridCoord(6, 4));
                return AiPlanner.Plan(grid, mage, new[] { mage, a, b }, new MovementRules(2), new System.Random(9));
            }

            var first = Run();
            var second = Run();
            Assert.That(second.SpellTile, Is.EqualTo(first.SpellTile));
            Assert.That(second.MoveBefore, Is.EqualTo(first.MoveBefore));
        }
    }
}
