using static FourD.Tests.Fixtures;

namespace FourD.Tests;

public partial class RotationTests
{
    // Short rod with a marker off its axis, in a world with sections at W = -.2, 0 and .2. One obstacle per
    // blockedW sits in the flip path at that W.
    private static (World World, TurnableDefinition Item, Creature3Definition Actor) FlipScene(params double[] blockedW)
    {
        var item = ShortRod with
        {
            Shape = Shape(
                Part("body", new(.24, .03, .03)),
                Part("marker", new(.03, .03, .03), center: new(.2, 0, .1))),
        };
        var actor = Actor(At(-1, 0, .05));
        var obstacles = blockedW.Select((w, i) => new BuildingElementDefinition(
            new($"obstacle{i}"), Origin, TestSource, new("b"), new(w - .01, w + .01), [],
            Shape(Part("part", new(.02, .02, .02), center: new(0, 0, .1)))));
        EntityDefinition[] entities = [.. obstacles, item, actor];
        var minus = new SectionDefinition(new("minus"), -.2, TestSource);
        var plus = new SectionDefinition(new("plus"), .2, TestSource);
        var building = new BuildingDefinition(new("b"), new(-1, 1), TestSource);
        return (Load(new([minus, SectionI, plus], [building], entities, [], [], [])), item, actor);
    }

