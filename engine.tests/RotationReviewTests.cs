using static FourD.Tests.Fixtures;

namespace FourD.Tests;

public partial class RotationTests
{
    [Fact]
    public void ShortLayDownMovesUniformlyAlongTheCheckedPathWithoutEnteringWall()
    {
        // The wall lies at negative W, behind the section the rod is laid down into.
        var wall = new BuildingElementDefinition(
            new("wall"), Origin, TestSource, new("b"), new(-.07, -.05), [],
            Shape(Part("part", new(.05, .03, .03), center: new(.4, 0, 0))));
        var (world, item, actor) = Scene(wall);
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id, LongRodTurnedOneDegree()));
        var plan = world.AllowedPlan(new LayDown(actor.Id, item.Id));
        var wallSpace = new OccupancyBox(At(.4, 0, 0, w: -.06), new(.05, .03, .03, .01));
        Assert.Empty(world.OccupancyHits(wallSpace, wall.Id));
        Assert.Equal(LayDownSeconds, Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);

        foreach (var (delta, expectedDegrees) in new[] { (.25, .75), (.25, .5), (.5, 0d) })
        {
            world.Step(delta);

            var rotation = world.State(item.Id).Pose.Rotation;
            Assert.Empty(world.OccupancyHits(wallSpace, wall.Id));
            Assert.Equal(expectedDegrees, Math.Atan2(rotation.X.W, rotation.X.X) * 180 / Math.PI, 9);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RotationEndsAtTheSpecifiedTimeWhenStartedAtNonzeroTime(bool split)
    {
        var (world, item, actor) = Scene();
        world.Step(.7);
        world.StartRotation(world.AllowedPlan(new StandUp(actor.Id, item.Id, new("II"))));
        if (split)
        {
            world.Step(.7);
        }

        var events = world.StepEvents(split ? .7 : StandUpSeconds);

        Assert.Contains(events, e => e.Event is RotationEnded && e.Time == .7 + StandUpSeconds);
        Assert.False(world.State(item.Id).Rotating);
        Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new LayDown(actor.Id, item.Id)));
    }

    [Fact]
    public void ExpectedSequenceCanAdvanceThenStandAndLayDown()
    {
        var (_, item, actor) = Scene();
        var operation = new ExpectedOperation(new("sequence"), TestSource, NoSetup,
        [
            new ExpectedAdvance(.7),
            new ExpectedRotation(new StandUp(actor.Id, item.Id, new("II")), null),
            new ExpectedRotation(new LayDown(actor.Id, item.Id), null),
        ]);

        var description = Description([SectionI, SectionII], item, actor) with { ExpectedOperations = [operation] };

        Assert.IsType<LoadResult.Loaded>(World.Load(description));
    }

    [Fact]
    public void OriginIsReportedThroughoutBothRotationDirections()
    {
        var (world, item, actor) = Scene();
        void AssertUprightFromI()
        {
            var state = world.State(item.Id);
            Assert.Null(state.Section);
            Assert.Equal(new SectionId("I"), state.UprightOrigin);
        }

        world.StartRotation(world.AllowedPlan(new StandUp(actor.Id, item.Id, new("II"))));
        AssertUprightFromI();
        world.Step(.35);
        AssertUprightFromI();
        world.Step(1.05);
        AssertUprightFromI();

        world.StartRotation(world.AllowedPlan(new LayDown(actor.Id, item.Id)));
        AssertUprightFromI();
        world.Step(.5);
        AssertUprightFromI();
        world.Step(.5);

        var final = world.State(item.Id);
        Assert.Equal(new SectionId("I"), final.Section);
        Assert.Null(final.UprightOrigin);
    }

    [Fact]
    public void CreatureHeightMustFitItsDiameterOnLoadAndAdd()
    {
        var (world, _, actor) = Scene();
        var tooLow = actor with { Id = new("bad"), PassageHeight = .1 };
        var fitting = tooLow with { PassageHeight = .4 };
        var description = new WorldDescription([SectionI], [], [tooLow], [], [], []);

        var errors = LoadErrors(description);
        Assert.Contains(errors, e => e.Entity == tooLow.Id && e.Source.Property == "definition.PassageHeight");
        Assert.Equal(FailureCode.InvalidInput, Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(tooLow)).Failure.Code);
        Assert.IsType<LoadResult.Loaded>(World.Load(description with { Entities = [fitting] }));
        Assert.IsType<AddEntityResult.Added>(world.AddEntity(fitting));
    }

    [Fact]
    public void ShortRotationReservesOnlyItsCheckedPath()
    {
        var (_, item, actor) = Scene();
        var before = new SectionDefinition(new("before"), -.06, TestSource);
        var world = Load(Description([before, SectionI, SectionII], item, actor));
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id, LongRodTurnedOneDegree()));
        world.StartRotation(world.AllowedPlan(new LayDown(actor.Id, item.Id)));

        var outside = item with
        {
            Id = new("outside"),
            Section = new("before"),
            Pose = At(.4, 0, 0, w: -.06),
            Shape = Box(new(.1, .03, .03)),
            Interaction = item.Interaction with { Hinge = new(-.08, 0, 0), Reach = .18 },
        };
        Assert.IsType<AddEntityResult.Added>(world.AddEntity(outside));

        var outsideSpace = new OccupancyBox(outside.Pose, new(.1, .03, .03, Epsilon));
        foreach (var delta in new[] { .25, .25, .5 })
        {
            world.Step(delta);

            Assert.Empty(world.OccupancyHits(outsideSpace, outside.Id));
        }
    }

    [Fact]
    public void PreviewSamplesRespectOneCentimeterFarthestSurfaceTravel()
    {
        var (world, item, actor) = Scene();
        var plan = world.AllowedPlan(new StandUp(actor.Id, item.Id, new("II")));

        var preview = Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan, new("II"))).Value;

        // The farthest vertex is sqrt(0.98^2 + 0.006^2) from the hinge in the turn plane.
        // Its quarter-circle arc needs 154 intervals of at most one centimetre.
        Assert.Equal(155, preview.Samples.Count);
        Assert.Equal(1d / 154, preview.Samples[1].Progress, 12);
        Assert.Equal(1, preview.Samples[^1].Progress);
    }

    [Fact]
    public void FlipKeepsItsSectionThroughoutAndRejectedStartDoesNotChangeState()
    {
        var (world, item, actor) = FlipScene();
        var before = world.State(item.Id);
        var plan = world.AllowedPlan(new Flip(actor.Id, item.Id));

        var foreign = Assert.IsType<StartRotationResult.Rejected>(FlipScene().World.StartRotation(plan));
        Assert.Equal(FailureCode.ForeignPlan, foreign.Failure.Code);
        Assert.Equal(before, world.State(item.Id));

        world.StartRotation(plan);
        foreach (var delta in new[] { .3, .9 })
        {
            var state = world.State(item.Id);
            Assert.Equal(new SectionId("I"), state.Section);
            Assert.Null(state.UprightOrigin);
            world.Step(delta);
        }
        var final = world.State(item.Id);
        Assert.Equal(new SectionId("I"), final.Section);
        Assert.Null(final.UprightOrigin);
        Assert.False(final.Rotating);
    }
}
