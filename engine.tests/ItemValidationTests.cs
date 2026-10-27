using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// Load checks for balls and nuts (docs/frame/scene-authoring.md). The size norms are the limits: a hidden ring stays at
// least its tube radius + 6 mm + 2 cm from every section, and the building holds everything the item can reach.
public class ItemValidationTests
{
    private static readonly SectionId II = new("II");

    // The errors of a world that may also load without any.
    private static IReadOnlyList<LoadError> Errors(WorldDescription description) =>
        World.Load(description) is LoadResult.Invalid invalid ? invalid.Errors : [];

    private static IReadOnlyList<LoadError> Errors(double buildingMin, params EntityDefinition[] entities) =>
        Errors(Description(entities, buildingMin));

    private static LoadError OnlyError(params EntityDefinition[] entities) => Assert.Single(LoadErrors(Description(entities)));

    [Fact]
    public void BallAndNutOfTheSizeNormsLoad()
    {
        var world = Load(Description([Ball(), Nut() with { Id = new("other") }]));

        Assert.NotNull(world.SliceOf(BallId, new("I")));
    }

    [Theory]
    [InlineData(0, LoadErrorCode.InvalidGeometry)]
    [InlineData(-.1, LoadErrorCode.InvalidGeometry)]
    [InlineData(.005, LoadErrorCode.InvalidGeometry)]
    [InlineData(double.NaN, LoadErrorCode.InvalidNumber)]
    public void BallNeedsARadiusThickerThanTheSamplingDistance(double radius, LoadErrorCode code)
    {
        var error = OnlyError(Ball(radius: radius));

        Assert.Equal(code, error.Code);
        Assert.Equal("definition.Shape.Radius", error.Source.Property);
        Assert.Equal(BallId, error.Entity);
    }

    [Theory]
    [InlineData(-.04, true)]
    [InlineData(-.03, false)]
    public void BuildingHoldsTheWholeBall(double buildingMin, bool loads)
    {
        // The ball reaches from W = .21 - .25 = -.04 to .46.
        var errors = Errors(buildingMin, Ball());

        if (loads) Assert.Empty(errors);
        else Assert.Equal(LoadErrorCode.BuildingCoverage, Assert.Single(errors).Code);
    }

    [Fact]
    public void BallMayStartTurnedIntoW()
    {
        var turned = Ball() with { Pose = At(0, .25, 0, BallW, XwQuarterTurn) };

        Assert.IsType<LoadResult.Loaded>(World.Load(Description([turned])));
    }

    [Fact]
    public void BallAndNutNeedKnownBuildingsAndUniqueIds()
    {
        var elsewhere = Ball() with { Building = new("elsewhere") };
        var clash = Nut() with { Id = BallId };

        Assert.Equal(LoadErrorCode.MissingReference, OnlyError(elsewhere).Code);
        Assert.Contains(LoadErrors(Description([Ball(), clash])), error => error.Code == LoadErrorCode.DuplicateId);
    }

    [Theory]
    [InlineData(.0759, false)]
    [InlineData(.076, true)]
    [InlineData(.4200 - .076, true)]
    [InlineData(.4200 - .0759, false)]
    public void HiddenRingKeepsTubeRadiusPlusEpsilonPlusTwoCentimetersFromEverySection(double ringW, bool loads)
    {
        // .05 + .006 + .02 = .076.
        var errors = Errors(Description([Nut(ringW: ringW)]));

        if (loads) Assert.Empty(errors);
        else Assert.Equal(LoadErrorCode.HiddenStructureTooClose, Assert.Single(errors).Code);
    }

    [Theory]
    [InlineData(.5, true)] // the circle spans W .1 to .3 around the ring's W of .2: 10 cm from section I, 12 cm from II
    [InlineData(.8, false)] // it spans W .04 to .36: 4 cm from section I
    public void TiltedHiddenRingKeepsItsWholeCircleClearOfEverySection(double sine, bool loads)
    {
        // The circle's plane is turned into W, so the circle reaches .2 * sine either side of its center.
        var nut = Nut();
        var hidden = (HiddenRingItem)nut.Shape;
        var tilted = nut with { Shape = hidden with { Ring = hidden.Ring with { Rotation = XwRotation(Math.Sqrt(1 - sine * sine), sine) } } };

        var errors = Errors(buildingMin: -1, tilted);

        if (loads) Assert.Empty(errors);
        else Assert.Equal(LoadErrorCode.HiddenStructureTooClose, Assert.Single(errors.Select(error => error.Code).Distinct()));
    }

