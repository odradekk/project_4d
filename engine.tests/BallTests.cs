using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// The four-dimensional ball of the size norms (docs/frame/rules.md): radius .25 m, center .21 m deep, between
// sections I (W = 0) and II (W = .42). Expected values are worked out by hand from the ball's formulas.
public class BallTests
{
    // The radius of the ball's slice .21 m from its center: the square root of .25 squared less .21 squared.
    private const double SliceRadius = .1356466;

    private static World Resting(params EntityDefinition[] more) =>
        Scene([FloorOf(new(-.5, 1)), Ball(y: 2), Actor(), .. more]);

    [Fact]
    public void BallSliceInEachSectionIsASphereOfTheSizeNormsRadius()
    {
        var world = Resting();

        foreach (var section in new[] { new SectionId("I"), new SectionId("II") })
        {
            var slice = world.SliceOf(BallId, section)!;
            var (shortest, longest) = DistancesFrom(slice, new(0, 2, 0));
            Assert.Equal(SliceRadius, shortest, 7);
            Assert.Equal(SliceRadius, longest, 7);
            Assert.True(slice.ThroughObject);
            Assert.All(Enumerable.Range(0, slice.Parts.Count / 3).Select(t => 3 * t), first => Assert.True(slice.IsWoundOutward(first)));
            // 64 meridians and 32 parallels make 2048 quads of two triangles, less the 128 that collapse at the poles.
            Assert.Equal(64 * 32 * 2 - 2 * 64, slice.Parts.Count / 3);
        }
    }

    [Fact]
    public void BallSliceGrowsToTheFullRadiusAtItsCenterAndVanishesBeyondItsPoles()
    {
        var world = Resting();

        var atCenter = world.SlicesAt(BallW).Single(slice => slice.Entity == BallId);
        Assert.Equal(BallRadius, DistancesFrom(atCenter, new(0, 2, 0)).Max, 9);
        Assert.DoesNotContain(world.SlicesAt(BallW + BallRadius), slice => slice.Entity == BallId);
        Assert.DoesNotContain(world.SlicesAt(BallW - BallRadius - .01), slice => slice.Entity == BallId);
    }

    [Fact]
    public void BallRestsOnTheFloorWithItsCenterOneRadiusUpAndHangsAboveIt()
    {
        var world = Resting();

        var support = Assert.IsType<SupportResult.Supported>(world.FindSupport(BallId));
        Assert.Equal(BallRadius, support.RestingOriginY, 10);
        var contact = Assert.Single(support.Contacts);
        Assert.Equal(Floor, contact.Support);
        AssertNear(new(0, 0, 0, BallW), contact.Point);
        Assert.Equal(1, contact.Normal.Y, 10);

        var fell = Assert.IsType<EntityFell>(Assert.Single(world.StepEvents(.1)).Event);
        Assert.Equal(BallRadius, fell.ToY, 10);
        foreach (var section in new[] { new SectionId("I"), new SectionId("II") })
        {
            var bottom = world.SliceOf(BallId, section)!.Coordinates(Axis3.Y).Min();
            Assert.InRange(bottom, .1143, .1144);
        }
    }

    [Fact]
    public void BallStaysOnTheFloorEvenWhereSectionIIHasAHole()
    {
        var hole = new Interval(.3, .6);
        var world = Scene(FloorOf(new(-.5, 1), hole), Ball(y: 2));

        var support = Assert.IsType<SupportResult.Supported>(world.FindSupport(BallId));
        Assert.Equal(BallRadius, support.RestingOriginY, 10);
        Assert.False(world.HasSliceVolume(Floor, new("II")));
        Assert.True(world.HasSliceVolume(BallId, new("II")));
    }

    [Fact]
    public void BallCenteredOverAHoleRestsOnTheHolesEdgeFace()
    {
        // The hole spans W = .3 to .6 and the ball's center is at W = .42, so .12 from the nearer edge, whose face
        // touches the ball at x = z = 0: the center is the square root of .25 squared less .12 squared above it.
        var world = Scene(FloorOf(new(-.5, 1), new Interval(.3, .6)), Ball(y: 2, w: .42));

        var support = Assert.IsType<SupportResult.Supported>(world.FindSupport(BallId));
        Assert.Equal(.2193171219946, support.RestingOriginY, 10);
        var contact = Assert.Single(support.Contacts);
        AssertNear(new(0, 0, 0, .3), contact.Point);
    }

