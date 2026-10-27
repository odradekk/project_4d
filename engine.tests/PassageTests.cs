using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;
using static FourD.Tests.SeamScenes;

namespace FourD.Tests;

// Whether there is room for the creature at both ends of a crossing. The creature has a passage radius of .2 m, a
// height of 1.6 m and W thickness of 2ε; the seam is at x = 0, so a creature at x = .15 reaches x = .35 on landing.
public class PassageTests
{
    [Fact]
    public void AWallThatTheCreatureWouldEmbedInAtTheLandingRefusesTheCrossing()
    {
        // The wall's face is at x = .3, .05 m inside the creature's reach.
        var world = SeamScene(Seam(), WallFrom(.3, "II"), Actor(x: .15));

        var failure = world.Refuse(Request(), FailureCode.PassageOccupied, ActorId);

        var contact = Assert.Single(failure.Contacts);
        Assert.Equal(new EntityId("wall"), contact.Obstacle);
        Assert.Equal(ActorId, contact.Moving);
        Assert.Equal(II, contact.Section);
        Assert.Equal(new SeamId("seam"), failure.Seam);
    }

    [Fact]
    public void TheSpaceIsCheckedWhereTheCreatureStandsAndNotAtTheSeamCentre()
    {
        var wall = WallFrom(.3, "II");
        var offset = SeamScene(Seam(), wall, Actor(x: .15));
        var centred = SeamScene(Seam(), wall, Actor(x: 0));

        offset.Refuse(Request(), FailureCode.PassageOccupied, ActorId);
        centred.Cross(Request());
    }

    [Theory]
    [InlineData(.35, true)]
    [InlineData(.346, true)]
    [InlineData(.344, false)]
    public void ASurfaceThatOnlyTouchesWithinTheContactToleranceDoesNotBlock(double wallEdge, bool crosses)
    {
        var world = SeamScene(Seam(), WallFrom(wallEdge, "II"), Actor(x: .15));

        if (crosses) world.Cross(Request());
        else world.Refuse(Request(), FailureCode.PassageOccupied, ActorId);
    }

    [Fact]
    public void AMirrorSeamChecksTheReflectedLandingNotTheUnreflectedOne()
    {
        // The creature at x = .15 lands at x = -.15 and reaches from -.35 to .05.
        var ahead = SeamScene(Seam(mirrored: true), WallFrom(.3, "II"), Actor(x: .15));
        var behind = SeamScene(Seam(mirrored: true), WallUpTo(-.3, "II"), Actor(x: .15));

        ahead.Cross(Request());
        behind.Refuse(Request(), FailureCode.PassageOccupied, ActorId);
    }

    [Fact]
    public void ARoomThatIsTooTightAtTheDepartureEndRefusesTheCrossingToo()
    {
        var wall = WallFrom(.3, "I");
        var near = SeamScene(Seam(), wall, Actor(x: .15));
        var away = SeamScene(Seam(), wall, Actor(x: 0));

        var failure = near.Refuse(Request(), FailureCode.PassageOccupied, ActorId);
        away.Cross(Request());

        Assert.Equal(I, Assert.Single(failure.Contacts).Section);
    }

    [Fact]
    public void SomethingThatIsOnlyBetweenTheSectionsDoesNotBlock()
    {
        var between = Block("between", new(0, .8, 0), new(.5, .8, .5), new(.1, .3));
        var world = SeamScene(Seam(), between, Actor(x: .15));

        world.Cross(Request());
    }

    [Fact]
    public void TheSweepOfARotationInProgressBlocksTheLanding()
    {
        // The rod lies at x from -.64 to -.16 in section I, clear of the creature's reach at x = .15 (from -.05 to
        // .35), with its hinge at its right end, x = -.18. Standing up toward II it becomes a plate at x = -.18 that
        // spans W up to .46, which is inside the landing reach of the creature mirrored to x = -.15 (from -.35 to .05).
        var yawedHalfTurn = Identity4 with { X = new(-1, 0, 0, 0), Z = new(0, 0, -1, 0) };
        var rod = Rod() with { Pose = At(-.4, 0, 0, rotation: yawedHalfTurn) };
        var idle = SeamScene(Seam(mirrored: true), rod, Actor(x: .15));
        var standing = SeamScene(Seam(mirrored: true), rod, Actor(x: .15));
        standing.StartRotation(standing.AllowedPlan(new StandUp(ActorId, RodId, II)));
        standing.StepEvents(.001);

        idle.Cross(Request());
        var failure = standing.Refuse(Request(), FailureCode.PassageOccupied, ActorId);

        Assert.Equal(RodId, Assert.Single(failure.Contacts).Obstacle);
    }
}
