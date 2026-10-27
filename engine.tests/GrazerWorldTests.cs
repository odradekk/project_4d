using static FourD.Tests.Fixtures;
using static FourD.Tests.GrazerScenes;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// How a grazer sits in the world: it blocks and supports nothing, it can be added, removed and read like the other
// dynamic entities, and the host's frames are checked and counted as changes of the world.
public class GrazerWorldTests
{
    private static readonly SectionId I = new("I");
    private static readonly Rotation4 Skewed = new(new(1, .001, 0, 0), Identity4.Y, Identity4.Z, Identity4.W);

    [Fact]
    public void AGrazerBlocksNothingAndSupportsNothing()
    {
        // The lump is 3 m in radius: it takes in the floor, the ball and the box that is queried.
        var world = GrazerScene(At(0, .5, 0, w: .21), Lump(default), FloorOf(new(-.5, 1)), Ball(y: 1));

        var hits = world.OccupancyHits(new OccupancyBox(At(0, .78, 0, w: .21), new(.05, .05, .05, .05)));
        Assert.Equal([BallId], hits.Select(hit => hit.Entity));
        Assert.IsType<SupportResult.Unsupported>(world.FindSupport(GrazerId));

        var fell = Assert.IsType<EntityFell>(Assert.Single(world.StepEvents(.1)).Event);
        Assert.Equal(BallId, fell.Entity);
        Assert.Equal(BallRadius, fell.ToY, 10);
    }

    [Fact]
    public void AGrazerMayBeFramedInsideTheScene()
    {
        var world = GrazerScene(At(0, 1, 0), Lump(default), FloorOf(new(-.5, 1)));

        world.Frame(At(0, 0, 0, w: .21), Lump(default));

        var hits = world.OccupancyHits(new OccupancyBox(At(0, -.05, 0, w: .21), new(.1, .04, .1, .1)));
        Assert.Equal([Floor], hits.Select(hit => hit.Entity));
        Assert.NotNull(world.SliceOf(GrazerId, I));
    }

    [Fact]
    public void AGrazerReadsWithItsCurrentPoseAndNoSectionOrHandedness()
    {
        var definition = Grazer(At(0, 1, 0, w: .18), Lump(default));
        var world = Load(Description([definition]));

        var loaded = world.State(GrazerId);
        Assert.Equal(definition.Shape.Balls, Assert.IsType<GrazerDefinition>(loaded.Definition).Shape.Balls);
        Assert.Equal(definition.Pose, loaded.Pose);
        Assert.Null(loaded.Section);
        Assert.Equal(Handedness.NotComparable, loaded.Handedness);

        world.Frame(At(1, 1, 0, w: .3), Lump(default));

        var moved = world.State(GrazerId);
        Assert.Equal(definition.Pose, moved.Definition.Pose);
        Assert.Equal(At(1, 1, 0, w: .3), moved.Pose);
    }

    [Fact]
    public void TheWorldKeepsItsOwnCopyOfTheBalls()
    {
        var balls = new[] { LumpBall(default) };
        var world = GrazerScene(At(0, 1, 0, w: .18), new(balls, LumpThreshold));
        var loaded = world.SliceOf(GrazerId, I)!.Positions;
        balls[0] = LumpBall(default, radius: 50);

        Assert.Equal(loaded, world.SliceOf(GrazerId, I)!.Positions);

        balls[0] = LumpBall(default);
        world.Frame(At(0, 1, 0, w: .18), new(balls, LumpThreshold));
        var framed = world.SliceOf(GrazerId, I)!.Positions;
        balls[0] = LumpBall(default, radius: 50);

        Assert.Equal(framed, world.SliceOf(GrazerId, I)!.Positions);
    }

    [Fact]
    public void AGrazerCanBeAddedAndRemovedWhileTheWorldRuns()
    {
        var world = Scene();

        var added = Assert.IsType<AddEntityResult.Added>(world.AddEntity(Grazer(At(0, 1, 0, w: .18), Lump(default))));
        Assert.Null(added.Entity.Section);
        Assert.NotNull(world.SliceOf(GrazerId, I));
        Assert.Equal(new EntityAdded(GrazerId), Assert.Single(world.StepEvents(.1)).Event);

        var duplicate = Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(Grazer(At(0, 1, 0), Lump(default))));
        Assert.Equal(FailureCode.DuplicateId, duplicate.Failure.Code);

        Assert.IsType<RemoveEntityResult.Removed>(world.RemoveEntity(GrazerId));
        Assert.Null(world.SliceOf(GrazerId, I));
        Assert.IsType<EntityReadResult.Missing>(world.ReadEntity(GrazerId));
        Assert.Equal(new EntityRemoved(GrazerId), Assert.Single(world.StepEvents(.1)).Event);
    }

    [Fact]
    public void AnInvalidGrazerIsNotAdded()
    {
        var world = Scene();

        foreach (var invalid in new[] { Grazer(At(0, 1, 0), new([], LumpThreshold)), Grazer(At(0, 1, 0), Lump(default)) with { Id = new("") } })
        {
            var rejected = Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(invalid));
            Assert.Equal(FailureCode.InvalidInput, rejected.Failure.Code);
        }

        Assert.IsType<EntityReadResult.Missing>(world.ReadEntity(GrazerId));
        Assert.Empty(world.StepEvents(.1));
    }

    [Fact]
    public void AFrameForAnythingButAGrazerIsRefused()
    {
        var world = GrazerScene(At(0, 1, 0), Lump(default), FloorOf(new(-.5, 1)), Ball());

        Assert.Equal(FailureCode.UnknownId, world.SetGrazerFrame(new("none"), Origin, Lump(default)).Refused().Code);
        Assert.Equal(FailureCode.WrongEntityKind, world.SetGrazerFrame(Floor, Origin, Lump(default)).Refused().Code);
        Assert.Equal(FailureCode.WrongEntityKind, world.SetGrazerFrame(BallId, Origin, Lump(default)).Refused().Code);
    }

    [Fact]
    public void AnInvalidFrameIsRefusedAndChangesNothing()
    {
        var world = GrazerScene(At(0, 1, 0, w: .18), Lump(default), Rod(), Actor(x: .1));
        var before = world.SliceOf(GrazerId, I)!.Positions;
        var plan = world.AllowedPlan(new StandUp(new("actor"), RodId, new("II")));

        var emptyBall = new MetaballShape([Ball("lump", default, radius: 0)], LumpThreshold);
        foreach (var (pose, shape) in new (Pose4, MetaballShape?)[]
        {
            (At(5, 1, 0), emptyBall), (At(5, 1, 0), null), (At(5, 1, 0, rotation: Skewed), Lump(default)),
            (At(double.NaN, 1, 0), Lump(default))
        })
            Assert.Equal(FailureCode.InvalidInput, world.SetGrazerFrame(GrazerId, pose, shape!).Refused().Code);

        Assert.Equal(before, world.SliceOf(GrazerId, I)!.Positions);
        Assert.Equal(At(0, 1, 0, w: .18), world.State(GrazerId).Pose);
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan));
    }

    [Fact]
    public void AFrameDoesNotMakeARotationPlanStale()
    {
        // A grazer blocks nothing, so the plan a player made is as good after the grazer moves.
        var world = GrazerScene(At(0, 1, 0, w: .18), Lump(default), Rod(), Actor(x: .1));
        var plan = world.AllowedPlan(new StandUp(new("actor"), RodId, new("II")));

        world.Frame(At(1, 1, 0, w: .4), Lump(default));
        world.Frame(At(2, 1, 0, w: .6), new([LumpBall(default, radius: 6)], LumpThreshold));

        Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan));
    }
}
