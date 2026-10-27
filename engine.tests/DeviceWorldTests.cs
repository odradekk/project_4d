using static FourD.Tests.DeviceScenes;
using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// Devices among the other solids of a world: what rests on them, what occupies them, and the calls that reach them.
public class DeviceWorldTests
{
    private static readonly SectionId I = new("I");
    private static readonly SectionId II = new("II");

    [Fact]
    public void ACrateRestsOnADeckAndFallsWhenTheDeckMovesAway()
    {
        var world = Scene(FloorOf(new(-.5, 1)), Lift(), Crate(1.1));

        var support = Assert.IsType<SupportResult.Supported>(world.FindSupport(CrateId));
        Assert.Equal(1.1, support.RestingOriginY, 9);
        Assert.Equal(LiftId, Assert.Single(support.Contacts).Support);

        // The deck sinks to y = .5, and the crate drops onto it at the next step: its origin is .1 above the top.
        world.Driven(LiftId, -.5);
        var fell = world.StepEvents(.1).Select(occurrence => occurrence.Event).OfType<EntityFell>().Single();
        Assert.Equal(CrateId, fell.Entity);
        Assert.Equal(.6, fell.ToY, 9);
    }

    [Fact]
    public void ABallRestsOnTheEdgeOfADeck()
    {
        var world = Scene(Lift(), Ball(x: .6, y: 3));

        var support = Assert.IsType<SupportResult.Supported>(world.FindSupport(BallId));

        // The ball's center is .1 beyond the deck's edge in x and .11 deeper than its W range, so it touches the
        // edge where its center is sqrt(.25² - .1² - .11²) above the deck's top at y = 1.
        Assert.Equal(1 + Math.Sqrt(.25 * .25 - .1 * .1 - .11 * .11), support.RestingOriginY, 9);
        Assert.Equal(LiftId, Assert.Single(support.Contacts).Support);
    }

    [Fact]
    public void ADeviceBlocksTheRingOfAnItemLikeAnyOtherObstacle()
    {
        // Where the ring of the nut passes at W = .3 (HiddenRingTests): x from .3 to .4, and W from .28 to .32.
        var wall = LoftBox("wall", (.3, .4), (0, 2), (-.5, .5));
        var device = new DeviceDefinition(
            new("wall"), Origin, TestSource, Loft(Key(.28, wall), Key(.32, wall)), new DeviceTranslation(Axis3.X, new(0, 1), 0));
        var world = Scene(FloorOf(new(-.5, 1)), Nut(), Actor(), device);

        var blocked = Assert.IsType<RotationPlanResult.Blocked>(world.PlanRotation(new StandUp(new("actor"), NutId, II)));

        Assert.Equal(new EntityId("wall"), Assert.Single(blocked.Failure.Contacts).Obstacle);
    }

    [Fact]
    public void ADeviceHasSupportLikeAnyOtherEntity()
    {
        var world = Scene(FloorOf(new(-.5, 1)), Lift());

        var support = Assert.IsType<SupportResult.Supported>(world.FindSupport(LiftId));

        // The deck's underside is at y = .9 and the floor's top at 0, so the device's origin would come to rest at -.9.
        Assert.Equal(-.9, support.RestingOriginY, 9);
        Assert.Equal(Floor, Assert.Single(support.Contacts).Support);
    }

    [Fact]
    public void OccupancyFindsADeviceOnlyWhereItReaches()
    {
        var world = Scene(Lift());

        var inside = Assert.Single(world.OccupancyHits(new OccupancyBox(At(0, .95, 0, .05), new(.1, .02, .1, .01))));
        Assert.Equal(LiftId, inside.Entity);
        Assert.Equal(new PartId("deck"), inside.Part);
        Assert.Empty(world.OccupancyHits(new OccupancyBox(At(0, .95, 0, .42), new(.1, .02, .1, .01))));
    }

    [Fact]
    public void ADeviceAddedLaterIsSlicedAndRemovedLikeAnyOtherEntity()
    {
        var world = Scene();

        var added = Assert.IsType<AddEntityResult.Added>(world.AddEntity(Cabinet(initial: .5)));
        Assert.Equal(.5, added.Entity.DevicePosition);
        Assert.Equal(Handedness.NotComparable, added.Entity.Handedness);
        Assert.NotNull(world.SliceOf(CabinetId, I));
        var duplicate = Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(Cabinet()));
        Assert.Equal(FailureCode.DuplicateId, duplicate.Failure.Code);

