using System.Collections.Generic;
using FriendshipWarriors.Audio;
using FriendshipWarriors.Battle;
using FriendshipWarriors.Feedback;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Status;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class FeedbackTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private Unit NewUnit()
        {
            var job = ScriptableObject.CreateInstance<JobDefinition>();
            job.Define("Job", "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), 4, 3, 5,
                new JobRequirement[0], Color.gray);
            _created.Add(job);
            // 30 HP, 10 MP.
            return new Unit("A", Team.Player, job, new RawStats(491520, 229376, 98304, 81920, 65536));
        }

        // ------------------------------------------------------------------ Unit changes

        [Test]
        public void Unit_RaisesHpAndMpChanges_InOrder()
        {
            var unit = NewUnit();
            var changes = new List<UnitChange>();
            unit.Changed += changes.Add;

            unit.TakeDamage(12);
            unit.RestoreHp(5);
            unit.TrySpendMp(4);
            unit.RestoreMp(3);

            Assert.That(changes, Is.EqualTo(new[]
            {
                new UnitChange(UnitChangeKind.HpLost, 12),
                new UnitChange(UnitChangeKind.HpRestored, 5),
                new UnitChange(UnitChangeKind.MpRestored, 3),
            }), "spending MP on an ability isn't shown");
        }

        [Test]
        public void Unit_ReportsWhatActuallyChanged_AndNothingForNoChange()
        {
            var unit = NewUnit();
            var changes = new List<UnitChange>();
            unit.Changed += changes.Add;

            unit.RestoreHp(10); // already full
            unit.TakeDamage(99);  // only 30 to lose
            unit.TakeDamage(5);   // already down
            unit.Revive(7);

            Assert.That(changes, Is.EqualTo(new[]
            {
                new UnitChange(UnitChangeKind.HpLost, 30),
                new UnitChange(UnitChangeKind.Revived, 7),
            }));
        }

        // ------------------------------------------------------------------ Popup look

        [Test]
        public void Popups_UseFftColours()
        {
            Assert.That(FeedbackStyle.Popup(PopupKind.Damage, 42).Text, Is.EqualTo("42"));
            Assert.That(FeedbackStyle.Popup(PopupKind.Damage, 42).Color, Is.EqualTo(FeedbackStyle.DamageColor));
            Assert.That(FeedbackStyle.Popup(PopupKind.Heal, 30).Color, Is.EqualTo(FeedbackStyle.HealColor));
            Assert.That(FeedbackStyle.Popup(PopupKind.MpRestore, 20).Color, Is.EqualTo(FeedbackStyle.MpColor));
            Assert.That(FeedbackStyle.Popup(PopupKind.MpRestore, 20).Text, Does.StartWith("20").And.Contains("MP"));
            Assert.That(FeedbackStyle.Popup(PopupKind.Miss).Text, Is.EqualTo("Miss"));
            Assert.That(FeedbackStyle.Popup(PopupKind.Guarded).Text, Is.EqualTo("Guarded"));
        }

        [Test]
        public void CriticalPopups_SayCriticalAndStandOut()
        {
            var style = FeedbackStyle.Popup(PopupKind.CriticalDamage, 57);
            Assert.That(style.Text, Does.Contain("Critical!").And.EndsWith("57"));
            Assert.That(style.Color, Is.EqualTo(FeedbackStyle.CriticalColor));
            Assert.That(style.Size, Is.GreaterThan(FeedbackStyle.Popup(PopupKind.Damage, 57).Size));
        }

        [Test]
        public void StatusPopups_NameTheStatus_ColouredByWhetherItHurts()
        {
            var poison = FeedbackStyle.Popup(PopupKind.StatusGained, status: StatusEffect.Poison);
            var regen = FeedbackStyle.Popup(PopupKind.StatusGained, status: StatusEffect.Regen);
            Assert.That(poison.Text, Is.EqualTo("Poison"));
            Assert.That(poison.Color, Is.EqualTo(FeedbackStyle.HarmfulStatusColor));
            Assert.That(regen.Text, Is.EqualTo("Regen"));
            Assert.That(regen.Color, Is.EqualTo(FeedbackStyle.HelpfulStatusColor));

            Assert.That(FeedbackStyle.Popup(PopupKind.StatusLifted, status: StatusEffect.Poison).Text, Is.EqualTo("Poison lifted"));
            Assert.That(FeedbackStyle.Popup(PopupKind.StatusLifted, status: StatusEffect.Haste).Text, Is.EqualTo("Haste wore off"));
            Assert.That(FeedbackStyle.Popup(PopupKind.StatusLifted, status: StatusEffect.Sleep).Text, Is.EqualTo("Awake"));
        }

        // ------------------------------------------------------------------ Sounds

        [TestCase(PopupKind.Damage, SoundCue.Hit)]
        [TestCase(PopupKind.CriticalDamage, SoundCue.CriticalHit)]
        [TestCase(PopupKind.Miss, SoundCue.Miss)]
        [TestCase(PopupKind.Guarded, SoundCue.Block)]
        [TestCase(PopupKind.Heal, SoundCue.Heal)]
        [TestCase(PopupKind.Revived, SoundCue.Revive)]
        [TestCase(PopupKind.StatusLifted, SoundCue.None)]
        public void Popups_PlayTheirSound(PopupKind kind, SoundCue cue)
        {
            Assert.That(FeedbackStyle.Sound(kind), Is.EqualTo(cue));
        }

        [TestCase(BattleCue.CastStarted, SoundCue.CastStart)]
        [TestCase(BattleCue.SpellResolved, SoundCue.SpellResolve)]
        [TestCase(BattleCue.TimedPressSuccess, SoundCue.TimedPressSuccess)]
        [TestCase(BattleCue.TimedPressFail, SoundCue.TimedPressFail)]
        [TestCase(BattleCue.MenuMove, SoundCue.MenuMove)]
        [TestCase(BattleCue.MenuConfirm, SoundCue.MenuConfirm)]
        [TestCase(BattleCue.MenuCancel, SoundCue.MenuCancel)]
        [TestCase(BattleCue.Miss, SoundCue.None)] // sounds with its popup instead
        public void BattleCues_PlayTheirSound(BattleCue cue, SoundCue sound)
        {
            Assert.That(FeedbackStyle.Sound(cue), Is.EqualTo(sound));
        }

        [Test]
        public void EveryCue_HasAPlaceholderBlip()
        {
            foreach (SoundCue cue in System.Enum.GetValues(typeof(SoundCue)))
            {
                var samples = PlaceholderBlips.Samples(cue);
                if (cue == SoundCue.None)
                {
                    Assert.That(samples, Is.Empty);
                    continue;
                }
                Assert.That(samples.Length, Is.GreaterThan(100), cue.ToString());
                foreach (float s in samples)
                    Assert.That(s, Is.InRange(-1f, 1f), cue.ToString());
            }
        }

        [Test]
        public void PlaceholderBlips_AreTheSameEveryTime()
        {
            Assert.That(PlaceholderBlips.Samples(SoundCue.Hit), Is.EqualTo(PlaceholderBlips.Samples(SoundCue.Hit)));
        }

        [Test]
        public void SoundCueSet_OnlyFindsCuesWithClips()
        {
            var set = ScriptableObject.CreateInstance<SoundCueSet>();
            _created.Add(set);
            var clip = AudioClip.Create("Test", 10, 1, 22050, false);
            _created.Add(clip);
            set.Define(new[]
            {
                new SoundCueSet.Entry { Cue = SoundCue.Hit, Clips = new[] { clip } },
                new SoundCueSet.Entry { Cue = SoundCue.Miss },
            }, usePlaceholderBlips: false, masterVolume: 1f);

            Assert.That(set.Find(SoundCue.Hit)?.Clips[0], Is.SameAs(clip));
            Assert.That(set.Find(SoundCue.Miss), Is.Null, "an entry without clips counts as missing");
            Assert.That(set.Find(SoundCue.Heal), Is.Null);
        }

        // ------------------------------------------------------------------ Motion and stagger

        [Test]
        public void Popups_BounceThenRestThenFade()
        {
            Assert.That(PopupMotion.Height(0f), Is.EqualTo(PopupMotion.RestHeight));
            Assert.That(PopupMotion.Height(0.08f), Is.GreaterThan(PopupMotion.RestHeight + 20f), "springs up");
            Assert.That(PopupMotion.Height(PopupMotion.BounceTime + 0.1f), Is.EqualTo(PopupMotion.RestHeight));

            float firstPeak = 0f, laterPeak = 0f;
            for (float t = 0f; t < PopupMotion.BounceTime; t += 0.005f)
            {
                if (t < PopupMotion.BounceTime * 0.4f)
                    firstPeak = Mathf.Max(firstPeak, PopupMotion.Height(t));
                else
                    laterPeak = Mathf.Max(laterPeak, PopupMotion.Height(t));
            }
            Assert.That(laterPeak, Is.LessThan(firstPeak), "each bounce is lower");

            Assert.That(PopupMotion.Alpha(0f), Is.EqualTo(1f));
            Assert.That(PopupMotion.Alpha(PopupMotion.Duration - PopupMotion.FadeTime), Is.EqualTo(1f));
            Assert.That(PopupMotion.Alpha(PopupMotion.Duration - 0.01f), Is.LessThan(0.1f));
            Assert.That(PopupMotion.IsFinished(PopupMotion.Duration), Is.True);
        }

        [Test]
        public void PopupsOnOneUnit_AreStaggeredAndStacked()
        {
            var schedule = new PopupSchedule();

            var first = schedule.Add(1, 10f);
            var second = schedule.Add(1, 10f);
            var third = schedule.Add(1, 10f);

            Assert.That(first, Is.EqualTo((10f, 0)));
            Assert.That(second.start, Is.EqualTo(10f + PopupSchedule.Stagger).Within(1e-4));
            Assert.That(second.row, Is.EqualTo(1), "the first is still showing");
            Assert.That(third.start, Is.EqualTo(10f + 2 * PopupSchedule.Stagger).Within(1e-4));
            Assert.That(third.row, Is.EqualTo(2));
        }

        [Test]
        public void PopupsOnDifferentUnits_ShowTogether()
        {
            var schedule = new PopupSchedule();

            Assert.That(schedule.Add(1, 5f), Is.EqualTo((5f, 0)));
            Assert.That(schedule.Add(2, 5f), Is.EqualTo((5f, 0)), "an area spell shows on everyone at once");
        }

        [Test]
        public void Rows_FreeUpOnceEarlierPopupsAreGone()
        {
            var schedule = new PopupSchedule();
            schedule.Add(1, 0f);

            var later = schedule.Add(1, PopupMotion.Duration + 0.5f);

            Assert.That(later, Is.EqualTo((PopupMotion.Duration + 0.5f, 0)));
        }

        // ------------------------------------------------------------------ Status changes

        [Test]
        public void StatusDiff_FindsGainedAndLifted_SkippingCharging()
        {
            var gained = new List<StatusEffect>();
            var lifted = new List<StatusEffect>();

            StatusDiff.Compare(new[] { StatusEffect.Poison, StatusEffect.Haste },
                new[] { StatusEffect.Haste, StatusEffect.Regen, StatusEffect.Charging }, knockedOutNow: false, gained, lifted);

            Assert.That(gained, Is.EqualTo(new[] { StatusEffect.Regen }));
            Assert.That(lifted, Is.EqualTo(new[] { StatusEffect.Poison }));
        }

        [Test]
        public void StatusDiff_DoesNotAnnounceStatusesAKnockOutCleared()
        {
            var gained = new List<StatusEffect>();
            var lifted = new List<StatusEffect>();

            StatusDiff.Compare(new[] { StatusEffect.Poison, StatusEffect.Protect }, new StatusEffect[0], knockedOutNow: true, gained, lifted);

            Assert.That(gained, Is.Empty);
            Assert.That(lifted, Is.Empty);
        }

        // ------------------------------------------------------------------ Hit reactions

        [Test]
        public void HitFlash_BlinksTwiceThenStops()
        {
            Assert.That(FeedbackCurves.Flash(0f), Is.GreaterThan(0.5f));
            Assert.That(FeedbackCurves.Flash(0.08f), Is.Zero, "off between the blinks");
            Assert.That(FeedbackCurves.Flash(0.12f), Is.GreaterThan(0.5f));
            Assert.That(FeedbackCurves.Flash(FeedbackCurves.FlashTime), Is.Zero);
            Assert.That(FeedbackCurves.Flash(-1f), Is.Zero);
        }

        [Test]
        public void ShakesAndGlows_DieOut()
        {
            Assert.That(FeedbackCurves.Shake(FeedbackCurves.ShakeTime), Is.Zero);
            Assert.That(FeedbackCurves.Glow(0f), Is.Zero);
            Assert.That(FeedbackCurves.Glow(FeedbackCurves.GlowTime / 2f), Is.GreaterThan(0.3f));
            Assert.That(FeedbackCurves.Glow(FeedbackCurves.GlowTime), Is.Zero);
            Assert.That(FeedbackCurves.CameraShake(0.3f, 0.28f, 0.1f), Is.EqualTo((0f, 0f)));
            Assert.That(FeedbackCurves.CameraShake(0.01f, 0.28f, 0f), Is.EqualTo((0f, 0f)), "strength 0 turns it off");
            var (x, y) = FeedbackCurves.CameraShake(0.02f, 0.28f, 0.1f);
            Assert.That(Mathf.Abs(x) + Mathf.Abs(y), Is.GreaterThan(0f));
        }

        [Test]
        public void Feedback_FreezesWhenTheBattleClockPauses()
        {
            // Every curve is read at clock.Now - start, so a paused clock holds each one where it was.
            var clock = new BattleClock();
            float start = clock.Now(0f);
            clock.Pause(0.3f);
            float heldHeight = PopupMotion.Height(clock.Now(0.3f) - start);

            Assert.That(PopupMotion.Height(clock.Now(5f) - start), Is.EqualTo(heldHeight));
            clock.Resume(5f);
            Assert.That(clock.Now(5.1f) - start, Is.EqualTo(0.4f).Within(1e-4), "carries on from where it paused");
        }
    }
}
