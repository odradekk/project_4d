using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// The world bounds of the slice an entity shows in a section, whole or for one of its parts. Lengths are in
// metres; sections I and II are at W = 0 and .42.
public class SliceBoundsTests
{
    private static readonly SectionId I = new("I");
    private static readonly SectionId II = new("II");
    private static readonly PartId Body = new("body");
    private static readonly Creature3Definition Player = Actor();

    private static void AssertBounds(Vec3 min, Vec3 max, SliceBoundsResult actual, int digits = 10)
    {
        var bounds = Assert.IsType<SliceBoundsResult.Bounds>(actual);
        AssertNear(min, bounds.Min, digits);
        AssertNear(max, bounds.Max, digits);
    }

    private static FailureCode Refusal(SliceBoundsResult result) =>
        Assert.IsType<SliceBoundsResult.Rejected>(result).Failure.Code;

    [Fact]
    public void FlatBoxBoundsAreItsBoxInItsSectionAndEmptyInTheOther()
    {
        var world = Scene(Rod());

        AssertBounds(new(-.24, -.03, -.03), new(.24, .03, .03), world.GetSliceBounds(RodId, I));
        Assert.IsType<SliceBoundsResult.Empty>(world.GetSliceBounds(RodId, II));
    }

    [Fact]
    public void NamedPartBoundsCoverOnlyThatPart()
    {
        var bar = Rod() with
        {
            Shape = Shape(Part("beam", new(.24, .03, .03)), Part("cap", new(.03, .03, .03), center: new(0, .1, 0)))
        };
        var world = Scene(bar);

        AssertBounds(new(-.24, -.03, -.03), new(.24, .13, .03), world.GetSliceBounds(RodId, I));
        AssertBounds(new(-.24, -.03, -.03), new(.24, .03, .03), world.GetSliceBounds(RodId, I, new("beam")));
        AssertBounds(new(-.03, .07, -.03), new(.03, .13, .03), world.GetSliceBounds(RodId, I, new("cap")));
    }

    [Fact]
    public void PartTurnedOnItsOwnHasTheBoundsOfTheTurnedBox()
    {
        // A box of half size (.5, .2, .3) turned a quarter about Y takes +X to -Z and +Z to +X, so it spans .3 along X and .5 along Z.
        var turned = new Rotation3(new(0, 0, -1), new(0, 1, 0), new(1, 0, 0));
        var shape = Shape(Part("part", new(.5, .2, .3), center: new(0, .5, 0), rotation: turned));
        var wall = new BuildingElementDefinition(new("wall"), Origin, TestSource, new("home"), new(-.5, 1), [], shape);
        var post = new FixedDefinition(new("post"), Origin, TestSource, I, shape);
        var world = Scene(wall, post);

        foreach (var id in new EntityId[] { new("wall"), new("post") })
            AssertBounds(new(-.3, .3, -.5), new(.3, .7, .5), world.GetSliceBounds(id, I, new("part")));
    }

    [Fact]
    public void PartOfADeviceHasBoundsOnlyWhereItsSegmentReaches()
    {
        // The flap of the cabinet is 40 cm high, 60 cm wide and 2 cm thick above the hinge at y = 2, in the segment up to
        // W = .21, so section I cuts it and not the plank of the segment after it.
        var world = Scene(DeviceScenes.Cabinet());

        AssertBounds(new(-.01, 2, -.3), new(.01, 2.4, .3), world.GetSliceBounds(DeviceScenes.CabinetId, I, new("flap")));
        Assert.Equal(world.GetSliceBounds(DeviceScenes.CabinetId, I), world.GetSliceBounds(DeviceScenes.CabinetId, I, new("flap")));
        Assert.IsType<SliceBoundsResult.Empty>(world.GetSliceBounds(DeviceScenes.CabinetId, I, new("plank")));
        Assert.IsType<SliceBoundsResult.Bounds>(world.GetSliceBounds(DeviceScenes.CabinetId, II, new("plank")));
        Assert.IsType<SliceBoundsResult.Empty>(world.GetSliceBounds(DeviceScenes.CabinetId, II, new("flap")));
    }

    [Fact]
    public void BodyOfAGrazerIsItsOnePartAndTheBallsAreNotParts()
    {
        // The lump is a four-dimensional ball of radius 3 whose center is in section I, which cuts it in a sphere of
        // radius 3 around (0, 4, 0). The mesh is within a few centimetres of the sphere.
        var world = GrazerScenes.GrazerScene(At(0, 4, 0), GrazerScenes.Lump(new(0, 0, 0, 0)));
        var body = Assert.IsType<SliceBoundsResult.Bounds>(world.GetSliceBounds(GrazerScenes.GrazerId, I, new("body")));

        Assert.Equal(-3, body.Min.X, .06);
        Assert.Equal(1, body.Min.Y, .06);
        Assert.Equal(3, body.Max.Z, .06);
        Assert.Equal(world.GetSliceBounds(GrazerScenes.GrazerId, I), body);
        Assert.Equal(FailureCode.UnknownId, Refusal(world.GetSliceBounds(GrazerScenes.GrazerId, I, new("lump"))));
    }

