using static FourD.Tests.DeviceScenes;
using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// Turning and sliding devices: where they stop, what they report, and the prologue's cabinet and window.
public class DeviceMotionTests
{
    private static readonly SectionId I = new("I");
    private static readonly SectionId II = new("II");

    private static void LayDownRod(World world)
    {
        var plan = world.AllowedPlan(new LayDown(new("actor"), RodId));
        var started = Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan));
        world.StepEvents(started.Duration);
    }

    [Fact]
    public void OpeningTheFlapLowersThePlankOnTheSameHingeAtTheSameTime()
    {
        var world = Scene(Cabinet());

        var driven = world.Driven(CabinetId, Math.PI / 2);

        Assert.Equal(Math.PI / 2, driven.Position);
        Assert.Empty(driven.Contacts);
        Assert.Equal(Math.PI / 2, world.State(CabinetId).DevicePosition);
        // Both leaves hang from the hinge at y = 2 and now point along +x, the flap .4 m long and the plank 1.2 m.
        var flap = world.SliceOf(CabinetId, I)!;
        AssertRange(flap.Range(Axis3.X), 0, .4);
        AssertRange(flap.Range(Axis3.Y), 1.99, 2.01);
        AssertRange(flap.Range(Axis3.Z), -.3, .3);
        var plank = world.SliceOf(CabinetId, II)!;
        AssertRange(plank.Range(Axis3.X), 0, 1.2);
        AssertRange(plank.Range(Axis3.Y), 1.99, 2.01);
        AssertRange(plank.Range(Axis3.Z), -.1, .1);
    }

    [Fact]
    public void ADeviceStartsAtItsInitialPosition()
    {
        var world = Scene(Cabinet(initial: .3));

        Assert.Equal(.3, world.State(CabinetId).DevicePosition);
        // The flap's far lower corner (.01, -.4) about the hinge.
        AssertRange(world.SliceOf(CabinetId, I)!.Range(Axis3.X), -.01 * Math.Cos(.3), .01 * Math.Cos(.3) + .4 * Math.Sin(.3));
    }

    [Fact]
    public void ThePoseOfARotatedDeviceIsTheTurnAboutItsPivot()
    {
        var cabinet = Cabinet() with { Motion = new DeviceRotation(RotationPlane.XY, new(.5, 0, 0, 0), new(0, Math.PI / 2), 0) };
        var world = Scene(cabinet);

        world.Driven(CabinetId, Math.PI / 2);

        // A quarter turn about (.5, 2) takes the point (.5, 2) to itself and the local origin (0, 2) to (.5, 1.5).
        var state = world.State(CabinetId);
        AssertNear(new(.5, 1.5, 0, 0), state.Pose.Position);
        AssertNear(new(0, 1, 0, 0), state.Pose.Rotation.X);
        AssertNear(new(-1, 0, 0, 0), state.Pose.Rotation.Y);
        var flap = world.SliceOf(CabinetId, I)!;
        AssertRange(flap.Range(Axis3.X), .5, .9);
        AssertRange(flap.Range(Axis3.Y), 1.49, 1.51);
    }

    [Fact]
    public void ARotationIsBlockedByAnObstacleThatIsInAnotherSection()
    {
        // A post that exists only from W = .3, so the flap in section I never meets it but the plank does.
        var post = Block("post", new(.7, 2, 0), new(.1, .4, 1), new Interval(.3, .6));
        var world = Scene(Cabinet(), post);

        Assert.Empty(world.Driven(CabinetId, Math.PI / 4).Contacts);
        var failure = world.Refused(CabinetId, Math.PI / 2);

        Assert.Equal(FailureCode.RotationBlocked, failure.Code);
        Assert.Equal(CabinetId, failure.Entity);
        var contact = Assert.Single(failure.Contacts);
        Assert.Equal(CabinetId, contact.Moving);
        Assert.Equal(new PartId("leaf"), contact.MovingPart);
        Assert.Equal(new EntityId("post"), contact.Obstacle);
        Assert.InRange(contact.Point.X, .6, .8);
        Assert.InRange(contact.Point.W, .3, .62);
    }

    [Fact]
    public void ARefusedRotationChangesNothing()
    {
        var post = Block("post", new(.7, 2, 0), new(.1, .4, 1), new Interval(.3, .6));
        var world = Scene(Cabinet(), Actor(), post, Rod());
        var plan = world.AllowedPlan(new StandUp(new("actor"), RodId, II));
        var before = world.State(CabinetId);

        world.Refused(CabinetId, Math.PI / 2);

        // The earlier plan still starts, so the world has the same revision, and no event of the device was queued.
        Assert.Equal(before, world.State(CabinetId));
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan));
        var events = world.StepEvents(.1).Select(occurrence => occurrence.Event);
        Assert.Equal(new SpaceEvent[] { new RotationStarted(RodId) }, events);
    }

    [Fact]
    public void ARotationIntoWIsCheckedInTheSectionsItReaches()
    {
        // A wall in W from .25 to .5. The board's far end reaches W = .3 sin + .1 cos as it turns toward W.
        var wall = Block("wall", new(0, 1, 0), new(2, 1, 2), new Interval(.25, .5));
        var world = Scene(Board(), wall);

        Assert.Empty(world.Driven(BoardId, .5).Contacts);
        var failure = world.Refused(BoardId, .7);

        Assert.Equal(FailureCode.RotationBlocked, failure.Code);
        Assert.Equal(new EntityId("wall"), Assert.Single(failure.Contacts).Obstacle);
    }

    [Fact]
    public void ARotationDoesNotSkipASmallObstacleOnItsWay()
    {
        // A 4 cm cube at the plank's tip where the plank hangs at 45°. The tip is 1.2 m from the hinge, so the turn
        // passes the cube within about 2° of its arc.
        var tip = new Vec3(1.2 * Math.Sin(Math.PI / 4), 2 - 1.2 * Math.Cos(Math.PI / 4), 0);
        var world = Scene(Cabinet(), Block("cube", tip, new(.02, .02, .1), new Interval(.3, .6)));

        Assert.Empty(world.Driven(CabinetId, .7).Contacts);
        Assert.Equal(FailureCode.RotationBlocked, world.Refused(CabinetId, Math.PI / 2).Code);
    }

    [Fact]
    public void ARotationIsCheckedAtItsFinalPose()
    {
        // The plank ends level at y = 2 and 1 cm thick there. A block whose underside is at 2.0045 is met by 5.5 mm
        // only in that last pose, and by about 1 mm one sample before it.
        var world = Scene(Cabinet(), Block("lid", new(.55, 2.1045, 0), new(.05, .1, 1), new Interval(.3, .6)));

        Assert.Empty(world.Driven(CabinetId, 1.5).Contacts);
        Assert.Equal(FailureCode.RotationBlocked, world.Refused(CabinetId, Math.PI / 2).Code);
    }

    [Fact]
    public void ARotationThatStartsDeeperInAnObstacleThanTheToleranceIsRefused()
    {
        var world = Scene(Cabinet(), Block("clamp", new(0, 1.5, 0), new(.05, .05, .5), new Interval(.4, .6)));

        var failure = world.Refused(CabinetId, .1);

        Assert.Equal(FailureCode.RotationBlocked, failure.Code);
        Assert.Equal(new EntityId("clamp"), Assert.Single(failure.Contacts).Obstacle);
    }

    [Fact]
    public void ARotationInProgressReservesItsSweepAgainstARotatingDevice()
    {
        // A pencil lying on a shelf the plank can reach. Standing, its pillar through W crosses the plank's way.
        var pencil = Rod() with { Pose = At(1.12, 1.5, 0) };
        Assert.Empty(Scene(Cabinet(), Actor(), pencil).Driven(CabinetId, Math.PI / 2).Contacts);

        var world = Scene(Cabinet(), Actor(), pencil);
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(world.AllowedPlan(new StandUp(new("actor"), RodId, II))));
        var failure = world.Refused(CabinetId, Math.PI / 2);

        Assert.Equal(FailureCode.RotationBlocked, failure.Code);
        Assert.Equal(RodId, Assert.Single(failure.Contacts).Obstacle);
    }

    [Fact]
    public void AWindowSashStopsAtAPencilStandingInItsRail()
    {
        var world = Scene(Rod(), Actor(), Sash());
        world.Stand(new("actor"), RodId, II);

        var driven = world.Driven(SashId, 1);

        // The sash's right edge starts at x = -.6 and meets the standing pencil's plate, whose left face is at
        // x = -.22 - ε.
        Assert.Equal(-.226 + .6, driven.Position, 9);
        var contact = Assert.Single(driven.Contacts);
        Assert.Equal(SashId, contact.Moving);
        Assert.Equal(RodId, contact.Obstacle);
        Assert.Equal(-.226, contact.Point.X, 9);
        AssertNear(new(-1, 0, 0, 0), contact.Normal);
        Assert.Equal(driven.Position, world.State(SashId).DevicePosition);
        Assert.Equal(driven.Position, world.State(SashId).Pose.Position.X, 9);
    }

    [Fact]
    public void AWindowSashClosesOnceThePencilLiesDown()
    {
        var world = Scene(Rod(), Actor(), Sash());
        world.Stand(new("actor"), RodId, II);
        Assert.NotEmpty(world.Driven(SashId, 1).Contacts);

        LayDownRod(world);
        var closed = world.Driven(SashId, 1);

        Assert.Equal(1, closed.Position);
        Assert.Empty(closed.Contacts);
    }

    [Fact]
    public void AWindowSashPassesAPencilThatLiesOnTheSill()
    {
        var world = Scene(Rod(), Actor(), Sash());

        var driven = world.Driven(SashId, 1);

        Assert.Equal(1, driven.Position);
        Assert.Empty(driven.Contacts);
    }

    [Fact]
    public void ATranslationIsBlockedByAnObstacleThatIsInAnotherSection()
    {
        var post = Block("post", new(-.3, .5, 0), new(.05, .3, .5), new Interval(.3, .6));
        var world = Scene(Sash(), post);

        var driven = world.Driven(SashId, 1);

        // The sash's right edge, at -.6, meets the post's left face at -.35.
        Assert.Equal(.25, driven.Position, 9);
        Assert.Equal(new EntityId("post"), Assert.Single(driven.Contacts).Obstacle);
    }

    [Fact]
    public void ATrackThatRunsAlongNegativeXStopsAtTheObstacleOnItsSide()
    {
        var turnedAboutY = new Rotation4(new(-1, 0, 0, 0), new(0, 1, 0, 0), new(0, 0, -1, 0), new(0, 0, 0, 1));
        var sash = Sash() with { Pose = new(default, turnedAboutY) };
        var post = Block("post", new(.325, 0, 0), new(.025, 1, 1), new Interval(-.5, 1));
        var world = Scene(sash, post);

        var driven = world.Driven(SashId, 1);

        // The pane now spans x from .6 to 1 and slides toward -x until its left edge meets the post at .35.
        Assert.Equal(.25, driven.Position, 9);
        AssertNear(new(1, 0, 0, 0), Assert.Single(driven.Contacts).Normal);
        Assert.Equal(.35, world.SliceOf(SashId, I)!.Range(Axis3.X).Min, 9);
    }

    [Fact]
    public void ALiftStopsUnderAnObjectAboveIt()
    {
        var world = Scene(Lift(), Crate(1.4));

        var driven = world.Driven(LiftId, .5);

        // The deck's top, at y = 1, rises to the crate's underside at 1.3.
        Assert.Equal(.3, driven.Position, 9);
        var contact = Assert.Single(driven.Contacts);
        Assert.Equal(CrateId, contact.Obstacle);
        AssertNear(new(0, -1, 0, 0), contact.Normal);
    }

    [Fact]
    public void ADeviceThatCannotMoveAtAllIsRefused()
    {
        var world = Scene(Lift(), Crate(1.1));

        var failure = world.Refused(LiftId, .5);

        Assert.Equal(FailureCode.TranslationBlocked, failure.Code);
        Assert.Equal(CrateId, Assert.Single(failure.Contacts).Obstacle);
        Assert.Equal(0, world.State(LiftId).DevicePosition);
    }

    [Fact]
    public void DrivingToTheCurrentPositionSucceedsEvenNextToAnObstacle()
    {
        var world = Scene(Lift(), Crate(1.1));

        Assert.Equal(0, world.Driven(LiftId, 0).Position);
    }

    [Fact]
    public void ADriveReportsItsStartItsStopAndItsEndInOrder()
    {
        var post = Block("post", new(-.3, .5, 0), new(.05, .3, .5), new Interval(.3, .6));
        var world = Scene(Sash(), post);
        var driven = world.Driven(SashId, 1);

        var events = world.StepEvents(.1);

        Assert.Equal(3, events.Count);
        Assert.Equal(new DeviceMotionStarted(SashId, 0, 1), events[0].Event);
        var blocked = Assert.IsType<MovementBlocked>(events[1].Event);
        Assert.Equal(SashId, blocked.Entity);
        Assert.Equal(driven.Contacts, blocked.Contacts);
        Assert.Equal(new DeviceMotionEnded(SashId, driven.Position), events[2].Event);
        Assert.All(events, occurrence => Assert.Equal(0, occurrence.Time));
        Assert.True(events[0].Sequence < events[1].Sequence && events[1].Sequence < events[2].Sequence);
        Assert.Empty(world.StepEvents(.1));
    }

    [Fact]
    public void ADriveThatIsNotStoppedReportsOnlyItsStartAndEnd()
    {
        var world = Scene(Cabinet());
        world.Driven(CabinetId, 1);

        var events = world.StepEvents(.1).Select(occurrence => occurrence.Event).ToArray();

        Assert.Equal(new SpaceEvent[] { new DeviceMotionStarted(CabinetId, 0, 1), new DeviceMotionEnded(CabinetId, 1) }, events);
    }
}
