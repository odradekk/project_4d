using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;
using static FourD.Tests.SeamScenes;

namespace FourD.Tests;

// A rod long enough to reach the target section stands up in the seam when the creature crosses with it in hand.
// The rod of the size norms (.48 m long, hinge 2 cm inside its left end) lies at the origin of section I with its
// hinge at (-.22, 0, 0), and reaches .46 m: past section II (.42 m deep) but not to the section .46 m away.
public class WedgeTests
{
    // A rod of the given reach, centered at the origin of section I, with its hinge 2 cm inside its left end.
    private static TurnableDefinition Stick(double reach, bool packable)
    {
        var half = (reach + .02) / 2;
        return Rod() with
        {
            Shape = Box(new(half, .03, .03)),
            Interaction = new TurnableProperties(Holdable: true, Packable: packable, HorizontalRadius: half, LongAxis: Axis3.X, Hinge: new(.02 - half, 0, 0), Reach: reach)
        };
    }

    private static World HoldingTheRod(SeamDefinition seam, params EntityDefinition[] more)
    {
        var world = SeamScene(seam, [Rod(), Actor(x: .15), .. more]);
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(ActorId, RodId));
        return world;
    }

    [Fact]
    public void ALongRodStandsUpInTheSeamTowardTheDeeperSectionAndStaysInTheHand()
    {
        // Turned a quarter about its hinge at (-.22, 0, 0, 0), the rod's center (.22 m along the long axis from the
        // hinge) goes from W = 0 to W = .22, and +X becomes +W.
        var world = HoldingTheRod(Seam());

        var crossed = world.Cross(Request(slots: 0));

        Assert.Equal(RodId, crossed.WedgedItem);
        Assert.Empty(crossed.Packed);
        var rod = world.State(RodId);
        AssertNear(new Vec4(-.22, 0, 0, .22), rod.Pose.Position);
        AssertNear(XwQuarterTurn, rod.Pose.Rotation);
        Assert.Equal(I, rod.UprightOrigin);
        Assert.Null(rod.Section);
        Assert.Equal(ActorId, rod.Holder);
        Assert.False(rod.Rotating);
        Assert.Empty(world.State(ActorId).CarriedItems);
        Assert.True(world.HasSliceVolume(RodId, II));
        Assert.True(world.HasSliceVolume(RodId, I));
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
        AssertNear(new Vec4(-.22, 0, 0, .2), state.Pose.Position);
        AssertNear(XwRotation(cos: 0, sin: -1), state.Pose.Rotation);
        Assert.Equal(II, state.UprightOrigin);
        Assert.True(world.HasSliceVolume(RodId, I));
    }

    [Fact]
    public void CrossingAMirrorSeamTurnsTheCreatureButNotTheRodThatStoodUp()
    {
        // The rod is already +W; turning it again by the half turn (which reverses W) would make it -W.
        // The creature lands at x = -.15, where its reach (-.35 to .05) includes the plate at x = -.22: the rod it
        // holds does not stand in its way.
        var world = HoldingTheRod(Seam(mirrored: true));

        var crossed = world.Cross(Request());

        Assert.Equal(Handedness.Mirrored, crossed.ActorHandedness);
        Assert.Equal(new Vec4(-.15, 0, 0, .42), world.State(ActorId).Pose.Position);
        var rod = world.State(RodId);
        AssertNear(new Vec4(-.22, 0, 0, .22), rod.Pose.Position);
        AssertNear(XwQuarterTurn, rod.Pose.Rotation);
        Assert.True(world.HasSliceVolume(RodId, II));
    }

    [Theory]
    [InlineData(.18, true)]
    [InlineData(.39, true)]
    [InlineData(.46, false)]
    public void OnlyARodThatReachesTheTargetSectionStandsUpAndTheOthersGoInTheBackpack(double reach, bool packed)
    {
        // The target is .42 m away. Part ends must stay 2 cm clear of a section, so reaches near .42 cannot exist.
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
        // Standing up, the rod becomes a plate at x = -.22 that spans W up to .46, through this block at W .2 to .3.
        var block = Block("block", new(-.22, 0, 0), new(.05, .05, .05), new(.2, .3));
        var world = HoldingTheRod(Seam(), block);

        var failure = world.Refuse(Request(), FailureCode.RotationBlocked, ActorId, RodId);

        Assert.Contains(failure.Contacts, contact => contact.Obstacle == new EntityId("block"));
        Assert.Equal(new SeamId("seam"), failure.Seam);
    }

    [Fact]
    public void ACreatureHoldingARodThatStoodUpCannotCrossAgainButCanLetGoAndLayItDownAtTheOrigin()
    {
        var world = HoldingTheRod(Seam());
        world.Cross(Request());
        world.StepEvents(.001);

        world.Refuse(Request(target: "I"), FailureCode.NotPackable, ActorId, RodId);
        world.ReleaseEntity(RodId);
        world.Cross(Request(target: "I"));

        // Crossing back only changes the section: the rod is still stood up, with its origin in section I.
        var standing = world.State(RodId);
        Assert.Equal(I, standing.UprightOrigin);
        Assert.Equal(I, world.State(ActorId).Section);
        var plan = world.AllowedPlan(new LayDown(ActorId, RodId));
        var started = Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan));
        world.StepEvents(started.Duration);
        var laid = world.State(RodId);
        Assert.Equal(I, laid.Section);
        Assert.Null(laid.UprightOrigin);
        AssertNear(new Vec4(0, 0, 0, 0), laid.Pose.Position);
        Assert.Equal(Identity4, laid.Pose.Rotation);
    }
}
