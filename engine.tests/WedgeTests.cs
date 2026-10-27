using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;
using static FourD.Tests.SeamScenes;

namespace FourD.Tests;

// A rod long enough to reach the target section stands up in the seam when the creature crosses with it in hand: it is
// first moved on the floor plan until its hinge is at the seam. The rod of the size norms (.48 m long, hinge 2 cm
// inside its left end) lies at the origin of section I with its hinge at (-.22, 0, 0), and reaches .46 m: past
// section II (.42 m deep) but not to the section .46 m away. The seam is at the origin.
public class WedgeTests
{
    private static World HoldingTheRod(SeamDefinition seam, params EntityDefinition[] more)
    {
        var world = SeamScene(seam, [Rod(), Actor(x: .15), .. more]);
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(ActorId, RodId));
        return world;
    }

    [Fact]
    public void ALongRodStandsUpInTheSeamTowardTheDeeperSectionAndStaysInTheHand()
    {
        // The rod is moved by .22 m in X to put its hinge at the seam. Turned a quarter about that hinge, its center
        // (.22 m along the long axis from the hinge) goes from W = 0 to W = .22, and +X becomes +W.
        var world = HoldingTheRod(Seam());

        var crossed = world.Cross(Request(slots: 0));

        Assert.Equal(RodId, crossed.WedgedItem);
        Assert.Empty(crossed.Packed);
        var rod = world.State(RodId);
        AssertNear(new Vec4(0, 0, 0, .22), rod.Pose.Position);
        AssertNear(XwQuarterTurn, rod.Pose.Rotation);
        Assert.Equal(I, rod.UprightOrigin);
        Assert.Null(rod.Section);
        Assert.Equal(ActorId, rod.Holder);
        Assert.False(rod.InBackpack);
        Assert.False(rod.Rotating);
        Assert.Empty(world.State(ActorId).CarriedItems);
        Assert.True(world.HasSliceVolume(RodId, II));
        Assert.True(world.HasSliceVolume(RodId, I));
    }

    [Fact]
    public void ARodHeldAwayFromTheSeamStandsUpWithItsHingeAtTheSeam()
    {
        // The rod lies at x = 1.5 with its hinge at x = 1.28, and the seam is at (.5, 2). The rod is moved on the floor
        // plan by (-.78, 2) first, so that its plate, 1.2 cm thick and 6 cm wide, stands centered on the seam.
        var rod = Rod() with { Pose = At(1.5, 0, 0) };
        var world = SeamScene(Seam(position: new(.5, 0, 2)), rod, Actor() with { Pose = At(.5, 0, 2.1) });
        world.HoldEntity(ActorId, RodId);

        world.Cross(Request());

        var slice = world.SliceOf(RodId, II)!;
        Assert.Equal(.5, Center(slice, Axis3.X), 9);
        Assert.Equal(2, Center(slice, Axis3.Z), 9);
        Assert.Equal(0, Center(slice, Axis3.Y), 9);
    }

    private static double Center(SliceGeometry slice, Axis3 axis) =>
        (slice.Coordinates(axis).Min() + slice.Coordinates(axis).Max()) / 2;

    [Fact]
    public void ARodThatCannotBeCarriedToTheSeamRefusesTheCrossingAndStaysWhereItWas()
    {
        // A wall 4 cm thick at x = .8 stands between the rod, at x = 1.5, and the seam.
        var wall = new FixedDefinition(new("wall"), At(.8, .5, 0), TestSource, I, Box(new(.02, .5, .5)));
        var rod = Rod() with { Pose = At(1.5, 0, 0) };
        var world = SeamScene(Seam(), wall, rod, Actor(x: .15));
        world.HoldEntity(ActorId, RodId);

        var failure = world.Refuse(Request(), FailureCode.TranslationBlocked, ActorId, RodId);

        Assert.Equal(new EntityId("wall"), Assert.Single(failure.Contacts).Obstacle);
        Assert.Equal(new SeamId("seam"), failure.Seam);
        Assert.Equal(1.5, world.State(RodId).Pose.Position.X);
    }

    [Fact]
    public void ASeamThatSkipsASectionStandsTheRodUpThroughIt()
    {
        // Seam I - III, with section II in between: the rod, 1.1 m of reach, passes through II and ends .1 m past III.
        var third = new SectionDefinition(new("III"), 1, TestSource);
        var rod = Stick(1.1, packable: false);
        var world = Load(new(
            [SectionI, SectionII, third], [new(new("home"), new(-1.2, 2.2), TestSource)],
            [rod, Actor(x: .15)], [Seam(sections: ["I", "III"])], [], []));
        world.HoldEntity(ActorId, RodId);

        var crossed = world.Cross(Request(target: "III"));

        Assert.Equal(RodId, crossed.WedgedItem);
        Assert.Equal(new SectionId("III"), world.State(ActorId).Section);
        Assert.True(world.HasSliceVolume(RodId, I));
        Assert.True(world.HasSliceVolume(RodId, II));
        Assert.True(world.HasSliceVolume(RodId, new("III")));
    }

    [Fact]
    public void TheRodStandsUpTowardTheShallowerSectionWhenThatIsTheTarget()
    {
        // From section II (W = .42) the rod turns toward -W: its center goes to W = .42 - .22 = .2, and +X becomes -W.
        var rod = Rod() with { Section = II, Pose = At(0, 0, 0, .42) };
        var world = SeamScene(Seam(), rod, Actor(section: "II", x: .15));
        world.HoldEntity(ActorId, RodId);

        var crossed = world.Cross(Request(target: "I"));

        Assert.Equal(RodId, crossed.WedgedItem);
        var state = world.State(RodId);
        AssertNear(new Vec4(0, 0, 0, .2), state.Pose.Position);
        AssertNear(XwRotation(cos: 0, sin: -1), state.Pose.Rotation);
        Assert.Equal(II, state.UprightOrigin);
        Assert.True(world.HasSliceVolume(RodId, I));
    }

    [Fact]
    public void CrossingAMirrorSeamTurnsTheCreatureButNotTheRodThatStoodUp()
    {
        // The rod is already +W; turning it again by the half turn (which reverses W) would make it -W.
        // The creature lands at x = -.15, where its reach (-.35 to .05) includes the plate at the seam, x = 0: the rod
        // it holds does not stand in its way.
        var world = HoldingTheRod(Seam(mirrored: true));

        var crossed = world.Cross(Request());

        Assert.Equal(Handedness.Mirrored, crossed.ActorHandedness);
        Assert.Equal(new Vec4(-.15, 0, 0, .42), world.State(ActorId).Pose.Position);
        var rod = world.State(RodId);
        AssertNear(new Vec4(0, 0, 0, .22), rod.Pose.Position);
        AssertNear(XwQuarterTurn, rod.Pose.Rotation);
        Assert.True(world.HasSliceVolume(RodId, II));
    }

    [Theory]
    [InlineData(.18, true)]
    [InlineData(.39, true)]
    [InlineData(.46, false)]
    public void OnlyARodThatReachesTheTargetSectionStandsUpAndTheOthersGoInTheBackpack(double reach, bool packed)
    {
        // The target is .42 m away. A reach near that cannot be loaded, since part ends must stay 2 cm clear of a section.
        var world = SeamScene(Seam(), Stick(reach, packable: true), Actor(x: .15));
        world.HoldEntity(ActorId, RodId);

        var crossed = world.Cross(Request());

        Assert.Equal(packed, crossed.Packed.Contains(RodId));
        Assert.Equal(packed, crossed.WedgedItem is null);
        Assert.Equal(packed, world.State(RodId).UprightOrigin is null);
    }

    [Fact]
    public void AShortRodThatIsNotPackableRefusesTheCrossing()
    {
        var world = SeamScene(Seam(), Stick(.18, packable: false), Actor(x: .15));
        world.HoldEntity(ActorId, RodId);

        world.Refuse(Request(), FailureCode.NotPackable, ActorId, RodId);
    }

    [Fact]
    public void ARodWhoseWayIsBlockedKeepsTheCreatureOnThisSide()
    {
        // Standing up, the rod becomes a plate at the seam that spans W up to .46, through this block at W .2 to .3.
        var block = Block("block", new(0, 0, 0), new(.05, .05, .05), new(.2, .3));
        var world = HoldingTheRod(Seam(), block);

        var failure = world.Refuse(Request(), FailureCode.RotationBlocked, ActorId, RodId);

        Assert.Contains(failure.Contacts, contact => contact.Obstacle == new EntityId("block"));
        Assert.Equal(new SeamId("seam"), failure.Seam);
    }

    [Fact]
    public void ARodThatStandsInTheSeamBlocksTheWayBackUntilItIsMovedAside()
    {
        var world = HoldingTheRod(Seam());
        world.Cross(Request());
        world.StepEvents(.001);

        // Held, it cannot cross again. Let go, its plate at the seam is in the way of the landing.
        world.Refuse(Request(target: "I"), FailureCode.NotPackable, ActorId, RodId);
        world.ReleaseEntity(RodId);
        world.Refuse(Request(target: "I"), FailureCode.PassageOccupied, ActorId, RodId);

        // Taken up again and moved 1 m aside in Z, its slices move in both sections, and the way is free.
        world.HoldEntity(ActorId, RodId);
        Assert.Equal(new Vec3(0, 0, 1), Assert.IsType<MoveEntityResult.Moved>(world.MoveEntity(new(RodId, new(0, 0, 1)))).Applied);
        Assert.Equal(1, Center(world.SliceOf(RodId, I)!, Axis3.Z), 9);
        Assert.Equal(1, Center(world.SliceOf(RodId, II)!, Axis3.Z), 9);
        world.ReleaseEntity(RodId);
        world.Cross(Request(target: "I"));

        // Crossing back only changes the section: the rod is still stood up, with its origin in section I.
        var standing = world.State(RodId);
        Assert.Equal(I, standing.UprightOrigin);
        Assert.Equal(I, world.State(ActorId).Section);
        Assert.True(world.HasSliceVolume(RodId, II));
    }

    [Fact]
    public void ARodThatStandsInTheSeamBlocksEveryCreatureOnThatSeamNotOnlyTheOneThatHoldsIt()
    {
        var cat = Actor("cat", x: .15) with { CanUseSeamsWithoutPrism = true };
        var world = HoldingTheRod(Seam(), cat);
        world.Cross(Request());
        world.StepEvents(.001);

        world.Refuse(Request(actor: "cat", hasPrism: false), FailureCode.PassageOccupied, new EntityId("cat"));
    }

    [Fact]
    public void ARodThatWasMovedAsideLiesDownWhereItsHingeIsInTheSectionItCameFrom()
    {
        var world = HoldingTheRod(Seam());
        world.Cross(Request());
        world.MoveEntity(new(RodId, new(0, 0, 1)));
        world.ReleaseEntity(RodId);
        world.Cross(Request(target: "I"));

        var plan = world.AllowedPlan(new LayDown(ActorId, RodId));
        var started = Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan));
        world.StepEvents(started.Duration);

        var laid = world.State(RodId);
        Assert.Equal(I, laid.Section);
        Assert.Null(laid.UprightOrigin);
        // It lies where it was moved to, with its hinge 1 m from the seam, the way a plan to lay it down follows the hinge.
        AssertNear(new Vec4(.22, 0, 1, 0), laid.Pose.Position);
        Assert.Equal(Identity4, laid.Pose.Rotation);
    }
}
