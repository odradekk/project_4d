using static FourD.Tests.Fixtures;

namespace FourD.Tests;

// The rotation tests span several files; this one holds the scenes they share.
public partial class RotationTests
{
    // Durations fixed by the engine rules (docs/frame/contract.md).
    private const double StandUpSeconds = 1.4;
    private const double LayDownSeconds = 1.0;
    private const double FlipInPlaceSeconds = 1.2;
    private const double FlipAroundObstacleSeconds = 1.4;

    private static readonly SectionDefinition SectionI = new(new("I"), 0, TestSource);
    private static readonly SectionDefinition SectionII = new(new("II"), .42, TestSource);

    private static readonly ExpectedSetup NoSetup = new([], [], [], []);

    // A 1 m rod in section I. Its hinge sits 2 cm inside its left end, so it reaches .98 m.
    private static readonly TurnableDefinition LongRod = new(
        new("item"), Origin, TestSource, new("I"), new("b"), Box(new(.5, .03, .03)),
        RodProperties(radius: .5, hingeX: -.48, reach: .98));

    // The 0.48 m rod from the size norms: a hinge 2 cm inside the end leaves .46 m of reach,
    // enough to enter section II, which is .42 m away.
    private static readonly TurnableDefinition ShortRod = new(
        new("item"), Origin, TestSource, new("I"), new("b"), Box(new(.24, .03, .03)),
        RodProperties(radius: .24, hingeX: -.22, reach: .46));

    // The same rod with its origin at the left end, so that the hinge is at x = .02.
    private static readonly TurnableDefinition OffsetRod = ShortRod with
    {
        Shape = Shape(Part("body", new(.24, .03, .03), center: new(.24, 0, 0))),
        Interaction = RodProperties(radius: .24, hingeX: .02, reach: .46),
    };

    private static TurnableProperties RodProperties(double radius, double hingeX, double reach) => new(
        Holdable: true, Packable: false, HorizontalRadius: radius, LongAxis: Axis3.X, Hinge: new(hingeX, 0, 0), Reach: reach);

    // LongRod turned about its hinge at x = -.48 toward +W, by the angle with the given cosine and sine.
    private static Pose4 TurnedAboutHinge(double cos, double sin) =>
        At(-.48 + .48 * cos, 0, 0, .48 * sin, XwRotation(cos, sin));

    private static Pose4 LongRodTurnedOneDegree()
    {
        var angle = Math.PI / 180;
        return TurnedAboutHinge(Math.Cos(angle), Math.Sin(angle));
    }

    private static Creature3Definition Actor(Pose4 pose, string section = "I") =>
        new(new("actor"), pose, TestSource, new(section), PassageRadius: .2, PassageHeight: 1.6, CanUseSeamsWithoutPrism: false);

    // A 4 cm thick wall that is 1 m high and wide, present in a single section.
    private static FixedDefinition ThinWall(Pose4 pose, string section = "I") =>
        new(new("wall"), pose, TestSource, new(section), Shape(Part("part", new(.02, .5, .5))));

    // A 4 cm cube at x = .2 (and offset z), present only for W in [.34, .36]. The rod reaches that W only
    // midway through standing up, so neither end of the path touches the block.
    private static BuildingElementDefinition MidwayBlock(double z = 0) => new(
        new("wall"), Origin, TestSource, new("b"), new(.34, .36), [],
        Shape(Part("block", new(.02, .02, .02), center: new(.2, 0, z))));

    // One building "b" that covers W in [-2, 2].
    private static WorldDescription Description(SectionDefinition[] sections, params EntityDefinition[] entities) => new(
        sections, [new(new("b"), new(-2, 2), TestSource)], entities, [], [], []);

    // The long rod in section I with the actor beside it, in a world with sections I and II.
    private static (World World, TurnableDefinition Item, Creature3Definition Actor) Scene(params EntityDefinition[] obstacles)
    {
        var actor = Actor(At(0, 1, 1));
        EntityDefinition[] entities = [.. obstacles, LongRod, actor];
        return (Load(Description([SectionI, SectionII], entities)), LongRod, actor);
    }

    [Fact]
    public void CreatureLoadsAsAnActorWithoutFourDimensionalGeometry()
    {
        var actor = Actor(At(1, 2, 3));
        var world = Load(new([SectionI], [], [actor], [], [], []));

        var state = world.State(actor.Id);
        Assert.Equal(actor.Pose, state.Pose);
        Assert.Equal(new SectionId("I"), state.Section);
        Assert.Empty(world.SlicesIn(new("I")));
    }

    [Fact]
    public void StandUpRotatesAroundHingeAndDeliversTimedEvents()
    {
        var actor = Actor(At(0, 1, 1));
        var building = new BuildingDefinition(new("b"), new(-1, 1), TestSource);
        var world = Load(new([SectionI, SectionII], [building], [OffsetRod, actor], [], [], []));
        var plan = world.AllowedPlan(new StandUp(actor.Id, OffsetRod.Id, new("II")));
        Assert.Equal(StandUpSeconds, Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);

        Assert.IsType<RotationStarted>(Assert.Single(world.StepEvents(1)).Event);
        var ended = Assert.Single(world.StepEvents(.5));
        Assert.Equal(StandUpSeconds, ended.Time, 10);
        Assert.IsType<RotationEnded>(ended.Event);

        var pose = world.State(OffsetRod.Id).Pose;
        Assert.Equal(.02, pose.Position.X, 10);
        Assert.Equal(-.02, pose.Position.W, 10);
        Assert.Equal(1, pose.Rotation.X.W, 10);
    }

