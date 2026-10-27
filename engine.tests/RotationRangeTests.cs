using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

public class RotationRangeTests
{
    // A wall 4 cm thick and 1 m high and wide, in section I, centered at x.
    private static FixedDefinition Wall(double x) =>
        new(new("wall"), At(x, 0, 0), TestSource, new("I"), Shape(Part("part", new(.02, .5, .5))));

    [Theory]
    [InlineData(-.5, false)] // about a wall at x = -.3 the rod's far end, .54 from it, swings to W = -.54, outside
    [InlineData(-.6, true)] // the same flip fits a building that reaches to W = -.6
    public void FlippingAboutAWallMustStayWithinTheBuildingsW(double buildingMin, bool allowed)
    {
        var actor = Actor(x: -3);
        var world = Load(Description([Rod(), Wall(-.3), actor], buildingMin));

        var result = world.PlanRotation(new Flip(actor.Id, RodId));

        if (allowed) Assert.IsType<RotationPlanResult.Allowed>(result);
        else Assert.Equal(FailureCode.OutOfRange, Assert.IsType<RotationPlanResult.Rejected>(result).Failure.Code);
    }
}
