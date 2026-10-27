using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;
using static FourD.Tests.SeamScenes;

namespace FourD.Tests;

// What a creature takes through a seam in its backpack: the item in the hand goes in, everything in the backpack is
// turned with the creature, and what cannot go in refuses the crossing. The nut lies at x = .1, next to the creature.
public class BackpackCrossingTests
{
    private static ItemDefinition SmallNut() => Nut(At(.1, .03, 0));

    // A short item that is not packable, lying in section II well away from where the creature lands.
    private static TurnableDefinition ChairInII() => Chair() with { Section = II, Pose = At(1, .03, 0, .42) };

    private static World HoldingTheNut(SeamDefinition seam, params EntityDefinition[] more)
    {
        var world = SeamScene(seam, [SmallNut(), Actor(x: .15), .. more]);
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(ActorId, NutId));
        return world;
    }

    [Fact]
    public void ASmallItemInTheHandGoesIntoTheBackpackAndCrossesWithTheCreature()
    {
        var world = HoldingTheNut(Seam());

        var crossed = world.Cross(Request(slots: 1));

        Assert.Equal([NutId], crossed.Packed);
        Assert.Null(crossed.WedgedItem);
        var nut = world.State(NutId);
        Assert.Equal(ActorId, nut.Holder);
        Assert.True(nut.InBackpack);
        Assert.Equal(II, nut.Section);
        Assert.Equal(new Vec4(.1, .03, 0, .42), nut.Pose.Position);
        Assert.Equal(Identity4, nut.Pose.Rotation);
        Assert.Equal([NutId], world.State(ActorId).CarriedItems);
    }

    [Fact]
    public void AnItemInTheBackpackIsNeitherSlicedNorSolid()
    {
        var world = HoldingTheNut(Seam());
        Assert.NotNull(world.SliceOf(NutId, I));

        world.Cross(Request());

        Assert.Null(world.SliceOf(NutId, I));
        Assert.Null(world.SliceOf(NutId, II));
        Assert.Empty(world.OccupancyHits(new OccupancyBox(At(.1, .03, 0, .42), new(.1, .1, .1, .02))));
    }

    [Fact]
    public void TheItemInTheHandDoesNotBlockTheCreatureThatCarriesIt()
    {
        // The nut at x = .1 is inside the reach of the creature at x = .15 at both ends of the crossing.
        var world = HoldingTheNut(Seam());

        world.Cross(Request());
    }

    [Fact]
    public void ACrossingWithoutAFreeSlotIsRefused()
    {
        var world = HoldingTheNut(Seam());

        var failure = world.Refuse(Request(slots: 0), FailureCode.CapacityExceeded, ActorId, NutId);

        Assert.Equal(NutId, failure.Entity);
    }

    [Fact]
    public void ABackpackItemTurnsHalfWayWithTheCreatureOnAMirrorSeamAndKeepsItsHandedness()
    {
        var chair = ChairInII();
        var world = HoldingTheNut(Seam(mirrored: true), chair);
        Assert.Equal(HandednessComparison.Same, Comparison(world, ActorId, NutId));
        Assert.Equal(HandednessComparison.Same, Comparison(world, ActorId, chair.Id));

        world.Cross(Request());

        var nut = world.State(NutId);
        Assert.Equal(new Vec4(-.1, .03, 0, .42), nut.Pose.Position);
        AssertNear(XwHalfTurn, nut.Pose.Rotation);
        Assert.Equal(HandednessComparison.Same, Comparison(world, ActorId, NutId));
        Assert.Equal(HandednessComparison.Different, Comparison(world, ActorId, chair.Id));
    }

    private static HandednessComparison Comparison(World world, EntityId a, EntityId b) =>
        Assert.IsType<HandednessComparisonResult.Comparison>(world.CompareHandedness(a, b)).Value;

    [Fact]
    public void AnItemAlreadyInTheBackpackCrossesAgainWithoutAFreeSlot()
    {
        var world = HoldingTheNut(Seam(mirrored: true));
        world.Cross(Request(slots: 1));

        var back = world.Cross(Request(target: "I", slots: 0));

        Assert.Empty(back.Packed);
        var nut = world.State(NutId);
        Assert.Equal(new Vec4(.1, .03, 0, 0), nut.Pose.Position);
        AssertNear(Identity4, nut.Pose.Rotation);
        Assert.Equal(ActorId, nut.Holder);
    }

    [Fact]
    public void ABackpackItemCanBeTakenOutIntoTheHandInTheNewSection()
    {
        var world = HoldingTheNut(Seam());
        world.Cross(Request());

        Assert.IsType<CarryResult.Changed>(world.HoldEntity(ActorId, NutId));

        Assert.NotNull(world.SliceOf(NutId, II));
        Assert.Equal(ActorId, world.State(NutId).Holder);
    }

    [Fact]
    public void TheBackpackDoesNotOccupyTheHand()
    {
        var chair = ChairInII();
        var world = HoldingTheNut(Seam(), chair);
        world.Cross(Request());

        Assert.IsType<CarryResult.Changed>(world.HoldEntity(ActorId, chair.Id));

        var full = Assert.IsType<CarryResult.Rejected>(world.HoldEntity(ActorId, NutId));
        Assert.Equal(FailureCode.EntityInUse, full.Failure.Code);
        Assert.Null(world.SliceOf(NutId, II));
    }

    [Fact]
    public void OnlyTheOwnerCanTakeAnItemOutOfItsBackpack()
    {
        var world = HoldingTheNut(Seam(), Actor("other", section: "II", x: 1));
        world.Cross(Request());

        var rejected = Assert.IsType<CarryResult.Rejected>(world.HoldEntity(new("other"), NutId));

        Assert.Equal(FailureCode.EntityInUse, rejected.Failure.Code);
        Assert.Equal(ActorId, world.State(NutId).Holder);
    }

    [Fact]
    public void AnItemLeavesTheBackpackWhereTheCreatureLetsGoOfIt()
    {
        var world = HoldingTheNut(Seam());
        world.Cross(Request());

        Assert.IsType<CarryResult.Changed>(world.ReleaseEntity(NutId));

        Assert.NotNull(world.SliceOf(NutId, II));
        Assert.Null(world.State(NutId).Holder);
        Assert.Empty(world.State(ActorId).CarriedItems);
    }

    [Fact]
    public void AnItemComesOutOfTheBackpackWhereTheCreatureStandsThen()
    {
        // The nut is a 6 cm cube whose lowest point is .03 m below its origin, and it was packed at a height of 1 m.
        // The creature stands .5 m up, on a platform, at (.65, -.5): the nut lies on it, whatever its offset to the
        // creature was when it was packed.
        var world = SeamScene(Seam(), Nut(At(.1, 1, 0)), Actor(x: .15));
        world.HoldEntity(ActorId, NutId);
        world.Cross(Request());
        world.SynchronizeActor(ActorId, new(.65, .5, -.5), 0);

        world.HoldEntity(ActorId, NutId);

        var nut = world.State(NutId);
        Assert.Equal(new Vec4(.65, .53, -.5, .42), nut.Pose.Position);
        Assert.Equal(Identity4, nut.Pose.Rotation);
        Assert.False(nut.InBackpack);
    }

    [Fact]
    public void LettingGoOfAnItemInTheBackpackPutsItWhereTheCreatureStandsToo()
    {
        var world = HoldingTheNut(Seam(mirrored: true));
        world.Cross(Request());
        world.SynchronizeActor(ActorId, new(-1, 0, 2), 0);

        world.ReleaseEntity(NutId);

        var nut = world.State(NutId);
        Assert.Equal(new Vec4(-1, .03, 2, .42), nut.Pose.Position);
        AssertNear(XwHalfTurn, nut.Pose.Rotation);
    }

    [Fact]
    public void TheItemInTheHandStaysWhereTheHostLeftIt()
    {
        var chair = ChairInII();
        var world = HoldingTheNut(Seam(), chair);
        world.Cross(Request());
        world.HoldEntity(ActorId, chair.Id);

        world.SynchronizeActor(ActorId, new(.65, 0, -.5), 0);

        Assert.Equal(new Vec4(1, .03, 0, .42), world.State(chair.Id).Pose.Position);
    }

    [Fact]
    public void ACreatureWithAnItemInItsBackpackCannotBeRemoved()
    {
        var world = HoldingTheNut(Seam());
        world.Cross(Request());

        var rejected = Assert.IsType<RemoveEntityResult.Rejected>(world.RemoveEntity(ActorId));

        Assert.Equal(FailureCode.EntityInUse, rejected.Failure.Code);
    }

    [Fact]
    public void AnItemInTheBackpackCannotBeMovedRotatedOrPlaced()
    {
        var world = HoldingTheNut(Seam());
        world.Cross(Request());

        var move = Assert.IsType<MoveEntityResult.Rejected>(world.MoveEntity(new(NutId, new(.1, 0, 0))));
        var plan = Assert.IsType<RotationPlanResult.Rejected>(world.PlanRotation(new Flip(ActorId, NutId)));
        var place = Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(NutId, At(0, .03, 0, .42)));

        Assert.Equal(FailureCode.InvalidInput, move.Failure.Code);
        Assert.Equal(FailureCode.NoSlice, plan.Failure.Code);
        Assert.Equal(FailureCode.EntityInUse, place.Failure.Code);
    }

    [Fact]
    public void ThingsThatCannotGoInTheBackpackRefuseTheCrossing()
    {
        // A ball can never go in, even when it is marked packable; neither can a short item that is not packable,
        // nor an object that has been stood up and is only grabbed.
        var ball = Ball(x: .1) with { Interaction = BallProperties() with { Packable = true } };
        var withBall = SeamScene(Seam(), ball, Actor(x: .15));
        var withChair = SeamScene(Seam(), Chair(), Actor(x: .15));
        var withUpright = SeamScene(Seam(), Rod(), Actor(x: .15));
        withUpright.Stand(ActorId, RodId, II);
        foreach (var (world, item) in new[] { (withBall, BallId), (withChair, new EntityId("chair")), (withUpright, RodId) })
        {
            Assert.IsType<CarryResult.Changed>(world.HoldEntity(ActorId, item));

            var failure = world.Refuse(Request(), FailureCode.NotPackable, ActorId, item);

            Assert.Equal(item, failure.Entity);
        }
    }

    [Fact]
    public void AnItemThatIsTurningInTheHandRefusesTheCrossing()
    {
        var world = SeamScene(Seam(), Rod(), Actor(x: .15));
        world.HoldEntity(ActorId, RodId);
        world.StartRotation(world.AllowedPlan(new StandUp(ActorId, RodId, II)));
        world.StepEvents(.001);

        var failure = world.Refuse(Request(), FailureCode.Busy, ActorId);

        Assert.Equal(RodId, failure.Entity);
    }
}
