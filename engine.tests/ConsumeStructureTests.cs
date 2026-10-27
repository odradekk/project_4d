using static FourD.Tests.Fixtures;
using static FourD.Tests.GrazerScenes;
using static FourD.Tests.ItemScenes;
using static FourD.Tests.SeamScenes;

namespace FourD.Tests;

// Grazing: the host names the building elements and seams a grazer bites, and they go for good
// (docs/frame/contract-actions.md). The scripted bite does not depend on where the grazer's body is, so the grazer
// here stays far from what it eats.
public class ConsumeStructureTests
{
    private static readonly SectionId I = new("I");
    private static readonly SectionId II = new("II");
    private static readonly EntityId LedgeId = new("ledge");

    // A block 2 m wide whose top is 1 m up: a ball can rest on it, and it stands on the floor.
    private static readonly BuildingElementDefinition Ledge = Block("ledge", new(0, .5, 0), new(1, .5, 1), new(-.5, 1));

    private static BuildingElementDefinition Pillar(string id, double x) => Block(id, new(x, .5, 0), new(.25, .5, .25), new(-.5, 1));

    // A region with the grazer, two seams and the entities given.
    private static World Region(params EntityDefinition[] more)
    {
        var description = Description([Grazer(At(0, 30, 0, w: .18), Lump(default)), .. more]);
        return Load(description with { Seams = [Seam(id: "s1"), Seam(id: "s2", position: new(1, 0, 0))] });
    }

    private static UpdateResult Consume(World world, string[] elements, string[] seams) =>
        world.ConsumeStructure(GrazerId, elements.Select(id => new EntityId(id)).ToArray(), seams.Select(id => new SeamId(id)).ToArray());

    private static bool Shows(World world, SectionId section, EntityId element) =>
        world.SlicesIn(section).Any(slice => slice.Entity == element);

    [Fact]
    public void AConsumedElementLeavesTheSlicesTheCollisionAndTheSupport()
    {
        var world = Region(FloorOf(new(-.5, 1)), Ledge, Ball(y: 1.25));
        var inside = new OccupancyBox(At(0, .5, 0), new(.2, .2, .2, .1));
        Assert.True(Shows(world, I, LedgeId) && Shows(world, II, LedgeId));
        Assert.Equal([LedgeId], world.OccupancyHits(inside).Select(hit => hit.Entity));
        var held = Assert.IsType<SupportResult.Supported>(world.FindSupport(BallId));
        Assert.Equal(1.25, held.RestingOriginY, 10);
        Assert.Equal(LedgeId, Assert.Single(held.Contacts).Support);

        Assert.IsType<UpdateResult.Updated>(Consume(world, ["ledge"], []));

        Assert.False(Shows(world, I, LedgeId) || Shows(world, II, LedgeId));
        Assert.True(Shows(world, I, Floor) && Shows(world, II, Floor));
        Assert.Empty(world.OccupancyHits(inside));
        var resting = Assert.IsType<SupportResult.Supported>(world.FindSupport(BallId));
        Assert.Equal(BallRadius, resting.RestingOriginY, 10);
        Assert.Equal(Floor, Assert.Single(resting.Contacts).Support);
        Assert.NotNull(world.SliceOf(GrazerId, I));
    }

    [Fact]
    public void WhatStoodOnAConsumedElementFallsAtTheNextStepAfterTheEvent()
    {
        var world = Region(FloorOf(new(-.5, 1)), Ledge, Ball(y: 1.25));

        Consume(world, ["ledge"], []);

        var events = world.StepEvents(.1);
        Assert.Equal(new StructureConsumed(GrazerId, LedgeId), events[0].Event);
        Assert.Equal(0, events[0].Time);
        var fell = Assert.IsType<EntityFell>(events[1].Event);
        Assert.Equal(BallId, fell.Entity);
        Assert.Equal(1.25, fell.FromY, 10);
        Assert.Equal(BallRadius, fell.ToY, 10);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void ARotationThatAConsumedElementBlockedIsFreeToStart()
    {
        // The rod stands up toward II about its hinge at x = -.22, and on the way its free end passes through the
        // beam, which fills x within .1 of the middle for W from .1 to .3.
        var beam = Block("beam", default, new(.1, .5, .5), new(.1, .3));
        var world = Region(Rod(), Actor(x: .1), beam);
        var request = new StandUp(ActorId, RodId, II);
        var blocked = Assert.IsType<RotationPlanResult.Blocked>(world.PlanRotation(request));
        Assert.Contains(blocked.Failure.Contacts, contact => contact.Obstacle == new EntityId("beam"));

        Consume(world, ["beam"], []);

        Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(request));
    }

    [Fact]
    public void AConsumedSeamIsSeveredAndReported()
    {
        var world = Region();

        Consume(world, [], ["s2"]);

        Assert.True(Assert.IsType<SeamReadResult.Found>(world.ReadSeam(new("s2"))).Seam.Severed);
        Assert.False(Assert.IsType<SeamReadResult.Found>(world.ReadSeam(new("s1"))).Seam.Severed);
        Assert.Equal(new SeamSevered(new("s2")), Assert.Single(world.StepEvents(.1)).Event);
    }

