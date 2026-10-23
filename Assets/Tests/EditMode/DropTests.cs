using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Audio;
using FriendshipWarriors.Battle.Abilities;
using FriendshipWarriors.Battle.AI;
using FriendshipWarriors.Battle.Drops;
using FriendshipWarriors.Feedback;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class DropTests
    {
        private readonly List<Object> _created = new();
        private JobDefinition _cadet, _vanguard, _beast;
        private AbilityDefinition _grit, _charge, _guard, _tonic, _salts, _panacea;
        private EquipmentDefinition _sword, _helmet;

        [SetUp]
        public void SetUp()
        {
            _grit = Ability("Grit");
            _charge = Ability("Shoulder Charge");
            _guard = Ability("Guard Stance");
            _tonic = Ability("Tonic");
            _salts = Ability("Revival Salts");
            _panacea = Ability("Panacea");

            _cadet = Job("Cadet");
            _cadet.SetAbilities("Drill", new[] { _grit, _charge });
            // Vanguard unlocks at Cadet job level 2, as in the game.
            _vanguard = Job("Vanguard", new JobRequirement(_cadet, 2));
            _vanguard.SetAbilities("Assault", new[] { _guard });
            _beast = Job("Beast");
            _beast.SetMonster(true);

            _sword = Make<EquipmentDefinition>();
            _sword.DefineWeapon("Iron Saber", "", WeaponType.Sword, 4, 1, 1, 3);
            _helmet = Make<EquipmentDefinition>();
            _helmet.DefineGear("Leather Cap", "", EquipmentCategory.Head, ArmorType.Helmet, 8, 0);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            _created.Add(obj);
            return obj;
        }

        private AbilityDefinition Ability(string name)
        {
            var ability = Make<AbilityDefinition>();
            ability.Define(name, "", AbilityKind.Action, 100);
            return ability;
        }

        private JobDefinition Job(string name, params JobRequirement[] requirements)
        {
            var job = Make<JobDefinition>();
            job.Define(name, "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), 4, 3, 5,
                requirements, Color.gray);
            job.SetEquipment(new[] { WeaponType.Sword }, canUseShields: false);
            job.SetArmorTypes(new[] { ArmorType.Helmet });
            return job;
        }

        private Unit NewUnit(string name, Team team, JobDefinition job, int x = 2, int y = 2) =>
            new(name, team, job, new RawStats(491520, 229376, 6 * 16384, 6 * 16384, 4 * 16384)) { Coord = new GridCoord(x, y) };

        /// <summary>A Cadet who has learned Grit and Shoulder Charge, and as a Vanguard, Guard Stance.</summary>
        private Unit Veteran()
        {
            var unit = NewUnit("Halloway", Team.Enemy, _cadet);
            unit.GainProficiency(1000, _cadet);
            unit.TryLearn(_grit, _cadet);
            unit.TryLearn(_charge, _cadet);
            unit.GainProficiency(500, _vanguard);
            unit.TryLearn(_guard, _vanguard);
            unit.TryEquipItem(_sword);
            unit.TryEquipItem(_helmet);
            return unit;
        }

        private static DropSettings Rods => new(100, 100, true);
        private static DropSettings Chests => new(0, 100, true);

        private MonsterLootTier[] Loot => new[]
        {
            new MonsterLootTier(1, _tonic),
            new MonsterLootTier(8, _salts),
            new MonsterLootTier(60, _panacea),
        };

        // ------------------------------------------------------------------ What drops

        [Test]
        public void ARod_HoldsEverythingTheFallenUnitLearned_WithTheJobThatTeachesIt()
        {
            var fallen = Veteran();
            var drop = DropRules.Create(1, fallen, new System.Random(3), Rods);

            Assert.That(drop.Kind, Is.EqualTo(DropKind.SteelRod));
            Assert.That(drop.Coord, Is.EqualTo(fallen.Coord));
            Assert.That(drop.FallenName, Is.EqualTo("Halloway"));
            Assert.That(drop.Abilities.Select(t => t.Ability.DisplayName),
                Is.EqualTo(new[] { "Grit", "Guard Stance", "Shoulder Charge" }), "sorted, so replays match");
            Assert.That(drop.Abilities.Single(t => t.Ability == _guard).Job, Is.SameAs(_vanguard));
        }

        [Test]
        public void AChest_HoldsOneOfTheFallenUnitsEquippedPieces()
        {
            var fallen = Veteran();
            var seen = new HashSet<EquipmentDefinition>();
            for (int seed = 0; seed < 40; seed++)
            {
                var drop = DropRules.Create(1, fallen, new System.Random(seed), Chests);
                Assert.That(drop.Kind, Is.EqualTo(DropKind.Chest));
                Assert.That(drop.Item, Is.Null);
                seen.Add(drop.Gear);
            }
            Assert.That(seen, Is.EquivalentTo(new[] { _sword, _helmet }), "either piece, never an empty slot");
        }

        [Test]
        public void Drops_AreTheSameForTheSameSeed()
        {
            var fallen = Veteran();
            var settings = DropSettings.Default;
            for (int seed = 0; seed < 20; seed++)
            {
                var a = DropRules.Create(1, fallen, new System.Random(seed), settings);
                var b = DropRules.Create(1, fallen, new System.Random(seed), settings);
                Assert.That(a.ToString(), Is.EqualTo(b.ToString()));
            }
        }

        [Test]
        public void TheDefaultOdds_GiveBothRodsAndChests()
        {
            var fallen = Veteran();
            var kinds = Enumerable.Range(0, 200)
                .Select(seed => DropRules.Create(1, fallen, new System.Random(seed), DropSettings.Default).Kind).ToList();
            int rods = kinds.Count(k => k == DropKind.SteelRod);
            Assert.That(rods, Is.InRange(70, 130), "about half");
        }

        [Test]
        public void AUnitWithNoGear_LeavesARodInsteadOfAnEmptyChest()
        {
            var bare = NewUnit("Bare", Team.Enemy, _cadet);

            var drop = DropRules.Create(1, bare, new System.Random(1), Chests);

            Assert.That(drop.Kind, Is.EqualTo(DropKind.SteelRod));
        }

        [Test]
        public void MonsterChests_HoldTheItemForTheirLevel()
        {
            Assert.That(DropRules.MonsterItem(1, Loot), Is.SameAs(_tonic));
            Assert.That(DropRules.MonsterItem(7, Loot), Is.SameAs(_tonic));
            Assert.That(DropRules.MonsterItem(8, Loot), Is.SameAs(_salts));
            Assert.That(DropRules.MonsterItem(99, Loot), Is.SameAs(_panacea));
            Assert.That(DropRules.MonsterItem(0, Loot), Is.Null, "below the first row");
            Assert.That(DropRules.MonsterItem(10, null), Is.Null);
        }

        [Test]
        public void AMonster_DropsItsLevelsItem_AndItsRodTeachesNothing()
        {
            var beast = NewUnit("Wolf", Team.Enemy, _beast);
            beast.GainExperience(100000); // well past level 8
            Assert.That(beast.IsMonster, Is.True);

            var chest = DropRules.Create(1, beast, new System.Random(1), Chests, Loot);
            Assert.That(chest.Kind, Is.EqualTo(DropKind.Chest));
            Assert.That(chest.Item, Is.SameAs(beast.Level >= 60 ? _panacea : _salts));
            Assert.That(chest.Gear, Is.Null);

            var rod = DropRules.Create(2, beast, new System.Random(1), Rods, Loot);
            Assert.That(rod.Kind, Is.EqualTo(DropKind.SteelRod));
            Assert.That(rod.FromMonster, Is.True);
            Assert.That(rod.Abilities, Is.Empty, "a monster's rod only restores (FFT)");
        }

        [Test]
        public void AMonsterWithNoLootRow_LeavesARod()
        {
            var beast = NewUnit("Rat", Team.Enemy, _beast);

            var drop = DropRules.Create(1, beast, new System.Random(1), Chests, System.Array.Empty<MonsterLootTier>());

            Assert.That(drop.Kind, Is.EqualTo(DropKind.SteelRod));
        }

        // ------------------------------------------------------------------ Taking a rod

        [Test]
        public void ARodTeaches_OnlyWhatThePickerDoesntKnow_FromJobsItHasUnlocked()
        {
            var drop = DropRules.Create(1, Veteran(), new System.Random(1), Rods);
            var picker = NewUnit("Pembroke", Team.Player, _cadet);
            picker.GainProficiency(100, _cadet);
            picker.TryLearn(_grit, _cadet);

            var learnable = DropRules.Inheritable(drop, picker, Rods);

            Assert.That(learnable, Is.EqualTo(new[] { _charge }), "Grit is known; Guard Stance needs Vanguard unlocked");
            Assert.That(DropRules.Inheritable(drop, picker, new DropSettings(100, 100, requireUnlockedJob: false)),
                Is.EqualTo(new[] { _guard, _charge }));
        }

        [Test]
        public void LearningFromARod_CostsNoProf()
        {
            var drop = DropRules.Create(1, Veteran(), new System.Random(1), Rods);
            var picker = NewUnit("Pembroke", Team.Player, _cadet);
            picker.GainProficiency(200, _cadet); // job level 2: Vanguard unlocked
            int prof = picker.AvailableProficiency(_cadet);

            var result = DropRules.Take(drop, picker, PickupChoice.Inherit, null, null, new System.Random(1), Rods);

            Assert.That(result.Choice, Is.EqualTo(PickupChoice.Inherit));
            Assert.That(result.Learned, Is.EquivalentTo(new[] { _grit, _guard, _charge }));
            Assert.That(picker.HasLearned(_guard) && picker.HasLearned(_grit) && picker.HasLearned(_charge), Is.True);
            Assert.That(picker.AvailableProficiency(_cadet), Is.EqualTo(prof));
            Assert.That(result.Summary, Is.EqualTo("Learned: Grit, Guard Stance, Shoulder Charge"));
            Assert.That(result.PopupText, Is.EqualTo(result.Summary), "three names still fit");
        }

        [Test]
        public void ALongListOfLearnedAbilities_IsShortenedInThePopup()
        {
            var result = new PickupResult(DropRules.Create(1, Veteran(), new System.Random(1), Rods), NewUnit("P", Team.Player, _cadet),
                PickupChoice.Inherit);
            result.Learned.AddRange(new[] { _grit, _charge, _guard, _tonic });

            Assert.That(result.PopupText, Is.EqualTo("Learned: Grit, Shoulder Charge +2 more"));
            Assert.That(result.Message, Does.EndWith("Learned: Grit, Shoulder Charge, Guard Stance, Tonic"), "the banner keeps them all");
        }

        [Test]
        public void TheInheritChance_IsRolledPerAbility()
        {
            var drop = DropRules.Create(1, Veteran(), new System.Random(1), Rods);
            var never = new DropSettings(100, 0, requireUnlockedJob: false);
            var picker = NewUnit("Pembroke", Team.Player, _cadet);

            var result = DropRules.Take(drop, picker, PickupChoice.Inherit, null, null, new System.Random(1), never);

            Assert.That(result.Learned, Is.Empty);
            Assert.That(result.Summary, Is.EqualTo("Nothing learned"));
        }

        [Test]
        public void Restoring_FillsHpAndMp()
        {
            var drop = DropRules.Create(1, Veteran(), new System.Random(1), Rods);
            var picker = NewUnit("Pembroke", Team.Player, _cadet);
            picker.TakeDamage(picker.CurrentHp - 1);
            picker.TrySpendMp(picker.CurrentMp);

            var result = DropRules.Take(drop, picker, PickupChoice.Restore, null, null, new System.Random(1), Rods);

            Assert.That(picker.CurrentHp, Is.EqualTo(picker.Stats.MaxHp));
            Assert.That(picker.CurrentMp, Is.EqualTo(picker.Stats.MaxMp));
            Assert.That(result.HpRestored, Is.EqualTo(picker.Stats.MaxHp - 1));
            Assert.That(result.Summary, Is.EqualTo("HP/MP restored"));
        }

        [Test]
        public void ChoosingToLearn_WithNothingToLearn_Restores()
        {
            var drop = DropRules.Create(1, NewUnit("Bare", Team.Enemy, _cadet), new System.Random(1), Rods);
            var picker = NewUnit("Pembroke", Team.Player, _cadet);

            Assert.That(DropRules.NeedsChoice(drop, picker, Rods), Is.False);
            var result = DropRules.Take(drop, picker, PickupChoice.Inherit, null, null, new System.Random(1), Rods);
            Assert.That(result.Choice, Is.EqualTo(PickupChoice.Restore));
        }

        // ------------------------------------------------------------------ Taking a chest

        [Test]
        public void ChestGear_GoesToTheTakersSpoils()
        {
            var drop = DropRules.Create(1, Veteran(), new System.Random(1), Chests);
            var picker = NewUnit("Pembroke", Team.Player, _cadet);
            var spoils = new Spoils();

            var result = DropRules.Take(drop, picker, PickupChoice.None, new Inventory(), spoils, new System.Random(1), Chests);

            Assert.That(spoils.Of(Team.Player), Is.EqualTo(new[] { drop.Gear }));
            Assert.That(spoils.Of(Team.Enemy), Is.Empty);
            Assert.That(result.Summary, Is.EqualTo($"Obtained: {drop.Gear.DisplayName}"));
            Assert.That(result.Message, Does.StartWith("Pembroke takes Halloway's treasure chest: Obtained:"));
        }

        [Test]
        public void ChestItems_GoIntoTheTakersStock()
        {
            var beast = NewUnit("Wolf", Team.Enemy, _beast);
            var drop = DropRules.Create(1, beast, new System.Random(1), Chests, Loot);
            var picker = NewUnit("Pembroke", Team.Player, _cadet);
            var stock = new Inventory();
            stock.Add(_tonic, 2);

            DropRules.Take(drop, picker, PickupChoice.None, stock, new Spoils(), new System.Random(1), Chests);

            Assert.That(stock.Count(_tonic), Is.EqualTo(3));
        }

        // ------------------------------------------------------------------ The AI

        [Test]
        public void TheAi_RestoresWhenHurt_AndLearnsWhenHealthy()
        {
            var drop = DropRules.Create(1, Veteran(), new System.Random(1), Rods);
            var picker = NewUnit("Brennan", Team.Enemy, _cadet);

            Assert.That(DropRules.AiChoice(drop, picker, Rods), Is.EqualTo(PickupChoice.Inherit));
            picker.TakeDamage(picker.CurrentHp / 2 + 1);
            Assert.That(DropRules.AiChoice(drop, picker, Rods), Is.EqualTo(PickupChoice.Restore));

            var nothing = DropRules.Create(2, NewUnit("Bare", Team.Enemy, _cadet), new System.Random(1), Rods);
            Assert.That(DropRules.AiChoice(nothing, NewUnit("Fresh", Team.Enemy, _cadet), Rods), Is.EqualTo(PickupChoice.Restore));
        }

        [Test]
        public void RodsAreWorthMoreToTheAi_TheMoreHurtItIs()
        {
            var drop = DropRules.Create(1, NewUnit("Bare", Team.Enemy, _cadet), new System.Random(1), Rods);
            var unit = NewUnit("Brennan", Team.Enemy, _cadet);
            double healthy = DropRules.AiValue(drop, unit, Rods);
            unit.TakeDamage(unit.CurrentHp - 1);

            Assert.That(DropRules.AiValue(drop, unit, Rods), Is.GreaterThan(healthy + 20));
            var chest = DropRules.Create(2, Veteran(), new System.Random(1), Chests);
            Assert.That(DropRules.AiValue(chest, unit, Chests), Is.EqualTo(DropRules.AiChestValue));
        }

        [Test]
        public void WithNothingToAttack_TheAiWalksOntoADrop()
        {
            var grid = TestGrids.Flat(12, 5);
            var ai = NewUnit("AI", Team.Enemy, _cadet, 1, 2);
            var player = NewUnit("P", Team.Player, _cadet, 11, 2);
            var drop = new GridCoord(1, 0);
            var hints = new AiHints(new HashSet<Unit>(), cautious: false, new Dictionary<GridCoord, double> { [drop] = 10.0 });

            var plan = AiPlanner.Plan(grid, ai, new[] { ai, player }, MovementRules.Default, new System.Random(1), hints);
            var without = AiPlanner.Plan(grid, ai, new[] { ai, player }, MovementRules.Default, new System.Random(1));

            Assert.That(plan.MoveBefore, Is.EqualTo(drop));
            Assert.That(without.MoveBefore, Is.Not.EqualTo(drop), "without the drop it closes in on the enemy");
        }

        [Test]
        public void TheAi_AttacksFromADropTileWhenItCan()
        {
            var grid = TestGrids.Flat(8, 8);
            var ai = NewUnit("AI", Team.Enemy, _cadet, 2, 2);
            ai.TryEquipItem(_sword);
            var player = NewUnit("P", Team.Player, _cadet, 5, 2);
            // Two tiles next to the player are equally good to attack from; the one with a drop wins.
            var drop = new GridCoord(5, 3);
            var hints = new AiHints(new HashSet<Unit>(), cautious: false, new Dictionary<GridCoord, double> { [drop] = 10.0 });

            var plan = AiPlanner.Plan(grid, ai, new[] { ai, player }, MovementRules.Default, new System.Random(1), hints);

            Assert.That(plan.Target, Is.SameAs(player));
            Assert.That(plan.MoveBefore, Is.EqualTo(drop));
        }

        // ------------------------------------------------------------------ Field and presentation

        [Test]
        public void TheField_FindsDropsByTileAndId_InTheOrderTheyAppeared()
        {
            var field = new DropField();
            var tile = new GridCoord(3, 3);
            var first = DropRules.Create(field.NextId(), Veteran(), new System.Random(1), Rods);
            var second = DropRules.Create(field.NextId(), Veteran(), new System.Random(2), Chests);
            field.Add(first);
            field.Add(second);

            Assert.That(first.Id, Is.EqualTo(1));
            Assert.That(second.Id, Is.EqualTo(2));
            Assert.That(field.At(first.Coord), Is.SameAs(first), "the oldest first when two share a tile");
            Assert.That(field.At(tile), Is.Null);
            Assert.That(field.Find(2), Is.SameAs(second));
            Assert.That(field.Remove(first), Is.True);
            Assert.That(field.At(second.Coord), Is.SameAs(second));
            field.Clear();
            Assert.That(field.All, Is.Empty);
            Assert.That(field.NextId(), Is.EqualTo(1), "ids restart each battle");
        }

        [Test]
        public void Pickups_ShowWhatWasTaken_AndSoundByKind()
        {
            var style = FeedbackStyle.Popup(PopupKind.Pickup, text: "Learned: Grit");
            Assert.That(style.Text, Is.EqualTo("Learned: Grit"));
            Assert.That((Color)style.Color, Is.EqualTo((Color)FeedbackStyle.PickupColor));
            Assert.That(FeedbackStyle.PickupSound(DropKind.SteelRod), Is.EqualTo(SoundCue.RodTaken));
            Assert.That(FeedbackStyle.PickupSound(DropKind.Chest), Is.EqualTo(SoundCue.ChestOpened));
        }

        [Test]
        public void Drops_FadeInAfterTheGhost_AndTheRodGlowBreathes()
        {
            Assert.That(FeedbackCurves.DropAppear(0f), Is.Zero);
            Assert.That(FeedbackCurves.DropAppear(FeedbackCurves.DropDelay), Is.Zero);
            Assert.That(FeedbackCurves.DropAppear(FeedbackCurves.DropDelay + FeedbackCurves.DropFadeIn / 2f), Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(FeedbackCurves.DropAppear(5f), Is.EqualTo(1f));
            for (float t = 0f; t < 4f; t += 0.05f)
            {
                Assert.That(FeedbackCurves.RodGlow(t), Is.InRange(0.45f, 1f));
                Assert.That(Mathf.Abs(FeedbackCurves.RodBob(t)), Is.LessThanOrEqualTo(0.06f));
            }
        }

        [Test]
        public void DropArt_HasTheExpectedShapes()
        {
            var rod = DropSprites.RodPixels();
            Assert.That(rod.Length, Is.EqualTo(6 * 24));
            Assert.That(rod[0].a, Is.EqualTo(255), "brass cap spans the full width");
            Assert.That(rod[5 * 6 + 0].a, Is.Zero, "the shaft is narrower than the caps");
            Assert.That(DropSprites.ChestPixels().All(p => p.a == 255), Is.True, "a solid box");
            var glow = DropSprites.GlowPixels();
            Assert.That(glow[14 * 28 + 14].a, Is.GreaterThan(glow[14 * 28 + 2].a), "brightest at the centre");
        }
    }
}
