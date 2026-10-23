using System.Collections.Generic;
using FriendshipWarriors.Battle;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.View.PaperDoll;
using FriendshipWarriors.Units.View.Walking;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class WalkPathTests
    {
        private static readonly WalkSettings Settings = WalkSettings.Default;
        private const float H = GridMetrics.StepHeight * GridMetrics.StepsPerH; // world units per h

        private static WalkPath Route(BattleGrid grid, GridCoord origin, params GridCoord[] path) =>
            WalkPath.ForRoute(grid, origin, new List<GridCoord>(path), Settings);

        [Test]
        public void FlatWalk_TakesOneStepPerTile_AndEndsOnTheDestination()
        {
            var grid = TestGrids.Flat(5, 1, heightInH: 1);
            var walk = Route(grid, new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(3, 0));

            Assert.That(walk.Segments, Has.Count.EqualTo(3));
            Assert.That(walk.Segments, Has.All.Matches<WalkSegment>(s => s.Motion == WalkMotion.Walk));
            Assert.That(walk.Duration, Is.EqualTo(3f / Settings.TilesPerSecond).Within(1e-4f));
            Assert.That(walk.End, Is.EqualTo(GridMetrics.SurfaceCenter(new GridCoord(3, 0), grid[new GridCoord(3, 0)])));
            Assert.That(walk.PositionAt(walk.Duration * 0.5f).x, Is.EqualTo(1.5f).Within(1e-4f), "constant speed");
            Assert.That(walk.PositionAt(walk.Duration * 0.5f).y, Is.EqualTo(H).Within(1e-4f), "on the surface");
        }

        [Test]
        public void EachStep_FacesTheWayItGoes()
        {
            var grid = TestGrids.Flat(3, 3);
            var walk = Route(grid, new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(1, 1), new GridCoord(0, 1));

            Assert.That(walk.Segments[0].Facing, Is.EqualTo(Direction.East));
            Assert.That(walk.Segments[1].Facing, Is.EqualTo(Direction.North));
            Assert.That(walk.Segments[2].Facing, Is.EqualTo(Direction.West));
        }

        [Test]
        public void Climbing_HopsAboveTheHigherTile_AndTakesLongerPerH()
        {
            var grid = TestGrids.FromRows("13");
            var hop = Route(grid, new GridCoord(0, 0), new GridCoord(1, 0)).Segments[0];

            Assert.That(hop.Motion, Is.EqualTo(WalkMotion.Hop));
            Assert.That(hop.Sample(0.5f).y, Is.EqualTo(3 * H + Settings.HopHeight).Within(1e-4f));
            Assert.That(hop.Duration, Is.EqualTo(Settings.StepSeconds * (1f + Settings.HopTimePerH * 2f)).Within(1e-4f));
            Assert.That(hop.Sample(0f).y, Is.EqualTo(1 * H).Within(1e-4f));
            Assert.That(hop.Sample(1f).y, Is.EqualTo(3 * H).Within(1e-4f));
        }

        [Test]
        public void Dropping_ArcsDownTheLedge()
        {
            var grid = TestGrids.FromRows("40");
            var drop = Route(grid, new GridCoord(0, 0), new GridCoord(1, 0)).Segments[0];

            Assert.That(drop.Motion, Is.EqualTo(WalkMotion.Hop));
            Assert.That(drop.Sample(0.5f).y, Is.GreaterThan(4 * H), "hops up off the ledge before falling");
            Assert.That(drop.Sample(0.9f).y, Is.LessThan(4 * H));
        }

        [Test]
        public void Slopes_AreWalkedAlongTheSurface()
        {
            // Flat 1h, then a ramp rising east from 1h to 2h, then flat 2h: the edges meet all the way.
            var grid = new BattleGrid(3, 1);
            grid[new GridCoord(0, 0)] = new Tile(2, TerrainType.Grass);
            grid[new GridCoord(1, 0)] = new Tile(2, TerrainType.Grass, slope: SlopeType.RiseEast, slopeHeight: 2);
            grid[new GridCoord(2, 0)] = new Tile(4, TerrainType.Grass);
            var walk = Route(grid, new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0));

            Assert.That(walk.Segments[0].Motion, Is.EqualTo(WalkMotion.Walk));
            Assert.That(walk.Segments[1].Motion, Is.EqualTo(WalkMotion.Walk));
            Assert.That(walk.Segments[0].Edge.y, Is.EqualTo(1 * H).Within(1e-4f), "bottom of the ramp");
            Assert.That(walk.Segments[1].Edge.y, Is.EqualTo(2 * H).Within(1e-4f), "top of the ramp");
            Assert.That(walk.Segments[1].Sample(0f).y, Is.EqualTo(1.5f * H).Within(1e-4f), "the ramp's middle");
        }

        [Test]
        public void SteppingOntoTheHighSideOfASlope_Hops()
        {
            // A ramp rising west: its east edge is low (1h) while the next tile is 2h, so the edges don't meet.
            var grid = new BattleGrid(2, 1);
            grid[new GridCoord(0, 0)] = new Tile(2, TerrainType.Grass, slope: SlopeType.RiseWest, slopeHeight: 2);
            grid[new GridCoord(1, 0)] = new Tile(4, TerrainType.Grass);

            Assert.That(Route(grid, new GridCoord(0, 0), new GridCoord(1, 0)).Segments[0].Motion, Is.EqualTo(WalkMotion.Hop));
        }

        [Test]
        public void Leaps_ArcOverTheGap_AndTakeAStepPerTileCrossed()
        {
            var grid = TestGrids.FromRows("2..2");
            var leap = Route(grid, new GridCoord(0, 0), new GridCoord(3, 0)).Segments[0];

            Assert.That(leap.Motion, Is.EqualTo(WalkMotion.Leap));
            Assert.That(leap.Facing, Is.EqualTo(Direction.East));
            Assert.That(leap.Duration, Is.EqualTo(Settings.StepSeconds * 3f).Within(1e-4f));
            Assert.That(leap.Sample(0.5f).y, Is.EqualTo(2 * H + Settings.LeapHeight).Within(1e-4f));
            for (float t = 0.05f; t < 1f; t += 0.05f)
                Assert.That(leap.Sample(t).y, Is.GreaterThan(2 * H), "stays above the take-off height over the gap");
        }

        [Test]
        public void LeapingOverALowerTile_ClearsTheHighestTileInBetween()
        {
            // 3h take-off over a 1h tile (where an enemy would stand) to 3h: the arc tops out over the take-off height.
            var grid = TestGrids.FromRows("313");
            var leap = Route(grid, new GridCoord(0, 0), new GridCoord(2, 0)).Segments[0];

            Assert.That(leap.Motion, Is.EqualTo(WalkMotion.Leap));
            Assert.That(leap.Sample(0.5f).y, Is.EqualTo(3 * H + Settings.LeapHeight).Within(1e-4f));
        }

        [Test]
        public void Pushes_KeepFacing_SlideOnLevelGround_AndDropOffLedges()
        {
            var grid = TestGrids.FromRows("220");
            var slide = WalkPath.ForPush(grid, new GridCoord(0, 0), new GridCoord(1, 0), Direction.West, Settings).Segments[0];
            var fall = WalkPath.ForPush(grid, new GridCoord(1, 0), new GridCoord(2, 0), Direction.West, Settings).Segments[0];

            Assert.That(slide.Motion, Is.EqualTo(WalkMotion.Pushed));
            Assert.That(slide.TurnsUnit, Is.False);
            Assert.That(slide.Sample(0.5f).y, Is.EqualTo(2 * H).Within(1e-4f), "a level push slides");
            Assert.That(slide.Duration, Is.EqualTo(Settings.PushSeconds).Within(1e-4f));
            Assert.That(fall.Sample(1f).y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(fall.Duration, Is.GreaterThan(Settings.PushSeconds));
        }

        [Test]
        public void Playback_AdvancesByBattleTime_AndFreezesWhilePaused()
        {
            var grid = TestGrids.Flat(4, 1);
            var playback = new WalkPlayback(Route(grid, new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0)));
            var clock = new BattleClock();
            float last = clock.Now(0f);

            void Frame(float realTime)
            {
                float now = clock.Now(realTime);
                playback.Advance(now - last);
                last = now;
            }

            Frame(0.1f);
            var before = playback.Position;
            clock.Pause(0.1f);
            Frame(5f);
            Assert.That(playback.Position, Is.EqualTo(before), "paused mid-step");
            Assert.That(playback.IsFinished, Is.False);

            clock.Resume(5f);
            Frame(5.1f);
            Assert.That(playback.Elapsed, Is.EqualTo(0.2f).Within(1e-4f), "carries on where it was");
            Frame(10f);
            Assert.That(playback.IsFinished, Is.True);
            Assert.That(playback.Position, Is.EqualTo(playback.Path.End));
        }

        [Test]
        public void Hurrying_PlaysTheWalkFaster()
        {
            var grid = TestGrids.Flat(4, 1);
            var playback = new WalkPlayback(Route(grid, new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(3, 0)));

            playback.Advance(playback.Path.Duration / Settings.HurryMultiplier, Settings.HurryMultiplier);

            Assert.That(playback.IsFinished, Is.True);
        }

        [Test]
        public void WalkFrames_FollowWalkTime_AndHoldTheStrideInTheAir()
        {
            var grid = TestGrids.FromRows("1113");
            var playback = new WalkPlayback(Route(grid, new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(3, 0)));
            float frame = PaperDollLayout.FrameDuration(DollAnimation.Walk);

            Assert.That(playback.WalkFrame, Is.EqualTo(0));
            playback.Advance(frame * 1.5f);
            Assert.That(playback.WalkFrame, Is.EqualTo(1));
            playback.Advance(Settings.StepSeconds * 2f - frame * 1.5f + 0.01f); // into the hop up to 3h
            Assert.That(playback.Current.Motion, Is.EqualTo(WalkMotion.Hop));
            Assert.That(playback.WalkFrame, Is.EqualTo(1));

            var push = new WalkPlayback(WalkPath.ForPush(grid, new GridCoord(0, 0), new GridCoord(1, 0), Direction.East, Settings));
            Assert.That(push.WalkFrame, Is.EqualTo(-1), "pushed units don't walk");
        }

        [Test]
        public void Animator_ShowsTheWalkFrame_UnlessAOneShotIsPlaying()
        {
            var animator = new DollAnimator();
            Assert.That(animator.Current(0f, false, false, walkFrame: 2), Is.EqualTo((DollAnimation.Walk, 2)));
            animator.Play(DollAnimation.Hurt, 0f);
            Assert.That(animator.Current(0.01f, false, false, walkFrame: 2).animation, Is.EqualTo(DollAnimation.Hurt));
            Assert.That(animator.Current(0.01f, knockedOut: true, false, walkFrame: 2).animation, Is.EqualTo(DollAnimation.KnockedOut));
        }
    }
}