    [Fact]
    public void BallIsHeldUpByAFloorThatOnlyExistsInAnotherSection()
    {
        // The floor starts at W = .38, .17 from the ball's center, and has no slice in section I.
        var world = Scene(FloorOf(new(.38, .6)), Ball(y: 2));

        var support = Assert.IsType<SupportResult.Supported>(world.FindSupport(BallId));
        Assert.Equal(.183303027798, support.RestingOriginY, 10);
        Assert.Equal(.38, Assert.Single(support.Contacts).Point.W, 10);
        Assert.False(world.HasSliceVolume(Floor, new("I")));
    }

    [Fact]
    public void TurningABallInAnyWayLeavesItWhereItIsAndLooksTheSame()
    {
        var actor = Actor();
        var world = Scene(FloorOf(new(-.5, 1)), Ball(), actor);
        var before = world.SlicesIn(new("II")).Single(slice => slice.Entity == BallId);

        world.Stand(actor.Id, BallId, new("II"));

        var state = world.State(BallId);
        AssertNear(new(0, BallRadius, 0, BallW), state.Pose.Position);
        Assert.Equal(0, state.Pose.Rotation.X.X, 10);
        Assert.Equal(1, state.Pose.Rotation.X.W, 10);
        Assert.Null(state.Section);
        Assert.Null(state.UprightOrigin);
        Assert.Equal(before.Positions, world.SlicesIn(new("II")).Single(slice => slice.Entity == BallId).Positions);

        var flip = world.AllowedPlan(new Flip(actor.Id, BallId));
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(flip));
        world.StepEvents(1.2);
        AssertNear(new(0, BallRadius, 0, BallW), world.State(BallId).Pose.Position);
        Assert.Equal(before.Positions, world.SlicesIn(new("II")).Single(slice => slice.Entity == BallId).Positions);