        Assert.IsType<RemoveEntityResult.Removed>(world.RemoveEntity(CabinetId));
        Assert.Null(world.SliceOf(CabinetId, I));
        Assert.IsType<EntityReadResult.Missing>(world.ReadEntity(CabinetId));
        var reused = Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(Cabinet()));
        Assert.Equal(FailureCode.DuplicateId, reused.Failure.Code);
    }

    [Fact]
    public void ADeviceCannotBeAddedInsideASweepThatIsReserved()
    {
        var board = Board() with { Pose = At(-.3, -.1, -.1) };
        Assert.IsType<AddEntityResult.Added>(Scene(Rod(), Actor()).AddEntity(board));

        var world = Scene(Rod(), Actor());
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(world.AllowedPlan(new StandUp(new("actor"), RodId, II))));
        var rejected = Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(board));

        Assert.Equal(FailureCode.TranslationBlocked, rejected.Failure.Code);
        Assert.IsType<EntityReadResult.Missing>(world.ReadEntity(BoardId));
    }

    [Fact]
    public void ADeviceThatIsNotValidIsRejectedWhenAdded()
    {
        var world = Scene();

        var rejected = Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(Cabinet(initial: 2)));

        Assert.Equal(FailureCode.InvalidInput, rejected.Failure.Code);
        Assert.IsType<EntityReadResult.Missing>(world.ReadEntity(CabinetId));
    }

    [Fact]
    public void ADeviceIsNeitherTurnedNorHeldNorPlacedByThePlayerActions()
    {
        var world = Scene(Actor(), Sash());
        var actor = new EntityId("actor");

        var plan = Assert.IsType<RotationPlanResult.Rejected>(world.PlanRotation(new StandUp(actor, SashId, II)));
        Assert.Equal(FailureCode.WrongEntityKind, plan.Failure.Code);
        var hold = Assert.IsType<CarryResult.Rejected>(world.HoldEntity(actor, SashId));
        Assert.Equal(FailureCode.WrongEntityKind, hold.Failure.Code);
        var place = Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(SashId, Origin));
        Assert.Equal(FailureCode.WrongEntityKind, place.Failure.Code);
    }

    [Fact]
    public void ASuccessfulDriveMakesAnEarlierRotationPlanStale()
    {
        var world = Scene(Rod(), Actor(), Sash());
        var plan = world.AllowedPlan(new StandUp(new("actor"), RodId, II));

        world.Driven(SashId, .3);

        var rejected = Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(plan));
        Assert.Equal(FailureCode.StalePlan, rejected.Failure.Code);
    }

    [Fact]
    public void ARotationInProgressReservesItsSweepAgainstADevice()
    {
        var world = Scene(Rod(), Actor(), Sash());
        var plan = world.AllowedPlan(new StandUp(new("actor"), RodId, II));
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan));

        var driven = world.Driven(SashId, 1);

        // The pencil is not yet standing, but the sash already stops where it will stand.
        Assert.Equal(.374, driven.Position, 9);
        Assert.Equal(RodId, Assert.Single(driven.Contacts).Obstacle);
    }

    [Fact]
    public void DrivingSomethingThatIsNotADeviceFails()
    {
        var world = Scene(Rod(), Sash());

        Assert.Equal(FailureCode.UnknownId, world.Refused(new("nothing"), 0).Code);
        Assert.Equal(FailureCode.WrongEntityKind, world.Refused(RodId, 0).Code);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ATargetThatIsNotANumberIsInvalid(double target)
    {
        var world = Scene(Sash());

        Assert.Equal(FailureCode.InvalidInput, world.Refused(SashId, target).Code);
    }

    [Theory]
    [InlineData(-1e-7)]
    [InlineData(1.0000001)]
    public void ATargetPastEitherLimitIsRefusedNotClamped(double target)
    {
        var world = Scene(Sash());

        Assert.Equal(FailureCode.OutOfRange, world.Refused(SashId, target).Code);
        Assert.Equal(0, world.State(SashId).DevicePosition);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ATargetAtEitherLimitIsReached(double target)
    {
        var world = Scene(Sash(initial: .5));

        Assert.Equal(target, world.Driven(SashId, target).Position);
    }
}
