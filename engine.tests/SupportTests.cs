using static FourD.Tests.Fixtures;

namespace FourD.Tests;

public class SupportTests
{
    private static readonly TurnableProperties CubeProperties = new(
        Holdable: true, Packable: false, HorizontalRadius: .5, LongAxis: Axis3.X, Hinge: new(-.48, -.5, 0), Reach: .98);

    // A 1 m cube whose origin is at height y; it rests at y = .5 on a floor with its top face at y = 0.
    private static TurnableDefinition Cube(string id, double y) =>
        new(new(id), At(0, y, 0), TestSource, new("I"), new("b"), Box(new(.5, .5, .5)), CubeProperties);

    private static BuildingElementDefinition Slab(string id, Vec3 center, Vec3 half, Interval? wRange = null) =>
        new(new(id), At(center.X, center.Y, center.Z), TestSource, new("b"), wRange ?? new Interval(-1, 1), [], Box(half));

    // A 4 m x 4 m floor whose top face is at y = 0.
    private static BuildingElementDefinition Floor(Interval? wRange = null) =>
        Slab("floor", new(0, -.05, 0), new(2, .05, 2), wRange);

    // Two 0.2 m cubes 2 m apart around centerX, one at height leftY and one at rightY.
    private static TurnableDefinition TwoCubes(
        string id, double leftY, double rightY, double centerX = 0, double radius = 2,
        double hingeX = -1.08, double reach = 2.18, BoxPart[]? extraParts = null)
    {
        var half = new Vec3(.1, .1, .1);
        var shape = Shape([
            Part("left", half, center: new(centerX - 1, leftY, 0)),
            Part("right", half, center: new(centerX + 1, rightY, 0)),
            .. extraParts ?? []]);
        var properties = new TurnableProperties(
            Holdable: true, Packable: false, HorizontalRadius: radius, LongAxis: Axis3.X, Hinge: new(hingeX, 0, 0), Reach: reach);
        return new(new(id), Origin, TestSource, new("I"), new("b"), shape, properties);
    }

    // One section "I" and one building "b" that covers W in [-buildingExtent, buildingExtent].
    private static WorldDescription Description(EntityDefinition[] entities, double buildingExtent = 2) => new(
        [new(new("I"), 0, TestSource)],
        [new(new("b"), new(-buildingExtent, buildingExtent), TestSource)],
        entities, [], [], []);

    private static SupportResult.Supported Supported(World world, EntityId id) =>
        Assert.IsType<SupportResult.Supported>(world.FindSupport(id));

    [Fact]
    public void ObjectFallsOntoFloorAndReportsRealContact()
    {
        var item = Cube("item", y: 2);
        var world = Load(Description([Floor(), item]));

        var support = Supported(world, item.Id);
        Assert.Equal(.5, support.RestingOriginY, 10);
        var contact = Assert.Single(support.Contacts);
        Assert.Equal(new EntityId("floor"), contact.Support);
        Assert.Equal(0, contact.Point.Y, 10);
        Assert.InRange(contact.Point.W, -Epsilon, Epsilon);
        Assert.True(contact.Normal.Y > 0);

        var fell = Assert.IsType<EntityFell>(Assert.Single(world.StepEvents(.1)).Event);
        Assert.Equal(.5, fell.ToY, 10);
        Assert.Equal(.5, world.State(item.Id).Pose.Position.Y, 10);
    }

