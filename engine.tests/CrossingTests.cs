using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;
using static FourD.Tests.SeamScenes;

namespace FourD.Tests;

// Crossing a seam without anything carried: where the creature ends up, how a mirror seam turns it, and every
// reason a crossing is refused. Values are worked out by hand from the seam plane.
public class CrossingTests
{
    private static readonly SectionDefinition SectionIII = new(new("III"), 1, TestSource);

    private static Creature3Definition Cat(double x) => Actor(x: x) with { CanUseSeamsWithoutPrism = true };

    // A world with sections I, II and III, so that a seam can leave one of them out.
    private static World ThreeSections(SeamDefinition seam, params EntityDefinition[] entities) =>
        Load(Description(entities) with { Sections = [SectionI, SectionII, SectionIII], Seams = [seam] });

    private static string Geometry(World world, SectionId section) => string.Join(";", world.SlicesIn(section)
        .Select(slice => slice.Entity.Value + ":" + string.Join(",", slice.Positions)));

    [Fact]
    public void CrossingAPlainSeamOnlyChangesTheSection()
    {
        var world = SeamScene(Seam(), Actor(x: .15));
        world.SynchronizeActor(ActorId, new(.15, 0, 0), 1.2);

        var crossed = world.Cross(Request());

        Assert.Equal(I, crossed.From);
        Assert.Equal(II, crossed.To);
        Assert.Equal(Handedness.Normal, crossed.ActorHandedness);
        Assert.Empty(crossed.Packed);
        Assert.Null(crossed.WedgedItem);
        var state = world.State(ActorId);
        Assert.Equal(II, state.Section);
        Assert.Equal(new Vec4(.15, 0, 0, .42), state.Pose.Position);
        Assert.Equal(Identity4, state.Pose.Rotation);
        Assert.Equal(1.2, state.ActorYaw);
        Assert.Equal(Handedness.Normal, state.Handedness);
    }

    [Fact]
    public void ACrossingIsReportedOnceAtTheTimeOfTheRequest()
    {
        var world = SeamScene(Seam(), Actor(x: .15));
        world.StepEvents(.5);

        world.Cross(Request());

        var occurrence = Assert.Single(world.StepEvents(.1));
        Assert.Equal(new SeamCrossed(ActorId, new("seam"), I, II), occurrence.Event);
        Assert.Equal(.5, occurrence.Time);
        Assert.Empty(world.StepEvents(.1));
    }

    [Fact]
    public void ACreatureCanCrossBackInTheOtherDirection()
    {
        var world = SeamScene(Seam(), Actor(x: .15));
        world.Cross(Request());

        var back = world.Cross(Request(target: "I"));

        Assert.Equal(II, back.From);
        Assert.Equal(I, back.To);
        Assert.Equal(new Vec4(.15, 0, 0, 0), world.State(ActorId).Pose.Position);
    }

    [Fact]
    public void CrossingAMirrorSeamTurnsTheCreatureHalfWayAboutTheSeamPlane()
    {
        // The seam plane is x = 0: the half turn reverses X and W, with W moving to the target section.
        var world = SeamScene(Seam(mirrored: true), Actor(x: .15));

        var crossed = world.Cross(Request());

        Assert.Equal(Handedness.Mirrored, crossed.ActorHandedness);
        var state = world.State(ActorId);
        Assert.Equal(new Vec4(-.15, 0, 0, .42), state.Pose.Position);
        AssertNear(XwHalfTurn, state.Pose.Rotation);
        Assert.Equal(Handedness.Mirrored, state.Handedness);
        Assert.Equal(II, state.Section);
    }

    [Fact]
    public void ASeamPlaneThatDoesNotPassThroughTheOriginReflectsAboutItself()
    {
        // The plane is z = 2, so the creature at z = 2.1 comes out at z = 1.9; height is kept.
        var seam = Seam(mirrored: true, position: new(1, 0, 2), normal: new(0, 0, 1));
        var world = SeamScene(seam, Actor() with { Pose = At(1, 1, 2.1) });

        world.Cross(Request());

        var state = world.State(ActorId);
        Assert.Equal(1, state.Pose.Position.X, 12);
        Assert.Equal(1, state.Pose.Position.Y, 12);
        Assert.Equal(1.9, state.Pose.Position.Z, 12);
        Assert.Equal(.42, state.Pose.Position.W);
        AssertNear(Identity4 with { Z = new(0, 0, -1, 0), W = new(0, 0, 0, -1) }, state.Pose.Rotation);
    }

