using static FourD.Tests.DeviceScenes;
using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// Fixed objects added and removed while the world runs (docs/frame/contract-data.md): a chair that is put on a desk, or
// taken away, is the same kind of change to the world as a bite out of a wall.
public class FixedEntityTests
{
    private static readonly SectionId I = new("I");
    private static readonly SectionId II = new("II");
    private static readonly EntityId PostId = new("post");

    // A 50 cm cube standing on the floor, centered at x, in a section: its top is .5 m up.
    private static FixedDefinition Post(string id, double x, SectionDefinition section, double halfX = .25) =>
        new(new(id), At(x, .25, 0, section.W), TestSource, section.Id, Shape(Part("part", new(halfX, .25, .25))));

    // The rod of the size norms resting at the height given, with its middle over the origin.
    private static TurnableDefinition RodAt(double y) => Rod() with { Pose = At(0, y, 0) };

    [Fact]
    public void AFixedObjectAddedAtRunTimeHasASliceInItsOwnSectionOnly()
    {
        var world = Scene(FloorOf(new(-.5, 1)));

        var added = Assert.IsType<AddEntityResult.Added>(world.AddEntity(Post("post", 0, SectionI)));

        Assert.Equal(I, added.Entity.Section);
        var slice = Assert.Single(world.SlicesIn(I), candidate => candidate.Entity == PostId);
        AssertRange(slice.Range(Axis3.X), -.25, .25);
        AssertRange(slice.Range(Axis3.Y), 0, .5);
        Assert.DoesNotContain(world.SlicesIn(II), candidate => candidate.Entity == PostId);
        Assert.Equal(new EntityAdded(PostId), world.StepEvents(.1)[0].Event);
    }

    [Fact]
    public void AFixedObjectAddedAtRunTimeSupportsWhatIsAboveItAndTheWorldChanges()
    {
        var actor = Actor();
        var world = Scene(FloorOf(new(-.5, 1)), actor, RodAt(1));
        var plan = world.AllowedPlan(new StandUp(actor.Id, RodId, II));

        Assert.IsType<AddEntityResult.Added>(world.AddEntity(Post("post", 0, SectionI)));

        // The rod, 6 cm thick, was a meter up; it falls onto the post, whose top is .5 m up, and stops with its middle .03 above it.
        var fell = Assert.IsType<EntityFell>(world.StepEvents(.1)[1].Event);
        Assert.Equal(RodId, fell.Entity);
        Assert.Equal(1, fell.FromY, 10);
        Assert.Equal(.53, fell.ToY, 10);
        Assert.Equal(FailureCode.StalePlan, Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(plan)).Failure.Code);
    }

    [Fact]
    public void ARemovedFixedObjectLeavesTheSlicesAndTheSupportAndItsIdStaysTaken()
    {
        var world = Scene(FloorOf(new(-.5, 1)), Post("post", 0, SectionI), RodAt(.53));
        Assert.Contains(world.SlicesIn(I), candidate => candidate.Entity == PostId);

        Assert.IsType<RemoveEntityResult.Removed>(world.RemoveEntity(PostId));

        Assert.DoesNotContain(world.SlicesIn(I), candidate => candidate.Entity == PostId);
        Assert.IsType<EntityReadResult.Missing>(world.ReadEntity(PostId));
        Assert.Empty(world.OccupancyHits(new OccupancyBox(At(0, .25, 0), new(.1, .1, .1, .1)), RodId));
        var events = world.StepEvents(.1);
        Assert.Equal(new EntityRemoved(PostId), events[0].Event);
        var fell = Assert.IsType<EntityFell>(events[1].Event);
        Assert.Equal(.53, fell.FromY, 10);
        Assert.Equal(.03, fell.ToY, 10);
        Assert.Equal(FailureCode.DuplicateId, Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(Post("post", 0, SectionI))).Failure.Code);
    }

    [Fact]
    public void AFixedObjectCannotBeAddedWhereSomethingIsOrWhenItIsNotValid()
    {
        var world = Scene(FloorOf(new(-.5, 1)), Post("post", 0, SectionI), RodAt(1));

        // Over the post by 25 cm, and over the rod, which hangs a meter up.
        var onPost = Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(Post("other", .25, SectionI)));
        Assert.Equal(FailureCode.TranslationBlocked, onPost.Failure.Code);
        Assert.Equal(PostId, Assert.Single(onPost.Failure.Contacts).Obstacle);
        var inRod = FixedAt("beam", At(0, 1, 0), new(.1, .1, .1));
        var onRod = Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(inRod));
        Assert.Equal(RodId, Assert.Single(onRod.Failure.Contacts).Obstacle);

        Assert.Equal(FailureCode.DuplicateId, Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(Post("post", 3, SectionI))).Failure.Code);
        Assert.Equal(FailureCode.InvalidInput, Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(FixedAt("flat", At(2, .25, 0), new(.25, 0, .25)))).Failure.Code);
        Assert.Equal(FailureCode.InvalidInput, Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(Post("misplaced", 2, SectionII) with { Pose = At(2, .25, 0, .1) })).Failure.Code);
        Assert.DoesNotContain(world.SlicesIn(I), candidate => candidate.Entity.Value is "other" or "beam" or "flat");
    }

    [Fact]
    public void AFixedObjectCannotBeAddedInTheWayOfARotationThatHasStarted()
    {
        var actor = Actor();
        var rod = Rod() with { Pose = At(0, .03, 0) };
        var world = Scene(FloorOf(new(-.5, 1)), actor, rod);
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(world.AllowedPlan(new StandUp(actor.Id, RodId, II))));

        // Stood up, the rod is a plate at x = -.22 that reaches W = .42; a post at that depth would stand in its way.
        var blocking = Post("post", -.22, SectionII, halfX: .05);
        var rejected = Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(blocking));
        Assert.Equal(FailureCode.TranslationBlocked, rejected.Failure.Code);
        Assert.Empty(rejected.Failure.Contacts);
        Assert.IsType<AddEntityResult.Added>(world.AddEntity(Post("clear", 2, SectionII)));
    }

    [Fact]
    public void ABuildingElementStillCannotBeRemoved()
    {
        var world = Scene(FloorOf(new(-.5, 1)));

        Assert.Equal(FailureCode.WrongEntityKind, Assert.IsType<RemoveEntityResult.Rejected>(world.RemoveEntity(Floor)).Failure.Code);
        Assert.Equal(FailureCode.UnknownId, Assert.IsType<RemoveEntityResult.Rejected>(world.RemoveEntity(PostId)).Failure.Code);
        Assert.Contains(world.SlicesIn(I), candidate => candidate.Entity == Floor);
    }

    private static FixedDefinition FixedAt(string id, Pose4 pose, Vec3 half) =>
        new(new(id), pose, TestSource, I, Shape(Part("part", half)));
}
