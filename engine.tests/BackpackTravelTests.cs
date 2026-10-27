using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;
using static FourD.Tests.SeamScenes;

namespace FourD.Tests;

// An item carried in the backpack ends up in a section other than the one it was checked against at load, so
// it is checked again where it comes out and where it stands up. The scenes have sections that are unevenly spaced.
public class BackpackTravelTests
{
    private static readonly SectionId III = new("III");

    private static SectionDefinition Section(string id, double w) => new(new(id), w, TestSource);

    // One building and one seam that joins all the sections.
    private static World Uneven(SectionDefinition[] sections, double buildingMax, params EntityDefinition[] entities) => Load(new(
        sections, [new(new("home"), new(-.6, buildingMax), TestSource)], entities,
        [Seam(sections: sections.Select(section => section.Id.Value).ToArray())], [], []));

    // Sections I, II and III at W = 0, .2 and .7.
    private static World StickWorld(double reach) =>
        Uneven([Section("I", 0), Section("II", .2), Section("III", .7)], 1.3, Stick(reach, packable: true), Actor(x: .1));

    [Theory]
    [InlineData(.5, false)]
    [InlineData(.51, false)]
    [InlineData(.53, true)]
    public void AStickBroughtToAnotherSectionNeedsItsEndToBeClearOfTheSectionsThereToStandUp(double reach, bool stands)
    {
        // From III (.7) the stick reaches .7 - reach toward II (.2): onto it, 1 cm short of it, or 3 cm past it.
        var world = StickWorld(reach);
        world.HoldEntity(ActorId, RodId);
        world.Cross(Request(target: "III", slots: 1));
        world.HoldEntity(ActorId, RodId);
        world.StepEvents(.001);

        if (stands) Assert.Equal(RodId, world.Cross(Request(target: "II")).WedgedItem);
        else world.Refuse(Request(target: "II"), FailureCode.InvalidInput, ActorId, RodId);
    }

    [Fact]
    public void AStickThatIsStoodUpByHandIsCheckedTheSameWay()
    {
        var world = StickWorld(.5);
        world.HoldEntity(ActorId, RodId);
        world.Cross(Request(target: "III", slots: 1));
        world.ReleaseEntity(RodId);

        var rejected = Assert.IsType<RotationPlanResult.Rejected>(world.PlanRotation(new StandUp(ActorId, RodId, new("II"))));

        Assert.Equal(FailureCode.InvalidInput, rejected.Failure.Code);
    }

    // The nut's ring rests .2 beyond its body in W. In II (.42) it is at .62, 3 cm from III (.65).
    private static World NutWorld() => Uneven(
        [Section("I", 0), Section("II", .42), Section("III", .65)], 1.2, Nut(At(.1, .03, 0)), Actor(x: .15));

    [Fact]
    public void ANutWhoseRingWouldBeTooCloseToASectionCannotComeOutOfTheBackpackThere()
    {
        var world = NutWorld();
        world.HoldEntity(ActorId, NutId);
        world.Cross(Request(slots: 1));

        var taken = Assert.IsType<CarryResult.Rejected>(world.HoldEntity(ActorId, NutId));
        var released = Assert.IsType<CarryResult.Rejected>(world.ReleaseEntity(NutId));

        Assert.Equal(FailureCode.InvalidInput, taken.Failure.Code);
        Assert.Equal(FailureCode.InvalidInput, released.Failure.Code);
        Assert.Equal(ActorId, world.State(NutId).Holder);
        Assert.Null(world.SliceOf(NutId, III));
    }

    [Fact]
    public void TheSameNutComesOutAgainInTheSectionItWasCheckedAgainst()
    {
        var world = NutWorld();
        world.HoldEntity(ActorId, NutId);
        world.Cross(Request(slots: 1));
        world.Cross(Request(target: "I"));

        Assert.IsType<CarryResult.Changed>(world.HoldEntity(ActorId, NutId));
    }

