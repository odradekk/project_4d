using static FourD.Tests.Fixtures;
using static FourD.Tests.GrazerScenes;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// The rules a grazer must meet to load (docs/frame/scene-authoring.md): every ball has a unique ID, a finite
// center, and a positive radius and weight; the threshold is positive; and the weights of the balls can add up to
// it. The errors point to the field that is wrong.
public class GrazerValidationTests
{
    private static readonly Metaball Good = Ball("a", default);

    public static TheoryData<MetaballShape, LoadErrorCode, string> InvalidShapes => new()
    {
        { new([], LumpThreshold), LoadErrorCode.InvalidGeometry, "definition.Shape.Balls" },
        { new([Good with { Radius = 0 }], LumpThreshold), LoadErrorCode.InvalidGeometry, "definition.Shape.Balls[0].Radius" },
        { new([Good, Good with { Id = new("b"), Weight = -1 }], LumpThreshold), LoadErrorCode.InvalidGeometry, "definition.Shape.Balls[1].Weight" },
        { new([Good], 0), LoadErrorCode.InvalidGeometry, "definition.Shape.Threshold" },
        { new([Good, Good], LumpThreshold), LoadErrorCode.DuplicateId, "definition.Shape.Balls[1].Id" },
        { new([Good with { Id = new("") }], LumpThreshold), LoadErrorCode.InvalidId, "definition.Shape.Balls[0].Id" },
        { new([Good with { Center = new(0, double.NaN, 0, 0) }], LumpThreshold), LoadErrorCode.InvalidNumber, "definition.Shape.Balls[0].Center" },
        { new([Good with { Radius = double.PositiveInfinity }], LumpThreshold), LoadErrorCode.InvalidNumber, "definition.Shape.Balls[0].Radius" },
        { new([Good with { Weight = double.NaN }], LumpThreshold), LoadErrorCode.InvalidNumber, "definition.Shape.Balls[0].Weight" },
        { new([Good], double.NaN), LoadErrorCode.InvalidNumber, "definition.Shape.Threshold" },
        // A weight of 1 never reaches a threshold above 1 where the balls are too far apart to add up, and balls of
        // radius .5 whose centers are 1 apart only touch.
        { new([Good, Good with { Id = new("b"), Center = new(5, 0, 0, 0) }], 1.5), LoadErrorCode.InvalidGeometry, "definition.Shape.Threshold" },
        { new([Good, Good with { Id = new("b"), Center = new(0, 0, 0, 1) }], 1.5), LoadErrorCode.InvalidGeometry, "definition.Shape.Threshold" }
    };

    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public void AnInvalidBodyFailsToLoadAndNamesTheField(MetaballShape shape, LoadErrorCode code, string property)
    {
        var error = Assert.Single(LoadErrors(Description([Grazer(Origin, shape)])));

        Assert.Equal(code, error.Code);
        Assert.Equal(property, error.Source.Property);
        Assert.Equal(GrazerId, error.Entity);
    }

    [Fact]
    public void AMissingShapeFailsToLoad()
    {
        var error = Assert.Single(LoadErrors(Description([Grazer(Origin, null!)])));

        Assert.Equal(LoadErrorCode.InvalidGeometry, error.Code);
        Assert.Equal("definition.Shape", error.Source.Property);
    }

    [Fact]
    public void ABodyLoadsWhenTheWeightsOfBallsThatOverlapCanReachTheThreshold()
    {
        // A ball's field is at most its weight, reached at its own center.
        Load(Description([Grazer(Origin, new([Good with { Weight = 2 }], 2))]));
        var empty = Assert.Single(LoadErrors(Description([Grazer(Origin, new([Good with { Weight = 2 }], 2.000001))])));
        Assert.Equal(LoadErrorCode.InvalidGeometry, empty.Code);

        // Two balls .3 apart add up to 1.656 midway, though only 1 + (1 - .09 / .25)² = 1.4096 at each center.
        Metaball[] pair = [Good with { Center = new(-.15, 0, 0, 0) }, Good with { Id = new("b"), Center = new(.15, 0, 0, 0) }];
        Assert.NotNull(GrazerScene(At(0, 1, 0), new(pair, 1.5)).SliceOf(GrazerId, new("I")));
        var tooHigh = Assert.Single(LoadErrors(Description([Grazer(Origin, new(pair, 2.1))])));
        Assert.Equal(LoadErrorCode.InvalidGeometry, tooHigh.Code);

        // Apart, the weights do not add, but one ball that is heavy enough reaches the threshold alone.
        Metaball[] apart = [Good, Good with { Id = new("b"), Center = new(5, 0, 0, 0) }];
        Load(Description([Grazer(Origin, new(apart, 1))]));
        Metaball[] mixed = [Good with { Weight = 2 }, Good with { Id = new("b"), Weight = .5, Center = new(5, 0, 0, 0) }];
        Load(Description([Grazer(Origin, new(mixed, 1.5))]));
    }

    [Fact]
    public void ABadPoseFailsToLoadAndATiltedOneDoesNot()
    {
        var skewed = new Rotation4(new(1, .001, 0, 0), Identity4.Y, Identity4.Z, Identity4.W);

        var error = Assert.Single(LoadErrors(Description([Grazer(At(0, 0, 0, rotation: skewed), Lump(default))])));

        Assert.Equal(LoadErrorCode.InvalidPose, error.Code);
        Assert.Equal("definition.Pose", error.Source.Property);
        Load(Description([Grazer(At(0, 0, 0, rotation: XwQuarterTurn), Lump(default))]));
    }
}
