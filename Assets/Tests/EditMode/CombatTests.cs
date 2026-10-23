using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Equipment;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class CombatTests
    {
        private readonly List<Object> _created = new();
        private JobDefinition _fighter;

        [SetUp]
        public void SetUp()
        {
            _fighter = ScriptableObject.CreateInstance<JobDefinition>();
            // 100% multipliers so shown stats equal raw / 16384.
            _fighter.Define("Fighter", "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), 4, 3, 10,
                new JobRequirement[0], Color.gray);
            _fighter.SetEquipment(new[] { WeaponType.Sword, WeaponType.Knife, WeaponType.Rifle, WeaponType.Rod, WeaponType.Staff }, canUseShields: true);
            _created.Add(_fighter);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        /// <summary>A unit whose shown P.Atk, M.Atk and Speed are exactly the given values.</summary>
        private Unit UnitWith(int pa = 6, int ma = 4, int speed = 8, Team team = Team.Player, int hpRaw = 491520) =>
            new("U", team, _fighter, new RawStats(hpRaw, 229376, speed * 16384, pa * 16384, ma * 16384));

        private EquipmentDefinition Weapon(WeaponType type, int power, int maxRange = 1, int reach = 3)
        {
            var item = ScriptableObject.CreateInstance<EquipmentDefinition>();
            item.DefineWeapon(type.ToString(), "", type, power, 1, maxRange, reach);
            _created.Add(item);
            return item;
        }

        private EquipmentDefinition Armor(EquipmentCategory category, int evasion)
        {
            var item = ScriptableObject.CreateInstance<EquipmentDefinition>();
            item.DefineArmor(category.ToString(), "", category, evasion);
            _created.Add(item);
            return item;
        }

        // ------------------------------------------------------------------ Damage formulas (FFT)

        [TestCase(WeaponType.Sword, 5, 6 * 5)]                 // PA × WP
        [TestCase(WeaponType.Knife, 5, (6 + 8) / 2 * 5)]       // (PA + Sp) / 2 × WP
        [TestCase(WeaponType.Rifle, 5, (6 + 8) / 2 * 5)]       // FFT's bow formula
        [TestCase(WeaponType.Rod, 5, 6 * 5)]                   // PA × WP
        [TestCase(WeaponType.Staff, 5, 4 * 5)]                 // MA × WP
        public void WeaponDamage_FollowsFftFormulas(WeaponType type, int power, int expected)
        {
            var unit = UnitWith();
            unit.TryEquipItem(Weapon(type, power));

            Assert.That(CombatFormulas.AttackFactors(unit).Damage, Is.EqualTo(expected));
        }

        [Test]
        public void BareHands_UseTheBraveStandIn()
        {
            var unit = UnitWith(pa: 10);

            // (PA × Brave / 100) × PA with Brave standing in at 70.
            Assert.That(CombatFormulas.AttackFactors(unit).Damage, Is.EqualTo(10 * 70 / 100 * 10));
        }

        [Test]
        public void CriticalDamage_RaisesXaByUpToXaMinusOne()
        {
            var factors = new AttackFactors(6, 5);
            var rng = new System.Random(1);
            var results = Enumerable.Range(0, 2000).Select(_ => CombatFormulas.CriticalDamage(factors, rng)).ToList();

            Assert.That(results.Min(), Is.EqualTo(6 * 5));
            Assert.That(results.Max(), Is.EqualTo(11 * 5));
        }

        // ------------------------------------------------------------------ Hit chance (FFT evasion by facing)

        private (Unit attacker, Unit target) Duel(Direction targetFacing, GridCoord attackerAt)
        {
            var target = UnitWith(team: Team.Enemy);
            target.Coord = new GridCoord(5, 5);
            target.Facing = targetFacing;
            target.TryEquipItem(Armor(EquipmentCategory.Shield, 20));
            target.TryEquipItem(Armor(EquipmentCategory.Accessory, 10));
            var attacker = UnitWith();
            attacker.Coord = attackerAt;
            return (attacker, target);
        }

        [Test]
        public void HitChance_FromTheFront_MultipliesAllThreeEvasions()
        {
            var (attacker, target) = Duel(Direction.North, new GridCoord(5, 6));

            // 100 × 90% (class) × 80% (shield) × 90% (accessory) = 64.8.
            Assert.That(CombatFormulas.HitChance(attacker, target), Is.EqualTo(64));
        }

        [Test]
        public void HitChance_FromTheSide_IgnoresClassEvasion()
        {
            var (attacker, target) = Duel(Direction.North, new GridCoord(6, 5));

            Assert.That(CombatFormulas.HitChance(attacker, target), Is.EqualTo(72)); // 80% × 90%
        }

        [Test]
        public void HitChance_FromBehind_OnlyCountsAccessoryEvasion()
        {
            var (attacker, target) = Duel(Direction.North, new GridCoord(5, 4));

            Assert.That(CombatFormulas.HitChance(attacker, target), Is.EqualTo(90));
        }

        // ------------------------------------------------------------------ Timed press

        [Test]
        public void Accuracy_PeaksAtThePerfectMomentAndFadesToZero()
        {
            var rules = TimedPressRules.Default;

            Assert.That(rules.Accuracy(rules.PerfectTime), Is.EqualTo(1f));
            Assert.That(rules.Accuracy(rules.PerfectTime + rules.Tolerance / 2f), Is.EqualTo(0.5f).Within(1e-4));
            Assert.That(rules.Accuracy(rules.PerfectTime - rules.Tolerance), Is.Zero.Within(1e-4));
            Assert.That(rules.Accuracy(rules.Duration + 0.1f), Is.Zero);
            Assert.That(rules.Accuracy(null), Is.Zero);
        }

        [Test]
        public void Chances_GrowWithAccuracy_ButNeverReachCertainty()
        {
            var rules = TimedPressRules.Default;
            double previousBlock = -1, previousCritical = -1;
            for (float a = 0f; a <= 1.0001f; a += 0.1f)
            {
                Assert.That(rules.BlockChance(a), Is.GreaterThanOrEqualTo(previousBlock));
                Assert.That(rules.CriticalChance(a), Is.GreaterThanOrEqualTo(previousCritical));
                previousBlock = rules.BlockChance(a);
                previousCritical = rules.CriticalChance(a);
            }
            Assert.That(rules.BlockChance(1f), Is.LessThan(1.0));
            Assert.That(rules.CriticalChance(1f), Is.LessThan(1.0));
            Assert.That(rules.CriticalChance(0f), Is.EqualTo(CombatFormulas.BaseCriticalChance));
        }

        // ------------------------------------------------------------------ Resolution

        private (Unit attacker, Unit target) OpenDuel()
        {
            // Attacking from behind with no accessory: nothing to evade with.
            var target = UnitWith(team: Team.Enemy);
            target.Coord = new GridCoord(5, 5);
            target.Facing = Direction.North;
            var attacker = UnitWith();
            attacker.Coord = new GridCoord(5, 4);
            attacker.TryEquipItem(Weapon(WeaponType.Sword, 4));
            return (attacker, target);
        }

        private static (int crits, int blocks, int hits) Tally(Unit attacker, Unit target, float offense, float defense, int trials = 10000)
        {
            var rng = new System.Random(42);
            int crits = 0, blocks = 0, hits = 0;
            for (int i = 0; i < trials; i++)
            {
                var r = AttackResolver.Resolve(attacker, target, rng, TimedPressRules.Default, offense, defense);
                if (r.Critical) crits++;
                if (r.Blocked) blocks++;
                if (r.Hit && !r.Blocked) hits++;
            }
            return (crits, blocks, hits);
        }

        [Test]
        public void WithoutAPress_CriticalsHappenAboutFivePercentOfTheTime()
        {
            var (attacker, target) = OpenDuel();
            var (crits, _, hits) = Tally(attacker, target, 0f, 0f);

            Assert.That(hits, Is.EqualTo(10000), "no evasion from behind");
            Assert.That(crits / 10000.0, Is.InRange(0.04, 0.06));
        }

        [Test]
        public void APerfectStrike_RaisesCriticalChanceToFiftyPercent()
        {
            var (attacker, target) = OpenDuel();
            var (crits, _, _) = Tally(attacker, target, 1f, 0f);

            Assert.That(crits / 10000.0, Is.InRange(0.47, 0.53));
        }

        [Test]
        public void APerfectBlock_StopsMostHitsButNeverAll()
        {
            var (attacker, target) = OpenDuel();
            var (_, blocks, hits) = Tally(attacker, target, 0f, 1f);

            Assert.That(blocks / 10000.0, Is.InRange(0.57, 0.63));
            Assert.That(hits, Is.GreaterThan(0));
        }

        [Test]
        public void FullEvasion_AlwaysMisses()
        {
            var (attacker, target) = OpenDuel();
            attacker.Coord = new GridCoord(5, 6); // in front now
            target.TryEquipItem(Armor(EquipmentCategory.Shield, 100));

            var (_, _, hits) = Tally(attacker, target, 1f, 0f, trials: 500);

            Assert.That(hits, Is.Zero);
        }

        [Test]
        public void Resolution_IsDeterministicForASeed()
        {
            var (attacker, target) = OpenDuel();
            var a = AttackResolver.Resolve(attacker, target, new System.Random(9), TimedPressRules.Default, 0.7f, 0f);
            var b = AttackResolver.Resolve(attacker, target, new System.Random(9), TimedPressRules.Default, 0.7f, 0f);

            Assert.That(a.Damage, Is.EqualTo(b.Damage));
            Assert.That(a.Critical, Is.EqualTo(b.Critical));
        }

        // ------------------------------------------------------------------ Knock-out, rewards, equipment, range

        [Test]
        public void ReachingZeroHp_KnocksOutAndStartsAThreeTurnCounter()
        {
            var unit = UnitWith();

            unit.TakeDamage(9999);
            Assert.That(unit.IsKnockedOut, Is.True);
            Assert.That(unit.CurrentHp, Is.Zero);
            Assert.That(unit.DeathCounter, Is.EqualTo(3));
            Assert.That(unit.TakeDamage(5), Is.Zero, "no further damage while down");

            Assert.That(unit.TickDeathCounter(), Is.False);
            Assert.That(unit.TickDeathCounter(), Is.False);
            Assert.That(unit.TickDeathCounter(), Is.True);
            Assert.That(unit.IsGone, Is.True);
        }

        [Test]
        public void Rewards_FollowFftFormulas()
        {
            var actor = UnitWith();
            actor.GainExperience(ExperienceCurve.TotalFor(8));
            actor.GainProficiency(400); // job level 3
            var target = UnitWith(team: Team.Enemy);
            target.GainExperience(ExperienceCurve.TotalFor(11));

            // (10 + 11 − 8)% of what level 8 → 9 costs; the reverse is (10 + 8 − 11)% of level 11 → 12.
            Assert.That(BattleRewards.Experience(actor, target), Is.EqualTo((int)System.Math.Round(ExperienceCurve.ToNextLevel(8) * 0.13)));
            Assert.That(BattleRewards.Proficiency(actor), Is.EqualTo(8 + 2 * 3 + 8 / 4));
            Assert.That(BattleRewards.SpilloverProficiency(16), Is.EqualTo(4));
            Assert.That(BattleRewards.Experience(target, actor), Is.EqualTo((int)System.Math.Round(ExperienceCurve.ToNextLevel(11) * 0.07)));
        }

        private Unit UnitAtLevel(int level)
        {
            var unit = UnitWith();
            unit.GainExperience(ExperienceCurve.TotalFor(level));
            return unit;
        }

        [TestCase(1)]
        [TestCase(20)]
        [TestCase(50)]
        [TestCase(98)]
        public void SameLevelTargets_AreWorthATenthOfALevel_AtAnyLevel(int level)
        {
            var actor = UnitAtLevel(level);
            var target = UnitAtLevel(level);

            double share = BattleRewards.Experience(actor, target) / (double)ExperienceCurve.ToNextLevel(level);
            Assert.That(share, Is.EqualTo(0.10).Within(0.01));
        }

        [Test]
        public void StrongerTargetsGiveMore_WeakerGiveLess()
        {
            var actor = UnitAtLevel(30);
            int weaker = BattleRewards.Experience(actor, UnitAtLevel(25));
            int equal = BattleRewards.Experience(actor, UnitAtLevel(30));
            int stronger = BattleRewards.Experience(actor, UnitAtLevel(35));

            Assert.That(weaker, Is.LessThan(equal));
            Assert.That(stronger, Is.GreaterThan(equal));
        }

        [Test]
        public void ExperienceIsAtLeastOne_AtMostOneLevel_AndZeroAtTheCap()
        {
            Assert.That(BattleRewards.Experience(UnitAtLevel(60), UnitAtLevel(1)), Is.EqualTo(1));
            var fresh = UnitAtLevel(1);
            Assert.That(BattleRewards.Experience(fresh, UnitAtLevel(99)), Is.EqualTo(ExperienceCurve.ToNextLevel(1)));
            Assert.That(BattleRewards.Experience(UnitAtLevel(99), UnitAtLevel(99)), Is.Zero);
        }

        [Test]
        public void Equipment_MustSuitTheJob()
        {
            var archer = ScriptableObject.CreateInstance<JobDefinition>();
            _created.Add(archer);
            archer.SetEquipment(new[] { WeaponType.Rifle }, canUseShields: false);
            var rifle = Weapon(WeaponType.Rifle, 4);
            var sword = Weapon(WeaponType.Sword, 4);
            var shield = Armor(EquipmentCategory.Shield, 10);

            Assert.That(archer.CanEquip(rifle), Is.True);
            Assert.That(archer.CanEquip(sword), Is.False);
            Assert.That(archer.CanEquip(shield), Is.False);
            Assert.That(archer.CanEquip(Armor(EquipmentCategory.Accessory, 5)), Is.True);
        }

        [Test]
        public void ChangingJob_TakesOffEquipmentTheNewJobCantUse()
        {
            var unit = UnitWith();
            unit.TryEquipItem(Weapon(WeaponType.Sword, 4));
            unit.TryEquipItem(Armor(EquipmentCategory.Shield, 10));
            var mage = ScriptableObject.CreateInstance<JobDefinition>();
            _created.Add(mage);
            mage.SetEquipment(new[] { WeaponType.Rod }, canUseShields: false);

            unit.TryChangeJob(mage);

            Assert.That(unit.Weapon, Is.Null);
            Assert.That(unit.Shield, Is.Null);
            Assert.That(unit.WeaponType, Is.EqualTo(WeaponType.Unarmed));
        }

        private EquipmentDefinition Gear(EquipmentCategory category, ArmorType type, int hp, int mp)
        {
            var item = ScriptableObject.CreateInstance<EquipmentDefinition>();
            item.DefineGear(type.ToString(), "", category, type, hp, mp);
            _created.Add(item);
            return item;
        }

        [Test]
        public void Units_HaveFiveSlots_TwoHandsHeadBodyAccessory()
        {
            _fighter.SetArmorTypes(new[] { ArmorType.Helmet, ArmorType.Armor });
            var unit = UnitWith();
            var sword = Weapon(WeaponType.Sword, 4);
            var shield = Armor(EquipmentCategory.Shield, 10);
            var helm = Gear(EquipmentCategory.Head, ArmorType.Helmet, 10, 0);
            var plate = Gear(EquipmentCategory.Body, ArmorType.Armor, 10, 0);
            var ring = Armor(EquipmentCategory.Accessory, 5);

            foreach (var item in new[] { sword, shield, helm, plate, ring })
                Assert.That(unit.TryEquipItem(item), Is.True, item.name);

            Assert.That(unit.Equipped(EquipmentSlot.MainHand), Is.SameAs(sword));
            Assert.That(unit.Equipped(EquipmentSlot.OffHand), Is.SameAs(shield));
            Assert.That(unit.Equipped(EquipmentSlot.Head), Is.SameAs(helm));
            Assert.That(unit.Equipped(EquipmentSlot.Body), Is.SameAs(plate));
            Assert.That(unit.Equipped(EquipmentSlot.Accessory), Is.SameAs(ring));
        }

        [Test]
        public void HeadAndBodyGear_AddToMaxHpAndMp()
        {
            _fighter.SetArmorTypes(new[] { ArmorType.Hat, ArmorType.Robe });
            var unit = UnitWith();
            int baseHp = unit.Stats.MaxHp, baseMp = unit.Stats.MaxMp;

            unit.TryEquipItem(Gear(EquipmentCategory.Head, ArmorType.Hat, 8, 0));
            unit.TryEquipItem(Gear(EquipmentCategory.Body, ArmorType.Robe, 10, 10));

            Assert.That(unit.Stats.MaxHp, Is.EqualTo(baseHp + 18));
            Assert.That(unit.Stats.MaxMp, Is.EqualTo(baseMp + 10));
            Assert.That(unit.CurrentHp, Is.EqualTo(baseHp), "equipping doesn't heal");
            unit.RestoreFully();
            Assert.That(unit.CurrentHp, Is.EqualTo(baseHp + 18));

            unit.Unequip(EquipmentSlot.Body);
            Assert.That(unit.CurrentHp, Is.EqualTo(baseHp + 8), "current HP is capped when the maximum drops");
        }

        [Test]
        public void ArmorTypes_AreLimitedByJob()
        {
            _fighter.SetArmorTypes(new[] { ArmorType.Hat, ArmorType.Clothing });

            Assert.That(_fighter.CanEquip(Gear(EquipmentCategory.Head, ArmorType.Hat, 8, 0)), Is.True);
            Assert.That(_fighter.CanEquip(Gear(EquipmentCategory.Head, ArmorType.Helmet, 10, 0)), Is.False);
            Assert.That(_fighter.CanEquip(Gear(EquipmentCategory.Body, ArmorType.Armor, 10, 0)), Is.False);
        }

        [Test]
        public void TwoHandedWeapons_LeaveNoRoomForAShield()
        {
            var unit = UnitWith();
            var shield = Armor(EquipmentCategory.Shield, 10);
            var rifle = ScriptableObject.CreateInstance<EquipmentDefinition>();
            _created.Add(rifle);
            rifle.DefineWeapon("Rifle", "", WeaponType.Rifle, 4, 1, 5, 99, twoHanded: true);

            unit.TryEquipItem(shield);
            unit.TryEquipItem(rifle);
            Assert.That(unit.Shield, Is.Null, "the rifle needs both hands");
            Assert.That(unit.TryEquipItem(shield), Is.False);
        }

        [Test]
        public void MeleeRange_IsTheFourNeighboursWithinVerticalReach()
        {
            var grid = TestGrids.FromRows(
                "000",
                "040",
                "000");
            var unit = UnitWith();
            unit.Coord = new GridCoord(1, 0);
            unit.TryEquipItem(Weapon(WeaponType.Sword, 4, maxRange: 1, reach: 3));

            var targets = AttackRange.Targets(grid, unit);

            // (1,1) is 4h up, beyond a 3h reach.
            Assert.That(targets, Is.EquivalentTo(new[] { new GridCoord(0, 0), new GridCoord(2, 0) }));
        }

        [Test]
        public void RifleRange_IsADiamondOfRadiusFive()
        {
            var grid = TestGrids.Flat(11, 11);
            var unit = UnitWith();
            unit.Coord = new GridCoord(5, 5);
            unit.TryEquipItem(Weapon(WeaponType.Rifle, 4, maxRange: 5, reach: 99));

            // Every tile within Manhattan distance 5 except the unit's own: 2 × 5 × 6 = 60.
            Assert.That(AttackRange.Targets(grid, unit).Count, Is.EqualTo(60));
        }
    }
}