    [Fact]
    public void ASeamPlaneAtAnAngleReflectsPositionAndAxesByTheHouseholderMatrix()
    {
        // The normal (.6, 0, .8) reflects a vector by I - 2nn^T, whose columns are (.28, 0, -.96), (0, 1, 0)
        // and (-.96, 0, -.28). The creature at (.1, 0, 0) is .06 m in front of the plane, and ends up behind it.
        var seam = Seam(mirrored: true, normal: new(.6, 0, .8));
        var world = SeamScene(seam, Actor() with { Pose = At(.1, 0, 0) });

        world.Cross(Request());

        var state = world.State(ActorId);
        Assert.Equal(.028, state.Pose.Position.X, 12);
        Assert.Equal(-.096, state.Pose.Position.Z, 12);
        AssertNear(new(new(.28, 0, -.96, 0), new(0, 1, 0, 0), new(-.96, 0, -.28, 0), new(0, 0, 0, -1)), state.Pose.Rotation);
        Assert.Equal(Handedness.Mirrored, state.Handedness);
    }

    [Fact]
    public void CrossingTwoMirrorSeamsInARowRestoresTheCreature()
    {
        var world = SeamScene(Seam(mirrored: true), Actor(x: .15));
        world.Cross(Request());

        var back = world.Cross(Request(target: "I"));

        Assert.Equal(Handedness.Normal, back.ActorHandedness);
        var state = world.State(ActorId);
        Assert.Equal(new Vec4(.15, 0, 0, 0), state.Pose.Position);
        AssertNear(Identity4, state.Pose.Rotation);
    }

    [Fact]
    public void ACreatureThatStartsMirroredBecomesNormalAgainOnAMirrorSeam()
    {
        var mirrored = Actor(x: .15) with { Pose = At(.15, 0, 0, rotation: XwHalfTurn) };
        var world = SeamScene(Seam(mirrored: true), mirrored);

        var crossed = world.Cross(Request());

        Assert.Equal(Handedness.Normal, crossed.ActorHandedness);
    }

    [Fact]
    public void WalkingAndTurningDoNotChangeTheFourDimensionalRotation()
    {
        var mirrored = Actor(x: .15) with { Pose = At(.15, 0, 0, rotation: XwHalfTurn) };
        var world = SeamScene(Seam(mirrored: true), mirrored);

        world.SynchronizeActor(ActorId, new(3, 0, -2), 2.5);

        var state = world.State(ActorId);
        Assert.Equal(XwHalfTurn, state.Pose.Rotation);
        Assert.Equal(Handedness.Mirrored, state.Handedness);
        Assert.Equal(I, state.Section);
        Assert.Equal(0, state.Pose.Position.W);
    }

    [Fact]
    public void CrossingAMirrorSeamDoesNotChangeTheGeometryOfAnySection()
    {
        var floor = FloorOf(new(-.5, 1));
        var block = Block("block", new(1, .5, .5), new(.2, .5, .2), new(.3, 1));
        var world = SeamScene(Seam(mirrored: true), floor, block, Actor(x: .15));
        var before = (Geometry(world, I), Geometry(world, II));

        world.Cross(Request());

        Assert.Equal(before, (Geometry(world, I), Geometry(world, II)));
        Assert.NotEmpty(before.Item2);
    }

    [Fact]
    public void ACatCrossesWithoutThePrismAndAPersonDoesNot()
    {
        var world = SeamScene(Seam(), Cat(.1), Actor("person", x: .1));

        world.Refuse(Request(actor: "person", hasPrism: false), FailureCode.RequiresPrism, new EntityId("person"));
        world.Cross(Request(actor: "actor", hasPrism: false));
    }