    [Fact]
    public void FloorBetweenSectionsSupportsTiltedBoxEvenWhenFloorHasNoSectionSlice()
    {
        var floor = Floor(new Interval(.18, .24));
        var item = Cube("item", y: 2);
        var sections = new SectionDefinition[] { new(new("I"), 0, TestSource), new(new("II"), .42, TestSource) };
        var world = Load(Description([floor, item]) with { Sections = sections });
        var tilt = At(0, 2, 0, w: .21, rotation: XwQuarterTurn);
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id, tilt));

        Assert.False(world.HasSliceVolume(floor.Id, new("I")));
        Assert.False(world.HasSliceVolume(floor.Id, new("II")));
        var support = Supported(world, item.Id);
        Assert.Equal(.5, support.RestingOriginY, 10);
        var contact = Assert.Single(support.Contacts);
        Assert.Equal(0, contact.Point.Y, 10);
        Assert.InRange(contact.Point.W, .18, .24);
        Assert.True(contact.Normal.Y > 0);
    }

    [Fact]
    public void SideTouchAloneDoesNotSupportAndNoFloorLeavesPositionUnchanged()
    {
        // The wall's -X face (x = .5) touches the cube's whole +X face.
        var wall = Slab("wall", new(1, 2, 0), new(.5, 1, .5));
        var item = Cube("item", y: 2);
        var world = Load(Description([wall, item]));

        Assert.IsType<SupportResult.Unsupported>(world.FindSupport(item.Id));
        Assert.Empty(world.StepEvents(.1));
        Assert.Equal(2, world.State(item.Id).Pose.Position.Y);
    }

    [Fact]
    public void SideTouchDoesNotReplaceTheFloorAsSupport()
    {
        var wall = Slab("wall", new(1, 2, 0), new(.5, 1, .5));
        var item = Cube("item", y: 2);
        var world = Load(Description([Floor(), wall, item]));

        var support = Supported(world, item.Id);
        Assert.Equal(.5, support.RestingOriginY, 10);
        Assert.Equal(new EntityId("floor"), Assert.Single(support.Contacts).Support);
        world.StepEvents(.1);
        Assert.Equal(.5, world.State(item.Id).Pose.Position.Y, 10);
    }

    [Theory]
    [InlineData(.497)]
    [InlineData(.5)]
    public void TouchOrSmallExistingPenetrationDoesNotLiftObject(double y)
    {
        var item = Cube("item", y);
        var world = Load(Description([Floor(), item]));

        var support = Supported(world, item.Id);
        Assert.Equal(y, support.RestingOriginY, 10);
        var point = Assert.Single(support.Contacts).Point;
        Assert.InRange(point.Y, y - .5, 0);
        Assert.Empty(world.StepEvents(.1));
    }

    [Fact]
    public void SlightPenetrationIntoCeilingDoesNotSupportObjectBelow()
    {
        var ceiling = Slab("ceiling", new(0, .05, 0), new(2, .05, 2));
        var item = Cube("item", y: -.497);
        var world = Load(Description([ceiling, item]));

        Assert.IsType<SupportResult.Unsupported>(world.FindSupport(item.Id));
    }

    [Fact]
    public void ShallowOverlapInWDoesNotEraseExistingFloorSupport()
    {
        var floor = Floor(new Interval(.003, .015));
        var item = Cube("item", y: .497);
        var world = Load(Description([floor, item]));

        var support = Supported(world, item.Id);
        Assert.Equal(.497, support.RestingOriginY, 10);
        var point = Assert.Single(support.Contacts).Point;
        Assert.InRange(point.W, .003, Epsilon);
        Assert.InRange(point.Y, -.003, 0);
    }

    [Fact]
    public void StaticEntityCanBeQueriedForSupportWithoutMovingIt()
    {
        var cabinet = new FixedDefinition(new("cabinet"), At(0, 2, 0), TestSource, new("I"), Box(new(.5, .5, .5)));
        var world = Load(Description([Floor(), cabinet]));

        Assert.Equal(.5, Supported(world, cabinet.Id).RestingOriginY, 10);
        Assert.Empty(world.StepEvents(.1));
        Assert.Equal(2, world.State(cabinet.Id).Pose.Position.Y);
    }

    [Theory]
    [InlineData("a-upper", "z-lower")]
    [InlineData("z-upper", "a-lower")]
    public void StepSettlesStackInOneCallRegardlessOfIdOrder(string upperId, string lowerId)
    {
        var upper = Cube(upperId, y: 4);
        var lower = Cube(lowerId, y: 2);
        var world = Load(Description([Floor(), upper, lower]));

        var events = world.StepEvents(.1);

        Assert.Equal(1.5, world.State(upper.Id).Pose.Position.Y, 10);
        Assert.Equal(.5, world.State(lower.Id).Pose.Position.Y, 10);
        var falls = events.Select(e => Assert.IsType<EntityFell>(e.Event)).ToList();
        Assert.Equal(new[] { upperId, lowerId }.Order(StringComparer.Ordinal), falls.Select(f => f.Entity.Value));
        Assert.All(events, e => Assert.Equal(.1, e.Time));
        var upperFall = falls.Single(f => f.Entity == upper.Id);
        Assert.Equal(4, upperFall.FromY);
        Assert.Equal(1.5, upperFall.ToY, 10);
        Assert.Equal(1.5, Supported(world, upper.Id).RestingOriginY, 10);
        Assert.Empty(world.StepEvents(.1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MutualPotentialSupportSettlesToFloorOrStaysWhenUnbounded(bool hasFloor)
    {
        // Each item's high cube sits over the other item's low cube, so neither can fall alone.
        var a = TwoCubes("a", leftY: 1, rightY: -1);
        var b = TwoCubes("b", leftY: -1, rightY: 1);
        var floor = Slab("floor", new(0, -10.05, 0), new(3, .05, 3));
        EntityDefinition[] entities = hasFloor ? [a, b, floor] : [a, b];
        var world = Load(Description(entities, buildingExtent: 4));

        var events = world.StepEvents(.1);

        var expectedY = hasFloor ? -8.9 : 0;
        Assert.Equal(expectedY, world.State(a.Id).Pose.Position.Y, 9);
        Assert.Equal(expectedY, world.State(b.Id).Pose.Position.Y, 9);
        Assert.Equal(hasFloor ? 2 : 0, events.Count);
    }

    [Fact]
    public void DescendingSupportPreservesEarlierShallowPenetration()
    {
        var upper = Cube("a", y: 2.997);
        var lower = Cube("z", y: 2);
        var world = Load(Description([Floor(), upper, lower]));

        Assert.IsType<StepResult.Advanced>(world.Step(.1));

        Assert.Equal(1.497, world.State(upper.Id).Pose.Position.Y, 10);
        Assert.Equal(.5, world.State(lower.Id).Pose.Position.Y, 10);
        var contact = Assert.Single(Supported(world, upper.Id).Contacts);
        Assert.Equal(lower.Id, contact.Support);
        Assert.InRange(contact.Point.Y, .997, 1);
        Assert.True(contact.Normal.Y > 0);
    }

    [Theory]
    [InlineData("a", "z")]
    [InlineData("z", "a")]
    public void MutuallyEmbeddedPartsKeepRelativePoseWhileFallingTogether(string firstId, string secondId)
    {
        var a = TwoCubes(firstId, leftY: .1, rightY: -.097);
        var b = TwoCubes(secondId, leftY: -.097, rightY: .1);
        var floor = Slab("floor", new(0, -10.05, 0), new(3, .05, 3));
        var world = Load(Description([a, b, floor], buildingExtent: 4));

        Assert.IsType<StepResult.Advanced>(world.Step(.1));

        Assert.Equal(-9.803, world.State(a.Id).Pose.Position.Y, 9);
        Assert.Equal(-9.803, world.State(b.Id).Pose.Position.Y, 9);
        Assert.Empty(world.StepEvents(.1));
    }

    [Fact]
    public void MutualPotentialSupportKeepsIndependentFallDistances()
    {
        var a = TwoCubes("z", leftY: 1, rightY: -1);
        var b = TwoCubes("a", leftY: -1, rightY: 1);
        var floor = Slab("floor", new(1, -10.05, 0), new(.2, .05, .2));
        var world = Load(Description([a, b, floor], buildingExtent: 4));

        Assert.IsType<StepResult.Advanced>(world.Step(.1));

        Assert.Equal(-8.9, world.State(a.Id).Pose.Position.Y, 9);
        Assert.Equal(-10.7, world.State(b.Id).Pose.Position.Y, 9);
    }

    [Fact]
    public void ThreeLevelsSettleInOneStepWithOneEventPerEntity()
    {
        var top = Cube("a-top", y: 6);
        var middle = Cube("m-middle", y: 4);
        var bottom = Cube("z-bottom", y: 2);
        var world = Load(Description([Floor(), top, middle, bottom]));

        var events = world.StepEvents(.1);

        Assert.Equal(2.5, world.State(top.Id).Pose.Position.Y, 10);
        Assert.Equal(1.5, world.State(middle.Id).Pose.Position.Y, 10);
        Assert.Equal(.5, world.State(bottom.Id).Pose.Position.Y, 10);
        var falls = events.Select(e => Assert.IsType<EntityFell>(e.Event)).ToList();
        Assert.Equal(new[] { "a-top", "m-middle", "z-bottom" }, falls.Select(f => f.Entity.Value));
        Assert.Equal(2.5, falls[0].ToY, 10);
        Assert.Equal(1.5, falls[1].ToY, 10);
        Assert.Equal(.5, falls[2].ToY, 10);
    }

    [Fact]
    public void UnanchoredTerminalCycleDoesNotFreezeTheCycleAboveIt()
    {
        // a and b interlock and rest on nothing, so they stay at y = 0. c and d interlock too. c's bridge reaches
        // over a's left cube, so c falls 1.8 m onto it, and d falls 3.6 m onto c.
        var bridge = Part("bridge", new(.1, .1, .1), center: new(-3, 3, 0));
        var a = TwoCubes("a", leftY: 1, rightY: -1, centerX: -2, radius: 4, hingeX: -3.08);
        var b = TwoCubes("b", leftY: -1, rightY: 1, centerX: -2, radius: 4, hingeX: -3.08);
        var c = TwoCubes("c", leftY: 1, rightY: -1, centerX: 2, radius: 4, hingeX: -3.08, reach: 6.18, extraParts: [bridge]);
        var d = TwoCubes("d", leftY: -1, rightY: 1, centerX: 2, radius: 4, hingeX: .92);
        var world = Load(Description([a, b, c, d], buildingExtent: 8));

        var events = world.StepEvents(.1);

        Assert.Equal(0, world.State(a.Id).Pose.Position.Y, 9);
        Assert.Equal(0, world.State(b.Id).Pose.Position.Y, 9);
        Assert.Equal(-1.8, world.State(c.Id).Pose.Position.Y, 9);
        Assert.Equal(-3.6, world.State(d.Id).Pose.Position.Y, 9);
        Assert.Equal(new[] { "c", "d" }, events.Select(e => Assert.IsType<EntityFell>(e.Event).Entity.Value));
    }
}
