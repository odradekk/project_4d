using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// The unit normal of the plane two local axes span, as it shows in world XYZ. Sections I and II are at W = 0 and .42.
public class SlicePlaneNormalTests
{
    private static readonly SectionId I = new("I");
    private static readonly SectionId II = new("II");
    private static readonly PartId Body = new("body");
    private static readonly Creature3Definition Player = Actor();

    // A quarter turn about Y takes +X to -Z and +Z to +X.
    private static readonly Rotation3 YawQuarter = new(new(0, 0, -1), new(0, 1, 0), new(1, 0, 0));
    private static readonly Rotation4 YawQuarter4 = new(new(0, 0, -1, 0), Identity4.Y, new(1, 0, 0, 0), Identity4.W);

    // A quarter turn about Z takes +X to +Y and +Y to -X.
    private static readonly Rotation4 RollQuarter4 = new(new(0, 1, 0, 0), new(-1, 0, 0, 0), Identity4.Z, Identity4.W);

    // The ring of the nut with the plane it turns in (its Z and W) turned a quarter: its circle stays where it was.
    private static readonly Rotation4 RingInYzTurnedInZw = RingInYz with { Z = RingInYz.W, W = new(-1, 0, 0, 0) };

    private static void AssertDirection(Vec3 expected, SliceDirectionResult actual) =>
        AssertNear(expected, Assert.IsType<SliceDirectionResult.Direction>(actual).UnitDirection);

    private static FailureCode Refusal(SliceDirectionResult result) =>
        Assert.IsType<SliceDirectionResult.Rejected>(result).Failure.Code;

    // A nut stood up toward section II, which puts its ring's center at W = .42.
    private static World StoodNut(Pose4 pose, Rotation4 ring)
    {
        var nut = Nut(pose);
        var hidden = (HiddenRingItem)nut.Shape;
        var world = Scene(FloorOf(new(-.5, 1)), nut with { Shape = hidden with { Ring = hidden.Ring with { Rotation = ring } } }, Player);
        world.Stand(Player.Id, NutId, II);
        return world;
    }

    [Theory]
    [InlineData(Axis3.X, Axis3.Y, 0, 0, 1)]
    [InlineData(Axis3.X, Axis3.Z, 0, 1, 0)]
    [InlineData(Axis3.Y, Axis3.Z, 1, 0, 0)]
    // The sign is only a way to write the axis: swapping the two axes, which negates the cross product,
    // gives the same result.
    [InlineData(Axis3.Y, Axis3.X, 0, 0, 1)]
    [InlineData(Axis3.Z, Axis3.X, 0, 1, 0)]
    [InlineData(Axis3.Z, Axis3.Y, 1, 0, 0)]
    public void UnrotatedBoxPlaneNormalIsTheThirdAxis(Axis3 first, Axis3 second, double x, double y, double z)
    {
        var world = Scene(Rod());

        AssertDirection(new(x, y, z), world.GetSlicePlaneNormal(RodId, I, first, second, Body));
        AssertDirection(new(x, y, z), world.GetSlicePlaneNormal(RodId, I, first, second));
    }

    [Fact]
    public void PartUsesItsOwnBasisAndTheWholeEntityUsesTheEntityBasis()
    {
        // The part is turned a quarter about Y, so its X and Y span the YZ plane while the entity's still span XY.
        var world = Scene(Rod() with { Shape = Shape(Part("body", new(.03, .03, .24), rotation: YawQuarter)) });

        AssertDirection(new(1, 0, 0), world.GetSlicePlaneNormal(RodId, I, Axis3.X, Axis3.Y, Body));
        AssertDirection(new(0, 0, 1), world.GetSlicePlaneNormal(RodId, I, Axis3.X, Axis3.Y));
    }

    [Fact]
    public void PartBasisIsTurnedByTheEntityPose()
    {
        // The part's X and Y are -Z and Y, which the pose's quarter turn about Z takes to -Z and -X: cross(-Z, -X) = +Y.
        // Applying the turns in the other order would give +X.
        var rod = Rod() with
        {
            Pose = At(0, 0, 0, rotation: RollQuarter4),
            Shape = Shape(Part("body", new(.03, .03, .24), rotation: YawQuarter))
        };
        var world = Scene(rod);

        AssertDirection(new(0, 1, 0), world.GetSlicePlaneNormal(RodId, I, Axis3.X, Axis3.Y, Body));
    }

