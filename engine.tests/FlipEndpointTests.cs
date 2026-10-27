using static FourD.Tests.Fixtures;

namespace FourD.Tests;

public partial class RotationTests
{
    // The preview's last sample is the flipped object, so its outline must span the same box as the slice.
    private static void AssertContoursSpanSlice(IReadOnlyList<Polyline> contours, SliceGeometry slice)
    {
        var points = Segments(contours).SelectMany(s => new[] { s.A, s.B }).ToArray();
        Assert.NotEmpty(points);
        Assert.Equal(slice.Coordinates(0).Min(), points.Min(p => p.X), 9);
        Assert.Equal(slice.Coordinates(0).Max(), points.Max(p => p.X), 9);
        Assert.Equal(slice.Coordinates(1).Min(), points.Min(p => p.Y), 9);
        Assert.Equal(slice.Coordinates(1).Max(), points.Max(p => p.Y), 9);
        Assert.Equal(slice.Coordinates(2).Min(), points.Min(p => p.Z), 9);
        Assert.Equal(slice.Coordinates(2).Max(), points.Max(p => p.Z), 9);
    }

    // The outline of the object at the end of the planned rotation.
    private static IReadOnlyList<Polyline> FinalContours(World world, RotationPlan plan, SectionId section) =>
        Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan, section)).Value.Samples[^1].Contours;

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
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id, ShiftedX(end.Pose, .1)));
        Assert.Equal(sectionW, end.Pose.Position.W);
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
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id, ShiftedX(end.Pose, .1)));
        Assert.Equal(sectionW, end.Pose.Position.W);
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