        var layDown = Assert.IsType<RotationPlanResult.Rejected>(world.PlanRotation(new LayDown(actor.Id, BallId)));
        Assert.Equal(FailureCode.InvalidInput, layDown.Failure.Code);
    }

    [Fact]
    public void BallPreviewOutlinesTheBallAsACircleAtItsCenterHeight()
    {
        var actor = Actor();
        var world = Scene(FloorOf(new(-.5, 1)), Ball(), actor);
        var plan = world.AllowedPlan(new StandUp(actor.Id, BallId, new("II")));

        var preview = Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan, new("II"))).Value;

        var points = preview.Samples[^1].Contours.SelectMany(line => Enumerable.Range(0, line.Positions.Count / 3).Select(line.Positions.Vertex)).ToArray();
        Assert.NotEmpty(points);
        Assert.All(points, point =>
        {
            Assert.Equal(BallRadius, point.Y, 9);
            Assert.Equal(SliceRadius, Math.Sqrt(point.X * point.X + point.Z * point.Z), 4);
        });
    }

    [Fact]
    public void BallTurnsAboutItsCenterWhateverHingeItIsGiven()
    {
        var actor = Actor();
        var offCenter = Ball(y: BallRadius) with { Interaction = BallProperties() with { Hinge = new(.1, 0, 0) } };
        var world = Scene(FloorOf(new(-.5, 1)), offCenter, actor);

        world.Stand(actor.Id, BallId, new("II"));

        AssertNear(new(0, BallRadius, 0, BallW), world.State(BallId).Pose.Position);
    }

    [Fact]
    public void FlippingABallAroundAWallLeavesItInPlace()
    {
        // The wall between the player and the ball is the flip plane of anything else, which would swing across it.
        var wall = new FixedDefinition(new("wall"), At(-.5, 0, 0), TestSource, new("I"), Shape(Part("part", new(.02, 1, 1))));
        var actor = Actor();
        var world = Scene(FloorOf(new(-.5, 1)), wall, Ball(), actor);

        var plan = world.AllowedPlan(new Flip(actor.Id, BallId));
        var started = Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan));
        world.StepEvents(started.Duration);

        Assert.Equal(1.4, started.Duration);
        AssertNear(new(0, BallRadius, 0, BallW), world.State(BallId).Pose.Position);
    }

    [Theory]
    [InlineData(.246, true)] // 4 mm into the floor: within the contact tolerance, so it rests where it is
    [InlineData(.244, false)] // 6 mm in: deeper than the tolerance, so nothing holds it
    public void BallEmbeddedInTheFloorRestsOnlyWithinTheContactTolerance(double y, bool resting)
    {
        var world = Scene(FloorOf(new(-.5, 1)), Ball(y: y));

        var support = world.FindSupport(BallId);

        if (resting) Assert.Equal(y, Assert.IsType<SupportResult.Supported>(support).RestingOriginY, 10);
        else Assert.IsType<SupportResult.Unsupported>(support);
    }

    [Fact]
    public void WallAgainstTheBallsSideDoesNotHoldItUp()
    {
        // The wall's left face is at x = .25, tangent to the ball at x = 0 with its radius of .25.
        var wall = Block("wall", new(.3, .25, 0), new(.05, 2, 2), new(-.5, 1));

        var withFloor = Scene(FloorOf(new(-.5, 1)), wall, Ball());
        var contact = Assert.Single(Assert.IsType<SupportResult.Supported>(withFloor.FindSupport(BallId)).Contacts);
        Assert.Equal(Floor, contact.Support);

        var wallOnly = Scene(wall, Ball());
        Assert.IsType<SupportResult.Unsupported>(wallOnly.FindSupport(BallId));
    }

    [Fact]
    public void BallHasNoHandedness()
    {
        var world = Resting();

        var handedness = Assert.IsType<HandednessResult.Value>(world.ReadHandedness(BallId));
        Assert.Equal(Handedness.NotComparable, handedness.Handedness);
    }

    [Fact]
    public void BallAddedAndRemovedAtRunTimeAppearsAndDisappearsInBothSections()
    {
        var world = Scene(FloorOf(new(-.5, 1)));

        var added = Assert.IsType<AddEntityResult.Added>(world.AddEntity(Ball()));
        Assert.Null(added.Entity.Section);
        Assert.NotNull(world.SliceOf(BallId, new("I")));
        Assert.NotNull(world.SliceOf(BallId, new("II")));

        Assert.IsType<RemoveEntityResult.Removed>(world.RemoveEntity(BallId));
        Assert.Null(world.SliceOf(BallId, new("I")));
        Assert.Null(world.SliceOf(BallId, new("II")));
    }

    [Fact]
    public void RodCannotBeStoodUpThroughABall()
    {
        // The rod swings toward section II at x = -.22; the ball sits right in the way, .3 deep and .12 beside it.
        var actor = Actor();
        var world = Scene(Rod(), Ball(x: -.1, y: 0, w: .3), actor);

        var blocked = Assert.IsType<RotationPlanResult.Blocked>(world.PlanRotation(new StandUp(actor.Id, new("rod"), new("II"))));

        Assert.Equal(BallId, blocked.Failure.Contacts[0].Obstacle);
    }

    [Fact]
    public void BallOverlappingTheFloorCannotBePlacedThere()
    {
        var world = Resting();

        var rejected = Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(BallId, At(0, .2, 0, BallW)));

        Assert.Equal(FailureCode.TranslationBlocked, rejected.Failure.Code);
        Assert.Equal(Floor, Assert.Single(rejected.Failure.Contacts).Obstacle);
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(BallId, At(0, .6, 0, .3)));
    }

    [Fact]
    public void OccupancyQueryFindsABallOnlyWhereItsSliceIs()
    {
        var world = Resting();

        var inside = new OccupancySphere(new(0, 2, 0, BallW), .05);
        var beyondPole = new OccupancySphere(new(0, 2, 0, BallW + BallRadius + .06), .05);
        Assert.Equal(BallId, Assert.Single(world.OccupancyHits(inside)).Entity);
        Assert.Empty(world.OccupancyHits(beyondPole));
        var box = new OccupancyBox(At(0, 2, 0, BallW), new(.01, .01, .01, .01));
        Assert.Equal(new PartId("ball"), Assert.Single(world.OccupancyHits(box)).Part);
    }
}