    [Fact]
    public void LayDownIsTheInverseOfStandUp()
    {
        var (world, item, actor) = Scene();
        var standUp = world.AllowedPlan(new StandUp(actor.Id, item.Id, new("II")));
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(standUp));
        world.Step(StandUpSeconds);
        var layDown = world.AllowedPlan(new LayDown(actor.Id, item.Id));
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(layDown));
        world.Step(LayDownSeconds);

        var state = world.State(item.Id);
        Assert.Equal(item.Pose.Position.X, state.Pose.Position.X, 10);
        Assert.Equal(item.Pose.Position.W, state.Pose.Position.W, 10);
        Assert.Equal(item.Pose, state.Pose);
        Assert.Equal(new SectionId("I"), state.Section);
        Assert.Null(state.UprightOrigin);
    }

    [Fact]
    public void MidwayCollisionBlocksEvenWhenBothEndsAreClear()
    {
        var (world, item, actor) = Scene(MidwayBlock());

        var blocked = Assert.IsType<RotationPlanResult.Blocked>(world.PlanRotation(new StandUp(actor.Id, item.Id, new("II"))));
        Assert.Equal(FailureCode.RotationBlocked, blocked.Failure.Code);
        Assert.NotEmpty(blocked.Failure.Contacts);
        var preview = Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(blocked.Plan, new("II"))).Value;
        Assert.Equal(0, preview.Samples[0].Progress);
        Assert.Equal(1, preview.Samples[^1].Progress);
        Assert.Contains(preview.Samples, s => s.Blocked);
        Assert.False(preview.Samples[0].Blocked);
        var rejected = Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(blocked.Plan));
        Assert.Equal(FailureCode.RotationBlocked, rejected.Failure.Code);
        Assert.Equal(item.Pose, world.State(item.Id).Pose);
    }

    [Fact]
    public void PlansAreBoundToWorldAndRevision()
    {
        var (world, item, actor) = Scene();
        var (other, _, _) = Scene();
        var plan = world.AllowedPlan(new StandUp(actor.Id, item.Id, new("II")));

        var foreign = Assert.IsType<StartRotationResult.Rejected>(other.StartRotation(plan));
        Assert.Equal(FailureCode.ForeignPlan, foreign.Failure.Code);
        Assert.IsType<UpdateResult.Updated>(world.SynchronizeActor(actor.Id, new(2, 1, 1), .3));
        var stale = Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(plan));
        Assert.Equal(FailureCode.StalePlan, stale.Failure.Code);
        var stalePreview = Assert.IsType<PreviewResult.Rejected>(world.GetRotationPreview(plan, new("I")));
        Assert.Equal(FailureCode.StalePlan, stalePreview.Failure.Code);
    }

    [Fact]
    public void StartEasingUsesFiveDegreesAtOneQuarterOfDuration()
    {
        var (world, item, actor) = Scene();
        var plan = world.AllowedPlan(new StandUp(actor.Id, item.Id, new("II")));
        world.StartRotation(plan);
        world.Step(.35);

        var state = world.State(item.Id);
        Assert.Equal(Math.Sin(Math.PI / 36), state.Pose.Rotation.X.W, 10);
        Assert.True(state.Rotating);
    }

    [Fact]
    public void FlipInPlaceChangesProjectedHandedness()
    {
        var (world, item, actor) = Scene();
        var plan = world.AllowedPlan(new Flip(actor.Id, item.Id));
        Assert.Equal(FlipInPlaceSeconds, Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);
        world.Step(FlipInPlaceSeconds);

        Assert.Equal(Handedness.Mirrored, Assert.IsType<HandednessResult.Value>(world.ReadHandedness(item.Id)).Handedness);
        var comparison = Assert.IsType<HandednessComparisonResult.Comparison>(world.CompareHandedness(actor.Id, item.Id));
        Assert.Equal(HandednessComparison.Different, comparison.Value);
    }

    [Fact]
    public void ExpectedRotationsUseIndependentWorldsAndDoNotChangeLoadedWorld()
    {
        var (_, item, actor) = Scene();
        var standUp = new ExpectedRotation(new StandUp(actor.Id, item.Id, new("II")), null);
        var operations = new[]
        {
            new ExpectedOperation(new("one"), TestSource, NoSetup, [standUp]),
            new ExpectedOperation(new("two"), TestSource, NoSetup, [standUp, new ExpectedAdvance(.1)]),
        };
        var loaded = Load(Description([SectionI, SectionII], item, actor) with { ExpectedOperations = operations });

        Assert.Equal(item.Pose, loaded.State(item.Id).Pose);
        Assert.Empty(loaded.StepEvents(.1));
    }

    [Fact]
    public void UnsupportedExpectedStepIsReportedAtItsIndex()
    {
        var (_, item, actor) = Scene();
        var move = new ExpectedMove(new MoveRequest(item.Id, default), default, null);
        var operation = new ExpectedOperation(new("unsupported"), TestSource, NoSetup, [move]);

        var errors = LoadErrors(Description([SectionI], item, actor) with { ExpectedOperations = [operation] });

        Assert.Contains(errors, e =>
            e.Code == LoadErrorCode.UnsupportedDefinition && e.Operation == operation.Id && e.StepIndex == 0);
    }
}