    // Two buildings with a gap between them. Seam to-annex joins I to III, in the annex; to-gap joins I to IV, in the
    // gap; to-v joins III to V, also in the annex.
    private static World TwoBuildings(params EntityDefinition[] entities) => Load(new(
        [Section("I", 0), Section("IV", 1.1), Section("V", 1.45), Section("III", 1.6)],
        [new(new("home"), new(-.5, .5), TestSource), new(new("annex"), new(1.3, 1.9), TestSource)],
        entities,
        [
            Seam(id: "to-annex", sections: ["I", "III"]),
            Seam(id: "to-gap", sections: ["I", "IV"], position: new(2, 0, 0)),
            Seam(id: "to-v", sections: ["V", "III"], position: new(3, 0, 0))
        ],
        [], []));

    private static BuildingId BuildingOf(World world, EntityId item) =>
        Assert.IsType<TurnableDefinition>(world.State(item).Definition).Building;

    private static readonly BuildingId Home = new("home");
    private static readonly BuildingId Annex = new("annex");

    [Fact]
    public void AnItemBelongsToTheBuildingOfTheSectionItComesOutOfTheBackpackIn()
    {
        var world = TwoBuildings(Stick(.18, packable: true), Actor(x: .1));
        world.HoldEntity(ActorId, RodId);
        world.Cross(Request(seam: "to-annex", target: "III", slots: 1));
        Assert.Equal(Home, BuildingOf(world, RodId));

        Assert.IsType<CarryResult.Changed>(world.ReleaseEntity(RodId));

        Assert.Equal(Annex, BuildingOf(world, RodId));
        world.HoldEntity(ActorId, RodId);
        world.Cross(Request(seam: "to-annex", target: "I", slots: 1));
        world.ReleaseEntity(RodId);
        Assert.Equal(Home, BuildingOf(world, RodId));
    }

    [Fact]
    public void AnItemCannotComeOutOfTheBackpackInASectionThatIsInNoBuilding()
    {
        var world = TwoBuildings(Stick(.18, packable: true), Actor(x: 2));
        world.HoldEntity(ActorId, RodId);
        world.Cross(Request(seam: "to-gap", target: "IV", slots: 1));

        var taken = Assert.IsType<CarryResult.Rejected>(world.HoldEntity(ActorId, RodId));
        var released = Assert.IsType<CarryResult.Rejected>(world.ReleaseEntity(RodId));

        Assert.Equal(FailureCode.OutOfRange, taken.Failure.Code);
        Assert.Equal(FailureCode.OutOfRange, released.Failure.Code);
        Assert.True(world.State(RodId).InBackpack);
    }

    [Fact]
    public void ARodBroughtToABuildingThatDoesNotHoldItsTurnRefusesToStandUpOutOfIt()
    {
        // In the annex (1.3 to 1.9) the rod, standing up from III (1.6) toward V (1.45), would reach W = 1.14.
        var world = TwoBuildings(Stick(.46, packable: true), Actor(x: .1));
        world.HoldEntity(ActorId, RodId);
        world.Cross(Request(seam: "to-annex", target: "III", slots: 1));
        world.HoldEntity(ActorId, RodId);
        world.SynchronizeActor(ActorId, new(3, 0, 0), 0);
        world.StepEvents(.001);

        world.Refuse(Request(seam: "to-v", target: "V"), FailureCode.OutOfRange, ActorId, RodId);
    }

    [Fact]
    public void AnItemWhoseSpotIsTakenCannotComeOutOfTheBackpack()
    {
        // The stick lies at (.5, 0, .6). In II a block fills that spot at W .3 to .6, which the creature, at the seam, does not touch.
        var stick = Stick(.18, packable: true) with { Pose = At(.5, 0, .6) };
        var block = Block("block", new(.5, 0, .6), new(.3, .3, .3), new(.3, .6));
        var world = SeamScene(Seam(), stick, block, Actor(x: .15));
        world.HoldEntity(ActorId, RodId);
        world.Cross(Request(slots: 1));

        var taken = Assert.IsType<CarryResult.Rejected>(world.HoldEntity(ActorId, RodId));
        var released = Assert.IsType<CarryResult.Rejected>(world.ReleaseEntity(RodId));

        Assert.Equal(FailureCode.TranslationBlocked, taken.Failure.Code);
        Assert.Equal(new EntityId("block"), Assert.Single(taken.Failure.Contacts).Obstacle);
        Assert.Equal(FailureCode.TranslationBlocked, released.Failure.Code);
        Assert.True(world.State(RodId).InBackpack);
    }
}