    [Fact]
    public void AnUnknownOrNonCreatureActorSeamOrTargetIsRefused()
    {
        var world = SeamScene(Seam(), FloorOf(new(-.5, 1)), Actor(x: .1));

        world.Refuse(Request(actor: "ghost"), FailureCode.UnknownId, ActorId);
        world.Refuse(Request(actor: "floor"), FailureCode.WrongEntityKind, ActorId);
        var seam = world.Refuse(Request(seam: "ghost"), FailureCode.UnknownId, ActorId);
        world.Refuse(Request(target: "ghost"), FailureCode.UnknownId, ActorId);
        Assert.Equal(new SeamId("ghost"), seam.Seam);
        Assert.Equal(FailureCode.InvalidInput, Assert.IsType<CrossSeamResult.Rejected>(world.CrossSeam(null!)).Failure.Code);
    }

    [Fact]
    public void ANegativeNumberOfFreeSlotsAndTheCurrentSectionAsTargetAreInvalid()
    {
        var world = SeamScene(Seam(), Actor(x: .1));

        world.Refuse(Request(slots: -1), FailureCode.InvalidInput, ActorId);
        world.Refuse(Request(target: "I"), FailureCode.InvalidInput, ActorId);
    }

    [Fact]
    public void BothSectionsMustBeOnTheSeam()
    {
        var joiningIAndII = ThreeSections(Seam(), Actor(x: .1));
        var joiningIIAndIII = ThreeSections(Seam(sections: ["II", "III"]), Actor(x: .1));

        var failure = joiningIAndII.Refuse(Request(target: "III"), FailureCode.NotConnected, ActorId);
        joiningIIAndIII.Refuse(Request(target: "II"), FailureCode.NotConnected, ActorId);
        Assert.Equal(new SeamId("seam"), failure.Seam);
    }

    [Theory]
    [InlineData(.2, 0, 0, true)]
    [InlineData(.21, 0, 0, false)]
    [InlineData(.15, 0, .15, false)]
    [InlineData(0, 3, 0, true)]
    public void TheCreatureMustBeWithinItsPassageRadiusOfTheSeamCentreOnTheFloorPlan(double x, double y, double z, bool crosses)
    {
        var world = SeamScene(Seam(), Actor() with { Pose = At(x, y, z) });

        if (crosses) world.Cross(Request());
        else world.Refuse(Request(), FailureCode.WrongSection, ActorId);
    }

    [Fact]
    public void ASeveredOrClosedSeamCannotBeCrossedAndSeveringComesFirst()
    {
        var severed = SeamScene(Seam(severed: true, open: false), Actor(x: .1));
        var closed = SeamScene(Seam(open: false), Actor(x: .1));
        var opened = SeamScene(Seam(), Actor(x: .1));
        opened.SeverSeam(new("seam"));
        opened.StepEvents(.001);

        severed.Refuse(Request(), FailureCode.SeamSevered, ActorId);
        closed.Refuse(Request(), FailureCode.SeamClosed, ActorId);
        opened.Refuse(Request(), FailureCode.SeamSevered, ActorId);
    }

    [Fact]
    public void ASeamThatIsClosedIsReportedBeforeTheMissingPrism()
    {
        var world = SeamScene(Seam(open: false), Actor(x: .1));

        world.Refuse(Request(hasPrism: false), FailureCode.SeamClosed, ActorId);
    }

    // The rod lies to the side, out of the way of the creature at the seam.
    private static TurnableDefinition RodAside() => Rod() with { Pose = At(0, 0, 1.5) };

    [Fact]
    public void ARefusedCrossingKeepsEarlierPlansValid()
    {
        var world = SeamScene(Seam(), RodAside(), Actor(x: .1));
        var plan = world.AllowedPlan(new StandUp(ActorId, RodId, II));

        Assert.IsType<CrossSeamResult.Rejected>(world.CrossSeam(Request(hasPrism: false)));

        Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan));
    }

    [Fact]
    public void ASuccessfulCrossingMakesEarlierPlansStale()
    {
        var world = SeamScene(Seam(), RodAside(), Actor(x: .1));
        var plan = world.AllowedPlan(new StandUp(ActorId, RodId, II));

        world.Cross(Request());

        var stale = Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(plan));
        Assert.Equal(FailureCode.StalePlan, stale.Failure.Code);
    }
}
