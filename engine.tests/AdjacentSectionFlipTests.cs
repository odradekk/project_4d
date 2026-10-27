using static FourD.Tests.Fixtures;

namespace FourD.Tests;

// An item in section "source" and an actor in section "actor-section", a few millimetres apart in W.
public partial class RotationTests
{
    private static TurnableDefinition SourceRod(double x, double sourceW) => ShortRod with
    {
        Section = new("source"),
        Pose = At(x, 0, 0, sourceW),
        Interaction = RodProperties(radius: .24, hingeX: -.20, reach: .44),
    };

    private static Creature3Definition ActorInSection(double actorW) => Actor(At(-1, 0, 0, actorW), section: "actor-section");

    private static SectionDefinition[] SourceAndActorSections(double sourceW, double actorW) =>
        new SectionDefinition[] { new(new("source"), sourceW, TestSource), new(new("actor-section"), actorW, TestSource) }
            .OrderBy(s => s.W)
            .ToArray();

    [Theory]
    [InlineData(false, 0d, .003)]
    [InlineData(true, 0d, .003)]
    [InlineData(false, .003, 0d)]
    [InlineData(true, .003, 0d)]
    [InlineData(false, .42, .423)]
    [InlineData(true, .42, .423)]
    [InlineData(false, .423, .42)]
    [InlineData(true, .423, .42)]
    public void AdjacentSectionActorFlipsWithoutMovingItemToAnotherW(bool obstacle, double sourceW, double actorW)
    {
        var item = SourceRod(x: obstacle ? 1 : 0, sourceW);
        var actor = ActorInSection(actorW);
        var wall = ThinWall(At(0, 0, 0, actorW), section: "actor-section");
        EntityDefinition[] entities = obstacle ? [item, actor, wall] : [item, actor];
        var world = Load(Description(SourceAndActorSections(sourceW, actorW), entities));
        Assert.True(world.HasSliceVolume(item.Id, actor.Section));
        var plan = world.AllowedPlan(new Flip(actor.Id, item.Id));
        var contours = FinalContours(world, plan, item.Section);
        var duration = obstacle ? FlipAroundObstacleSeconds : FlipInPlaceSeconds;
        Assert.Equal(duration, Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);

        world.Step(duration);

        var end = world.State(item.Id);
        Assert.Equal(item.Section, end.Section);
        Assert.Null(end.UprightOrigin);
        Assert.False(end.Rotating);
        AssertContoursSpanSlice(contours, Assert.Single(world.SlicesIn(item.Section), s => s.Entity == item.Id));
        AssertCanShiftInXAtW(world, end, sourceW);
    }

    [Theory]
    [InlineData(0d, .003, .002, .013)]
    [InlineData(.003, 0d, -.010, .001)]
    public void FlipFindsObstacleInActorsSectionButKeepsItemsSection(double sourceW, double actorW, double minW, double maxW)
    {
        var item = SourceRod(x: 1, sourceW);
        var actor = ActorInSection(actorW);
        var wall = new BuildingElementDefinition(
            new("wall"), Origin, TestSource, new("b"), new(minW, maxW), [],
            Shape(Part("part", new(.02, .5, .5))));
        var world = Load(Description(SourceAndActorSections(sourceW, actorW), item, actor, wall));
        Assert.False(world.HasSliceVolume(wall.Id, item.Section));
        Assert.True(world.HasSliceVolume(wall.Id, actor.Section));
        var plan = world.AllowedPlan(new Flip(actor.Id, item.Id));
        Assert.Equal(FlipAroundObstacleSeconds, Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);

        world.Step(FlipAroundObstacleSeconds);

        var end = world.State(item.Id);
        Assert.Equal(-1.04, end.Pose.Position.X, 10);
        Assert.Equal(sourceW, end.Pose.Position.W);
        Assert.Equal(item.Section, end.Section);
    }
}
