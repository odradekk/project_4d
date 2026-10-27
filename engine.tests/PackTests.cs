using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;
using static FourD.Tests.SeamScenes;

namespace FourD.Tests;

// Putting an item into the backpack, and what the state of an item says about where it is.
public class PackTests
{
    private static readonly EntityId ChairId = new("chair");

    private static World Scene(params EntityDefinition[] more) => SeamScene(Seam(), [SmallNut(), Actor(x: .15), .. more]);

    private static ItemDefinition SmallNut() => Nut(At(.1, .03, 0));

    private static CarryResult.Changed Pack(World world, int slots = 1) =>
        Assert.IsType<CarryResult.Changed>(world.PackEntity(ActorId, NutId, slots));

    private static FailureCode Refusal(World world, EntityId actor, EntityId item, int slots = 1) =>
        Assert.IsType<CarryResult.Rejected>(world.PackEntity(actor, item, slots)).Failure.Code;

    [Fact]
    public void ALyingItemOfTheSectionGoesFromTheWorldIntoTheBackpack()
    {
        var world = Scene();
        Assert.NotNull(world.SliceOf(NutId, I));

        var packed = Pack(world).Item;

        Assert.True(packed.InBackpack);
        Assert.Equal(ActorId, packed.Holder);
        Assert.Equal(I, packed.Section);
        Assert.Equal([NutId], world.State(ActorId).CarriedItems);
        Assert.Null(world.SliceOf(NutId, I));
    }

    [Fact]
    public void PackingCountsAsAChangeOfTheWorld()
    {
        var world = Scene(Rod() with { Pose = At(1, 0, 1) });
        var plan = world.AllowedPlan(new StandUp(ActorId, RodId, II));

        Pack(world);

        var stale = Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(plan));
        Assert.Equal(FailureCode.StalePlan, stale.Failure.Code);
    }

    [Fact]
    public void TheItemInTheHandGoesIntoTheBackpackAndFreesTheHand()
    {
        var world = Scene(Chair() with { Pose = At(1, .03, 0) });
        world.HoldEntity(ActorId, NutId);
        Assert.False(world.State(NutId).InBackpack);

        Pack(world);

        Assert.IsType<CarryResult.Changed>(world.HoldEntity(ActorId, ChairId));
    }

    [Fact]
    public void OnlyAnItemInTheBackpackIsMarkedAsBeingInIt()
    {
        var world = Scene(Chair() with { Pose = At(1, .03, 0) });
        world.HoldEntity(ActorId, ChairId);
        Pack(world);

        Assert.True(world.State(NutId).InBackpack);
        Assert.False(world.State(ChairId).InBackpack);
        Assert.False(world.State(ActorId).InBackpack);

        world.ReleaseEntity(ChairId);
        world.HoldEntity(ActorId, NutId);
        Assert.False(world.State(NutId).InBackpack);
    }

    [Fact]
    public void ABackpackNeedsAFreeSlotThatTheHostCounts()
    {
        var world = Scene();

        Assert.Equal(FailureCode.CapacityExceeded, Refusal(world, ActorId, NutId, slots: 0));
        Assert.Equal(FailureCode.InvalidInput, Refusal(world, ActorId, NutId, slots: -1));
        Assert.False(world.State(NutId).InBackpack);
    }

    [Fact]
    public void OnlyLyingPackableItemsGoInTheBackpack()
    {
        // A ball never goes in, even marked packable. A stood-up rod does not either, nor an item that is not packable.
        var ball = Ball(x: 1) with { Interaction = BallProperties() with { Packable = true } };
        var world = Scene(ball, Chair() with { Pose = At(1, .03, 0) }, Stick(.18, packable: true) with { Pose = At(1, 0, 1) });
        world.Stand(ActorId, RodId, II);

        Assert.Equal(FailureCode.NotPackable, Refusal(world, ActorId, BallId));
        Assert.Equal(FailureCode.NotPackable, Refusal(world, ActorId, ChairId));
        Assert.Equal(FailureCode.NotPackable, Refusal(world, ActorId, RodId));
    }

    [Fact]
    public void TheItemMustLieInTheSectionOfTheCreature()
    {
        var world = Scene(Actor("other", section: "II", x: .15));

        Assert.Equal(FailureCode.WrongSection, Refusal(world, new("other"), NutId));
    }

    [Fact]
    public void AnItemThatIsHeldByAnotherCreatureOrAlreadyPackedOrTurningIsRefused()
    {
        var world = Scene(Actor("other", x: .3));
        world.HoldEntity(new("other"), NutId);
        Assert.Equal(FailureCode.EntityInUse, Refusal(world, ActorId, NutId));

        world.ReleaseEntity(NutId);
        Pack(world);
        Assert.Equal(FailureCode.InvalidInput, Refusal(world, ActorId, NutId));
    }

    [Fact]
    public void ARotatingItemIsBusy()
    {
        var world = Scene(Rod() with { Pose = At(1, 0, 1), Interaction = Rod().Interaction with { Packable = true } });
        world.StartRotation(world.AllowedPlan(new StandUp(ActorId, RodId, II)));

        Assert.Equal(FailureCode.Busy, Refusal(world, ActorId, RodId));
    }

    [Fact]
    public void UnknownOrWrongKindOfIdsAreRefused()
    {
        var world = Scene();

        Assert.Equal(FailureCode.UnknownId, Refusal(world, new("ghost"), NutId));
        Assert.Equal(FailureCode.UnknownId, Refusal(world, ActorId, new("ghost")));
        Assert.Equal(FailureCode.WrongEntityKind, Refusal(world, NutId, NutId));
    }
}
