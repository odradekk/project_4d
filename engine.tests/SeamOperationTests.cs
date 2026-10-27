using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;
using static FourD.Tests.SeamScenes;

namespace FourD.Tests;

// Expected operations that cross seams: the seam and carry overrides of their setup, and the crossing step. An
// operation starts from its own copy of the world, so nothing it does is seen in the loaded world.
public class SeamOperationTests
{
    private static ExpectedSetup Setup(SeamOverride[]? seams = null, CarrySetup[]? carry = null) => new([], seams ?? [], [], carry ?? []);

    private static ExpectedCrossing Crossing(CrossSeamRequest request, FailureCode? expected = null) => new(request, expected);

    private static WorldDescription WithOperation(ExpectedSetup setup, ExpectedStep[] steps, params EntityDefinition[] entities) =>
        Description(entities) with
        {
            Seams = [Seam()],
            ExpectedOperations = [new(new("operation"), TestSource, setup, steps)]
        };

    private static IReadOnlyList<LoadError> Errors(ExpectedSetup setup, ExpectedStep[] steps, params EntityDefinition[] entities) =>
        LoadErrors(WithOperation(setup, steps, entities));

    [Fact]
    public void ACrossingStepThatEndsAsExpectedLoads()
    {
        var succeeds = Crossing(Request());
        var refused = Crossing(Request(hasPrism: false, target: "I"), FailureCode.RequiresPrism);

        Load(WithOperation(Setup(), [succeeds, refused], Actor(x: .1)));
    }

    [Fact]
    public void ACrossingStepThatDoesNotEndAsExpectedIsReportedWithItsStep()
    {
        var errors = Errors(Setup(), [Crossing(Request()), Crossing(Request(target: "II"), FailureCode.SeamClosed)], Actor(x: .1));

        var error = Assert.Single(errors);
        Assert.Equal(LoadErrorCode.ExpectedOperationFailed, error.Code);
        Assert.Equal(1, error.StepIndex);
        Assert.Equal(new OperationId("operation"), error.Operation);
    }

    [Fact]
    public void ACrossingThatWasExpectedToSucceedButIsRefusedIsReported()
    {
        var errors = Errors(Setup(), [Crossing(Request(hasPrism: false))], Actor(x: .1));

        Assert.Equal(0, Assert.Single(errors).StepIndex);
    }

    [Fact]
    public void ASeamOverrideDecidesWhetherTheSeamIsClosedOrSeveredForThatOperationOnly()
    {
        var closed = new SeamOverride(new("seam"), Open: false, Severed: false);
        var severed = new SeamOverride(new("seam"), Open: true, Severed: true);
        var description = Description([Actor(x: .1)]) with
        {
            Seams = [Seam()],
            ExpectedOperations =
            [
                new(new("closed"), TestSource, Setup([closed]), [Crossing(Request(), FailureCode.SeamClosed)]),
                new(new("severed"), TestSource, Setup([severed]), [Crossing(Request(), FailureCode.SeamSevered)])
            ]
        };

        var world = Load(description);

        Assert.Equal(new SeamState(new("seam"), true, false), Assert.IsType<SeamReadResult.Found>(world.ReadSeam(new("seam"))).Seam);
    }

    [Fact]
    public void ASeamOverrideNeedsAKnownSeamAndMayNotRepeatOne()
    {
        var unknown = Errors(Setup([new(new("none"), true, false)]), [], Actor(x: .1));
        var twice = Errors(Setup([new(new("seam"), true, false), new(new("seam"), false, false)]), [], Actor(x: .1));

        Assert.Contains(unknown, error => error.Code == LoadErrorCode.MissingReference && error.Source.Property.EndsWith(".Setup.Seams"));
        Assert.Contains(twice, error => error.Code == LoadErrorCode.DuplicateId && error.Source.Property.EndsWith(".Setup.Seams"));
    }

    [Fact]
    public void ACarrySetupPutsTheItemInTheHandOrTheBackpackBeforeTheSteps()
    {
        // The rod in the hand stands up in the seam, so it needs no slot; the nut in the backpack crosses without one.
        var hand = new CarrySetup(ActorId, RodId, CarryLocation.Hand);
        var backpack = new CarrySetup(ActorId, NutId, CarryLocation.Backpack);
        var steps = new ExpectedStep[] { Crossing(Request(slots: 0)) };

        Load(WithOperation(Setup(carry: [hand, backpack]), steps, Rod(), Nut(At(.1, .03, 0)), Actor(x: .1)));
    }

    [Fact]
    public void ACarrySetupIsInForceForTheSteps()
    {
        // The chair is too big for the backpack, so holding it makes the crossing fail.
        var chair = new CarrySetup(ActorId, new("chair"), CarryLocation.Hand);

        Load(WithOperation(Setup(carry: [chair]), [Crossing(Request(), FailureCode.NotPackable)], Chair(), Actor(x: .1)));
    }

    [Fact]
    public void ACarrySetupTheWorldCannotMakeIsAnInvalidCarry()
    {
        // A ball never goes into a backpack.
        var ball = new CarrySetup(ActorId, BallId, CarryLocation.Backpack);

        var errors = Errors(Setup(carry: [ball]), [], Ball(), Actor(x: .1));

        var error = Assert.Single(errors);
        Assert.Equal(LoadErrorCode.InvalidCarry, error.Code);
        Assert.Equal(BallId, error.Entity);
        Assert.EndsWith(".Setup.Carry", error.Source.Property);
    }

    [Fact]
    public void ACarrySetupNeedsKnownEntitiesAndOneHolderPerItem()
    {
        var unknown = Errors(Setup(carry: [new(ActorId, new("none"), CarryLocation.Hand)]), [], Actor(x: .1));
        var noActor = Errors(Setup(carry: [new(new("none"), RodId, CarryLocation.Hand)]), [], Rod(), Actor(x: .1));
        var twice = Errors(
            Setup(carry: [new(ActorId, RodId, CarryLocation.Hand), new(ActorId, RodId, CarryLocation.Backpack)]), [], Rod(), Actor(x: .1));

        Assert.Contains(unknown, error => error.Code == LoadErrorCode.MissingReference);
        Assert.Contains(noActor, error => error.Code == LoadErrorCode.MissingReference);
        Assert.Contains(twice, error => error.Code == LoadErrorCode.DuplicateId);
    }
}
