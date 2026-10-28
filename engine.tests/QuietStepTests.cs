using static FourD.Tests.DeviceScenes;
using static FourD.Tests.Fixtures;
using static FourD.Tests.GrazerScenes;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// A Step that finds nothing changed since the last one has no turnable to settle. Whatever then changes where a
// turnable rests must still reach the next Step, so each test lets the world go quiet first.
public class QuietStepTests
{
    // A world whose turnables have all settled, and that has gone through two Steps with nothing to report.
    private static World Quiet(params EntityDefinition[] entities)
    {
        var world = Scene(entities);
        world.StepEvents(.1);
        Assert.Empty(world.StepEvents(.1));
        Assert.Empty(world.StepEvents(.1));
        return world;
    }

    private static EntityFell Fell(World world) =>
        world.StepEvents(.1).Select(occurrence => occurrence.Event).OfType<EntityFell>().Single();

    // A ball over a crate: it comes to rest on the crate, and falls to the floor when the crate is gone.
    private static World BallOnCrate() => Quiet(FloorOf(new(-.5, 1)), Crate(.1), Ball(y: 1));

    [Fact]
    public void AQuietStepStillAdvancesTimeAndReportsWhatOtherCallsQueued()
    {
        var world = Quiet(FloorOf(new(-.5, 1)), Ball(y: 1));
        var before = Assert.IsType<StepResult.Advanced>(world.Step(.1)).Time;

        Assert.IsType<RemoveEntityResult.Removed>(world.RemoveEntity(BallId));
        var step = Assert.IsType<StepResult.Advanced>(world.Step(.25));

        Assert.Equal(before + .25, step.Time, 9);
        Assert.Equal(new EntityRemoved(BallId), Assert.Single(step.Events).Event);
    }

    [Fact]
    public void WhatRestedOnAConsumedElementFallsAfterAQuietStep()
    {
        var ledge = Block("ledge", new(0, .5, 0), new(1, .5, 1), new(-.5, 1));
        var world = Quiet(Grazer(At(0, 30, 0, w: .18), Lump(default)), FloorOf(new(-.5, 1)), ledge, Ball(y: 1.25));

        world.ConsumeStructure(GrazerId, [new("ledge")], []);

        var fell = Fell(world);
        Assert.Equal(BallId, fell.Entity);
        Assert.Equal(BallRadius, fell.ToY, 9);
    }

    [Fact]
    public void WhatRestedOnADeckThatMovesAwayFallsAfterAQuietStep()
    {
        var world = Quiet(FloorOf(new(-.5, 1)), Lift(), Crate(1.1));

        world.Driven(LiftId, -.5);

        Assert.Equal(CrateId, Fell(world).Entity);
    }

    [Fact]
    public void WhatRestedOnARemovedTurnableFallsAfterAQuietStep()
    {
        var world = BallOnCrate();

        Assert.IsType<RemoveEntityResult.Removed>(world.RemoveEntity(CrateId));

        Assert.Equal(BallRadius, Fell(world).ToY, 9);
    }

    [Fact]
    public void WhatRestedOnAPlacedTurnableFallsAfterAQuietStep()
    {
        var world = BallOnCrate();

        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(CrateId, At(2, .1, 0)));

        Assert.Equal(BallRadius, Fell(world).ToY, 9);
    }

    [Fact]
    public void WhatRestedOnAHeldTurnableFallsAfterItIsMovedAwayAndAQuietStep()
    {
        var world = Quiet(Actor(), FloorOf(new(-.5, 1)), Crate(.1), Ball(y: 1));
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(new("actor"), CrateId));
        Assert.Empty(world.StepEvents(.1));

        Assert.IsType<MoveEntityResult.Moved>(world.MoveEntity(new(CrateId, new(1.5, 0, 0))));

        Assert.Equal(BallId, Fell(world).Entity);
    }

    [Fact]
    public void AReleasedTurnableInTheAirFallsAfterAQuietStep()
    {
        var world = Quiet(Actor(), FloorOf(new(-.5, 1)), Crate(.1));
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(new("actor"), CrateId));
        Assert.IsType<MoveEntityResult.Moved>(world.MoveEntity(new(CrateId, new(0, 1.4, 0))));
        Assert.Empty(world.StepEvents(.1));
        Assert.Empty(world.StepEvents(.1));

        Assert.IsType<CarryResult.Changed>(world.ReleaseEntity(CrateId));

        Assert.Equal(CrateId, Fell(world).Entity);
    }

    [Fact]
    public void ATurnableAddedInTheAirFallsAfterAQuietStep()
    {
        var world = Quiet(FloorOf(new(-.5, 1)));

        Assert.IsType<AddEntityResult.Added>(world.AddEntity(Ball(y: 2)));

        Assert.Equal(BallRadius, Fell(world).ToY, 9);
    }
}
