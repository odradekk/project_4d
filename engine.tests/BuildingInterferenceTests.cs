using static FourD.Tests.Fixtures;

namespace FourD.Tests;

public class BuildingInterferenceTests
{
    // A 1 m rod in building "owner", hinged 2 cm inside its left end at the bottom face.
    private static TurnableDefinition Item() => new(
        new("item"), Origin, TestSource, new("I"), new("owner"), Box(new(.5, .1, .1)),
        new(Holdable: true, Packable: false, HorizontalRadius: .5, LongAxis: Axis3.X, Hinge: new(-.48, -.1, 0), Reach: .98));

    // A 20 cm cube wall in building "other" at x, present for the W range.
    private static BuildingElementDefinition Other(double x, Interval range, Interval[]? gaps = null) =>
        new(new("wall"), At(x, 0, 0), TestSource, new("other"), range, gaps ?? [], Box(new(.1, .1, .1)));

    private static WorldDescription Description(params EntityDefinition[] entities) => new(
        [new(new("I"), 0, TestSource)],
        [new(new("owner"), new(-2, 2), TestSource), new(new("other"), new(-2, 2), TestSource)],
        entities, [], [], []);

    [Theory]
    [InlineData(.5, 1.5)]
    [InlineData(-1.5, -.5)]
    public void UprightObjectInterferingWithOtherBuildingsSolidIsRejected(double min, double max)
    {
        var errors = LoadErrors(Description(Item(), Other(-.48, new(min, max))));

        var error = Assert.Single(errors, e => e.Code == LoadErrorCode.BuildingInterference);
        Assert.Equal(new EntityId("item"), error.Entity);
        Assert.EndsWith(".Building", error.Source.Property);
        var contact = Assert.Single(error.Contacts);
        Assert.Equal(new EntityId("wall"), contact.Obstacle);
        Assert.InRange(contact.Point.X, -.58, -.38);
        Assert.InRange(contact.Point.W, min, max);
    }

    [Fact]
    public void OverlappingBuildingRangesWithDistantGeometryAreAllowed()
    {
        Assert.IsType<LoadResult.Loaded>(World.Load(Description(Item(), Other(10, new(.5, 1.5)))));
    }

    [Fact]
    public void BuildingGapDoesNotBlockTheUprightObject()
    {
        var wall = Other(-.48, new(.5, 1.5), gaps: [new(.5, .99)]);

        Assert.IsType<LoadResult.Loaded>(World.Load(Description(Item(), wall)));
    }

    [Fact]
    public void DynamicAdditionUsesTheSameOtherBuildingValidation()
    {
        var world = Load(Description(Other(-.48, new(.5, 1.5))));

        var rejected = Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(Item()));

        Assert.Equal(FailureCode.InvalidInput, rejected.Failure.Code);
        Assert.Equal(new EntityId("wall"), Assert.Single(rejected.Failure.Contacts).Obstacle);
        Assert.IsType<EntityReadResult.Missing>(world.ReadEntity(Item().Id));
    }

    [Fact]
    public void RotatedInitialPoseAndLongAxisUseTheActualHinge()
    {
        var turnedToY = new Rotation4(new(0, 1, 0, 0), new(-1, 0, 0, 0), Identity4.Z, Identity4.W);
        var item = Item() with { Pose = new(default, turnedToY) };
        var obstacle = Other(0, new(.5, 1.5)) with { Pose = At(0, -.48, 0) };

        var errors = LoadErrors(Description(item, obstacle));

        var contact = Assert.Single(Assert.Single(errors, e => e.Code == LoadErrorCode.BuildingInterference).Contacts);
        Assert.InRange(contact.Point.Y, -.58, -.38);
        Assert.InRange(contact.Point.W, .5, .98);
    }

    [Fact]
    public void LongAxisYUsesItsOwnHingeCoordinate()
    {
        var item = Item() with
        {
            Shape = Box(new(.1, .5, .1)),
            Interaction = new(
                Holdable: true, Packable: false, HorizontalRadius: .5, LongAxis: Axis3.Y, Hinge: new(-.1, -.48, 0), Reach: .98),
        };
        var obstacle = Other(0, new(.5, 1.5)) with { Pose = At(0, -.48, 0) };

        var errors = LoadErrors(Description(item, obstacle));

        Assert.Contains(errors, e => e.Code == LoadErrorCode.BuildingInterference);
    }

    [Theory]
    [InlineData(.195, false)]
    [InlineData(.194, true)]
    public void InterferenceHonorsFiveMillimeterContactTolerance(double wallZ, bool blocked)
    {
        var wall = Other(-.48, new(.5, 1.5)) with { Pose = At(-.48, 0, wallZ) };

        var result = World.Load(Description(Item(), wall));

        var interferes = result is LoadResult.Invalid invalid
            && invalid.Errors.Any(e => e.Code == LoadErrorCode.BuildingInterference);
        Assert.Equal(blocked, interferes);
    }

    [Fact]
    public void ObstaclesInTheOwnerBuildingDoNotTriggerOtherBuildingInterference()
    {
        var sameBuilding = Other(-.48, new(.5, 1.5)) with { Building = new("owner") };

        Assert.IsType<LoadResult.Loaded>(World.Load(Description(Item(), sameBuilding)));
    }

    [Fact]
    public void OtherBuildingIsRejectedWhenOnlyTheMiddleOfTheSweepIntersects()
    {
        var wall = Other(0, new(.34, .36)) with { Shape = Box(new(.02, .02, .02)), Pose = At(.2, 0, 0) };

        var errors = LoadErrors(Description(Item(), wall));

        Assert.Contains(errors, e => e.Code == LoadErrorCode.BuildingInterference && e.Contacts.Any(c => c.Obstacle == wall.Id));
        var far = wall with { Pose = At(.2, 0, 1) };
        Assert.IsType<LoadResult.Loaded>(World.Load(Description(Item(), far)));
    }

    [Fact]
    public void BuildingMustCoverWExtremumBetweenEndpoints()
    {
        var narrowOwner = new BuildingDefinition(new("owner"), new(-.98, .98), TestSource);
        var other = new BuildingDefinition(new("other"), new(-2, 2), TestSource);
        var description = Description(Item()) with { Buildings = [narrowOwner, other] };

        Assert.Contains(LoadErrors(description), e => e.Code == LoadErrorCode.BuildingCoverage);
    }
}