    [Theory]
    [InlineData(-.47, true)]
    [InlineData(-.46, false)]
    public void BuildingHoldsTheRingWhicheverWayTheNutStandsUp(double buildingMin, bool loads)
    {
        // Standing up away from section II turns the ring's center to W = -.42, and its tube reaches .05 further.
        var errors = Errors(buildingMin, Nut());

        if (loads) Assert.Empty(errors);
        else Assert.Equal(LoadErrorCode.BuildingCoverage, Assert.Single(errors).Code);
    }

    [Fact]
    public void RingMustBeAThinnerTubeAroundAProperCircleWithUniqueNames()
    {
        var ring = ((HiddenRingItem)Nut().Shape).Ring;
        ItemDefinition WithRing(Ring4 changed) => Nut() with { Shape = new HiddenRingItem(Box(new(.03, .03, .03)), changed) };

        Assert.Equal(LoadErrorCode.InvalidGeometry, OnlyError(WithRing(ring with { TubeRadius = ring.MajorRadius })).Code);
        Assert.Equal(LoadErrorCode.InvalidGeometry, OnlyError(WithRing(ring with { TubeRadius = .005 })).Code);
        Assert.Equal(LoadErrorCode.InvalidGeometry, OnlyError(WithRing(ring with { MajorRadius = 0 })).Code);
        Assert.Equal(LoadErrorCode.InvalidNumber, OnlyError(WithRing(ring with { Center = new(.41, double.NaN, 0, .2) })).Code);
        Assert.Equal(LoadErrorCode.InvalidPose, OnlyError(WithRing(ring with { Rotation = XwRotation(.5, .5) })).Code);
        Assert.Equal(LoadErrorCode.DuplicateId, OnlyError(WithRing(ring with { Part = new("body") })).Code);
    }

    [Fact]
    public void NutBodyStartsLyingInASection()
    {
        var error = OnlyError(Nut() with { Pose = At(0, .03, 0, .1) });

        Assert.Equal(LoadErrorCode.InvalidPose, error.Code);
        Assert.Equal("definition.Pose.Position.W", error.Source.Property);
    }

    [Fact]
    public void ItemMustNotReachIntoAnotherBuildingWhileTurning()
    {
        // A second building spans W = .3 to .4, where the nut's ring passes while it turns toward section II.
        var wall = new BuildingElementDefinition(
            new("wall"), Origin, TestSource, new("far"), new(.3, .4), [], Shape(Part("part", new(.1, 1, .5), center: new(.35, 1, 0))));
        var description = Description([Nut(), wall]) with
        {
            Buildings = [new(new("home"), new(-.5, 1), TestSource), new(new("far"), new(.3, .4), TestSource)]
        };

        var errors = LoadErrors(description);

        Assert.Contains(errors, error => error.Code == LoadErrorCode.BuildingInterference && error.Entity == NutId);
    }

    [Fact]
    public void ExpectedStandingUpOfTheNutIsCheckedWithItsRing()
    {
        var player = Actor();
        var operation = new ExpectedOperation(
            new("nut-stands"), TestSource, new([], [], [], []), [new ExpectedRotation(new StandUp(player.Id, NutId, II), null)]);
        var clear = Description([Nut(), player]) with { ExpectedOperations = [operation] };
        Assert.NotNull(Load(clear));

        var block = Block("block", new(.35, 1, 0), new(.05, 1, .5), new(.28, .32));
        var blocked = Description([Nut(), player, block]) with { ExpectedOperations = [operation] };

        var error = Assert.Single(LoadErrors(blocked));
        Assert.Equal(LoadErrorCode.ExpectedOperationFailed, error.Code);
        Assert.Equal(RingPart, error.Contacts[0].MovingPart);
    }

    [Fact]
    public void ExpectedOperationMayStartFromAnotherBallPlacement()
    {
        var operation = new ExpectedOperation(
            new("ball-falls"), TestSource, new([Ball(y: 1)], [], [], []), [new ExpectedAdvance(.1)]);

        var world = Load(Description([FloorOf(new(-.5, 1)), Ball()]) with { ExpectedOperations = [operation] });

        Assert.Equal(BallRadius, world.State(BallId).Pose.Position.Y);
    }
}