    [Fact]
    public void ABiteReportsItsElementsAndThenItsSeamsEachInIdOrder()
    {
        var world = Region(Pillar("b", 2), Pillar("a", -2));

        Consume(world, ["b", "a"], ["s2", "s1"]);

        var events = world.StepEvents(.1);
        Assert.Equal(
            [new StructureConsumed(GrazerId, new("a")), new StructureConsumed(GrazerId, new("b")),
                new SeamSevered(new("s1")), new SeamSevered(new("s2"))],
            events.Select(occurrence => occurrence.Event));
        Assert.All(events, occurrence => Assert.Equal(0, occurrence.Time));
        Assert.Equal(events.Select(occurrence => occurrence.Sequence).Order(), events.Select(occurrence => occurrence.Sequence));
        Assert.Equal(4, events.Select(occurrence => occurrence.Sequence).Distinct().Count());
    }

    [Fact]
    public void BitingWhatIsAlreadyGoneSucceedsWithoutAnotherEvent()
    {
        var world = Region(Pillar("a", -2));
        Consume(world, ["a"], ["s1"]);
        Assert.Equal(2, world.StepEvents(.1).Count);

        Assert.IsType<UpdateResult.Updated>(Consume(world, ["a", "a"], ["s1"]));
        Assert.IsType<UpdateResult.Updated>(Consume(world, [], []));

        Assert.Empty(world.StepEvents(.1));
    }

    [Fact]
    public void AnElementOrSeamNamedTwiceInOneBiteIsReportedOnce()
    {
        var world = Region(Pillar("a", -2));

        Assert.IsType<UpdateResult.Updated>(Consume(world, ["a", "a"], ["s1", "s1"]));

        Assert.Equal(
            [new StructureConsumed(GrazerId, new("a")), new SeamSevered(new("s1"))],
            world.StepEvents(.1).Select(occurrence => occurrence.Event));
    }

    [Fact]
    public void ABiteWithAnyBadReferenceChangesNothing()
    {
        var world = Region(Pillar("a", -2), Ball(x: 2), WallFrom(5, "I"), Rod(), Actor(x: .1));
        var plan = world.AllowedPlan(new StandUp(ActorId, RodId, II));

        Assert.Equal(FailureCode.UnknownId, Consume(world, ["a", "none"], ["s1"]).Refused().Code);
        Assert.Equal(FailureCode.UnknownId, Consume(world, ["a"], ["s1", "none"]).Refused().Code);
        Assert.Equal(FailureCode.WrongEntityKind, Consume(world, ["a", "wall"], []).Refused().Code);
        Assert.Equal(FailureCode.WrongEntityKind, Consume(world, ["a", "ball"], []).Refused().Code);
        Assert.Equal(FailureCode.WrongEntityKind, Consume(world, ["grazer"], []).Refused().Code);
        Assert.Equal(FailureCode.InvalidInput, world.ConsumeStructure(GrazerId, null!, []).Refused().Code);
        Assert.Equal(FailureCode.InvalidInput, world.ConsumeStructure(GrazerId, [], null!).Refused().Code);

        Assert.True(Shows(world, I, new("a")));
        Assert.False(Assert.IsType<SeamReadResult.Found>(world.ReadSeam(new("s1"))).Seam.Severed);
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan));
        Assert.Equal(new RotationStarted(RodId), Assert.Single(world.StepEvents(.1)).Event);
    }

    [Fact]
    public void OnlyAGrazerCanBite()
    {
        var world = Region(Pillar("a", -2), Ball());

        Assert.Equal(FailureCode.UnknownId, world.ConsumeStructure(new("none"), [new("a")], []).Refused().Code);
        Assert.Equal(FailureCode.WrongEntityKind, world.ConsumeStructure(BallId, [new("a")], []).Refused().Code);
        Assert.Equal(FailureCode.WrongEntityKind, world.ConsumeStructure(new("a"), [new("a")], []).Refused().Code);
        Assert.True(Shows(world, I, new("a")));
    }

    [Fact]
    public void AConsumedElementIsGoneForGoodAndItsIdIsNotReused()
    {
        var world = Region(Pillar("a", -2));
        Consume(world, ["a"], []);

        Assert.IsType<EntityReadResult.Missing>(world.ReadEntity(new("a")));
        Assert.Equal(FailureCode.UnknownId, Assert.IsType<RemoveEntityResult.Rejected>(world.RemoveEntity(new("a"))).Failure.Code);
        var again = Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(Rod() with { Id = new("a") }));
        Assert.Equal(FailureCode.DuplicateId, again.Failure.Code);
    }

    [Fact]
    public void ABiteCountsAsAChangeOfTheWorld()
    {
        var world = Region(Rod(), Actor(x: .1), Pillar("a", -2));
        var plan = world.AllowedPlan(new StandUp(ActorId, RodId, II));

        Consume(world, ["a"], []);

        var stale = Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(plan));
        Assert.Equal(FailureCode.StalePlan, stale.Failure.Code);
    }
}
