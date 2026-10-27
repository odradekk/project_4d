using static FourD.Tests.DeviceScenes;
using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// Expected operations that drive devices: the device step with the position it names, and the replacement of a device
// in the setup. An operation starts from its own copy of the world, so nothing it does is seen in the loaded world.
public class DeviceOperationTests
{
    private static readonly EntityId ActorId = new("actor");
    private static readonly SectionId II = new("II");

    private static ExpectedSetup Setup(params EntityDefinition[] replacements) => new(replacements, [], [], []);

    private static ExpectedDevice Drive(EntityId device, double target, double expectedPosition, FailureCode? failure = null) =>
        new(device, target, expectedPosition, failure);

    private static WorldDescription WithOperation(ExpectedSetup setup, ExpectedStep[] steps, params EntityDefinition[] entities) =>
        Description(entities) with { ExpectedOperations = [new(new("operation"), TestSource, setup, steps)] };

    private static IReadOnlyList<LoadError> Errors(ExpectedSetup setup, ExpectedStep[] steps, params EntityDefinition[] entities) =>
        LoadErrors(WithOperation(setup, steps, entities));

    [Fact]
    public void TheWindowOfTheClassroomIsAnExpectedOperation()
    {
        // The pencil stands in the rail, which stops the sash short; laid down again, the sash closes.
        ExpectedStep[] steps =
        [
            new ExpectedRotation(new StandUp(ActorId, RodId, II), null),
            Drive(SashId, 1, .374),
            new ExpectedRotation(new LayDown(ActorId, RodId), null),
            Drive(SashId, 1, 1)
        ];

        Load(WithOperation(Setup(), steps, Rod(), Actor(), Sash()));
    }

    [Fact]
    public void TheCabinetCanBeOpenedBeforeAndBlockedAfterAnObstacle()
    {
        var post = Block("post", new(.7, 2, 0), new(.1, .4, 1), new Interval(.3, .6));
        ExpectedStep[] steps = [Drive(CabinetId, Math.PI / 4, Math.PI / 4), Drive(CabinetId, Math.PI / 2, 0, FailureCode.RotationBlocked)];

        Load(WithOperation(Setup(), steps, Cabinet(), post));
    }

    [Fact]
    public void ADeviceThatEndsAtAnotherPositionThanTheStepNamesIsReportedWithItsStep()
    {
        ExpectedStep[] steps = [new ExpectedRotation(new StandUp(ActorId, RodId, II), null), Drive(SashId, 1, .5)];

        var error = Assert.Single(Errors(Setup(), steps, Rod(), Actor(), Sash()));

        Assert.Equal(LoadErrorCode.ExpectedOperationFailed, error.Code);
        Assert.Equal(1, error.StepIndex);
        Assert.Equal(SashId, error.Entity);
        Assert.Equal(new OperationId("operation"), error.Operation);
    }

    [Fact]
    public void ThePositionIsComparedWithinANanometre()
    {
        Load(WithOperation(Setup(), [Drive(CabinetId, Math.PI / 2, Math.PI / 2 + 5e-10)], Cabinet()));
        var beyond = Errors(Setup(), [Drive(CabinetId, Math.PI / 2, Math.PI / 2 + 2e-9)], Cabinet());

        Assert.Equal(0, Assert.Single(beyond).StepIndex);
    }

    [Fact]
    public void AStepThatWasToSucceedButIsRefusedIsReportedWithTheContacts()
    {
        var post = Block("post", new(.7, 2, 0), new(.1, .4, 1), new Interval(.3, .6));

        var error = Assert.Single(Errors(Setup(), [Drive(CabinetId, Math.PI / 2, Math.PI / 2)], Cabinet(), post));

        Assert.Equal(LoadErrorCode.ExpectedOperationFailed, error.Code);
        Assert.Equal(new EntityId("post"), Assert.Single(error.Contacts).Obstacle);
    }

    [Fact]
    public void AStepThatWasToFailButSucceedsIsReported()
    {
        var error = Assert.Single(Errors(Setup(), [Drive(CabinetId, Math.PI / 4, 0, FailureCode.RotationBlocked)], Cabinet()));

        Assert.Equal(LoadErrorCode.ExpectedOperationFailed, error.Code);
        Assert.Equal(0, error.StepIndex);
    }

    [Fact]
    public void AStepFailsWithTheCodeOfTheCallItMakes()
    {
        ExpectedStep[] steps = [Drive(SashId, 2, 0, FailureCode.OutOfRange), Drive(RodId, 0, 0, FailureCode.WrongEntityKind), Drive(new("none"), 0, 0, FailureCode.UnknownId)];

        Load(WithOperation(Setup(), steps, Rod(), Sash()));
    }

    [Fact]
    public void AReplacedDeviceStartsWhereTheSetupPutsItForThatOperationOnly()
    {
        // The deck rises to the crate's underside at .3 and is stopped there. From a start at .3 it cannot move at all.
        var atTheCrate = Lift(initial: .3);
        var refused = WithOperation(Setup(atTheCrate), [Drive(LiftId, .5, 0, FailureCode.TranslationBlocked)], Lift(), Crate(1.4));
        var stopped = WithOperation(Setup(), [Drive(LiftId, .5, .3)], Lift(), Crate(1.4));

        var world = Load(refused);
        Load(stopped);

        Assert.Equal(0, world.State(LiftId).DevicePosition);
    }

    [Fact]
    public void ADeviceCanOnlyBeReplacedByADeviceAndTheReplacementIsChecked()
    {
        var notADevice = Errors(Setup(Sash() with { Id = RodId }), [], Rod(), Sash());
        var outOfLimits = Errors(Setup(Sash(initial: 2)), [], Sash());

        Assert.Contains(notADevice, error => error.Code == LoadErrorCode.UnsupportedDefinition && error.Entity == RodId);
        var invalid = Assert.Single(outOfLimits);
        Assert.Equal(LoadErrorCode.InvalidMotion, invalid.Code);
        Assert.Equal(new OperationId("operation"), invalid.Operation);
    }
}