    [Fact]
    public void BuildingElementHasBoundsWhereItSpansTheSectionAndNoneOutsideItsWRange()
    {
        var world = Scene(Block("block", new(1, .5, 0), new(.2, .5, .3), new(-.2, .2)));

        AssertBounds(new(.8, 0, -.3), new(1.2, 1, .3), world.GetSliceBounds(new("block"), I));
        Assert.IsType<SliceBoundsResult.Empty>(world.GetSliceBounds(new("block"), II));
    }

    [Fact]
    public void UprightRodIsAThinPlateInEverySectionItSpans()
    {
        // Stood up along W at W = .2, the rod spans W = -.04 to .44, so sections I and II cut it as a plate that
        // reaches Epsilon to each side of x = .1.
        var world = Scene(Rod());
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(RodId, At(.1, 0, 0, w: .2, rotation: XwQuarterTurn)));

        foreach (var section in new[] { I, II })
            AssertBounds(new(.1 - Epsilon, -.03, -.03), new(.1 + Epsilon, .03, .03), world.GetSliceBounds(RodId, section));
    }

    [Theory]
    [InlineData("I")]
    [InlineData("II")]
    public void BallCutOffCentreHasTheRadiusOfItsSmallerCircle(string section)
    {
        // Both sections are .21 m from the ball's center, so each cuts a ball of radius sqrt(R^2 - d^2) = .1356 m.
        var radius = Math.Sqrt(BallRadius * BallRadius - BallW * BallW);
        var world = Scene(Ball());

        var whole = world.GetSliceBounds(BallId, new(section));

        AssertBounds(new(-radius, BallRadius - radius, -radius), new(radius, BallRadius + radius, radius), whole, 12);
        Assert.Equal(whole, world.GetSliceBounds(BallId, new(section), new("ball")));
    }

    [Fact]
    public void BallThatDoesNotReachASectionHasNoBoundsThere()
    {
        // Section II is .32 m from the center, farther than the radius of .25 m.
        var world = Scene(Ball(w: .1));

        var radius = Math.Sqrt(BallRadius * BallRadius - .1 * .1);
        AssertBounds(new(-radius, BallRadius - radius, -radius), new(radius, BallRadius + radius, radius), world.GetSliceBounds(BallId, I), 12);
        Assert.IsType<SliceBoundsResult.Empty>(world.GetSliceBounds(BallId, II));
    }

    [Fact]
    public void HiddenRingHasNoBoundsWhileItRestsBetweenSections()
    {
        var world = Scene(FloorOf(new(-.5, 1)), Nut(), Player);

        AssertBounds(new(-.03, 0, -.03), new(.03, .06, .03), world.GetSliceBounds(NutId, I, Body));
        Assert.IsType<SliceBoundsResult.Empty>(world.GetSliceBounds(NutId, I, RingPart));
        Assert.Equal(world.GetSliceBounds(NutId, I, Body), world.GetSliceBounds(NutId, I));
        Assert.IsType<SliceBoundsResult.Empty>(world.GetSliceBounds(NutId, II));
    }

    [Fact]
    public void StoodUpHiddenRingIsBoundedAsATorus()
    {
        // The ring's center is at (-.21, .9, 0, .42) once the nut is up. Its circle of radius .2 lies in the YZ
        // plane and its tube has radius .05, so the torus spans x = -.26 to -.16, y = .65 to 1.15, z = -.25 to .25.
        var world = Scene(FloorOf(new(-.5, 1)), Nut(), Player);
        world.Stand(Player.Id, NutId, II);

        var ring = world.GetSliceBounds(NutId, II, RingPart);

        AssertBounds(new(-.26, .65, -.25), new(-.16, 1.15, .25), ring, 2);
        Assert.Equal(ring, world.GetSliceBounds(NutId, II));
        Assert.IsType<SliceBoundsResult.Empty>(world.GetSliceBounds(NutId, II, Body));
        Assert.IsType<SliceBoundsResult.Bounds>(world.GetSliceBounds(NutId, I, Body));
    }

    [Fact]
    public void ItemInTheBackpackHasNoBounds()
    {
        var world = Scene(Nut(At(.1, .03, 0)), Actor(x: .15));
        Assert.IsType<CarryResult.Changed>(world.PackEntity(Player.Id, NutId, 1));

        Assert.IsType<SliceBoundsResult.Empty>(world.GetSliceBounds(NutId, I));
    }

    [Fact]
    public void CreatureHasNoSliceAndNoParts()
    {
        var world = Scene(Player);

        Assert.IsType<SliceBoundsResult.Empty>(world.GetSliceBounds(Player.Id, I));
        Assert.Equal(FailureCode.UnknownId, Refusal(world.GetSliceBounds(Player.Id, I, Body)));
    }

    [Fact]
    public void UnknownEntitySectionAndPartAreRejected()
    {
        var world = Scene(Rod());

        Assert.Equal(FailureCode.UnknownId, Refusal(world.GetSliceBounds(new("ghost"), I)));
        Assert.Equal(FailureCode.UnknownId, Refusal(world.GetSliceBounds(RodId, new("III"))));
        Assert.Equal(FailureCode.UnknownId, Refusal(world.GetSliceBounds(RodId, I, new("ghost"))));
        Assert.Equal(FailureCode.UnknownId, Refusal(world.GetSliceBounds(RodId, II, new("ghost"))));
    }
}
