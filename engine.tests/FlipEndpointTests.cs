using static FourD.Tests.Fixtures;

namespace FourD.Tests;

public partial class RotationTests
{
    [Theory]
    [InlineData(false, 0d)]
    [InlineData(true, 0d)]
    [InlineData(false, .42)]
    [InlineData(true, .42)]
    public void ObstacleFlipEndsAtSectionAndAllowsXYZPlacement(bool forceNegative, double sectionW)
    {
        var item = ShortRod with { Pose = At(1, 0, 0, sectionW) };
        var actor = Actor(At(-1, 0, 0, sectionW));
        var wall = ThinWall(At(0, 0, 0, sectionW));
        var block = new BuildingElementDefinition(
            new("block"), Origin, TestSource, new("b"), new(sectionW - .52, sectionW - .50), [],
            Shape(Part("part", new(.03, .1, .1), center: new(.863, 0, 0))));
        EntityDefinition[] entities = forceNegative ? [item, actor, wall, block] : [item, actor, wall];
        var world = Load(Description([new(new("I"), sectionW, TestSource)], entities));
        var plan = world.AllowedPlan(new Flip(actor.Id, item.Id));
        var contours = FinalContours(world, plan, new("I"));
        Assert.Equal(FlipAroundObstacleSeconds, Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);

        world.Step(.35);
        var midpoint = world.State(item.Id);
        Assert.True(forceNegative ? midpoint.Pose.Position.W > sectionW : midpoint.Pose.Position.W < sectionW);
        world.Step(1.05);

        var end = world.State(item.Id);
        Assert.Equal(new SectionId("I"), end.Section);
        Assert.False(end.Rotating);
        AssertContoursSpanSlice(contours, Assert.Single(world.SlicesIn(new("I")), s => s.Entity == item.Id));
        AssertCanShiftInXAtW(world, end, sectionW);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(.42)]
    public void InPlaceFlipWithOffsetShapeAllowsXYZPlacement(double sectionW)
    {
        var item = OffsetRod with { Pose = At(0, 0, 0, sectionW) };
        var actor = Actor(At(-1, 0, 0, sectionW));
        var world = Load(Description([new(new("I"), sectionW, TestSource)], item, actor));
        var plan = world.AllowedPlan(new Flip(actor.Id, item.Id));
        Assert.Equal(FlipInPlaceSeconds, Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);

        world.Step(FlipInPlaceSeconds);

        var end = world.State(item.Id);
        AssertCanShiftInXAtW(world, end, sectionW);
    }

    [Fact]
    public void NearHalfTurnIsNotSnappedBeforeItsEnd()
    {
        var item = ShortRod with { Pose = At(1, 0, 0) };
        var actor = Actor(At(-1, 0, 0));
        var world = Load(Description([SectionI], item, actor, ThinWall(Origin)));
        world.StartRotation(world.AllowedPlan(new Flip(actor.Id, item.Id)));

        world.Step(FlipAroundObstacleSeconds - 1e-9);

        var state = world.State(item.Id);
        Assert.True(state.Rotating);
        Assert.NotEqual(0, state.Pose.Position.W);
        world.Step(1e-9);
        Assert.Equal(0, world.State(item.Id).Pose.Position.W);
    }
}