    [Fact]
    public void RingNormalIsTheAxisItsCentralCircleFaces()
    {
        // The nut's ring lies in its YZ plane. Turned a quarter about Y, the nut lays its ring in the XY plane.
        var plain = StoodNut(At(0, .03, 0), RingInYz);
        var turned = StoodNut(At(0, .03, 0, rotation: YawQuarter4), RingInYz);

        AssertDirection(new(1, 0, 0), plain.GetSlicePlaneNormal(NutId, II, Axis3.X, Axis3.Y, RingPart));
        AssertDirection(new(0, 0, 1), turned.GetSlicePlaneNormal(NutId, II, Axis3.X, Axis3.Y, RingPart));
    }

    [Fact]
    public void RingNormalSurvivesLocalZLyingInW()
    {
        // Standing the nut up takes the ring's local Z into W, so X and Z span no plane in the slice, but the
        // circle's own plane, X and Y, keeps its normal. Turning the ring in its own ZW plane moves nothing in it.
        var upright = StoodNut(At(0, .03, 0), RingInYz);
        var turnedInZw = StoodNut(At(0, .03, 0), RingInYzTurnedInZw);

        Assert.IsType<SliceDirectionResult.Degenerate>(upright.GetSlicePlaneNormal(NutId, II, Axis3.X, Axis3.Z, RingPart));
        AssertDirection(new(1, 0, 0), upright.GetSlicePlaneNormal(NutId, II, Axis3.X, Axis3.Y, RingPart));
        AssertDirection(new(1, 0, 0), turnedInZw.GetSlicePlaneNormal(NutId, II, Axis3.X, Axis3.Y, RingPart));
    }

    [Fact]
    public void AxisLyingInWLeavesNoPlaneAndOtherAxesStillDo()
    {
        // The rod stands along W, so its local X has no XYZ part: any plane through X is edge-on.
        var world = Scene(Rod());
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(RodId, At(.1, 0, 0, w: .2, rotation: XwQuarterTurn)));

        Assert.IsType<SliceDirectionResult.Degenerate>(world.GetSlicePlaneNormal(RodId, I, Axis3.X, Axis3.Y));
        Assert.IsType<SliceDirectionResult.Degenerate>(world.GetSlicePlaneNormal(RodId, II, Axis3.Z, Axis3.X, Body));
        AssertDirection(new(1, 0, 0), world.GetSlicePlaneNormal(RodId, II, Axis3.Y, Axis3.Z));
    }

    [Theory]
    [InlineData(Axis3.X)]
    [InlineData(Axis3.Y)]
    [InlineData(Axis3.Z)]
    public void SameAxisTwiceIsInvalidInput(Axis3 axis)
    {
        var world = Scene(Rod());

        Assert.Equal(FailureCode.InvalidInput, Refusal(world.GetSlicePlaneNormal(RodId, I, axis, axis)));
    }

    [Fact]
    public void UnknownAxisIsInvalidInput()
    {
        var world = Scene(Rod());

        Assert.Equal(FailureCode.InvalidInput, Refusal(world.GetSlicePlaneNormal(RodId, I, Axis3.X, (Axis3)7)));
    }

    [Fact]
    public void NoSliceInTheSectionOrForThePartIsNoSlice()
    {
        var world = Scene(FloorOf(new(-.5, 1)), Nut(), Player);

        Assert.Equal(FailureCode.NoSlice, Refusal(world.GetSlicePlaneNormal(NutId, II, Axis3.X, Axis3.Y)));
        Assert.Equal(FailureCode.NoSlice, Refusal(world.GetSlicePlaneNormal(NutId, I, Axis3.X, Axis3.Y, RingPart)));
        Assert.Equal(FailureCode.NoSlice, Refusal(world.GetSlicePlaneNormal(Player.Id, I, Axis3.X, Axis3.Y)));
        AssertDirection(new(0, 0, 1), world.GetSlicePlaneNormal(NutId, I, Axis3.X, Axis3.Y));
    }

    [Fact]
    public void UnknownEntitySectionAndPartAreRejected()
    {
        var world = Scene(Rod());

        Assert.Equal(FailureCode.UnknownId, Refusal(world.GetSlicePlaneNormal(new("ghost"), I, Axis3.X, Axis3.Y)));
        Assert.Equal(FailureCode.UnknownId, Refusal(world.GetSlicePlaneNormal(RodId, new("III"), Axis3.X, Axis3.Y)));
        Assert.Equal(FailureCode.UnknownId, Refusal(world.GetSlicePlaneNormal(RodId, I, Axis3.X, Axis3.Y, new("ghost"))));
    }
}