    private static Pose4 ShiftedX(Pose4 pose, double dx) =>
        pose with { Position = pose.Position with { X = pose.Position.X + dx } };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlipPrefersPositiveAndUsesNegativeWhenPositiveIsBlocked(bool blockPositive)
    {
        // The actor is at -X of the item, so the positive angle sweeps through negative W.
        var (world, item, actor) = blockPositive ? FlipScene(-.2) : FlipScene();
        var plan = world.AllowedPlan(new Flip(actor.Id, item.Id));
        var target = new SectionId(blockPositive ? "plus" : "minus");
        var preview = Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan, target)).Value;
        Assert.Contains(preview.Samples, s => s.Contours.Count > 0);

        world.StartRotation(plan);
        world.Step(.6);

        var w = world.State(item.Id).Pose.Rotation.X.W;
        Assert.True(blockPositive ? w > 0 : w < 0);
    }

    [Fact]
    public void FlipBlockedInBothDirectionsReturnsThePositivePathWithoutMutation()
    {
        var (world, item, actor) = FlipScene(-.2, .2);

        var blocked = Assert.IsType<RotationPlanResult.Blocked>(world.PlanRotation(new Flip(actor.Id, item.Id)));
        Assert.Equal(new EntityId("obstacle0"), Assert.Single(blocked.Failure.Contacts).Obstacle);
        var preview = Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(blocked.Plan, new("minus"))).Value;
        Assert.Contains(preview.Samples, s => s.Blocked);
        var rejected = Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(blocked.Plan));
        Assert.Equal(FailureCode.RotationBlocked, rejected.Failure.Code);
        Assert.Equal(item.Pose, world.State(item.Id).Pose);
    }

    [Fact]
    public void UprightObjectTranslatedInXReturnsAlongTranslatedInverse()
    {
        var (world, item, actor) = Scene();
        world.StartRotation(world.AllowedPlan(new StandUp(actor.Id, item.Id, new("II"))));
        world.Step(StandUpSeconds);
        var upright = world.State(item.Id).Pose;
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id, ShiftedX(upright, .3)));

        world.StartRotation(world.AllowedPlan(new LayDown(actor.Id, item.Id)));
        world.Step(LayDownSeconds);

        var pose = world.State(item.Id).Pose;
        Assert.Equal(.3, pose.Position.X, 10);
        Assert.Equal(0, pose.Position.W, 10);
    }

    [Fact]
    public void ArbitraryTiltThatCannotReturnAroundItsHingeIsRejected()
    {
        var (world, item, actor) = Scene();
        var c = Math.Sqrt(.5);
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id, At(0, 0, 0, w: .21, rotation: XwRotation(c, c))));

        var rejected = Assert.IsType<RotationPlanResult.Rejected>(world.PlanRotation(new LayDown(actor.Id, item.Id)));
        Assert.Equal(FailureCode.InvalidInput, rejected.Failure.Code);
    }

    [Fact]
    public void FlipAroundObstacleEndsOnActorsSide()
    {
        var item = ShortRod with { Pose = At(1, 0, 0) };
        var actor = Actor(At(-1, 0, 0));
        var world = Load(Description([SectionI], item, actor, ThinWall(Origin)));
        var plan = world.AllowedPlan(new Flip(actor.Id, item.Id));

        Assert.Equal(FlipAroundObstacleSeconds, Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);
        world.Step(FlipAroundObstacleSeconds);

        // The flip plane is the wall's face next to the actor, x = -.02, which mirrors x = 1 to x = -1.04.
        Assert.Equal(-1.04, world.State(item.Id).Pose.Position.X, 10);
    }

    [Fact]
    public void ReservedSweepRejectsPlacementAndKeepsFallingObjectAboveIt()
    {
        var floor = new BuildingElementDefinition(
            new("floor"), Origin, TestSource, new("b"), new(-1, 1), [],
            Shape(Part("part", new(2, .1, 2), center: new(0, -1, 0))));
        var (world, item, actor) = Scene(floor);
        var other = new TurnableDefinition(
            new("other"), At(-.48, .5, 0, w: .42), TestSource, new("II"), new("b"),
            Shape(Part("part", new(.1, .03, .03))), RodProperties(radius: .1, hingeX: -.08, reach: .18));
        Assert.IsType<AddEntityResult.Added>(world.AddEntity(other));
        world.StartRotation(world.AllowedPlan(new StandUp(actor.Id, item.Id, new("II"))));

        var lowered = other.Pose with { Position = other.Pose.Position with { Y = 0 } };
        var rejected = Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(other.Id, lowered));
        Assert.Equal(FailureCode.TranslationBlocked, rejected.Failure.Code);
        world.Step(.3);

        Assert.Equal(.5, world.State(other.Id).Pose.Position.Y, 10);
    }

    [Fact]
    public void ActorSynchronizationPreservesSectionRotationAndRejectsNonfiniteInput()
    {
        var (world, _, actor) = Scene();
        Assert.IsType<UpdateResult.Updated>(world.SynchronizeActor(actor.Id, new(2, 3, 4), 1.2));

        var state = world.State(actor.Id);
        Assert.Equal(new Vec4(2, 3, 4, 0), state.Pose.Position);
        Assert.Equal(Identity4, state.Pose.Rotation);
        Assert.Equal(1.2, state.ActorYaw);
        Assert.Equal(new SectionId("I"), state.Section);
        var rejected = Assert.IsType<UpdateResult.Rejected>(world.SynchronizeActor(actor.Id, new(double.NaN, 0, 0), 0));
        Assert.Equal(FailureCode.InvalidInput, rejected.Failure.Code);
        Assert.Equal(state, world.State(actor.Id));
    }

    [Fact]
    public void ExpectedFailureAndWrongExpectedFailureAreChecked()
    {
        var (_, item, actor) = Scene();
        var layDown = new LayDown(actor.Id, item.Id);
        var failingStep = new ExpectedRotation(layDown, FailureCode.InvalidInput);
        var expected = new ExpectedOperation(new("fail"), TestSource, NoSetup, [failingStep]);
        var description = Description([SectionI], item, actor);
        Assert.IsType<LoadResult.Loaded>(World.Load(description with { ExpectedOperations = [expected] }));

        var wrong = expected with { Steps = [new ExpectedRotation(layDown, FailureCode.Busy)] };

        var errors = LoadErrors(description with { ExpectedOperations = [wrong] });
        Assert.Contains(errors, e =>
            e.Code == LoadErrorCode.ExpectedOperationFailed && e.Operation == expected.Id && e.StepIndex == 0);
    }

    [Fact]
    public void ArbitraryTiltWithAHingeOnOriginCanReturnToItsSection()
    {
        var (world, item, actor) = Scene();
        var c = Math.Sqrt(.5);
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id, TurnedAboutHinge(cos: c, sin: c)));
        var plan = world.AllowedPlan(new LayDown(actor.Id, item.Id));
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan));
        world.Step(1);

        var pose = world.State(item.Id).Pose;
        Assert.Equal(0, pose.Position.X, 10);
        Assert.Equal(0, pose.Position.W, 10);
        Assert.Equal(1, pose.Rotation.W.W, 10);
    }

    [Fact]
    public void ShallowerTargetTurnsTowardNegativeW()
    {
        var item = LongRod with { Section = new("II"), Pose = At(0, 0, 0, .42) };
        var actor = Actor(At(0, 1, 1, .42), section: "II");
        var world = Load(Description([SectionI, SectionII], item, actor));
        world.StartRotation(world.AllowedPlan(new StandUp(actor.Id, item.Id, new("I"))));
        world.Step(StandUpSeconds);

        Assert.Equal(-1, world.State(item.Id).Pose.Rotation.X.W, 10);
    }

    [Theory]
    [InlineData(.044, true)]
    [InlineData(.046, false)]
    public void SweepHonorsFiveMillimeterContactTolerance(double z, bool blocked)
    {
        var (world, item, actor) = Scene(MidwayBlock(z));

        var plan = world.PlanRotation(new StandUp(actor.Id, item.Id, new("II")));

        Assert.Equal(blocked, plan is RotationPlanResult.Blocked);
    }

    [Fact]
    public void InitialSupportContactWithinToleranceDoesNotBlockRotation()
    {
        var floor = new BuildingElementDefinition(
            new("floor"), Origin, TestSource, new("b"), new(-1, 1), [],
            Shape(Part("part", new(2, .1, 2), center: new(0, -.126, 0))));
        var (world, item, actor) = Scene(floor);

        Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id, item.Id, new("II"))));
    }

    [Fact]
    public void InitialSupportEntityStillBlocksLaterMovement()
    {
        var item = ShortRod with { Pose = At(.5, 0, 0) };
        var actor = Actor(At(-1, 0, 0));
        var support = new BuildingElementDefinition(
            new("support"), Origin, TestSource, new("b"), new(-1, 1), [],
            Shape(
                Part("floor", new(2, .1, 2), center: new(0, -.13, 0)),
                Part("wall", new(.02, .1, .1))));
        var world = Load(Description([SectionI], item, actor, support));

        var blocked = Assert.IsType<RotationPlanResult.Blocked>(world.PlanRotation(new Flip(actor.Id, item.Id)));

        Assert.Contains(blocked.Failure.Contacts, c => c.Obstacle == support.Id && c.ObstaclePart == new PartId("wall"));
    }
}
