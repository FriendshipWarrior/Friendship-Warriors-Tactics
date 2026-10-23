using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Abilities;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Grid;
using FriendshipWarriors.UI;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class AbilityTests
    {
        private readonly List<Object> _created = new();
        private JobDefinition _cadet;
        private JobDefinition _corpsman;
        private AbilityDefinition _grit, _charge, _rivet, _lightPack, _diligence;
        private AbilityDefinition _tonic, _ether, _panacea, _salts, _longToss;

        [SetUp]
        public void SetUp()
        {
            _grit = Ability(AbilityKind.Action, 300, a => a.SetAction(AbilityEffect.BoostPhysicalAttack, AbilityTargeting.Self, 0, 0, 0, amount: 1));
            _charge = Ability(AbilityKind.Action, 80, a => a.SetAction(AbilityEffect.Damage, AbilityTargeting.Standing, 1, 1, 3, randomMultiplier: new Vector2Int(1, 4), knockback: true));
            _rivet = Ability(AbilityKind.Action, 90, a => a.SetAction(AbilityEffect.Damage, AbilityTargeting.Standing, 1, 4, 99, randomMultiplier: new Vector2Int(1, 2), knockback: true));
            _lightPack = Ability(AbilityKind.Movement, 200, a => a.SetPassive(moveBonus: 1));
            _diligence = Ability(AbilityKind.Support, 200, a => a.SetPassive(proficiencyBonusPercent: 50));
            _tonic = Ability(AbilityKind.Action, 30, a => a.SetAction(AbilityEffect.RestoreHp, AbilityTargeting.Standing, 0, 1, 99, amount: 30, isItem: true));
            _ether = Ability(AbilityKind.Action, 300, a => a.SetAction(AbilityEffect.RestoreMp, AbilityTargeting.Standing, 0, 1, 99, amount: 20, isItem: true));
            _panacea = Ability(AbilityKind.Action, 900, a => a.SetAction(AbilityEffect.RestoreAll, AbilityTargeting.Standing, 0, 1, 99, isItem: true));
            _salts = Ability(AbilityKind.Action, 90, a => a.SetAction(AbilityEffect.Revive, AbilityTargeting.KnockedOut, 0, 1, 99, isItem: true));
            _longToss = Ability(AbilityKind.Support, 350, a => a.SetPassive(throwsItems: true));

            _cadet = Job("Cadet", 4, _grit, _charge, _rivet, _lightPack, _diligence);
            _corpsman = Job("Corpsman", 3, _tonic, _ether, _panacea, _salts, _longToss);
            _corpsman.SetInnateAbilities(new[] { _longToss });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private AbilityDefinition Ability(AbilityKind kind, int cost, System.Action<AbilityDefinition> setup)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Define(kind.ToString(), "", kind, cost);
            setup(ability);
            _created.Add(ability);
            return ability;
        }

        private JobDefinition Job(string name, int move, params AbilityDefinition[] abilities)
        {
            var job = ScriptableObject.CreateInstance<JobDefinition>();
            job.Define(name, "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), move, 3, 5,
                new JobRequirement[0], Color.gray);
            job.SetAbilities(name, abilities);
            _created.Add(job);
            return job;
        }

        /// <summary>A unit with exact shown stats (HP 30, MP 14, P.Atk = pa) that has learned all its job's abilities.</summary>
        private Unit UnitOf(JobDefinition job, GridCoord at, Team team = Team.Player, int pa = 5)
        {
            var unit = new Unit(job.DisplayName, team, job, new RawStats(491520, 229376, 6 * 16384, pa * 16384, 4 * 16384)) { Coord = at };
            foreach (var ability in job.Abilities)
            {
                unit.GainProficiency(ability.LearnCost);
                unit.TryLearn(ability, job);
            }
            return unit;
        }

        // ------------------------------------------------------------------ Drill

        [Test]
        public void Grit_RaisesPhysicalAttackByOne_StacksAndEndsWithTheBattle()
        {
            var unit = UnitOf(_cadet, new GridCoord(0, 0));
            int pa = unit.Stats.PhysicalAttack;
            var rng = new System.Random(1);

            AbilityResolver.Apply(AbilityResolver.Resolve(_grit, unit, unit, rng), unit);
            AbilityResolver.Apply(AbilityResolver.Resolve(_grit, unit, unit, rng), unit);
            Assert.That(unit.Stats.PhysicalAttack, Is.EqualTo(pa + 2));

            unit.ClearBattleModifiers();
            Assert.That(unit.Stats.PhysicalAttack, Is.EqualTo(pa));
        }

        [Test]
        public void Grit_OnlyTargetsTheUser()
        {
            var unit = UnitOf(_cadet, new GridCoord(1, 1));
            var friend = UnitOf(_cadet, new GridCoord(1, 2));

            var targets = AbilityRules.Targets(TestGrids.Flat(4, 4), _grit, unit, new[] { unit, friend });

            Assert.That(targets, Is.EqualTo(new[] { unit }));
        }

        [Test]
        public void ShoulderCharge_DealsPhysicalAttackTimesOneToFour()
        {
            var user = UnitOf(_cadet, new GridCoord(0, 0), pa: 6);
            var target = UnitOf(_cadet, new GridCoord(0, 1), Team.Enemy);
            target.Facing = Direction.North; // hit from behind: no evasion
            var rng = new System.Random(3);

            var damages = Enumerable.Range(0, 400).Select(_ => AbilityResolver.Resolve(_charge, user, target, rng).Damage).ToList();

            Assert.That(damages.Distinct().OrderBy(d => d), Is.EqualTo(new[] { 6, 12, 18, 24 }));
        }

        [Test]
        public void DamageAbilities_AreEvadedLikeAttacks()
        {
            var user = UnitOf(_cadet, new GridCoord(0, 0));
            var target = UnitOf(_cadet, new GridCoord(0, 1), Team.Enemy);
            target.Facing = Direction.South; // facing the user: class evasion 5% applies

            var outcome = AbilityResolver.Resolve(_rivet, user, target, new System.Random(1));

            Assert.That(outcome.HitChance, Is.EqualTo(CombatFormulas.HitChance(user, target)));
            Assert.That(outcome.HitChance, Is.EqualTo(95));
        }

        [Test]
        public void HurlRivet_ReachesFourTiles()
        {
            var grid = TestGrids.Flat(9, 9);
            var user = UnitOf(_cadet, new GridCoord(4, 4));

            var tiles = AbilityRules.TargetTiles(grid, _rivet, user);

            Assert.That(tiles.Count, Is.EqualTo(2 * 4 * 5), "diamond of radius 4 without the centre");
            Assert.That(tiles.Max(t => t.ManhattanDistance(user.Coord)), Is.EqualTo(4));
        }

        // ------------------------------------------------------------------ Knockback

        private static bool Nobody(GridCoord _) => false;

        [Test]
        public void Knockback_PushesStraightAway()
        {
            var grid = TestGrids.Flat(5, 1);

            Assert.That(Knockback.Destination(grid, new GridCoord(0, 0), new GridCoord(1, 0), Nobody), Is.EqualTo(new GridCoord(2, 0)));
        }

        [TestCase("001")]  // a wall (higher ground) behind the target
        [TestCase("00.")]  // a gap
        [TestCase("00#")]  // a blocked tile
        [TestCase("00")]   // the map edge
        public void Knockback_IsBlocked(string row)
        {
            var grid = TestGrids.FromRows(row);

            Assert.That(Knockback.Destination(grid, new GridCoord(0, 0), new GridCoord(1, 0), Nobody), Is.Null);
        }

        [Test]
        public void Knockback_IsBlockedByAnotherUnit_ButCanDropToLowerGround()
        {
            var flat = TestGrids.Flat(3, 1);
            Assert.That(Knockback.Destination(flat, new GridCoord(0, 0), new GridCoord(1, 0), c => c == new GridCoord(2, 0)), Is.Null);

            var ledge = TestGrids.FromRows("220");
            Assert.That(Knockback.Destination(ledge, new GridCoord(0, 0), new GridCoord(1, 0), Nobody), Is.EqualTo(new GridCoord(2, 0)));
        }

        [Test]
        public void KnockbackChance_IsAttackerMoraleOverBothMorales()
        {
            var job = ScriptableObject.CreateInstance<JobDefinition>();
            var raw = new RawStats(491520, 229376, 98304, 81920, 65536);
            var even = new Unit("A", Team.Player, job, raw);
            var bold = new Unit("B", Team.Player, job, raw, morale: 90);
            var timid = new Unit("C", Team.Enemy, job, raw, morale: 30);

            Assert.That(Knockback.Chance(even, even), Is.EqualTo(0.5));
            Assert.That(Knockback.Chance(bold, timid), Is.EqualTo(0.75));
            Assert.That(Knockback.Chance(timid, bold), Is.EqualTo(0.25));
            Object.DestroyImmediate(job);
        }

        // ------------------------------------------------------------------ Items

        [Test]
        public void Items_UseUpStock_AndCantBeUsedWhenNoneAreLeft()
        {
            var grid = TestGrids.Flat(3, 3);
            var medic = UnitOf(_corpsman, new GridCoord(1, 1));
            var hurt = UnitOf(_cadet, new GridCoord(1, 2));
            hurt.TakeDamage(20);
            var inventory = new Inventory();
            inventory.Add(_tonic, 1);
            var units = new[] { medic, hurt };

            Assert.That(AbilityRules.Unavailable(grid, _tonic, medic, units, inventory), Is.Null);
            AbilityCommand.Execute(_tonic, medic, hurt, grid, units, inventory, new System.Random(1));

            Assert.That(inventory.Count(_tonic), Is.Zero);
            Assert.That(hurt.CurrentHp, Is.EqualTo(30), "30 HP restored, capped at max");
            Assert.That(AbilityRules.Unavailable(grid, _tonic, medic, units, inventory), Is.EqualTo("none left"));
        }

        [Test]
        public void RestoreItems_CapAtTheMaximum()
        {
            var unit = UnitOf(_corpsman, new GridCoord(0, 0));
            unit.TakeDamage(10);
            unit.TrySpendMp(5);
            var rng = new System.Random(1);

            var hp = AbilityResolver.Apply(AbilityResolver.Resolve(_tonic, unit, unit, rng), unit);
            var mp = AbilityResolver.Apply(AbilityResolver.Resolve(_ether, unit, unit, rng), unit);

            Assert.That(hp.HpRestored, Is.EqualTo(10));
            Assert.That(mp.MpRestored, Is.EqualTo(5));
        }

        [Test]
        public void Panacea_RestoresEverything()
        {
            var unit = UnitOf(_corpsman, new GridCoord(0, 0));
            unit.TakeDamage(25);
            unit.TrySpendMp(10);

            AbilityResolver.Apply(AbilityResolver.Resolve(_panacea, unit, unit, new System.Random(1)), unit);

            Assert.That(unit.CurrentHp, Is.EqualTo(unit.Stats.MaxHp));
            Assert.That(unit.CurrentMp, Is.EqualTo(unit.Stats.MaxMp));
        }

        [Test]
        public void RevivalSalts_OnlyWorkWhileTheDeathCounterRuns()
        {
            var medic = UnitOf(_corpsman, new GridCoord(0, 0));
            var fallen = UnitOf(_cadet, new GridCoord(0, 1));
            var standing = UnitOf(_cadet, new GridCoord(1, 0));
            fallen.TakeDamage(999);

            Assert.That(AbilityRules.IsValidTarget(_salts, medic, fallen), Is.True);
            Assert.That(AbilityRules.IsValidTarget(_salts, medic, standing), Is.False, "only knocked-out units");
            Assert.That(AbilityRules.IsValidTarget(_tonic, medic, fallen), Is.False, "tonics don't work on the fallen");

            var outcome = AbilityResolver.Apply(AbilityResolver.Resolve(_salts, medic, fallen, new System.Random(4)), fallen);
            Assert.That(outcome.Revived, Is.True);
            Assert.That(fallen.IsKnockedOut, Is.False);
            Assert.That(fallen.CurrentHp, Is.InRange(AbilityResolver.ReviveHpMin, AbilityResolver.ReviveHpMax));

            var lost = UnitOf(_cadet, new GridCoord(2, 2));
            lost.TakeDamage(999);
            lost.TickDeathCounter();
            lost.TickDeathCounter();
            lost.TickDeathCounter();
            Assert.That(AbilityRules.IsValidTarget(_salts, medic, lost), Is.False, "too late once the counter runs out");
            Assert.That(lost.Revive(10), Is.False);
        }

        [Test]
        public void ItemRange_IsFourForThrowers_OtherwiseOne()
        {
            var corpsman = UnitOf(_corpsman, new GridCoord(0, 0));
            var cadet = UnitOf(_cadet, new GridCoord(0, 0));

            Assert.That(corpsman.ThrowsItems, Is.True, "Long Toss is innate to the Corpsman");
            Assert.That(AbilityRules.Range(_tonic, corpsman).max, Is.EqualTo(AbilityRules.ThrownItemRange));
            Assert.That(cadet.ThrowsItems, Is.False);
            Assert.That(AbilityRules.Range(_tonic, cadet).max, Is.EqualTo(1));

            cadet.GainProficiency(350, _corpsman);
            cadet.TryLearn(_longToss, _corpsman);
            cadet.TryEquip(_longToss);
            Assert.That(AbilityRules.Range(_tonic, cadet).max, Is.EqualTo(AbilityRules.ThrownItemRange), "equipped Long Toss");
        }

        // ------------------------------------------------------------------ Passives

        [Test]
        public void LightPack_AddsOneMove()
        {
            var unit = UnitOf(_cadet, new GridCoord(0, 0));
            Assert.That(unit.Stats.Move, Is.EqualTo(4));

            unit.TryEquip(_lightPack);

            Assert.That(unit.Stats.Move, Is.EqualTo(5));
        }

        [Test]
        public void Diligence_GivesHalfAgainAsMuchProf()
        {
            var unit = UnitOf(_cadet, new GridCoord(0, 0));
            int normal = BattleRewards.Proficiency(unit);

            unit.TryEquip(_diligence);

            Assert.That(BattleRewards.Proficiency(unit), Is.EqualTo(normal * 150 / 100));
        }

        // ------------------------------------------------------------------ Menu and rewards

        [Test]
        public void Menu_ListsLearnedActions_DisablingUnusableOnes()
        {
            var grid = TestGrids.Flat(3, 3);
            var medic = UnitOf(_corpsman, new GridCoord(1, 1));
            var inventory = new Inventory();
            inventory.Add(_tonic, 2);
            var menu = new MenuModel();

            new AbilityCommand().BuildMenu(menu, _corpsman, medic, grid, new[] { medic }, inventory);

            // Long Toss is a passive, so only the four items are listed.
            Assert.That(menu.Items.Count, Is.EqualTo(4));
            Assert.That(menu.Items[0].Enabled, Is.True);
            StringAssert.Contains("x2", menu.Items[0].Label);
            Assert.That(menu.Items[1].Enabled, Is.False, "no ethers in stock");
            Assert.That(menu.Items[3].Enabled, Is.False, "nobody knocked out to revive");
        }

        [Test]
        public void SupportActionsGiveSupportExp_DamageGivesLevelBasedExp()
        {
            var grid = TestGrids.Flat(3, 3);
            var medic = UnitOf(_corpsman, new GridCoord(1, 1));
            var friend = UnitOf(_cadet, new GridCoord(1, 2));
            var inventory = new Inventory();
            inventory.Add(_tonic, 1);

            int before = medic.Experience;
            AbilityCommand.Execute(_tonic, medic, friend, grid, new[] { medic, friend }, inventory, new System.Random(1));
            Assert.That(medic.Experience - before, Is.EqualTo(BattleRewards.SupportExperience(medic)));

            var cadet = UnitOf(_cadet, new GridCoord(0, 0));
            var foe = UnitOf(_cadet, new GridCoord(0, 1), Team.Enemy);
            foe.Facing = Direction.North;
            int expected = BattleRewards.Experience(cadet, foe);
            AbilityCommand.Execute(_charge, cadet, foe, grid, new[] { cadet, foe }, null, new System.Random(1));
            Assert.That(cadet.Experience, Is.EqualTo(expected));
        }
    }
}
