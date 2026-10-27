using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// A flip swings about a wall in the plane of its axis and W, so it can leave the building's W range
// (docs/frame/rules.md: the building must hold everything an item can reach).
public class RotationRangeTests
{
    // A wall 4 cm thick and 1 m high and wide, in section I, centered at x.
    private static FixedDefinition Wall(double x) =>
        new(new("wall"), At(x, 0, 0), TestSource, new("I"), Shape(Part("part", new(.02, .5, .5))));

    // The rod flips about a wall at x = -.3, its far end .54 away: it swings to W = -.54 one way and .54 the other.
    private static (World World, Creature3Definition Actor) FlipScene(double buildingMin, double buildingMax, params EntityDefinition[] more)
    {
        var actor = Actor(x: -3);
        return (Load(Description([Rod(), Wall(-.3), actor, .. more], buildingMin, buildingMax)), actor);
    }

    [Fact]
    public void FlipTakesTheWayThatStaysInsideTheBuilding()
    {
        // Turning one way would reach W = -.54, outside a building that starts at -.5, so the flip turns the other way.
        var (world, actor) = FlipScene(-.5, 1);

        var allowed = Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new Flip(actor.Id, RodId)));

        world.StartRotation(allowed.Plan);
        world.StepEvents(.6);
        Assert.True(world.State(RodId).Pose.Position.W > 0);
        world.StepEvents(.8); // a flip about an obstacle takes 1.4 s
        Assert.Equal(0, world.State(RodId).Pose.Position.W, 9);
    }

    [Fact]
    public void FlipIsRefusedWhenNeitherWayStaysInsideTheBuilding()
    {
        var (world, actor) = FlipScene(-.5, .5);

        var rejected = Assert.IsType<RotationPlanResult.Rejected>(world.PlanRotation(new Flip(actor.Id, RodId)));

        Assert.Equal(FailureCode.OutOfRange, rejected.Failure.Code);
    }

    [Fact]
    public void FlipThatIsObstructedInTheOnlyWayInsideTheBuildingShowsThatWay()
    {
        // The way toward -W fits the building (W -1 to .5) but passes a block at W -.45 to -.35; the way toward +W
        // reaches .54 and does not fit. The plan is the obstructed one, so that the preview can show where.
        var block = Block("block", new(-.3, 0, 0), new(.02, 1, 1), new(-.45, -.35));
        var (world, actor) = FlipScene(-1, .5, block);

        var blocked = Assert.IsType<RotationPlanResult.Blocked>(world.PlanRotation(new Flip(actor.Id, RodId)));

        Assert.Equal(new EntityId("block"), blocked.Failure.Contacts[0].Obstacle);
    }
}
